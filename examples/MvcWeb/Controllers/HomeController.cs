using Microsoft.AspNetCore.Mvc;
using Piranha;
using Piranha.AspNetCore.Services;
using Piranha.Models;

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
            var startPage = await _api.Pages.GetStartpageAsync();
            
            if (startPage == null)
            {
                return NotFound();
            }
            
            // Redirect to the /page route with the ID as a query parameter
            return Redirect($"/page?id={startPage.Id}");
        }
    }
}