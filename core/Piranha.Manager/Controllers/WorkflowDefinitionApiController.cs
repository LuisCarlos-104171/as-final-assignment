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
using Piranha.Manager.Models.Workflow;
using Piranha.Manager.Services;

namespace Piranha.Manager.Controllers;

/// <summary>
/// API controller for workflow definition management.
/// </summary>
[Area("Manager")]
[Route("manager/api/workflow-definitions")]
[Authorize(Policy = Permission.Admin)]
[ApiController]
public class WorkflowDefinitionApiController : Controller
{
    private readonly WorkflowDefinitionManagerService _service;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="service">The workflow definition manager service</param>
    public WorkflowDefinitionApiController(WorkflowDefinitionManagerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets all workflow definitions.
    /// </summary>
    /// <returns>The workflow definition list</returns>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var model = await _service.GetListAsync();
        return Ok(model);
    }

    /// <summary>
    /// Gets the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The workflow definition id</param>
    /// <returns>The workflow definition</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var model = await _service.GetEditModelAsync(id);
        if (model.Id == Guid.Empty)
        {
            return NotFound();
        }
        return Ok(model);
    }

    /// <summary>
    /// Gets a new workflow definition edit model.
    /// </summary>
    /// <returns>The workflow definition edit model</returns>
    [HttpGet("new")]
    public async Task<IActionResult> Create()
    {
        var model = await _service.GetEditModelAsync();
        return Ok(model);
    }

    /// <summary>
    /// Saves the given workflow definition.
    /// </summary>
    /// <param name="model">The workflow definition</param>
    /// <returns>The result</returns>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] Models.Workflow.WorkflowDefinitionEditModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = string.Join(", ", errors)
            });
        }

        var result = await _service.SaveAsync(model);
        
        if (result.Type == StatusMessage.Success)
        {
            return Ok(result);
        }
        
        return BadRequest(result);
    }

    /// <summary>
    /// Deletes the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The workflow definition id</param>
    /// <returns>The result</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _service.DeleteAsync(id);
        
        if (result.Type == StatusMessage.Success)
        {
            return Ok(result);
        }
        
        return BadRequest(result);
    }

    /// <summary>
    /// Creates a default workflow.
    /// </summary>
    /// <param name="request">The create request</param>
    /// <returns>The created workflow id</returns>
    [HttpPost("create-default")]
    public async Task<IActionResult> CreateDefault([FromBody] CreateDefaultWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = "Workflow name is required"
            });
        }

        if (request.ContentTypes == null || request.ContentTypes.Length == 0)
        {
            return BadRequest(new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = "At least one content type must be selected"
            });
        }

        try
        {
            var workflowId = await _service.CreateDefaultWorkflowAsync(request.Name, request.ContentTypes);
            
            return Ok(new
            {
                Id = workflowId,
                Message = "Default workflow created successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = ex.Message
            });
        }
    }

    /// <summary>
    /// Request model for creating a default workflow.
    /// </summary>
    public class CreateDefaultWorkflowRequest
    {
        /// <summary>
        /// Gets/sets the workflow name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets/sets the content types.
        /// </summary>
        public string[] ContentTypes { get; set; }
    }
}