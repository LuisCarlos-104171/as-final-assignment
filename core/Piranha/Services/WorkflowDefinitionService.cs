/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Piranha.Models;
using Piranha.Repositories;

namespace Piranha.Services;

/// <summary>
/// Service for managing workflow definitions.
/// </summary>
internal class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowRepository _repo;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="repo">The workflow repository</param>
    public WorkflowDefinitionService(IWorkflowRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Gets all workflow definitions.
    /// </summary>
    /// <returns>The available workflow definitions</returns>
    public async Task<IEnumerable<WorkflowDefinition>> GetAllAsync()
    {
        return await _repo.GetAllAsync();
    }

    /// <summary>
    /// Gets the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The workflow definition</returns>
    public async Task<WorkflowDefinition> GetByIdAsync(Guid id)
    {
        return await _repo.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets the default workflow definition for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definition</returns>
    public async Task<WorkflowDefinition> GetDefaultByContentTypeAsync(string contentType)
    {
        return await _repo.GetDefaultByContentTypeAsync(contentType);
    }

    /// <summary>
    /// Gets all workflow definitions for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definitions</returns>
    public async Task<IEnumerable<WorkflowDefinition>> GetByContentTypeAsync(string contentType)
    {
        return await _repo.GetByContentTypeAsync(contentType);
    }

    /// <summary>
    /// Saves the given workflow definition.
    /// </summary>
    /// <param name="model">The workflow definition</param>
    public async Task SaveAsync(WorkflowDefinition model)
    {
        var validationErrors = await ValidateAsync(model);
        if (validationErrors.Any())
        {
            throw new InvalidOperationException($"Workflow validation failed: {string.Join(", ", validationErrors)}");
        }

        // If this workflow is being set as default, unset other defaults for the same content types
        if (model.IsDefault)
        {
            var contentTypes = model.GetContentTypes();
            foreach (var contentType in contentTypes)
            {
                var existingDefaults = await GetByContentTypeAsync(contentType);
                foreach (var existing in existingDefaults.Where(w => w.IsDefault && w.Id != model.Id))
                {
                    existing.IsDefault = false;
                    await _repo.SaveAsync(existing);
                }
            }
        }

        await _repo.SaveAsync(model);
    }

    /// <summary>
    /// Deletes the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    public async Task DeleteAsync(Guid id)
    {
        await _repo.DeleteAsync(id);
    }

    /// <summary>
    /// Creates a default workflow definition.
    /// </summary>
    /// <param name="name">The workflow name</param>
    /// <param name="contentTypes">The content types</param>
    /// <returns>The default workflow definition</returns>
    public async Task<WorkflowDefinition> CreateDefaultWorkflowAsync(string name, string[] contentTypes)
    {
        var workflow = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Default workflow with basic editorial states",
            IsDefault = true,
            IsActive = true,
            InitialState = "draft",
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };

        workflow.SetContentTypes(contentTypes);

        // Create default states
        workflow.States = new List<WorkflowState>
        {
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "draft",
                Name = "Draft",
                Description = "Content is being drafted",
                Color = "#6c757d",
                Icon = "fas fa-edit",
                SortOrder = 1,
                IsInitial = true,
                IsPublished = false,
                IsFinal = false
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "in_review",
                Name = "In Review",
                Description = "Content is under review",
                Color = "#ffc107",
                Icon = "fas fa-eye",
                SortOrder = 2,
                IsInitial = false,
                IsPublished = false,
                IsFinal = false
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "approved",
                Name = "Approved",
                Description = "Content has been approved",
                Color = "#28a745",
                Icon = "fas fa-check",
                SortOrder = 3,
                IsInitial = false,
                IsPublished = false,
                IsFinal = false
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "published",
                Name = "Published",
                Description = "Content is published",
                Color = "#007bff",
                Icon = "fas fa-globe",
                SortOrder = 4,
                IsInitial = false,
                IsPublished = true,
                IsFinal = true
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "rejected",
                Name = "Rejected",
                Description = "Content has been rejected",
                Color = "#dc3545",
                Icon = "fas fa-times",
                SortOrder = 5,
                IsInitial = false,
                IsPublished = false,
                IsFinal = false
            }
        };

        // Create default transitions
        workflow.Transitions = new List<WorkflowTransition>
        {
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "draft",
                ToStateKey = "in_review",
                Name = "Submit for Review",
                Description = "Submit content for editorial review",
                RequiredPermission = "PiranhaContentSubmitForReview",
                CssClass = "btn-primary",
                Icon = "fas fa-paper-plane",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                NotificationTemplate = "Content has been submitted for review"
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "in_review",
                ToStateKey = "approved",
                Name = "Approve",
                Description = "Approve the content",
                RequiredPermission = "PiranhaContentApprove",
                CssClass = "btn-success",
                Icon = "fas fa-check",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                NotificationTemplate = "Content has been approved"
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "in_review",
                ToStateKey = "rejected",
                Name = "Reject",
                Description = "Reject the content",
                RequiredPermission = "PiranhaContentReject",
                CssClass = "btn-danger",
                Icon = "fas fa-times",
                SortOrder = 2,
                RequiresComment = true,
                SendNotification = true,
                NotificationTemplate = "Content has been rejected"
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "approved",
                ToStateKey = "published",
                Name = "Publish",
                Description = "Publish the content",
                RequiredPermission = "PiranhaContentPublish",
                CssClass = "btn-success",
                Icon = "fas fa-globe",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                NotificationTemplate = "Content has been published"
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "rejected",
                ToStateKey = "draft",
                Name = "Back to Draft",
                Description = "Return content to draft state",
                RequiredPermission = null,
                CssClass = "btn-secondary",
                Icon = "fas fa-undo",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = false,
                NotificationTemplate = null
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "published",
                ToStateKey = "draft",
                Name = "Unpublish",
                Description = "Unpublish the content",
                RequiredPermission = "PiranhaContentPublish",
                CssClass = "btn-warning",
                Icon = "fas fa-eye-slash",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                NotificationTemplate = "Content has been unpublished"
            }
        };

        return workflow;
    }

    /// <summary>
    /// Validates a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition</param>
    /// <returns>Validation errors, if any</returns>
    public async Task<IEnumerable<string>> ValidateAsync(WorkflowDefinition workflow)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add("Workflow name is required");
        }

        if (string.IsNullOrWhiteSpace(workflow.ContentTypes))
        {
            errors.Add("At least one content type must be specified");
        }

        if (string.IsNullOrWhiteSpace(workflow.InitialState))
        {
            errors.Add("Initial state is required");
        }

        if (!workflow.States.Any())
        {
            errors.Add("At least one state must be defined");
        }
        else
        {
            // Check that initial state exists
            if (!workflow.States.Any(s => s.Key == workflow.InitialState))
            {
                errors.Add("Initial state must be one of the defined states");
            }

            // Check for duplicate state keys
            var duplicateStates = workflow.States.GroupBy(s => s.Key).Where(g => g.Count() > 1);
            if (duplicateStates.Any())
            {
                errors.Add($"Duplicate state keys found: {string.Join(", ", duplicateStates.Select(g => g.Key))}");
            }

            // Check that all transition states exist
            var stateKeys = workflow.States.Select(s => s.Key).ToHashSet();
            var invalidTransitions = workflow.Transitions.Where(t => 
                !stateKeys.Contains(t.FromStateKey) || !stateKeys.Contains(t.ToStateKey));
            
            if (invalidTransitions.Any())
            {
                errors.Add("Some transitions reference non-existent states");
            }
        }

        return await Task.FromResult(errors);
    }

    /// <summary>
    /// Gets available workflow transitions for content in the given state.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="currentState">The current state</param>
    /// <param name="permissions">The user permissions</param>
    /// <returns>The available transitions</returns>
    public async Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(string contentType, string currentState, IEnumerable<string> permissions)
    {
        var workflow = await GetDefaultByContentTypeAsync(contentType);
        if (workflow == null)
        {
            return Enumerable.Empty<WorkflowTransition>();
        }

        var userPermissions = permissions.ToHashSet();
        var availableTransitions = workflow.Transitions
            .Where(t => t.FromStateKey == currentState)
            .Where(t => string.IsNullOrEmpty(t.RequiredPermission) || userPermissions.Contains(t.RequiredPermission))
            .OrderBy(t => t.SortOrder)
            .ToList();

        return availableTransitions;
    }

    /// <summary>
    /// Performs a workflow transition validation.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="fromState">The from state</param>
    /// <param name="toState">The to state</param>
    /// <param name="permissions">The user permissions</param>
    /// <returns>True if the transition is valid</returns>
    public async Task<bool> ValidateTransitionAsync(string contentType, string fromState, string toState, IEnumerable<string> permissions)
    {
        var availableTransitions = await GetAvailableTransitionsAsync(contentType, fromState, permissions);
        return availableTransitions.Any(t => t.ToStateKey == toState);
    }
}