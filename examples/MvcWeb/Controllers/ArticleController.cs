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
                    // Get the current user ID
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    
                    // Add the submission
                    var submission = await _repository.AddSubmissionAsync(model, blogId, userId);
                    
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
        /// Debug endpoint to manually publish a post
        /// </summary>
        [Route("debug/publish/{postId:Guid}")]
        public async Task<IActionResult> DebugPublish(Guid postId)
        {
            try
            {
                var post = await _api.Posts.GetByIdAsync(postId);
                if (post == null)
                {
                    return NotFound($"Post {postId} not found");
                }

                post.Published = DateTime.Now;
                await _api.Posts.SaveAsync(post);

                return Json(new { 
                    success = true, 
                    message = $"Post '{post.Title}' published at {post.Published}",
                    postId = post.Id,
                    title = post.Title,
                    published = post.Published
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Debug endpoint to check workflow transitions for an article
        /// </summary>
        [Route("debug/workflow/{articleId:Guid}")]
        public async Task<IActionResult> DebugWorkflow(Guid articleId)
        {
            try
            {
                var article = await _repository.GetSubmissionByIdAsync(articleId);
                if (article == null)
                {
                    return NotFound($"Article {articleId} not found");
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId);
                var roles = await _userManager.GetRolesAsync(user);
                
                var availableActions = await GetAvailableActionsAsync(article, roles, userId);

                return Json(new {
                    articleId = article.Id,
                    title = article.Submission.Title,
                    currentWorkflowState = article.WorkflowState,
                    currentStatus = article.Status.ToString(),
                    postId = article.PostId,
                    userRoles = roles,
                    availableActions = availableActions.Select(a => new {
                        name = a.Name,
                        targetState = a.TargetState,
                        requiresComment = a.RequiresComment,
                        cssClass = a.CssClass
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Debug endpoint to check submitted articles and their posts
        /// </summary>
        [Route("debug/articles")]
        public async Task<IActionResult> DebugArticles()
        {
            try
            {
                var allArticles = await _repository.GetSubmissionsAsync();
                var result = new List<object>();

                foreach (var article in allArticles)
                {
                    object postData = null;

                    if (article.PostId.HasValue)
                    {
                        var post = await _api.Posts.GetByIdAsync(article.PostId.Value);
                        if (post != null)
                        {
                            postData = new {
                                id = post.Id,
                                title = post.Title,
                                published = post.Published,
                                slug = post.Slug,
                                url = post.Permalink
                            };
                        }
                    }

                    result.Add(new {
                        articleId = article.Id,
                        title = article.Submission.Title,
                        workflowState = article.WorkflowState,
                        status = article.Status.ToString(),
                        postId = article.PostId,
                        submittedById = article.SubmittedById,
                        post = postData
                    });
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Debug endpoint to check all posts
        /// </summary>
        [Route("debug/posts")]
        public async Task<IActionResult> DebugPosts()
        {
            var allPosts = await _api.Posts.GetAllBySiteIdAsync();
            var result = allPosts.Select(p => new {
                p.Id,
                p.Title,
                p.Slug,
                p.Published,
                p.BlogId,
                p.Category,
                ContentType = "post",
                Url = p.Permalink
            }).ToList();
            
            return Json(result);
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
        /// Lists articles and posts in the workflow that the user can modify.
        /// </summary>
        [Authorize]
        [Route("workflow")]
        public async Task<IActionResult> Workflow()
        {
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
            var userRoleIds = await GetUserRoleIdsAsync(userId);
            
            // Validate workflow exists
            await ValidateWorkflowExistsAsync();
            
            var workflowItems = new List<WorkflowItem>();
            
            try
            {
                // Get ALL submitted articles and filter by workflow permissions
                List<SubmittedArticle> allArticles;
                
                if (roles.Contains("SysAdmin"))
                {
                    allArticles = await _repository.GetSubmissionsAsync();
                }
                else if (roles.Contains("Writer"))
                {
                    // Writers see their own articles
                    allArticles = await _repository.GetSubmissionsAsync(null, userId);
                }
                else
                {
                    // Editors and Approvers see all articles (we'll filter by available actions)
                    allArticles = await _repository.GetSubmissionsAsync();
                }
                
                // Convert submitted articles to workflow items and filter by available actions
                foreach (var article in allArticles)
                {
                    var availableActions = await GetAvailableActionsAsync(article, roles, userId);
                    
                    // Only include articles where the user has available actions OR owns the article
                    bool isOwner = article.SubmittedById == userId;
                    
                    if (availableActions.Any() || isOwner || roles.Contains("SysAdmin"))
                    {
                        var isPublished = await IsWorkflowStatePublishedAsync(article.WorkflowState);
                        
                        _logger.LogInformation("Article {ArticleId} ({Title}) - WorkflowState: {WorkflowState}, IsPublished: {IsPublished}, PostId: {PostId}, Actions: {ActionCount}", 
                            article.Id, article.Submission.Title, article.WorkflowState, isPublished, article.PostId, availableActions.Count);
                        
                        workflowItems.Add(new WorkflowItem
                        {
                            Id = article.Id,
                            Title = article.Submission.Title,
                            Author = article.Submission.Author,
                            Created = article.Created,
                            LastModified = article.LastModified,
                            WorkflowState = article.WorkflowState ?? "draft",
                            ContentType = "post",
                            BlogId = article.BlogId,
                            IsPublished = isPublished,
                            SubmittedArticle = article,
                            AvailableActions = availableActions
                        });
                    }
                }
                
                // Get all Piranha posts and filter by what the user can modify
                var allPosts = await _api.Posts.GetAllBySiteIdAsync();
                
                foreach (var post in allPosts)
                {
                    // Check if user has any available transitions for this post
                    var currentState = !string.IsNullOrEmpty(post.WorkflowState) ? post.WorkflowState : "draft";
                    var availableTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("post", currentState, userRoleIds);
                    
                    // Only include posts that the user can modify (has available transitions)
                    if (availableTransitions.Any() || roles.Contains("SysAdmin"))
                    {
                        var availableActions = new List<ArticleAction>();
                        
                        // Convert workflow transitions to article actions
                        foreach (var transition in availableTransitions)
                        {
                            var action = CreateActionFromTransition(transition, transition.ToStateKey);
                            if (action != null)
                            {
                                availableActions.Add(action);
                            }
                        }
                        
                        workflowItems.Add(new WorkflowItem
                        {
                            Id = post.Id,
                            Title = post.Title,
                            Author = "System", // Posts don't have an author field like articles
                            Created = post.Created,
                            LastModified = post.LastModified,
                            WorkflowState = currentState,
                            ContentType = "post",
                            BlogId = post.BlogId,
                            IsPublished = post.Published.HasValue,
                            Post = post,
                            AvailableActions = availableActions
                        });
                    }
                }
                
                // Sort by last modified descending
                workflowItems = workflowItems.OrderByDescending(w => w.LastModified).ToList();
                
                _logger.LogInformation("Found {ArticleCount} articles and {PostCount} posts for user {UserId} with roles {UserRoles}", 
                    workflowItems.Count(w => w.ContentType == "post" && w.SubmittedArticle != null), workflowItems.Count(w => w.ContentType == "post"), userId, string.Join(", ", roles));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow items for user {UserId}", userId);
                ViewBag.ErrorMessage = "An error occurred while loading workflow items.";
            }
            
            ViewBag.UserRoles = roles;
            
            return View(workflowItems);
        }

        /// <summary>
        /// Shows a specific post for review using workflow.
        /// </summary>
        [Authorize]
        [Route("review-post/{id:Guid}")]
        public async Task<IActionResult> ReviewPost(Guid id)
        {
            var post = await _api.Posts.GetByIdAsync(id);
            
            if (post == null)
            {
                return NotFound();
            }
            
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
            var userRoleIds = await GetUserRoleIdsAsync(userId);
            
            // Check if user has any transitions available for this post
            var currentState = !string.IsNullOrEmpty(post.WorkflowState) ? post.WorkflowState : "draft";
            var availableTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("post", currentState, userRoleIds);
            
            if (!availableTransitions.Any() && !roles.Contains("SysAdmin"))
            {
                return Forbid();
            }
            
            // Convert to WorkflowItem for consistent view model
            var workflowItem = new WorkflowItem
            {
                Id = post.Id,
                Title = post.Title,
                Author = "System",
                Created = post.Created,
                LastModified = post.LastModified,
                WorkflowState = currentState,
                ContentType = "post",
                BlogId = post.BlogId,
                IsPublished = post.Published.HasValue,
                Post = post
            };
            
            // Get available actions
            var availableActions = new List<ArticleAction>();
            foreach (var transition in availableTransitions)
            {
                var action = CreateActionFromTransition(transition, transition.ToStateKey);
                if (action != null)
                {
                    availableActions.Add(action);
                }
            }
            workflowItem.AvailableActions = availableActions;
            
            ViewBag.UserRoles = roles;
            ViewBag.AvailableActions = availableActions;
            
            return View("ReviewPost", workflowItem);
        }

        /// <summary>
        /// Updates a post's workflow state.
        /// </summary>
        [Authorize]
        [Route("review-post/{id:Guid}")]
        [HttpPost]
        public async Task<IActionResult> ReviewPost(Guid id, string targetState, string feedback)
        {
            var post = await _api.Posts.GetByIdAsync(id);
            if (post == null)
            {
                return NotFound();
            }
            
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
            
            try
            {
                // Validate that a workflow exists
                await ValidateWorkflowExistsAsync();
                
                var currentState = !string.IsNullOrEmpty(post.WorkflowState) ? post.WorkflowState : "draft";
                
                // Create workflow model for the transition
                var workflowModel = new WorkflowModel
                {
                    ContentId = post.Id,
                    ContentType = "post",
                    CurrentState = currentState,
                    TargetState = targetState,
                    Comment = feedback
                };
                
                _logger.LogInformation("Attempting workflow transition for post {PostId}: {CurrentState} -> {TargetState}", 
                    id, currentState, targetState);
                
                // Perform the workflow transition using Piranha's workflow service
                var result = await _workflowService.PerformTransitionAsync(workflowModel, userId);
                
                if (result.Type == StatusMessage.Success)
                {
                    _logger.LogInformation("Post {PostId} workflow transition from {CurrentState} to {TargetState} by user {UserId}", 
                        id, currentState, targetState, User.Identity?.Name);
                    
                    return RedirectToAction("Workflow");
                }
                else
                {
                    _logger.LogWarning("Workflow transition failed for post {PostId} ({CurrentState} -> {TargetState}): {Error}", 
                        id, currentState, targetState, result.Body);
                    
                    ModelState.AddModelError("", $"Status transition failed: {result.Body}");
                    
                    // Reload the view with error
                    var workflowItem = new WorkflowItem
                    {
                        Id = post.Id,
                        Title = post.Title,
                        Author = "System",
                        Created = post.Created,
                        LastModified = post.LastModified,
                        WorkflowState = currentState,
                        ContentType = "post",
                        BlogId = post.BlogId,
                        IsPublished = post.Published.HasValue,
                        Post = post
                    };
                    
                    ViewBag.UserRoles = roles;
                    return View("ReviewPost", workflowItem);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing workflow transition for post {PostId}", id);
                ModelState.AddModelError("", "An error occurred while updating the post status.");
                
                var workflowItem = new WorkflowItem
                {
                    Id = post.Id,
                    Title = post.Title,
                    Author = "System",
                    Created = post.Created,
                    LastModified = post.LastModified,
                    WorkflowState = !string.IsNullOrEmpty(post.WorkflowState) ? post.WorkflowState : "draft",
                    ContentType = "post",
                    BlogId = post.BlogId,
                    IsPublished = post.Published.HasValue,
                    Post = post
                };
                
                ViewBag.UserRoles = roles;
                return View("ReviewPost", workflowItem);
            }
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
            
            // Debug: Log user identity information
            _logger.LogInformation("Review attempt - User.Identity.IsAuthenticated: {IsAuthenticated}, UserId: {UserId}, UserName: {UserName}", 
                User.Identity?.IsAuthenticated, userId, User.Identity?.Name);
            
            // Debug: Log all claims
            foreach (var claim in User.Claims)
            {
                _logger.LogDebug("User claim - Type: {ClaimType}, Value: {ClaimValue}", claim.Type, claim.Value);
            }
            
            if (string.IsNullOrEmpty(userId))
            {
                // Try alternative claim types
                userId = User.FindFirstValue("sub") ?? User.FindFirstValue("id") ?? User.FindFirstValue("nameid");
                _logger.LogInformation("Alternative userId lookup result: {AlternativeUserId}", userId);
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("No valid user ID found in claims, redirecting to login");
                    return Challenge(); // Redirect to login if userId is null
                }
            }
            
            var user = await _userManager.FindByIdAsync(userId);
            
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return Challenge(); // Redirect to login if user not found
            }
            
            var roles = await _userManager.GetRolesAsync(user);
            
            // Check permissions based on workflow - if user has any available actions, they can view
            if (!roles.Contains("SysAdmin"))
            {
                var userActions = await GetAvailableActionsAsync(article, roles, userId);
                
                // If user has no available actions and doesn't own the article, they can't view it
                bool isOwner = article.SubmittedById == userId;
                
                if (!userActions.Any() && !isOwner)
                {
                    _logger.LogInformation("User {UserId} has no workflow actions available for article {ArticleId} and is not the owner", 
                        userId, article.Id);
                    return Forbid();
                }
                
                _logger.LogInformation("User {UserId} has {ActionCount} available actions for article {ArticleId}, isOwner: {IsOwner}", 
                    userId, userActions.Count, article.Id, isOwner);
            }
            
            ViewBag.UserRoles = roles;
            
            // Get available actions for this article based on current status and user roles
            var availableActions = await GetAvailableActionsAsync(article, roles, userId);
            ViewBag.AvailableActions = availableActions;
            
            return View(article);
        }

        /// <summary>
        /// Updates an article's workflow state using Piranha workflow transitions.
        /// </summary>
        [Authorize]
        [Route("review/{id:Guid}")]
        [HttpPost]
        public async Task<IActionResult> Review(Guid id, string targetState, string feedback)
        {
            _logger.LogInformation("=== REVIEW POST METHOD CALLED === ArticleId: {ArticleId}, TargetState: {TargetState}, User: {User}", 
                id, targetState, User.Identity?.Name);
                
            using var activity = ActivitySource.StartActivity("ArticleController.Review");
            
            activity?.SetTag("articleId", id.ToString());
            activity?.SetTag("newState", targetState);
            activity?.SetTag("userId", User.Identity?.Name ?? "unknown");
            
            var article = await _repository.GetSubmissionByIdAsync(id);
            
            if (article == null)
            {
                activity?.SetTag("outcome", "not_found");
                return NotFound();
            }
            
            activity?.SetTag("previousState", article.WorkflowState);
            
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
                
                // Validate that a workflow exists (but use whatever is configured)
                await ValidateWorkflowExistsAsync();
                
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
                
                // Use current workflow state and target state from the workflow transition
                var currentState = article.WorkflowState ?? "draft";
                
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
                    _logger.LogInformation("=== PIRANHA WORKFLOW TRANSITION SUCCESSFUL === Calling UpdateSubmissionStateAsync for article {ArticleId} to state {TargetState}", 
                        id, targetState);
                        
                    // Update the article state in our database to match the workflow transition
                    await _repository.UpdateSubmissionStateAsync(id, targetState, userId, feedback);
                    
                    _logger.LogInformation("=== UpdateSubmissionStateAsync COMPLETED === for article {ArticleId}", id);
                    
                    ArticleReviewsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "success"), 
                        new KeyValuePair<string, object?>("previousState", article.WorkflowState),
                        new KeyValuePair<string, object?>("newState", targetState));
                    
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
                    new KeyValuePair<string, object?>("previousState", article.WorkflowState),
                    new KeyValuePair<string, object?>("newState", targetState));
                
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
                // Validate workflow exists first  
                await ValidateWorkflowExistsAsync();
                
                // Create or get post ID for workflow queries
                Guid postId;
                if (article.PostId.HasValue)
                {
                    postId = article.PostId.Value;
                }
                else
                {
                    // For articles without posts yet, we need to get user role IDs and query the workflow definition directly
                    var currentState = article.WorkflowState ?? "draft";
                    
                    // Get user role IDs
                    var userRoleIds = await GetUserRoleIdsAsync(userId);
                    
                    var availableTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("post", currentState, userRoleIds);
                    
                    _logger.LogInformation("Found {Count} workflow transitions for article {ArticleId} in state {CurrentState} (no post yet), user roles: {UserRoles}", 
                        availableTransitions.Count(), article.Id, currentState, string.Join(", ", userRoleIds));
                    
                    // Log each available transition for debugging
                    foreach (var transition in availableTransitions)
                    {
                        _logger.LogInformation("Available transition for article {ArticleId}: {FromState} -> {ToState} ('{Name}')", 
                            article.Id, transition.FromStateKey, transition.ToStateKey, transition.Name);
                    }
                    
                    // Convert workflow transitions to article actions
                    foreach (var transition in availableTransitions)
                    {
                        var action = CreateActionFromTransition(transition, transition.ToStateKey);
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
                    var action = CreateActionFromTransition(transition, transition.ToState);
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
        private ArticleAction CreateActionFromTransition(dynamic transition, string targetState)
        {
            // Get transition properties - handle different transition object types
            string name = transition.Name ?? "Transition";
            
            // Safely get RequiresComment property
            bool requiresComment = false;
            try
            {
                requiresComment = transition.RequiresComment ?? false;
            }
            catch
            {
                // Property doesn't exist on this transition type
                requiresComment = false;
            }
            
            // Safely get other properties with fallbacks
            string cssClass = "btn-primary";
            try
            {
                cssClass = transition.CssClass ?? "btn-primary";
            }
            catch
            {
                cssClass = "btn-primary";
            }
            
            string icon = "fas fa-arrow-right";
            try
            {
                icon = transition.Icon ?? "fas fa-arrow-right";
            }
            catch
            {
                icon = "fas fa-arrow-right";
            }
            
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
                TargetState = targetState,
                RequiresComment = requiresComment,
                IsWorkflowTransition = true
            };
        }


        /// <summary>
        /// Checks if a workflow state is considered published
        /// </summary>
        private async Task<bool> IsWorkflowStatePublishedAsync(string workflowState)
        {
            if (string.IsNullOrEmpty(workflowState))
                return false;

            try
            {
                var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync("post");
                if (workflow?.States == null || !workflow.States.Any())
                {
                    return false;
                }

                var state = workflow.States.FirstOrDefault(s => s.Key == workflowState);
                if (state == null)
                {
                    return false;
                }

                // Check if the state is marked as published or is final
                return state.IsPublished || state.IsFinal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if workflow state {WorkflowState} is published", workflowState);
                return false;
            }
        }

        /// <summary>
        /// Validates that a workflow exists for the content type, but uses whatever workflow is configured
        /// </summary>
        private async Task ValidateWorkflowExistsAsync()
        {
            try
            {
                var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync("post");
                
                if (workflow == null)
                {
                    _logger.LogWarning("No default workflow found for post content type");
                    
                    // Try to get all workflows to see what's available
                    var allWorkflows = await _workflowDefinitionService.GetAllAsync();
                    _logger.LogInformation("Found {WorkflowCount} total workflows in system", allWorkflows.Count());
                    
                    foreach (var wf in allWorkflows)
                    {
                        _logger.LogInformation("Available workflow: '{Name}' for content type '{ContentType}' with {StateCount} states", 
                            wf.Name, wf.ContentTypes, wf.States?.Count ?? 0);
                    }
                    return;
                }

                _logger.LogInformation("Using existing workflow '{WorkflowName}' (ID: {WorkflowId}) for posts with {StateCount} states and {TransitionCount} transitions", 
                    workflow.Name, workflow.Id, workflow.States?.Count ?? 0, workflow.Transitions?.Count ?? 0);
                    
                // Log the available states and transitions for debugging
                if (workflow.States?.Any() == true)
                {
                    foreach (var state in workflow.States)
                    {
                        _logger.LogInformation("Workflow state: '{StateKey}' - {StateName} (Color: {Color})", 
                            state.Key, state.Name, state.Color);
                    }
                }
                
                if (workflow.Transitions?.Any() == true)
                {
                    foreach (var transition in workflow.Transitions)
                    {
                        _logger.LogInformation("Workflow transition: {FromState} -> {ToState} ('{Name}') requires role: {RoleId}, requires comment: {RequiresComment}", 
                            transition.FromStateKey, transition.ToStateKey, transition.Name, 
                            transition.RequiredRoleId?.ToString() ?? "None", transition.RequiresComment);
                    }
                }
                else
                {
                    _logger.LogWarning("Workflow '{WorkflowName}' has no transitions defined!", workflow.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow exists");
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