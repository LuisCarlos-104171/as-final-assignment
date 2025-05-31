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
using System.Reflection;

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
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="service">The workflow definition manager service</param>
    /// <param name="serviceProvider">The service provider</param>
    public WorkflowDefinitionApiController(WorkflowDefinitionManagerService service, IServiceProvider serviceProvider)
    {
        _service = service;
        _serviceProvider = serviceProvider;
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
    public async Task<IActionResult> Save([FromBody] WorkflowDefinitionEditModel model)
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
    /// Gets all available roles.
    /// </summary>
    /// <returns>The available roles</returns>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        try
        {
            // Try to get roles from Identity if available
            var roles = await GetRolesFromIdentity();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            // For debugging - in production this should just return empty list
            return Ok(new List<RoleViewModel>
            {
                new RoleViewModel { Id = Guid.Empty, Name = $"Debug: {ex.Message}" }
            });
        }
    }

    private Task<List<RoleViewModel>> GetRolesFromIdentity()
    {
        try
        {
            // Try to get the Identity database context using reflection
            var piranhaIdentityAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Piranha.AspNetCore.Identity");
            
            if (piranhaIdentityAssembly == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Get the IDb interface type
            var idbType = piranhaIdentityAssembly.GetType("Piranha.AspNetCore.Identity.IDb");
            if (idbType == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Get the Identity DB from DI container
            var identityDb = _serviceProvider.GetService(idbType);
            if (identityDb == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Get the Roles property
            var rolesProperty = idbType.GetProperty("Roles");
            if (rolesProperty == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            var rolesDbSet = rolesProperty.GetValue(identityDb);
            if (rolesDbSet == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Get the role type
            var roleType = piranhaIdentityAssembly.GetType("Piranha.AspNetCore.Identity.Data.Role");
            if (roleType == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Convert DbSet to List using reflection
            var toListMethod = typeof(Enumerable).GetMethods()
                .Where(m => m.Name == "ToList" && m.IsGenericMethodDefinition)
                .First().MakeGenericMethod(roleType);

            var rolesList = toListMethod.Invoke(null, new[] { rolesDbSet });
            
            if (rolesList == null)
            {
                return Task.FromResult(new List<RoleViewModel>());
            }

            // Extract Id and Name properties from each role
            var roles = new List<RoleViewModel>();
            var idProperty = roleType.GetProperty("Id");
            var nameProperty = roleType.GetProperty("Name");

            if (idProperty != null && nameProperty != null)
            {
                foreach (var role in (System.Collections.IEnumerable)rolesList)
                {
                    var id = idProperty.GetValue(role);
                    var name = nameProperty.GetValue(role);
                    
                    if (id is Guid guidId && name is string stringName)
                    {
                        roles.Add(new RoleViewModel
                        {
                            Id = guidId,
                            Name = stringName
                        });
                    }
                }
            }

            return Task.FromResult(roles.OrderBy(r => r.Name).ToList());
        }
        catch (Exception ex)
        {
            // Return empty list if something goes wrong
            return Task.FromResult(new List<RoleViewModel>());
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

    /// <summary>
    /// View model for role dropdown.
    /// </summary>
    public class RoleViewModel
    {
        /// <summary>
        /// Gets/sets the role id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets/sets the role name.
        /// </summary>
        public string Name { get; set; }
    }
}