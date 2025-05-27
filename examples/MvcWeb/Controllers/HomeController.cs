using Microsoft.AspNetCore.Mvc;
using Piranha;
using Piranha.AspNetCore.Services;
using Piranha.Models;
using MvcWeb.Services;
using System.Diagnostics;

namespace MvcWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly IApi _api;
        private readonly IModelLoader _loader;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="api">The current api</param>
        /// <param name="loader">The model loader</param>
        public HomeController(IApi api, IModelLoader loader)
        {
            _api = api;
            _loader = loader;
        }

        [Route("/")]
        public async Task<IActionResult> Index()
        {
            using var activity = MetricsService.StartActivity("HomeController.Index");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Record page view for home page
                var userRole = User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
                MetricsService.RecordPageView("home", userRole);
                
                // Record user action
                MetricsService.RecordUserAction("home_page_visit", "navigation");
                
                var startPage = await _api.Pages.GetStartpageAsync();
                
                if (startPage == null)
                {
                    stopwatch.Stop();
                    MetricsService.RecordHttpRequest("GET", "/", 404, stopwatch.ElapsedMilliseconds);
                    MetricsService.RecordError("startpage_not_found", "warning", "HomeController");
                    
                    activity?.SetTag("outcome", "not_found");
                    activity?.SetTag("error", "startpage_not_configured");
                    
                    return NotFound();
                }
                
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("GET", "/", 302, stopwatch.ElapsedMilliseconds);
                
                activity?.SetTag("outcome", "redirect_to_startpage");
                activity?.SetTag("startpage_id", startPage.Id.ToString());
                activity?.SetTag("user_authenticated", User.Identity?.IsAuthenticated ?? false);
                
                // Record successful home page load
                MetricsService.RecordUserAction("startpage_redirect", "navigation");
                
                // Redirect to the /page route with the ID as a query parameter
                return Redirect($"/page?id={startPage.Id}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("GET", "/", 500, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("home_controller_exception", "error", "HomeController");
                
                activity?.SetTag("outcome", "error");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                throw;
            }
        }
    }
}