using Microsoft.AspNetCore.Mvc;
using Piranha;
using Piranha.AspNetCore.Services;
using Piranha.Models;
using MvcWeb.Models;
using MvcWeb.Services;
using System.Diagnostics;

namespace MvcWeb.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("")]
public class CmsController : Controller
{
    private readonly IApi _api;
    private readonly IModelLoader _loader;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="api">The current api</param>
    public CmsController(IApi api, IModelLoader loader)
    {
        _api = api;
        _loader = loader;
    }
    
    /// <summary>
    /// Gets the index page
    /// </summary>
    [Route("cms")]
    [Route("cms/index")]
    public async Task<IActionResult> Index()
    {
        using var activity = MetricsService.StartActivity("CmsController.Index");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            MetricsService.RecordPageView("cms_index", userRole);
            MetricsService.RecordUserAction("cms_access", "navigation");
            
            var startPage = await _api.Pages.GetStartpageAsync();
            
            if (startPage == null)
            {
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("GET", "/cms", 404, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("cms_startpage_not_found", "warning", "CmsController");
                
                activity?.SetTag("outcome", "not_found");
                return NotFound();
            }
            
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/cms", 302, stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("outcome", "redirect_to_startpage");
            activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
            
            return RedirectToAction("Page", new { id = startPage.Id });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/cms", 500, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordError("cms_index_exception", "error", "CmsController");
            
            activity?.SetTag("outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the blog archive with the given id.
    /// </summary>
    /// <param name="id">The unique page id</param>
    /// <param name="year">The optional year</param>
    /// <param name="month">The optional month</param>
    /// <param name="page">The optional page</param>
    /// <param name="category">The optional category</param>
    /// <param name="tag">The optional tag</param>
    /// <param name="draft">If a draft is requested</param>
    [Route("archive")]
    public async Task<IActionResult> Archive(Guid id, int? year = null, int? month = null, int? page = null,
        Guid? category = null, Guid? tag = null, bool draft = false)
    {
        using var activity = MetricsService.StartActivity("CmsController.Archive");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            MetricsService.RecordPageView("archive", userRole);
            MetricsService.RecordContentView("archive", "view");
            MetricsService.RecordUserAction("archive_view", "content");
            
            activity?.SetTag("has_filters", (year.HasValue || month.HasValue || category.HasValue || tag.HasValue));
            activity?.SetTag("is_draft", draft);
            activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
            
            var model = await _loader.GetPageAsync<StandardArchive>(id, HttpContext.User, draft);
            model.Archive = await _api.Archives.GetByIdAsync<PostInfo>(id, page, category, tag, year, month);

            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/archive", 200, stopwatch.ElapsedMilliseconds);
            
            var loadTimeMs = stopwatch.ElapsedMilliseconds;
            MetricsService.ContentLoadTimeCounter.Record(loadTimeMs,
                new KeyValuePair<string, object?>("content_type", "archive"));
            
            activity?.SetTag("outcome", "success");
            activity?.SetTag("post_count", model.Archive?.Posts?.Count() ?? 0);
            
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/archive", 401, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordSecurityEvent("unauthorized_archive_access", "medium");
            
            activity?.SetTag("outcome", "unauthorized");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/archive", 500, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordError("archive_load_exception", "error", "CmsController");
            
            activity?.SetTag("outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the page with the given id.
    /// </summary>
    /// <param name="id">The unique page id</param>
    /// <param name="draft">If a draft is requested</param>
    [Route("page")]
    public async Task<IActionResult> Page(Guid id, bool draft = false)
    {
        using var activity = MetricsService.StartActivity("CmsController.Page");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            MetricsService.RecordPageView("page", userRole);
            MetricsService.RecordContentView("page", "view");
            MetricsService.RecordUserAction("page_view", "content");
            
            activity?.SetTag("is_draft", draft);
            activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
            
            var model = await _loader.GetPageAsync<StandardPage>(id, HttpContext.User, draft);

            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/page", 200, stopwatch.ElapsedMilliseconds);
            
            var loadTimeMs = stopwatch.ElapsedMilliseconds;
            MetricsService.ContentLoadTimeCounter.Record(loadTimeMs,
                new KeyValuePair<string, object?>("content_type", "page"));
            
            activity?.SetTag("outcome", "success");
            activity?.SetTag("page_type", model.TypeId);
            
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/page", 401, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordSecurityEvent("unauthorized_page_access", "medium");
            
            activity?.SetTag("outcome", "unauthorized");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/page", 500, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordError("page_load_exception", "error", "CmsController");
            
            activity?.SetTag("outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the post with the given id.
    /// </summary>
    /// <param name="id">The unique post id</param>
    /// <param name="draft">If a draft is requested</param>
    [Route("post")]
    public async Task<IActionResult> Post(Guid id, bool draft = false)
    {
        using var activity = MetricsService.StartActivity("CmsController.Post");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            MetricsService.RecordPageView("post", userRole);
            MetricsService.RecordContentView("post", "view");
            MetricsService.RecordUserAction("post_view", "content");
            
            activity?.SetTag("is_draft", draft);
            activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
            
            var model = await _loader.GetPostAsync<StandardPost>(id, HttpContext.User, draft);

            var commentCount = 0;
            if (model.IsCommentsOpen)
            {
                model.Comments = await _api.Posts.GetAllCommentsAsync(model.Id, true);
                commentCount = model.Comments?.Count() ?? 0;
            }
            
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/post", 200, stopwatch.ElapsedMilliseconds);
            
            var loadTimeMs = stopwatch.ElapsedMilliseconds;
            MetricsService.ContentLoadTimeCounter.Record(loadTimeMs,
                new KeyValuePair<string, object?>("content_type", "post"));
            
            activity?.SetTag("outcome", "success");
            activity?.SetTag("comments_enabled", model.IsCommentsOpen);
            activity?.SetTag("comment_count", commentCount);
            activity?.SetTag("post_type", model.TypeId);
            
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/post", 401, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordSecurityEvent("unauthorized_post_access", "medium");
            
            activity?.SetTag("outcome", "unauthorized");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("GET", "/post", 500, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordError("post_load_exception", "error", "CmsController");
            
            activity?.SetTag("outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Saves the given comment and then redirects to the post.
    /// </summary>
    /// <param name="id">The unique post id</param>
    /// <param name="commentModel">The comment model</param>
    [HttpPost]
    [Route("post/comment")]
    public async Task<IActionResult> SavePostComment(SaveCommentModel commentModel)
    {
        using var activity = MetricsService.StartActivity("CmsController.SavePostComment");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            MetricsService.RecordUserAction("comment_submission", "engagement");
            MetricsService.RecordContentView("comment", "create");
            
            activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
            activity?.SetTag("has_url", !string.IsNullOrEmpty(commentModel.CommentUrl));
            // Note: We do NOT log the actual comment content, author, or email - that would be PII
            
            var model = await _loader.GetPostAsync<StandardPost>(commentModel.Id, HttpContext.User);

            // Create the comment (contains PII but we don't log it in metrics)
            var comment = new PostComment
            {
                IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                UserAgent = Request.Headers.ContainsKey("User-Agent") ? Request.Headers["User-Agent"].ToString() : "",
                Author = commentModel.CommentAuthor,
                Email = commentModel.CommentEmail,
                Url = commentModel.CommentUrl,
                Body = commentModel.CommentBody
            };
            await _api.Posts.SaveCommentAndVerifyAsync(commentModel.Id, comment);

            stopwatch.Stop();
            MetricsService.RecordHttpRequest("POST", "/post/comment", 302, stopwatch.ElapsedMilliseconds);
            
            // Record successful comment creation (no PII)
            MetricsService.ContentCreationCounter.Add(1,
                new KeyValuePair<string, object?>("content_type", "comment"),
                new KeyValuePair<string, object?>("user_role", userRole));
            
            activity?.SetTag("outcome", "success");
            activity?.SetTag("redirect_to", "post_with_comments");
            
            return Redirect(model.Permalink + "#comments");
        }
        catch (UnauthorizedAccessException)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("POST", "/post/comment", 401, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordSecurityEvent("unauthorized_comment_attempt", "high");
            
            activity?.SetTag("outcome", "unauthorized");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.RecordHttpRequest("POST", "/post/comment", 500, stopwatch.ElapsedMilliseconds);
            MetricsService.RecordError("comment_save_exception", "error", "CmsController");
            
            activity?.SetTag("outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
