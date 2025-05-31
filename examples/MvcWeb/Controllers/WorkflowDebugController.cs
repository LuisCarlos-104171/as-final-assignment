using Microsoft.AspNetCore.Mvc;
using Piranha.Services;

namespace MvcWeb.Controllers
{
    [Route("debug/workflow")]
    public class WorkflowDebugController : Controller
    {
        private readonly IDynamicWorkflowService _workflowService;

        public WorkflowDebugController(IDynamicWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var result = new
            {
                Message = "Workflow Debug Information",
                DefaultWorkflow = await GetDefaultWorkflowInfo(),
                AllWorkflows = await GetAllWorkflowsInfo(),
                ArticleWorkflows = await GetArticleWorkflowsInfo()
            };

            return Json(result);
        }

        [HttpPost("fix")]
        public async Task<IActionResult> FixWorkflows()
        {
            try
            {
                // Force creation of a complete workflow
                var workflow = await _workflowService.CreateDefaultWorkflowAsync("post", "Article Approval Workflow");
                
                var result = new
                {
                    Success = true,
                    Message = "Created complete workflow with roles",
                    WorkflowId = workflow.Id,
                    WorkflowName = workflow.Name,
                    RoleCount = workflow.Roles?.Count ?? 0,
                    StateCount = workflow.States?.Count ?? 0,
                    TransitionCount = workflow.Transitions?.Count ?? 0
                };
                
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Error = ex.Message });
            }
        }

        private async Task<object> GetDefaultWorkflowInfo()
        {
            try
            {
                var defaultWorkflow = await _workflowService.GetDefaultWorkflowAsync("post");
                if (defaultWorkflow == null)
                {
                    return new { Found = false, Message = "No default workflow found for 'post'" };
                }
                return new
                {
                    Found = true,
                    Id = defaultWorkflow.Id,
                    Name = defaultWorkflow.Name,
                    ContentTypes = defaultWorkflow.ContentTypes,
                    ContentTypesArray = defaultWorkflow.GetContentTypes(),
                    IsDefault = defaultWorkflow.IsDefault,
                    IsActive = defaultWorkflow.IsActive,
                    InitialState = defaultWorkflow.InitialState
                };
            }
            catch (Exception ex)
            {
                return new { Found = false, Error = ex.Message };
            }
        }

        private async Task<object> GetAllWorkflowsInfo()
        {
            try
            {
                var allWorkflows = await _workflowService.GetWorkflowsForContentTypeAsync("post");
                var workflowList = allWorkflows.ToList();
                
                return new
                {
                    Count = workflowList.Count,
                    Workflows = workflowList.Select(w => new
                    {
                        Id = w.Id,
                        Name = w.Name,
                        ContentTypes = w.ContentTypes,
                        ContentTypesArray = w.GetContentTypes(),
                        IsDefault = w.IsDefault,
                        IsActive = w.IsActive,
                        InitialState = w.InitialState
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                return new { Count = 0, Error = ex.Message };
            }
        }

        private async Task<object> GetArticleWorkflowsInfo()
        {
            try
            {
                var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("post");
                return new
                {
                    SearchTerm = "post",
                    Found = workflows.Any(),
                    Count = workflows.Count(),
                    Details = workflows.Select(w => new
                    {
                        Name = w.Name,
                        ContentTypes = w.ContentTypes,
                        ContainsPost = w.GetContentTypes().Contains("post"),
                        IsActive = w.IsActive,
                        IsDefault = w.IsDefault
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                return new { SearchTerm = "post", Found = false, Error = ex.Message };
            }
        }
    }
}