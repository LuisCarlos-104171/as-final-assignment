using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using MvcWeb.Services;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Models;
using Piranha.Services;
using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MvcWeb.Controllers
{
    [Route("article")]
    public class ArticleController : Controller
    {
        private readonly IApi _api;
        private readonly ILogger<ArticleController> _logger;
        private readonly ArticleSubmissionRepository _repository;
        private readonly UserManager<User> _userManager;
        private readonly IDynamicWorkflowService _workflowService;
        
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
            IDynamicWorkflowService workflowService)
        {
            _api = api;
            _logger = logger;
            _repository = repository;
            _userManager = userManager;
            _workflowService = workflowService;
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
        /// Lists articles in the workflow using dynamic workflow configuration.
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
            
            var userRoles = await _userManager.GetRolesAsync(user);
            
            // Get all workflows for articles
            var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("post");
            _logger.LogInformation("Found {WorkflowCount} workflows for 'post' content type", workflows.Count());
            
            var allArticles = new List<SubmittedArticle>();
            var accessibleStates = new HashSet<string>();
            
            foreach (var workflow in workflows)
            {
                _logger.LogInformation("Processing workflow {WorkflowId} ({WorkflowName})", workflow.Id, workflow.Name);
                
                // Get effective roles for this user in this workflow
                var effectiveRoles = await _workflowService.GetEffectiveRolesAsync(workflow.Id, userRoles);
                _logger.LogInformation("User has {EffectiveRoleCount} effective roles in workflow {WorkflowId}: {RoleNames}", 
                    effectiveRoles.Count(), workflow.Id, string.Join(", ", effectiveRoles.Select(r => r.RoleKey)));
                
                if (effectiveRoles.Any())
                {
                    // For each effective role, determine which states they can view
                    foreach (var role in effectiveRoles)
                    {
                        // If role can view all content, get all articles in this workflow
                        if (role.CanViewAll)
                        {
                            _logger.LogInformation("Role {RoleKey} can view all content, getting all articles for workflow {WorkflowId}", role.RoleKey, workflow.Id);
                            var allWorkflowArticles = await GetArticlesForWorkflowAsync(workflow.Id);
                            _logger.LogInformation("Found {ArticleCount} articles for workflow {WorkflowId}", allWorkflowArticles.Count, workflow.Id);
                            allArticles.AddRange(allWorkflowArticles);
                            
                            // Add all states from this workflow to accessible states
                            foreach (var state in workflow.States)
                            {
                                accessibleStates.Add(state.Key);
                            }
                            break; // No need to check other roles if this one can view all
                        }
                        else
                        {
                            // Get allowed states for this role
                            var allowedStates = role.GetAllowedFromStates();
                            
                            if (allowedStates.Length == 0)
                            {
                                // If no specific states defined, can view all states
                                _logger.LogInformation("Role {RoleKey} has no specific allowed states, getting all articles for workflow {WorkflowId}", role.RoleKey, workflow.Id);
                                var allWorkflowArticles = await GetArticlesForWorkflowAsync(workflow.Id);
                                _logger.LogInformation("Found {ArticleCount} articles for workflow {WorkflowId}", allWorkflowArticles.Count, workflow.Id);
                                allArticles.AddRange(allWorkflowArticles);
                                
                                foreach (var state in workflow.States)
                                {
                                    accessibleStates.Add(state.Key);
                                }
                            }
                            else
                            {
                                // Get articles only for allowed states
                                foreach (var state in allowedStates)
                                {
                                    var stateArticles = await GetArticlesForWorkflowStateAsync(workflow.Id, state);
                                    allArticles.AddRange(stateArticles);
                                    accessibleStates.Add(state);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("User has no effective roles in workflow {WorkflowId}, skipping this workflow", workflow.Id);
                }
            }
            
            // Remove duplicates and apply user ownership filter for non-admin roles
            var uniqueArticles = allArticles.GroupBy(a => a.Id).Select(g => g.First()).ToList();
            _logger.LogInformation("Found {ArticleCount} unique articles across all workflows", uniqueArticles.Count);
            
            // Apply additional filtering based on user permissions
            var filteredArticles = new List<SubmittedArticle>();
            foreach (var article in uniqueArticles)
            {
                var articleWorkflowId = await GetWorkflowIdFromArticleAsync(article);
                var articleWorkflow = workflows.FirstOrDefault(w => w.Id == articleWorkflowId);
                if (articleWorkflow != null)
                {
                    // Use workflow state if available, otherwise convert from status
                    var currentState = !string.IsNullOrEmpty(article.WorkflowState) 
                        ? article.WorkflowState 
                        : GetWorkflowStateFromStatus(article.Status);
                        
                    var canView = await _workflowService.CanViewContentAsync(
                        articleWorkflow.Id, 
                        currentState, 
                        userRoles, 
                        article.Submission.Author, // Using Author as owner ID for now
                        userId);
                    
                    if (canView)
                    {
                        filteredArticles.Add(article);
                    }
                }
            }
            
            ViewBag.UserRoles = userRoles;
            ViewBag.AccessibleStates = accessibleStates;
            ViewBag.Workflows = workflows;
            
            var finalArticles = filteredArticles.OrderByDescending(a => a.LastModified).ToList();
            _logger.LogInformation("Returning {FinalArticleCount} articles after filtering", finalArticles.Count);
            
            return View(finalArticles);
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
            
            // Get workflow information for permissions check and transitions
            var articleWorkflowId = await GetWorkflowIdFromArticleAsync(article);
            var currentState = !string.IsNullOrEmpty(article.WorkflowState) 
                ? article.WorkflowState 
                : GetWorkflowStateFromStatus(article.Status);

            // Use dynamic workflow permissions instead of hardcoded role checks
            if (!roles.Contains("SysAdmin"))
            {
                var canView = await _workflowService.CanViewContentAsync(
                    articleWorkflowId,
                    currentState,
                    roles,
                    article.Submission.Author, // Using Author as owner ID
                    userId);
                
                if (!canView)
                {
                    return Forbid();
                }
            }
                
            var availableTransitions = await _workflowService.GetAvailableTransitionsAsync(
                articleWorkflowId, currentState, roles, article.Id, userId);
            
            ViewBag.UserRoles = roles;
            ViewBag.AvailableTransitions = availableTransitions?.ToList() ?? new List<WorkflowTransition>();
            ViewBag.CurrentState = currentState;
            
            return View(article);
        }

        /// <summary>
        /// Executes a workflow transition on an article.
        /// </summary>
        [Authorize]
        [Route("review/{id:Guid}")]
        [HttpPost]
        public async Task<IActionResult> Review(Guid id, string targetState, string feedback)
        {
            using var activity = ActivitySource.StartActivity("ArticleController.Review");
            
            activity?.SetTag("articleId", id.ToString());
            activity?.SetTag("targetState", targetState);
            activity?.SetTag("userId", User.Identity?.Name ?? "unknown");
            
            var article = await _repository.GetSubmissionByIdAsync(id);
            
            if (article == null)
            {
                activity?.SetTag("outcome", "not_found");
                return NotFound();
            }
            
            var currentState = !string.IsNullOrEmpty(article.WorkflowState) 
                ? article.WorkflowState 
                : GetWorkflowStateFromStatus(article.Status);
            
            activity?.SetTag("previousState", currentState);
            
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
                // Get workflow information
                var articleWorkflowId = await GetWorkflowIdFromArticleAsync(article);
                var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("post");
                var workflow = workflows.FirstOrDefault(w => w.Id == articleWorkflowId);
                
                if (workflow == null)
                {
                    throw new InvalidOperationException("No workflow found for this article");
                }
                
                // Find the transition from current state to target state
                var availableTransitions = await _workflowService.GetAvailableTransitionsAsync(
                    workflow.Id, currentState, roles, id, userId);
                
                var transition = availableTransitions.FirstOrDefault(t => t.ToStateKey == targetState);
                
                if (transition == null)
                {
                    activity?.SetTag("outcome", "no_transition");
                    throw new InvalidOperationException($"No available transition from '{currentState}' to '{targetState}' for current user roles");
                }
                
                // Check if user can execute this transition
                var canExecute = await _workflowService.CanExecuteTransitionAsync(
                    transition.Id, roles, id, userId);
                
                if (!canExecute)
                {
                    activity?.SetTag("outcome", "forbidden");
                    return Forbid();
                }
                
                // Execute the workflow transition
                await _repository.UpdateSubmissionWorkflowStateAsync(id, targetState, userId, feedback);
                
                ArticleReviewsCounter.Add(1, 
                    new KeyValuePair<string, object?>("status", "success"), 
                    new KeyValuePair<string, object?>("previousState", currentState),
                    new KeyValuePair<string, object?>("newState", targetState));
                
                activity?.SetTag("outcome", "success");
                
                _logger.LogInformation("Article {ArticleId} transitioned from {PreviousState} to {NewState} by user {UserId}", 
                    id, currentState, targetState, User.Identity?.Name);
                
                return RedirectToAction("Workflow");
            }
            catch (Exception ex)
            {
                ArticleReviewsCounter.Add(1, 
                    new KeyValuePair<string, object?>("status", "error"), 
                    new KeyValuePair<string, object?>("previousState", currentState),
                    new KeyValuePair<string, object?>("targetState", targetState));
                
                activity?.SetTag("outcome", "error");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _logger.LogError(ex, "Error executing workflow transition");
                ModelState.AddModelError("", $"An error occurred while updating the article: {ex.Message}");
                ViewBag.UserRoles = roles;
                return View(article);
            }
        }

        /// <summary>
        /// Helper method to convert ArticleStatus to workflow state
        /// </summary>
        private static string GetWorkflowStateFromStatus(ArticleStatus status)
        {
            return status switch
            {
                ArticleStatus.Draft => "draft",
                ArticleStatus.InReview => "in_review",
                ArticleStatus.Rejected => "rejected",
                ArticleStatus.Approved => "approved",
                ArticleStatus.Published => "published",
                ArticleStatus.Archived => "archived",
                _ => "draft"
            };
        }

        /// <summary>
        /// Gets all articles for a specific workflow
        /// </summary>
        private async Task<List<SubmittedArticle>> GetArticlesForWorkflowAsync(Guid workflowId)
        {
            // Get all articles and filter by workflow ID
            var allArticles = await _repository.GetSubmissionsAsync(null, null);
            _logger.LogInformation("GetArticlesForWorkflowAsync: Found {TotalArticles} total articles, looking for workflow {WorkflowId}", allArticles.Count, workflowId);
            
            var result = new List<SubmittedArticle>();
            
            foreach (var article in allArticles)
            {
                var articleWorkflowId = await GetWorkflowIdFromArticleAsync(article);
                _logger.LogInformation("Article {ArticleId}: WorkflowId={ArticleWorkflowId}, looking for {TargetWorkflowId}", 
                    article.Id, articleWorkflowId, workflowId);
                
                if (articleWorkflowId == workflowId)
                {
                    result.Add(article);
                    _logger.LogInformation("Article {ArticleId} matched workflow {WorkflowId}", article.Id, workflowId);
                }
            }
            
            _logger.LogInformation("GetArticlesForWorkflowAsync: Returning {ResultCount} articles for workflow {WorkflowId}", result.Count, workflowId);
            return result;
        }

        /// <summary>
        /// Gets articles for a specific workflow state
        /// </summary>
        private async Task<List<SubmittedArticle>> GetArticlesForWorkflowStateAsync(Guid workflowId, string state)
        {
            // Get articles in specific workflow state
            var workflowArticles = await GetArticlesForWorkflowAsync(workflowId);
            var targetStatus = GetStatusFromWorkflowState(state);
            return workflowArticles.Where(a => a.Status == targetStatus).ToList();
        }

        /// <summary>
        /// Helper method to get workflow ID from article
        /// </summary>
        private async Task<Guid> GetWorkflowIdFromArticleAsync(SubmittedArticle article)
        {
            // If article has workflow ID, use it
            if (article.WorkflowId.HasValue && article.WorkflowId.Value != Guid.Empty)
            {
                return article.WorkflowId.Value;
            }
            
            // Otherwise, get the default workflow for articles
            var defaultWorkflow = await _workflowService.GetDefaultWorkflowAsync("post");
            return defaultWorkflow?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Helper method to convert workflow state to ArticleStatus
        /// </summary>
        private static ArticleStatus GetStatusFromWorkflowState(string workflowState)
        {
            return workflowState?.ToLower() switch
            {
                "draft" => ArticleStatus.Draft,
                "in_review" => ArticleStatus.InReview,
                "rejected" => ArticleStatus.Rejected,
                "approved" => ArticleStatus.Approved,
                "published" => ArticleStatus.Published,
                "archived" => ArticleStatus.Archived,
                _ => ArticleStatus.Draft
            };
        }
    }
}