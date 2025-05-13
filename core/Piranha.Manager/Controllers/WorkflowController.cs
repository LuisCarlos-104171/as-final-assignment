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
using Microsoft.AspNetCore.Mvc;
using Piranha.Manager.Models;
using Piranha.Manager.Services;

namespace Piranha.Manager.Controllers;

/// <summary>
/// API controller for workflow management.
/// </summary>
[Area("Manager")]
[Route("manager/api/workflow")]
[Authorize(Policy = Permission.Admin)]
[ApiController]
public class WorkflowApiController : Controller
{
    private readonly WorkflowService _service;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="service">The workflow service</param>
    public WorkflowApiController(WorkflowService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets the available workflow transitions for the current content.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="contentId">The content id</param>
    /// <returns>The workflow model</returns>
    [HttpGet("transitions/{contentType}/{contentId}")]
    public async Task<IActionResult> GetTransitions(string contentType, Guid contentId)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var model = await _service.GetWorkflowTransitionsAsync(contentType, contentId, userId);
        
        return Ok(model);
    }

    /// <summary>
    /// Performs a workflow transition.
    /// </summary>
    /// <param name="model">The workflow model</param>
    /// <returns>The status message</returns>
    [HttpPost("transition")]
    public async Task<IActionResult> PerformTransition([FromBody] WorkflowModel model)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var result = await _service.PerformTransitionAsync(model, userId);
        
        return Ok(result);
    }
}

/// <summary>
/// Controller for workflow pages.
/// </summary>
[Area("Manager")]
[Authorize(Policy = Permission.Admin)]
public class WorkflowController : Controller
{
    private readonly WorkflowService _service;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="service">The workflow service</param>
    public WorkflowController(WorkflowService service)
    {
        _service = service;
    }

    /// <summary>
    /// Performs a workflow transition.
    /// </summary>
    /// <param name="model">The workflow model</param>
    /// <returns>The status message</returns>
    [HttpPost]
    public async Task<IActionResult> PerformTransition([FromBody] WorkflowModel model)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var result = await _service.PerformTransitionAsync(model, userId);
        
        return Json(result);
    }
}