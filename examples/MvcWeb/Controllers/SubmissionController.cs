using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using Piranha;
using Piranha.Models;

namespace MvcWeb.Controllers
{
    /// <summary>
    /// Controller for displaying article submissions on the frontend
    /// </summary>
    [Route("submissions")]
    public class SubmissionController : Controller
    {
        private readonly IApi _api;
        private readonly ArticleSubmissionRepository _repository;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SubmissionController(IApi api, ArticleSubmissionRepository repository)
        {
            _api = api;
            _repository = repository;
        }

        /// <summary>
        /// Gets the article submission listing page.
        /// </summary>
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index()
        {
            // Get all published submissions
            var submissions = await _repository.GetSubmissionsAsync(ArticleStatus.Published);
            
            return View(submissions);
        }

        /// <summary>
        /// Gets a specific published submission.
        /// </summary>
        [Route("{id:Guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var submission = await _repository.GetSubmissionByIdAsync(id);
            
            if (submission == null || submission.Status != ArticleStatus.Published)
            {
                return NotFound();
            }
            
            // If this submission has a published post, redirect to that post instead
            if (submission.PostId.HasValue)
            {
                var post = await _api.Posts.GetByIdAsync<PostInfo>(submission.PostId.Value);
                if (post != null)
                {
                    return RedirectPermanent(post.Permalink);
                }
            }
            
            return View(submission);
        }
    }
}