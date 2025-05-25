using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Models;
using System.Security.Claims;

namespace MvcWeb.Controllers
{
    [Route("article")]
    public class ArticleController : Controller
    {
        private readonly IApi _api;
        private readonly ILogger<ArticleController> _logger;
        private readonly ArticleSubmissionRepository _repository;
        private readonly UserManager<User> _userManager;

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
            if (ModelState.IsValid)
            {
                try
                {
                    // Add the submission
                    var submission = await _repository.AddSubmissionAsync(model, blogId);
                    
                    // Redirect to a thank you page
                    return RedirectToAction("ThankYou", new { id = submission.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error submitting article");
                    ModelState.AddModelError("", "An error occurred while submitting your article. Please try again.");
                }
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
                if (status == ArticleStatus.InReview || status == ArticleStatus.Rejected)
                {
                    if (!roles.Contains("Editor"))
                    {
                        return Forbid();
                    }
                }
                
                if (status == ArticleStatus.Approved || status == ArticleStatus.Published)
                {
                    if (!roles.Contains("Approver"))
                    {
                        return Forbid();
                    }
                }
            }
            
            try
            {
                await _repository.UpdateSubmissionStatusAsync(id, status, userId, feedback);
                return RedirectToAction("Workflow");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating article status");
                ModelState.AddModelError("", "An error occurred while updating the article status.");
                ViewBag.UserRoles = roles;
                return View(article);
            }
        }
    }
}