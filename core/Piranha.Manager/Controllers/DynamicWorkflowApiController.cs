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
using Piranha.Models;
using Piranha.Services;
using Piranha.Repositories;
using System.Security.Claims;

namespace Piranha.Manager.Controllers;

/// <summary>
/// Dynamic workflow API controller that provides configurable workflow management
/// without hardcoded role dependencies.
/// </summary>
[Route("manager/api/dynamicworkflow")]
[Authorize(Policy = Permission.Admin)]
[ApiController]
public class DynamicWorkflowApiController : Controller
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicWorkflowService _dynamicWorkflowService;

    public DynamicWorkflowApiController(IWorkflowRepository workflowRepository, IDynamicWorkflowService dynamicWorkflowService)
    {
        _workflowRepository = workflowRepository;
        _dynamicWorkflowService = dynamicWorkflowService;
    }

    /// <summary>
    /// Gets all available workflows for a content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>Available workflows</returns>
    [HttpGet("workflows/{contentType}")]
    public async Task<IActionResult> GetWorkflowsForContentType(string contentType)
    {
        try
        {
            var workflows = await _dynamicWorkflowService.GetWorkflowsForContentTypeAsync(contentType);
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets available transitions for the current user and content state.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="currentState">The current workflow state</param>
    /// <param name="contentId">The content ID</param>
    /// <returns>Available transitions</returns>
    [HttpGet("transitions/{workflowId}/{currentState}")]
    public async Task<IActionResult> GetAvailableTransitions(Guid workflowId, string currentState, [FromQuery] Guid contentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            var transitions = await _dynamicWorkflowService.GetAvailableTransitionsAsync(
                workflowId, currentState, userRoles, contentId, userId);

            return Ok(transitions);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Checks if the current user can execute a specific transition.
    /// </summary>
    /// <param name="transitionId">The transition ID</param>
    /// <param name="contentId">The content ID</param>
    /// <returns>Permission result</returns>
    [HttpGet("can-execute/{transitionId}")]
    public async Task<IActionResult> CanExecuteTransition(Guid transitionId, [FromQuery] Guid contentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            var canExecute = await _dynamicWorkflowService.CanExecuteTransitionAsync(
                transitionId, userRoles, contentId, userId);

            return Ok(new { canExecute });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Checks if the current user can view specific content.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="contentState">The content's workflow state</param>
    /// <param name="contentOwnerId">The content owner's ID</param>
    /// <returns>View permission result</returns>
    [HttpGet("can-view/{workflowId}/{contentState}")]
    public async Task<IActionResult> CanViewContent(Guid workflowId, string contentState, [FromQuery] string contentOwnerId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            var canView = await _dynamicWorkflowService.CanViewContentAsync(
                workflowId, contentState, userRoles, contentOwnerId, userId);

            return Ok(new { canView });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the effective roles for the current user in a workflow.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <returns>Effective workflow roles</returns>
    [HttpGet("effective-roles/{workflowId}")]
    public async Task<IActionResult> GetEffectiveRoles(Guid workflowId)
    {
        try
        {
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
            var effectiveRoles = await _dynamicWorkflowService.GetEffectiveRolesAsync(workflowId, userRoles);

            return Ok(effectiveRoles);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new dynamic workflow for a content type.
    /// </summary>
    /// <param name="request">The workflow creation request</param>
    /// <returns>The created workflow</returns>
    [HttpPost("create")]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        try
        {
            var workflow = await _dynamicWorkflowService.CreateDefaultWorkflowAsync(
                request.ContentType, request.WorkflowName);

            return Ok(workflow);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Validates a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow to validate</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateWorkflow([FromBody] WorkflowDefinition workflow)
    {
        try
        {
            var result = await _dynamicWorkflowService.ValidateWorkflowAsync(workflow);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets workflow analytics.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="fromDate">Start date for analytics</param>
    /// <param name="toDate">End date for analytics</param>
    /// <returns>Workflow analytics</returns>
    [HttpGet("analytics/{workflowId}")]
    public async Task<IActionResult> GetWorkflowAnalytics(Guid workflowId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            var from = fromDate ?? DateTime.Now.AddMonths(-1);
            var to = toDate ?? DateTime.Now;

            var analytics = await _dynamicWorkflowService.GetWorkflowAnalyticsAsync(workflowId, from, to);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets all workflow definitions (admin only).
    /// </summary>
    /// <returns>All workflow definitions</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllWorkflows()
    {
        try
        {
            var workflows = await _workflowRepository.GetAllAsync();
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Saves a workflow definition (admin only).
    /// </summary>
    /// <param name="workflow">The workflow to save</param>
    /// <returns>Save result</returns>
    [HttpPost("save")]
    public async Task<IActionResult> SaveWorkflow([FromBody] WorkflowDefinition workflow)
    {
        try
        {
            await _workflowRepository.SaveAsync(workflow);
            return Ok(new { message = "Workflow saved successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a workflow definition (admin only).
    /// </summary>
    /// <param name="id">The workflow ID</param>
    /// <returns>Delete result</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkflow(Guid id)
    {
        try
        {
            await _workflowRepository.DeleteAsync(id);
            return Ok(new { message = "Workflow deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// Request model for creating a new workflow.
/// </summary>
public class CreateWorkflowRequest
{
    public string ContentType { get; set; }
    public string WorkflowName { get; set; }
}