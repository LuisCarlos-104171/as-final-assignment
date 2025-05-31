/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcWeb.Models;
using Piranha;
using Piranha.Services;
using System.Security.Claims;

namespace MvcWeb.Controllers;

/// <summary>
/// Dynamic article controller that uses configurable workflows instead of hardcoded role checks.
/// This controller demonstrates how to replace tight coupling between roles and workflow logic
/// with a flexible, configurable system.
/// </summary>
[Authorize]
public class DynamicArticleController : Controller
{
    private readonly IApi _api;
    private readonly DynamicArticleSubmissionRepository _repository;
    private readonly IDynamicWorkflowService _workflowService;
    private readonly UserManager<IdentityUser> _userManager;

    public DynamicArticleController(
        IApi api, 
        DynamicArticleSubmissionRepository repository, 
        IDynamicWorkflowService workflowService,
        UserManager<IdentityUser> userManager)
    {
        _api = api;
        _repository = repository;
        _workflowService = workflowService;
        _userManager = userManager;
    }

    /// <summary>
    /// Shows the article submission form.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Submit()
    {
        var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
        if (!workflows.Any())
        {
            // Create default workflow if none exists
            await _workflowService.CreateDefaultWorkflowAsync("article", "Article Approval Workflow");
            workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
        }

        var model = new DynamicArticleSubmissionModel
        {
            Workflow = workflows.First(),
            WorkflowId = workflows.First().Id
        };

        return View(model);
    }

    /// <summary>
    /// Handles article submission.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(DynamicArticleSubmissionModel model)
    {
        if (!ModelState.IsValid)
        {
            var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
            model.Workflow = workflows.FirstOrDefault();
            return View(model);
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            model.AuthorId = user.Id;
            model.Author = user.UserName;

            var createdArticle = await _repository.CreateAsync(model);
            
            TempData["SuccessMessage"] = "Article submitted successfully!";
            return RedirectToAction("ThankYou", new { id = createdArticle.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error submitting article: {ex.Message}");
            var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
            model.Workflow = workflows.FirstOrDefault();
            return View(model);
        }
    }

    /// <summary>
    /// Shows thank you page after submission.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ThankYou(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

        var article = await _repository.GetByIdAsync(id, userRoles, userId);
        if (article == null)
        {
            return NotFound();
        }

        return View(article);
    }

    /// <summary>
    /// Shows the workflow dashboard with dynamic content filtering based on user's workflow roles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Workflow()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

        // Get all workflows for articles
        var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
        var allArticles = new List<DynamicArticleSubmissionModel>();

        foreach (var workflow in workflows)
        {
            var effectiveRoles = await _workflowService.GetEffectiveRolesAsync(workflow.Id, userRoles);
            
            if (effectiveRoles.Any())
            {
                // Get articles for states this user can work with
                var articles = await _repository.GetSubmissionsAsync(userRoles, userId);
                allArticles.AddRange(articles.Where(a => a.WorkflowId == workflow.Id));
            }
        }

        // Group articles by workflow state for dashboard view
        var groupedArticles = allArticles
            .GroupBy(a => a.WorkflowState)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.GroupedArticles = groupedArticles;
        ViewBag.UserRoles = userRoles;
        ViewBag.Workflows = workflows;

        return View(allArticles.OrderByDescending(a => a.Created).ToList());
    }

    /// <summary>
    /// Shows article review page with dynamic workflow transitions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Review(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

        var article = await _repository.GetByIdAsync(id, userRoles, userId);
        if (article == null)
        {
            return NotFound();
        }

        // Check if user can view this article
        var canView = await _workflowService.CanViewContentAsync(
            article.WorkflowId, article.WorkflowState, userRoles, article.AuthorId, userId);

        if (!canView)
        {
            return Forbid();
        }

        ViewBag.UserRoles = userRoles;
        return View(article);
    }

    /// <summary>
    /// Executes a workflow transition dynamically based on configured permissions.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteTransition(WorkflowTransitionRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
            var user = await _userManager.GetUserAsync(User);

            var updatedArticle = await _repository.ExecuteTransitionAsync(
                request, userRoles, userId, user.UserName);

            TempData["SuccessMessage"] = "Workflow transition completed successfully!";
            return RedirectToAction("Review", new { id = request.ArticleId });
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Review", new { id = request.ArticleId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error executing transition: {ex.Message}";
            return RedirectToAction("Review", new { id = request.ArticleId });
        }
    }

    /// <summary>
    /// API endpoint to get available transitions for an article.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableTransitions(Guid articleId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            var article = await _repository.GetByIdAsync(articleId, userRoles, userId);
            if (article == null)
            {
                return NotFound();
            }

            var transitions = await _workflowService.GetAvailableTransitionsAsync(
                article.WorkflowId, article.WorkflowState, userRoles, articleId, userId);

            return Json(transitions);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin endpoint to configure workflows (requires admin role).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SysAdmin")]
    public async Task<IActionResult> ConfigureWorkflows()
    {
        var workflows = await _workflowService.GetWorkflowsForContentTypeAsync("article");
        
        var model = new WorkflowConfigurationModel
        {
            AvailableWorkflows = workflows.ToList()
        };

        if (workflows.Any())
        {
            model.SelectedWorkflow = workflows.First();
            model.WorkflowRoles = model.SelectedWorkflow.Roles.ToList();
            
            // Group transitions by from state for easier management
            model.TransitionsByState = model.SelectedWorkflow.Transitions
                .GroupBy(t => t.FromStateKey)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        return View(model);
    }

    /// <summary>
    /// Creates a new workflow for articles.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SysAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWorkflow(string workflowName)
    {
        try
        {
            var workflow = await _workflowService.CreateDefaultWorkflowAsync("article", workflowName);
            TempData["SuccessMessage"] = $"Workflow '{workflowName}' created successfully!";
            return RedirectToAction("ConfigureWorkflows");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error creating workflow: {ex.Message}";
            return RedirectToAction("ConfigureWorkflows");
        }
    }

    /// <summary>
    /// Gets workflow analytics for monitoring and optimization.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SysAdmin")]
    public async Task<IActionResult> Analytics(Guid workflowId, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var from = fromDate ?? DateTime.Now.AddMonths(-1);
            var to = toDate ?? DateTime.Now;

            var analytics = await _repository.GetAnalyticsAsync(workflowId, from, to);
            return Json(analytics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}