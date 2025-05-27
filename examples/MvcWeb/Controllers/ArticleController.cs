using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using MvcWeb.Services;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Models;
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
            UserManager<User> userManager)
        {
            _api = api;
            _logger = logger;
            _repository = repository;
            _userManager = userManager;
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
            
            return View(article);
        }

        /// <summary>
        /// Updates an article's status.
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
            activity?.SetTag("userRoles", string.Join(",", roles));
            
            if (!roles.Contains("SysAdmin"))
            {
                if (status == ArticleStatus.InReview || status == ArticleStatus.Rejected)
                {
                    if (!roles.Contains("Editor"))
                    {
                        activity?.SetTag("outcome", "forbidden");
                        return Forbid();
                    }
                }
                
                if (status == ArticleStatus.Approved || status == ArticleStatus.Published)
                {
                    if (!roles.Contains("Approver"))
                    {
                        activity?.SetTag("outcome", "forbidden");
                        return Forbid();
                    }
                }
            }
            
            try
            {
                await _repository.UpdateSubmissionStatusAsync(id, status, userId, feedback);
                
                ArticleReviewsCounter.Add(1, 
                    new KeyValuePair<string, object?>("status", "success"), 
                    new KeyValuePair<string, object?>("previousStatus", article.Status.ToString()),
                    new KeyValuePair<string, object?>("newStatus", status.ToString()));
                
                activity?.SetTag("outcome", "success");
                
                _logger.LogInformation("Article {ArticleId} status updated from {PreviousStatus} to {NewStatus} by user {UserId}", 
                    id, article.Status, status, User.Identity?.Name);
                
                return RedirectToAction("Workflow");
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
                
                _logger.LogError(ex, "Error updating article status");
                ModelState.AddModelError("", "An error occurred while updating the article status.");
                ViewBag.UserRoles = roles;
                return View(article);
            }
        }
    }
}