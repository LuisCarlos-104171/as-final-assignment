using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using MvcWeb.Services;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Models;
using Piranha.Manager.Models;
using Piranha.Manager.Services;
using Piranha.Services;
using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MvcWeb.Controllers
{
    [Route("article")]
    public class ArticleController : Controller
    {
        private readonly IApi _api;
        private readonly ILogger<ArticleController> _logger;
        private readonly ArticleSubmissionRepository _repository;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly WorkflowService _workflowService;
        private readonly IWorkflowDefinitionService _workflowDefinitionService;
        
        private static readonly ActivitySource ActivitySource = new("MvcWeb.ArticleController");
        private static readonly Meter Meter = new("MvcWeb.ArticleController");
        private static readonly Counter<int> ArticleSubmissionsCounter = Meter.CreateCounter<int>("article_submissions_total", "Total number of article submissions");
        private static readonly Counter<int> ArticleReviewsCounter = Meter.CreateCounter<int>("article_reviews_total", "Total number of article reviews");
        private static readonly Histogram<double> ArticleSubmissionDuration = Meter.CreateHistogram<double>("article_submission_duration_ms", "Duration of article submissions in milliseconds");

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ArticleController(IApi api, 
            ILogger<ArticleController> logger, 
            ArticleSubmissionRepository repository,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            WorkflowService workflowService,
            IWorkflowDefinitionService workflowDefinitionService)
        {
            _api = api;
            _logger = logger;
            _repository = repository;
            _userManager = userManager;
            _roleManager = roleManager;
            _workflowService = workflowService;
            _workflowDefinitionService = workflowDefinitionService;
        }

        /// <summary>
        /// Gets the article submission form.
        /// </summary>
        [Route("submit")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Submit()
        {
            // Get the default blog
            var blogs = await _api.Pages
                .GetAllBlogsAsync();
            
            if (!blogs.Any())
            {
                return NotFound("No blog found");
            }

            ViewBag.BlogId = blogs.First().Id;
            
            // Return the submission form
            return View(new ArticleSubmissionModel());
        }

        /// <summary>
        /// Submits a new article.
        /// </summary>
        [Route("submit")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Submit(ArticleSubmissionModel model, Guid blogId)
        {
            using var activity = ActivitySource.StartActivity("ArticleController.Submit");
            var stopwatch = Stopwatch.StartNew();
            
            activity?.SetTag("blogId", blogId.ToString());
            activity?.SetTag("userId", User.Identity?.Name ?? "unknown");
            
            if (ModelState.IsValid)
            {
                try
                {
                    // Add the submission
                    var submission = await _repository.AddSubmissionAsync(model, blogId);
                    
                    stopwatch.Stop();
                    
                    // Record metrics using centralized service
                    ArticleSubmissionDuration.Record(stopwatch.ElapsedMilliseconds);
                    ArticleSubmissionsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "success"), 
                        new KeyValuePair<string, object?>("blogId", blogId.ToString()));
                    
                    // Also record using centralized metrics (for consistency)
                    MetricsService.RecordHttpRequest("POST", "/article/submit", 302, stopwatch.ElapsedMilliseconds);
                    MetricsService.RecordUserAction("article_submission_success", "content");
                    MetricsService.RecordWorkflowTransition("new", "draft", "article_workflow");
                    
                    activity?.SetTag("submissionId", submission.Id.ToString());
                    activity?.SetTag("outcome", "success");
                    
                    _logger.LogInformation("Article submitted successfully with ID {SubmissionId} by user {UserId}", 
                        submission.Id, User.Identity?.Name);
                    
                    // Redirect to a thank you page
                    return RedirectToAction("ThankYou", new { id = submission.Id });
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    ArticleSubmissionsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "error"), 
                        new KeyValuePair<string, object?>("blogId", blogId.ToString()));
                    
                    activity?.SetTag("outcome", "error");
                    activity?.SetTag("error.message", ex.Message);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    
                    _logger.LogError(ex, "Error submitting article");
                    ModelState.AddModelError("", "An error occurred while submitting your article. Please try again.");
                }
            }
            else
            {
                ArticleSubmissionsCounter.Add(1, 
                    new KeyValuePair<string, object?>("status", "validation_error"), 
                    new KeyValuePair<string, object?>("blogId", blogId.ToString()));
                activity?.SetTag("outcome", "validation_error");
            }
            
            // If we got here, something went wrong
            ViewBag.BlogId = blogId;
            return View(model);
        }

        /// <summary>
        /// Shows a thank you page after submission.
        /// </summary>
        [Route("thank-you/{id:Guid}")]
        public async Task<IActionResult> ThankYou(Guid id)
        {
            var submission = await _repository.GetSubmissionByIdAsync(id);
            
            if (submission == null)
            {
                return NotFound();
            }
            
            return View(submission);
        }

        /// <summary>
        /// Lists articles in the workflow.
        /// </summary>
        [Authorize]
        [Route("workflow")]
        public async Task<IActionResult> Workflow()
        {
            // We need to fetch different articles based on the user's role
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(); // Redirect to login if userId is null
            }
            
            var user = await _userManager.FindByIdAsync(userId);
            
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return Challenge(); // Redirect to login if user not found
            }
            
            var roles = await _userManager.GetRolesAsync(user);
            
            List<SubmittedArticle> articles;
            ArticleStatus? statusFilter = null;
            
            if (roles.Contains("SysAdmin"))
            {
                // Admin can see all articles
                articles = await _repository.GetSubmissionsAsync();
            }
            else if (roles.Contains("Approver"))
            {
                // Approver can see articles ready for approval
                statusFilter = ArticleStatus.InReview;
                articles = await _repository.GetSubmissionsAsync(statusFilter);
            }
            else if (roles.Contains("Editor"))
            {
                // Editor can see submitted articles to review
                statusFilter = ArticleStatus.Draft;
                articles = await _repository.GetSubmissionsAsync(statusFilter);
            }
            else if (roles.Contains("Writer"))
            {
                // Writer can only see their own articles
                articles = await _repository.GetSubmissionsAsync(null, userId);
            }
            else
            {
                // No permissions
                return Forbid();
            }
            
            ViewBag.UserRoles = roles;
            ViewBag.StatusFilter = statusFilter;
            
            return View(articles);
        }

        /// <summary>
        /// Shows a specific article for review.
        /// </summary>
        [Authorize]
        [Route("review/{id:Guid}")]
        public async Task<IActionResult> Review(Guid id)
        {
            var article = await _repository.GetSubmissionByIdAsync(id);
            
            if (article == null)
            {
                return NotFound();
            }
            
            // Check permissions based on status
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(); // Redirect to login if userId is null
            }
            
            var user = await _userManager.FindByIdAsync(userId);
            
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return Challenge(); // Redirect to login if user not found
            }
            
            var roles = await _userManager.GetRolesAsync(user);
            
            if (!roles.Contains("SysAdmin"))
            {
                if (article.Status == ArticleStatus.Draft && !roles.Contains("Editor"))
                {
                    return Forbid();
                }
                
                if (article.Status == ArticleStatus.InReview && !roles.Contains("Approver") && !roles.Contains("Editor"))
                {
                    return Forbid();
                }
                
                if (article.Status == ArticleStatus.Approved && !roles.Contains("Approver"))
                {
                    return Forbid();
                }
            }
            
            ViewBag.UserRoles = roles;
            
            // Get available actions for this article based on current status and user roles
            var availableActions = await GetAvailableActionsAsync(article, roles, userId);
            ViewBag.AvailableActions = availableActions;
            
            return View(article);
        }

        /// <summary>
        /// Updates an article's status using Piranha workflow transitions.
        /// </summary>
        [Authorize]
        [Route("review/{id:Guid}")]
        [HttpPost]
        public async Task<IActionResult> Review(Guid id, ArticleStatus status, string feedback)
        {
            using var activity = ActivitySource.StartActivity("ArticleController.Review");
            
            activity?.SetTag("articleId", id.ToString());
            activity?.SetTag("newStatus", status.ToString());
            activity?.SetTag("userId", User.Identity?.Name ?? "unknown");
            
            var article = await _repository.GetSubmissionByIdAsync(id);
            
            if (article == null)
            {
                activity?.SetTag("outcome", "not_found");
                return NotFound();
            }
            
            activity?.SetTag("previousStatus", article.Status.ToString());
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return Challenge();
            }
            
            var roles = await _userManager.GetRolesAsync(user);
            activity?.SetTag("userRoles", string.Join(",", roles));
            
            try
            {
                // ALWAYS use workflow transitions for ALL status changes
                
                // Ensure we have a proper workflow first
                await EnsureArticleWorkflowExistsAsync();
                
                // Create or get Piranha post for workflow management
                Guid postId;
                if (article.PostId.HasValue)
                {
                    postId = article.PostId.Value;
                }
                else
                {
                    // Create a new Piranha post for this article to manage via workflow
                    var newPostId = await CreatePostFromArticleAsync(article);
                    if (!newPostId.HasValue)
                    {
                        ModelState.AddModelError("", "Error creating post for workflow management.");
                        ViewBag.UserRoles = roles;
                        return View(article);
                    }
                    postId = newPostId.Value;
                    
                    // Update the article with the new post ID in our database
                    var tempArticle = await _repository.GetSubmissionByIdAsync(id);
                    // We need to update the database directly since PostId isn't handled by UpdateSubmissionStatusAsync
                    // For now, log this - in production you'd want to add a method to update PostId
                    _logger.LogInformation("Created post {PostId} for article {ArticleId}", postId, id);
                }
                
                // Map current and target status to workflow states
                var currentState = MapStatusToWorkflowState(article.Status);
                var targetState = MapStatusToWorkflowState(status);
                
                // Create workflow model for the transition
                var workflowModel = new WorkflowModel
                {
                    ContentId = postId,
                    ContentType = "post",
                    CurrentState = currentState,
                    TargetState = targetState,
                    Comment = feedback
                };
                
                _logger.LogInformation("Attempting workflow transition for article {ArticleId}: {CurrentState} -> {TargetState}", 
                    id, currentState, targetState);
                
                // Perform the workflow transition using Piranha's workflow service
                var result = await _workflowService.PerformTransitionAsync(workflowModel, userId);
                
                if (result.Type == StatusMessage.Success)
                {
                    // Update the article status in our database to match the workflow transition
                    await _repository.UpdateSubmissionStatusAsync(id, status, userId, feedback);
                    
                    ArticleReviewsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "success"), 
                        new KeyValuePair<string, object?>("previousStatus", article.Status.ToString()),
                        new KeyValuePair<string, object?>("newStatus", status.ToString()));
                    
                    activity?.SetTag("outcome", "success");
                    activity?.SetTag("workflowTransition", $"{currentState}->{targetState}");
                    
                    _logger.LogInformation("Article {ArticleId} workflow transition from {CurrentState} to {TargetState} by user {UserId}", 
                        id, currentState, targetState, User.Identity?.Name);
                    
                    return RedirectToAction("Workflow");
                }
                else
                {
                    // Workflow transition failed - this means the user doesn't have permission or the transition doesn't exist
                    activity?.SetTag("outcome", "workflow_error");
                    activity?.SetTag("workflowError", result.Body);
                    
                    _logger.LogWarning("Workflow transition failed for article {ArticleId} ({CurrentState} -> {TargetState}): {Error}", 
                        id, currentState, targetState, result.Body);
                    
                    ModelState.AddModelError("", $"Status transition failed: {result.Body}");
                    ViewBag.UserRoles = roles;
                    return View(article);
                }
            }
            catch (Exception ex)
            {
                ArticleReviewsCounter.Add(1, 
                    new KeyValuePair<string, object?>("status", "error"), 
                    new KeyValuePair<string, object?>("previousStatus", article.Status.ToString()),
                    new KeyValuePair<string, object?>("newStatus", status.ToString()));
                
                activity?.SetTag("outcome", "error");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _logger.LogError(ex, "Error performing workflow transition for article {ArticleId}", id);
                ModelState.AddModelError("", "An error occurred while updating the article status.");
                ViewBag.UserRoles = roles;
                return View(article);
            }
        }


        /// <summary>
        /// Gets available actions for an article based on actual workflow transitions
        /// </summary>
        private async Task<List<ArticleAction>> GetAvailableActionsAsync(SubmittedArticle article, IList<string> userRoles, string userId)
        {
            var actions = new List<ArticleAction>();
            
            try
            {
                // Ensure workflow exists first
                await EnsureArticleWorkflowExistsAsync();
                
                // Create or get post ID for workflow queries
                Guid postId;
                if (article.PostId.HasValue)
                {
                    postId = article.PostId.Value;
                }
                else
                {
                    // For articles without posts yet, we need to get user role IDs and query the workflow definition directly
                    var currentState = MapStatusToWorkflowState(article.Status);
                    
                    // Get user role IDs
                    var userRoleIds = await GetUserRoleIdsAsync(userId);
                    
                    var availableTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("post", currentState, userRoleIds);
                    
                    _logger.LogInformation("Found {Count} workflow transitions for article {ArticleId} in state {CurrentState} (no post yet), user roles: {UserRoles}", 
                        availableTransitions.Count(), article.Id, currentState, string.Join(", ", userRoleIds));
                    
                    // Convert workflow transitions to article actions
                    foreach (var transition in availableTransitions)
                    {
                        var targetStatus = MapWorkflowStateToStatus(transition.ToStateKey);
                        var action = CreateActionFromTransition(transition, targetStatus);
                        if (action != null)
                        {
                            actions.Add(action);
                        }
                    }
                    
                    _logger.LogInformation("Generated {Count} available actions for article {ArticleId} in status {Status} (from workflow definition)", 
                        actions.Count, article.Id, article.Status);
                    
                    return actions;
                }
                
                // Get actual workflow transitions for the post
                var workflowModel = await _workflowService.GetWorkflowTransitionsAsync("post", postId, userId);
                var workflowTransitions = workflowModel.AvailableTransitions.ToList();
                
                _logger.LogInformation("Found {Count} workflow transitions for article {ArticleId} with post {PostId}", 
                    workflowTransitions.Count, article.Id, postId);
                
                // Convert workflow transitions to article actions
                foreach (var transition in workflowTransitions)
                {
                    var targetStatus = MapWorkflowStateToStatus(transition.ToState);
                    var action = CreateActionFromTransition(transition, targetStatus);
                    if (action != null)
                    {
                        actions.Add(action);
                    }
                }
                
                _logger.LogInformation("Generated {Count} available actions for article {ArticleId} in status {Status} (from workflow transitions)", 
                    actions.Count, article.Id, article.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available actions for article {ArticleId}", article.Id);
            }
            
            return actions;
        }

        /// <summary>
        /// Gets user role IDs for the given user
        /// </summary>
        private async Task<List<Guid>> GetUserRoleIdsAsync(string userId)
        {
            var roleIds = new List<Guid>();
            
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var roleNames = await _userManager.GetRolesAsync(user);
                    
                    _logger.LogInformation("User {UserId} has roles: {RoleNames}", userId, string.Join(", ", roleNames));
                    
                    // Look up role IDs from the database using RoleManager
                    foreach (var roleName in roleNames)
                    {
                        var role = await _roleManager.FindByNameAsync(roleName);
                        if (role != null)
                        {
                            // role.Id is of type Guid, so use it directly
                            roleIds.Add(role.Id);
                            _logger.LogDebug("Mapped role {RoleName} to ID {RoleId}", roleName, role.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find role ID for role name: {RoleName}", roleName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role IDs for user {UserId}", userId);
            }
            
            return roleIds;
        }

        /// <summary>
        /// Creates an ArticleAction from a workflow transition
        /// </summary>
        private ArticleAction CreateActionFromTransition(dynamic transition, ArticleStatus targetStatus)
        {
            // Get transition properties (works for both WorkflowTransition and WorkflowModel.WorkflowTransition)
            string name = transition.Name;
            bool requiresComment = transition.RequiresComment ?? false;
            string cssClass = transition.CssClass ?? "btn-primary";
            string icon = transition.Icon ?? "fas fa-arrow-right";
            
            // Map common transition names to better action names and icons
            var actionMapping = new Dictionary<string, (string name, string icon, string cssClass)>
            {
                {"Submit for Review", ("Send for Review", "fas fa-paper-plane", "btn-primary")},
                {"Approve", ("Approve", "fas fa-check", "btn-success")},
                {"Reject", ("Reject", "fas fa-times", "btn-danger")},
                {"Publish", ("Publish", "fas fa-globe", "btn-primary")},
                {"Back to Draft", ("Send Back to Draft", "fas fa-undo", "btn-secondary")},
                {"Unpublish", ("Unpublish", "fas fa-eye-slash", "btn-warning")}
            };
            
            if (actionMapping.ContainsKey(name))
            {
                var mapping = actionMapping[name];
                name = mapping.name;
                icon = mapping.icon;
                cssClass = mapping.cssClass;
            }
            
            return new ArticleAction
            {
                Name = name,
                Icon = icon,
                CssClass = cssClass,
                TargetStatus = targetStatus,
                RequiresComment = requiresComment,
                IsWorkflowTransition = true
            };
        }

        /// <summary>
        /// Maps ArticleStatus to workflow state string
        /// </summary>
        private string MapStatusToWorkflowState(ArticleStatus status)
        {
            return status switch
            {
                ArticleStatus.Draft => "draft",
                ArticleStatus.InReview => "in_review",
                ArticleStatus.Approved => "approved",
                ArticleStatus.Published => "published",
                ArticleStatus.Rejected => "rejected",
                _ => "draft"
            };
        }

        /// <summary>
        /// Maps workflow state string to ArticleStatus
        /// </summary>
        private ArticleStatus MapWorkflowStateToStatus(string workflowState)
        {
            return workflowState switch
            {
                "draft" => ArticleStatus.Draft,
                "in_review" => ArticleStatus.InReview,
                "approved" => ArticleStatus.Approved,
                "published" => ArticleStatus.Published,
                "rejected" => ArticleStatus.Rejected,
                _ => ArticleStatus.Draft
            };
        }

        /// <summary>
        /// Ensures a proper workflow definition exists for articles with all required states and transitions
        /// </summary>
        private async Task EnsureArticleWorkflowExistsAsync()
        {
            try
            {
                // Check if we need to create a complete workflow for articles
                var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync("post");
                
                if (workflow == null)
                {
                    _logger.LogWarning("No default workflow found for post content type - creating one");
                    await CreateCompleteArticleWorkflowAsync();
                    return;
                }

                // Check if the workflow has all the states we need
                var requiredStates = new[] { "draft", "in_review", "approved", "rejected", "published" };
                var existingStates = workflow.States?.Select(s => s.Key).ToHashSet() ?? new HashSet<string>();
                
                var missingStates = requiredStates.Where(s => !existingStates.Contains(s)).ToList();
                
                if (missingStates.Any())
                {
                    _logger.LogWarning("Workflow is missing required states: {MissingStates}. Creating complete workflow.", 
                        string.Join(", ", missingStates));
                    await CreateCompleteArticleWorkflowAsync();
                    return;
                }

                // Check if the workflow has all the transitions we need
                var existingTransitions = workflow.Transitions?.Select(t => $"{t.FromStateKey}->{t.ToStateKey}").ToHashSet() ?? new HashSet<string>();
                var requiredTransitions = new[] {
                    "draft->in_review",
                    "draft->rejected", 
                    "in_review->approved",
                    "in_review->rejected",
                    "in_review->draft",
                    "approved->published",
                    "approved->in_review",
                    "rejected->draft"
                };
                
                var missingTransitions = requiredTransitions.Where(t => !existingTransitions.Contains(t)).ToList();
                
                if (missingTransitions.Any())
                {
                    _logger.LogWarning("Workflow is missing required transitions: {MissingTransitions}. Creating complete workflow.", 
                        string.Join(", ", missingTransitions));
                    await CreateCompleteArticleWorkflowAsync();
                    return;
                }
                
                _logger.LogInformation("Article workflow is complete. States: {States}, Transitions: {Transitions}", 
                    string.Join(", ", existingStates), string.Join(", ", existingTransitions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring article workflow exists");
            }
        }

        /// <summary>
        /// Creates a complete article workflow with all required states and transitions
        /// </summary>
        private async Task CreateCompleteArticleWorkflowAsync()
        {
            try
            {
                _logger.LogInformation("Creating complete article workflow definition");
                
                // Use the WorkflowDefinitionService to create a default workflow with proper states and transitions
                var workflow = await _workflowDefinitionService.CreateDefaultWorkflowAsync("Article Workflow", new[] { "post" });
                
                if (workflow != null)
                {
                    _logger.LogInformation("Successfully created article workflow with {StateCount} states and {TransitionCount} transitions", 
                        workflow.States?.Count ?? 0, workflow.Transitions?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning("Failed to create default workflow via WorkflowDefinitionService");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating complete article workflow");
            }
        }

        /// <summary>
        /// Creates a Piranha post from article and returns its ID
        /// </summary>
        private async Task<Guid?> CreatePostFromArticleAsync(SubmittedArticle article)
        {
            try
            {
                // Create a new post
                var post = await StandardPost.CreateAsync(_api);
                
                // Set basic fields
                post.BlogId = article.BlogId;
                post.Title = article.Submission.Title;
                post.Excerpt = !string.IsNullOrWhiteSpace(article.Submission.Excerpt) 
                    ? article.Submission.Excerpt 
                    : article.Submission.Title;
                post.Category = !string.IsNullOrWhiteSpace(article.Submission.Category)
                    ? article.Submission.Category
                    : "General";
                
                // Generate a unique slug by appending the article ID
                var baseSlug = Utils.GenerateSlug(article.Submission.Title);
                post.Slug = $"{baseSlug}-{article.Id.ToString("N")[..8]}"; // Use first 8 chars of article ID
                
                // Set the workflow state to draft initially
                post.WorkflowState = "draft";
                
                _logger.LogInformation("Creating post with slug: {Slug} for article {ArticleId}", post.Slug, article.Id);
                
                // Process tags
                if (!string.IsNullOrWhiteSpace(article.Submission.Tags))
                {
                    var tags = article.Submission.Tags
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t));
                    
                    foreach (var tag in tags)
                    {
                        post.Tags.Add(tag);
                    }
                }

                // Create content block
                var htmlBlock = new Piranha.Extend.Blocks.HtmlBlock
                {
                    Body = new Piranha.Extend.Fields.HtmlField
                    {
                        Value = article.Submission.Content
                    }
                };
                post.Blocks.Add(htmlBlock);

                // Set metadata
                post.MetaTitle = article.Submission.Title;
                post.MetaDescription = article.Submission.Excerpt;

                // Save the post (without publishing yet)
                await _api.Posts.SaveAsync(post);
                
                _logger.LogInformation("Created Piranha post {PostId} from article {ArticleId}", 
                    post.Id, article.Id);
                
                return post.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Piranha post from article {ArticleId}", article.Id);
                return null;
            }
        }
    }
}