using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using Piranha;
using Piranha.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

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
        
        private static readonly ActivitySource ActivitySource = new("MvcWeb.SubmissionController");
        private static readonly Meter Meter = new("MvcWeb.SubmissionController");
        private static readonly Counter<int> SubmissionViewsCounter = Meter.CreateCounter<int>("submission_views_total", "Total number of submission views");
        private static readonly Counter<int> SubmissionListViewsCounter = Meter.CreateCounter<int>("submission_list_views_total", "Total number of submission list views");

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
            using var activity = ActivitySource.StartActivity("SubmissionController.Index");
            
            // Get all published submissions
            var submissions = await _repository.GetSubmissionsAsync(ArticleStatus.Published);
            
            SubmissionListViewsCounter.Add(1, new KeyValuePair<string, object?>("submissionCount", submissions.Count));
            activity?.SetTag("submissionCount", submissions.Count);
            activity?.SetTag("outcome", "success");
            
            return View(submissions);
        }

        /// <summary>
        /// Gets a specific published submission.
        /// </summary>
        [Route("{id:Guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            using var activity = ActivitySource.StartActivity("SubmissionController.Detail");
            
            activity?.SetTag("submissionId", id.ToString());
            
            var submission = await _repository.GetSubmissionByIdAsync(id);
            
            if (submission == null || submission.Status != ArticleStatus.Published)
            {
                SubmissionViewsCounter.Add(1, new KeyValuePair<string, object?>("status", "not_found"));
                activity?.SetTag("outcome", "not_found");
                return NotFound();
            }
            
            activity?.SetTag("submissionTitle", submission.Submission.Title);
            activity?.SetTag("hasPostId", submission.PostId.HasValue);
            
            // If this submission has a published post, redirect to that post instead
            if (submission.PostId.HasValue)
            {
                var post = await _api.Posts.GetByIdAsync<PostInfo>(submission.PostId.Value);
                if (post != null)
                {
                    SubmissionViewsCounter.Add(1, new KeyValuePair<string, object?>("status", "redirected_to_post"));
                    activity?.SetTag("outcome", "redirected_to_post");
                    activity?.SetTag("postPermalink", post.Permalink);
                    return RedirectPermanent(post.Permalink);
                }
            }
            
            SubmissionViewsCounter.Add(1, new KeyValuePair<string, object?>("status", "success"));
            activity?.SetTag("outcome", "success");
            
            return View(submission);
        }
    }
}