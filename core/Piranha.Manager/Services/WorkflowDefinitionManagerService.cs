/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.AspNetCore.Identity;
using Piranha.Manager.Models;
using Piranha.Services;

namespace Piranha.Manager.Services;

/// <summary>
/// Service for managing workflow definitions in the manager.
/// </summary>
public class WorkflowDefinitionManagerService
{
    private readonly IWorkflowDefinitionService _workflowService;
    private readonly IDynamicWorkflowService _dynamicWorkflowService;
    private readonly IApi _api;
    private readonly ManagerLocalizer _localizer;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="workflowService">The workflow definition service</param>
    /// <param name="dynamicWorkflowService">The dynamic workflow service</param>
    /// <param name="api">The current API</param>
    /// <param name="localizer">The localizer</param>
    public WorkflowDefinitionManagerService(IWorkflowDefinitionService workflowService, IDynamicWorkflowService dynamicWorkflowService, IApi api, ManagerLocalizer localizer)
    {
        _workflowService = workflowService;
        _dynamicWorkflowService = dynamicWorkflowService;
        _api = api;
        _localizer = localizer;
    }

    /// <summary>
    /// Gets all workflow definitions for the list view.
    /// </summary>
    /// <returns>The workflow definition list model</returns>
    public async Task<WorkflowDefinitionListModel> GetListAsync()
    {
        var workflows = await _workflowService.GetAllAsync();
        var model = new WorkflowDefinitionListModel();

        foreach (var workflow in workflows)
        {
            model.Items.Add(new WorkflowDefinitionListModel.WorkflowDefinitionItem
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                ContentTypes = workflow.GetContentTypes(),
                IsDefault = workflow.IsDefault,
                IsActive = workflow.IsActive,
                StateCount = workflow.States?.Count ?? 0,
                TransitionCount = workflow.Transitions?.Count ?? 0,
                Created = workflow.Created,
                LastModified = workflow.LastModified
            });
        }

        return model;
    }

    /// <summary>
    /// Gets the workflow definition edit model.
    /// </summary>
    /// <param name="id">The workflow id</param>
    /// <returns>The edit model</returns>
    public async Task<WorkflowDefinitionEditModel> GetEditModelAsync(Guid? id = null)
    {
        var model = new WorkflowDefinitionEditModel();

        if (id.HasValue && id != Guid.Empty)
        {
            var workflow = await _workflowService.GetByIdAsync(id.Value);
            if (workflow != null)
            {
                model.Id = workflow.Id;
                model.Name = workflow.Name;
                model.Description = workflow.Description;
                model.ContentTypes = workflow.GetContentTypes();
                model.IsDefault = workflow.IsDefault;
                model.IsActive = workflow.IsActive;
                model.InitialState = workflow.InitialState;
                model.Created = workflow.Created;
                model.LastModified = workflow.LastModified;

                // Map states
                foreach (var state in workflow.States ?? new List<Piranha.Models.WorkflowState>())
                {
                    model.States.Add(new WorkflowStateEditModel
                    {
                        Id = state.Id,
                        WorkflowDefinitionId = state.WorkflowDefinitionId,
                        Key = state.Key,
                        Name = state.Name,
                        Description = state.Description,
                        Color = state.Color,
                        Icon = state.Icon,
                        SortOrder = state.SortOrder,
                        IsPublished = state.IsPublished,
                        IsInitial = state.IsInitial,
                        IsFinal = state.IsFinal
                    });
                }

                // Map transitions
                foreach (var transition in workflow.Transitions ?? new List<Piranha.Models.WorkflowTransition>())
                {
                    model.Transitions.Add(new WorkflowTransitionEditModel
                    {
                        Id = transition.Id,
                        WorkflowDefinitionId = transition.WorkflowDefinitionId,
                        FromStateKey = transition.FromStateKey,
                        ToStateKey = transition.ToStateKey,
                        Name = transition.Name,
                        Description = transition.Description,
                        RequiredRole = transition.RequiredPermission, // Map legacy permission field to role
                        CssClass = transition.CssClass,
                        Icon = transition.Icon,
                        SortOrder = transition.SortOrder,
                        RequiresComment = transition.RequiresComment,
                        SendNotification = transition.SendNotification,
                        NotificationTemplate = transition.NotificationTemplate
                    });
                }

                // Note: Roles are now managed through ASP.NET Core Identity system
                // Workflow-specific roles are no longer used
            }
        }
        else
        {
            // Create new workflow with default state
            model.Id = Guid.NewGuid();
            model.IsActive = true;
            model.InitialState = "draft";
            model.Created = DateTime.Now;
            model.LastModified = DateTime.Now;

            // Add default draft state
            model.States.Add(new WorkflowStateEditModel
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = model.Id,
                Key = "draft",
                Name = "Draft",
                Description = "Content is being drafted",
                Color = "#6c757d",
                Icon = "fas fa-edit",
                SortOrder = 1,
                IsInitial = true,
                IsPublished = false,
                IsFinal = false
            });

            // Note: Roles are now managed through ASP.NET Core Identity system
            // No default workflow-specific roles are created
        }

        // Set available options
        await SetAvailableOptionsAsync(model);

        return model;
    }

    /// <summary>
    /// Saves the workflow definition.
    /// </summary>
    /// <param name="model">The edit model</param>
    /// <returns>Status message</returns>
    public async Task<StatusMessage> SaveAsync(WorkflowDefinitionEditModel model)
    {
        try
        {
            // Validate the model
            var validationErrors = ValidateModel(model);
            if (validationErrors.Any())
            {
                return new StatusMessage
                {
                    Type = StatusMessage.Error,
                    Body = string.Join(", ", validationErrors)
                };
            }

            // Create the workflow definition
            var workflow = new Piranha.Models.WorkflowDefinition
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description,
                IsDefault = model.IsDefault,
                IsActive = model.IsActive,
                InitialState = model.InitialState,
                Created = model.Created != DateTime.MinValue ? model.Created : DateTime.Now,
                LastModified = DateTime.Now
            };

            workflow.SetContentTypes(model.ContentTypes);

            // Map states
            foreach (var stateModel in model.States.Where(s => !s.IsDeleted))
            {
                workflow.States.Add(new Piranha.Models.WorkflowState
                {
                    Id = stateModel.Id != Guid.Empty ? stateModel.Id : Guid.NewGuid(),
                    WorkflowDefinitionId = workflow.Id,
                    Key = stateModel.Key,
                    Name = stateModel.Name,
                    Description = stateModel.Description,
                    Color = stateModel.Color,
                    Icon = stateModel.Icon,
                    SortOrder = stateModel.SortOrder,
                    IsPublished = stateModel.IsPublished,
                    IsInitial = stateModel.IsInitial,
                    IsFinal = stateModel.IsFinal
                });
            }

            // Map transitions
            foreach (var transitionModel in model.Transitions.Where(t => !t.IsDeleted))
            {
                workflow.Transitions.Add(new Piranha.Models.WorkflowTransition
                {
                    Id = transitionModel.Id != Guid.Empty ? transitionModel.Id : Guid.NewGuid(),
                    WorkflowDefinitionId = workflow.Id,
                    FromStateKey = transitionModel.FromStateKey,
                    ToStateKey = transitionModel.ToStateKey,
                    Name = transitionModel.Name,
                    Description = transitionModel.Description,
                    RequiredPermission = transitionModel.RequiredRole, // Map role back to legacy permission field
                    CssClass = transitionModel.CssClass,
                    Icon = transitionModel.Icon,
                    SortOrder = transitionModel.SortOrder,
                    RequiresComment = transitionModel.RequiresComment,
                    SendNotification = transitionModel.SendNotification,
                    NotificationTemplate = transitionModel.NotificationTemplate
                });
            }

            // Note: Roles are now managed through ASP.NET Core Identity system
            // No workflow-specific roles are saved

            await _workflowService.SaveAsync(workflow);

            return new StatusMessage
            {
                Type = StatusMessage.Success,
                Body = _localizer.General["Workflow definition saved successfully."]
            };
        }
        catch (Exception ex)
        {
            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = ex.Message
            };
        }
    }

    /// <summary>
    /// Deletes the workflow definition.
    /// </summary>
    /// <param name="id">The workflow id</param>
    /// <returns>Status message</returns>
    public async Task<StatusMessage> DeleteAsync(Guid id)
    {
        try
        {
            await _workflowService.DeleteAsync(id);

            return new StatusMessage
            {
                Type = StatusMessage.Success,
                Body = _localizer.General["Workflow definition deleted successfully."]
            };
        }
        catch (Exception ex)
        {
            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a default workflow from template.
    /// </summary>
    /// <param name="name">The workflow name</param>
    /// <param name="contentTypes">The content types</param>
    /// <returns>The created workflow id</returns>
    public async Task<Guid> CreateDefaultWorkflowAsync(string name, string[] contentTypes)
    {
        var contentType = contentTypes.FirstOrDefault() ?? "content";
        var workflow = await _dynamicWorkflowService.CreateDefaultWorkflowAsync(contentType, name);
        await _workflowService.SaveAsync(workflow);
        return workflow.Id;
    }

    /// <summary>
    /// Sets the available options for the edit model.
    /// </summary>
    /// <param name="model">The edit model</param>
    private async Task SetAvailableOptionsAsync(WorkflowDefinitionEditModel model)
    {
        // Set available content types
        model.AvailableContentTypes = new List<WorkflowDefinitionEditModel.ContentTypeOption>
        {
            new WorkflowDefinitionEditModel.ContentTypeOption { Value = "page", Text = "Pages", Selected = model.ContentTypes.Contains("page") },
            new WorkflowDefinitionEditModel.ContentTypeOption { Value = "post", Text = "Posts", Selected = model.ContentTypes.Contains("post") },
            new WorkflowDefinitionEditModel.ContentTypeOption { Value = "content", Text = "Content", Selected = model.ContentTypes.Contains("content") }
        };

        // Set available system roles (from ASP.NET Core Identity)
        model.AvailablePermissions = new List<WorkflowDefinitionEditModel.PermissionOption>
        {
            new WorkflowDefinitionEditModel.PermissionOption { Value = "", Text = "No role required" },
            new WorkflowDefinitionEditModel.PermissionOption { Value = "SysAdmin", Text = "System Administrator" },
            new WorkflowDefinitionEditModel.PermissionOption { Value = "Writer", Text = "Writer" },
            new WorkflowDefinitionEditModel.PermissionOption { Value = "Editor", Text = "Editor" },
            new WorkflowDefinitionEditModel.PermissionOption { Value = "Reviewer", Text = "Reviewer" }
        };

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validates the edit model.
    /// </summary>
    /// <param name="model">The edit model</param>
    /// <returns>Validation errors</returns>
    private List<string> ValidateModel(WorkflowDefinitionEditModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            errors.Add("Workflow name is required");
        }

        if (model.ContentTypes == null || model.ContentTypes.Length == 0)
        {
            errors.Add("At least one content type must be selected");
        }

        if (string.IsNullOrWhiteSpace(model.InitialState))
        {
            errors.Add("Initial state is required");
        }

        var activeStates = model.States.Where(s => !s.IsDeleted).ToList();
        if (!activeStates.Any())
        {
            errors.Add("At least one state must be defined");
        }
        else
        {
            // Check that initial state exists
            if (!activeStates.Any(s => s.Key == model.InitialState))
            {
                errors.Add("Initial state must be one of the defined states");
            }

            // Check for duplicate state keys
            var duplicateStates = activeStates.GroupBy(s => s.Key).Where(g => g.Count() > 1);
            if (duplicateStates.Any())
            {
                errors.Add($"Duplicate state keys found: {string.Join(", ", duplicateStates.Select(g => g.Key))}");
            }

            // Check for missing required fields
            foreach (var state in activeStates)
            {
                if (string.IsNullOrWhiteSpace(state.Key))
                {
                    errors.Add("All states must have a key");
                }
                if (string.IsNullOrWhiteSpace(state.Name))
                {
                    errors.Add("All states must have a name");
                }
            }

            // Check transitions
            var activeTransitions = model.Transitions.Where(t => !t.IsDeleted).ToList();
            var stateKeys = activeStates.Select(s => s.Key).ToHashSet();
            
            foreach (var transition in activeTransitions)
            {
                if (string.IsNullOrWhiteSpace(transition.FromStateKey) || !stateKeys.Contains(transition.FromStateKey))
                {
                    errors.Add($"Transition '{transition.Name}' has invalid from state");
                }
                if (string.IsNullOrWhiteSpace(transition.ToStateKey) || !stateKeys.Contains(transition.ToStateKey))
                {
                    errors.Add($"Transition '{transition.Name}' has invalid to state");
                }
                if (string.IsNullOrWhiteSpace(transition.Name))
                {
                    errors.Add("All transitions must have a name");
                }
            }
        }

        return errors;
    }
}