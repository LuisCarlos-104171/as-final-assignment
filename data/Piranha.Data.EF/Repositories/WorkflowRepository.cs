/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.EntityFrameworkCore;
using Piranha.Data;

namespace Piranha.Repositories;

/// <summary>
/// Repository for workflow definitions.
/// </summary>
internal class WorkflowRepository : IWorkflowRepository
{
    private readonly IDb _db;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="db">The current db connection</param>
    public WorkflowRepository(IDb db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets all workflow definitions.
    /// </summary>
    /// <returns>The available workflow definitions</returns>
    public async Task<IEnumerable<Models.WorkflowDefinition>> GetAllAsync()
    {
        var workflows = await _db.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .OrderBy(w => w.Name)
            .ToListAsync();

        return workflows.Select(w => CreateModel(w));
    }

    /// <summary>
    /// Gets the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The workflow definition</returns>
    public async Task<Models.WorkflowDefinition> GetByIdAsync(Guid id)
    {
        var workflow = await _db.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == id);

        return workflow != null ? CreateModel(workflow) : null;
    }

    /// <summary>
    /// Gets the default workflow definition for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definition</returns>
    public async Task<Models.WorkflowDefinition> GetDefaultByContentTypeAsync(string contentType)
    {
        var workflow = await _db.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .Where(w => w.IsActive && w.IsDefault && w.ContentTypes.Contains(contentType))
            .FirstOrDefaultAsync();

        return workflow != null ? CreateModel(workflow) : null;
    }

    /// <summary>
    /// Gets all workflow definitions for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definitions</returns>
    public async Task<IEnumerable<Models.WorkflowDefinition>> GetByContentTypeAsync(string contentType)
    {
        var workflows = await _db.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .Where(w => w.IsActive && w.ContentTypes.Contains(contentType))
            .OrderBy(w => w.Name)
            .ToListAsync();

        return workflows.Select(w => CreateModel(w));
    }

    /// <summary>
    /// Saves the given workflow definition.
    /// </summary>
    /// <param name="model">The workflow definition</param>
    public async Task SaveAsync(Models.WorkflowDefinition model)
    {
        var workflow = await _db.WorkflowDefinitions
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == model.Id);

        if (workflow == null)
        {
            workflow = new WorkflowDefinition
            {
                Id = model.Id != Guid.Empty ? model.Id : Guid.NewGuid(),
                Created = DateTime.Now
            };
            _db.WorkflowDefinitions.Add(workflow);
        }

        UpdateEntity(workflow, model);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    public async Task DeleteAsync(Guid id)
    {
        var workflow = await _db.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workflow != null)
        {
            _db.WorkflowDefinitions.Remove(workflow);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets all states for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow states</returns>
    public async Task<IEnumerable<Models.WorkflowState>> GetStatesAsync(Guid workflowId)
    {
        var states = await _db.WorkflowStates
            .AsNoTracking()
            .Where(s => s.WorkflowDefinitionId == workflowId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        return states.Select(s => CreateStateModel(s));
    }

    /// <summary>
    /// Gets all transitions for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow transitions</returns>
    public async Task<IEnumerable<Models.WorkflowTransition>> GetTransitionsAsync(Guid workflowId)
    {
        var transitions = await _db.WorkflowTransitions
            .AsNoTracking()
            .Where(t => t.WorkflowDefinitionId == workflowId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return transitions.Select(t => CreateTransitionModel(t));
    }

    /// <summary>
    /// Gets available transitions from the given state.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <param name="fromState">The from state key</param>
    /// <returns>The available transitions</returns>
    public async Task<IEnumerable<Models.WorkflowTransition>> GetTransitionsFromStateAsync(Guid workflowId, string fromState)
    {
        var transitions = await _db.WorkflowTransitions
            .AsNoTracking()
            .Where(t => t.WorkflowDefinitionId == workflowId && t.FromStateKey == fromState)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return transitions.Select(t => CreateTransitionModel(t));
    }

    /// <summary>
    /// Saves a workflow state.
    /// </summary>
    /// <param name="model">The workflow state</param>
    public async Task SaveStateAsync(Models.WorkflowState model)
    {
        var state = await _db.WorkflowStates
            .FirstOrDefaultAsync(s => s.Id == model.Id);

        if (state == null)
        {
            state = new WorkflowState
            {
                Id = model.Id != Guid.Empty ? model.Id : Guid.NewGuid(),
                WorkflowDefinitionId = model.WorkflowDefinitionId
            };
            _db.WorkflowStates.Add(state);
        }

        UpdateStateEntity(state, model);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Saves a workflow transition.
    /// </summary>
    /// <param name="model">The workflow transition</param>
    public async Task SaveTransitionAsync(Models.WorkflowTransition model)
    {
        var transition = await _db.WorkflowTransitions
            .FirstOrDefaultAsync(t => t.Id == model.Id);

        if (transition == null)
        {
            transition = new WorkflowTransition
            {
                Id = model.Id != Guid.Empty ? model.Id : Guid.NewGuid(),
                WorkflowDefinitionId = model.WorkflowDefinitionId
            };
            _db.WorkflowTransitions.Add(transition);
        }

        UpdateTransitionEntity(transition, model);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a workflow state.
    /// </summary>
    /// <param name="id">The state id</param>
    public async Task DeleteStateAsync(Guid id)
    {
        var state = await _db.WorkflowStates
            .FirstOrDefaultAsync(s => s.Id == id);

        if (state != null)
        {
            _db.WorkflowStates.Remove(state);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deletes a workflow transition.
    /// </summary>
    /// <param name="id">The transition id</param>
    public async Task DeleteTransitionAsync(Guid id)
    {
        var transition = await _db.WorkflowTransitions
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transition != null)
        {
            _db.WorkflowTransitions.Remove(transition);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Creates a model from the data entity.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <returns>The model</returns>
    private Models.WorkflowDefinition CreateModel(WorkflowDefinition entity)
    {
        return new Models.WorkflowDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ContentTypes = entity.ContentTypes,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            InitialState = entity.InitialState,
            Created = entity.Created,
            LastModified = entity.LastModified,
            States = entity.States?.Select(CreateStateModel).ToList() ?? new List<Models.WorkflowState>(),
            Transitions = entity.Transitions?.Select(CreateTransitionModel).ToList() ?? new List<Models.WorkflowTransition>()
        };
    }

    /// <summary>
    /// Creates a state model from the data entity.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <returns>The model</returns>
    private Models.WorkflowState CreateStateModel(WorkflowState entity)
    {
        return new Models.WorkflowState
        {
            Id = entity.Id,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            IsPublished = entity.IsPublished,
            IsInitial = entity.IsInitial,
            IsFinal = entity.IsFinal
        };
    }

    /// <summary>
    /// Creates a transition model from the data entity.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <returns>The model</returns>
    private Models.WorkflowTransition CreateTransitionModel(WorkflowTransition entity)
    {
        return new Models.WorkflowTransition
        {
            Id = entity.Id,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            FromStateKey = entity.FromStateKey,
            ToStateKey = entity.ToStateKey,
            Name = entity.Name,
            Description = entity.Description,
            RequiredRoleId = entity.RequiredRoleId,
            CssClass = entity.CssClass,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            RequiresComment = entity.RequiresComment,
            SendNotification = entity.SendNotification,
            NotificationTemplate = entity.NotificationTemplate
        };
    }

    /// <summary>
    /// Updates the data entity from the model.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <param name="model">The model</param>
    private void UpdateEntity(WorkflowDefinition entity, Models.WorkflowDefinition model)
    {
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.ContentTypes = model.ContentTypes;
        entity.IsDefault = model.IsDefault;
        entity.IsActive = model.IsActive;
        entity.InitialState = model.InitialState;
        entity.LastModified = DateTime.Now;

        // Update states - proper handling to avoid concurrency issues
        var existingStateIds = entity.States.Select(s => s.Id).ToList();
        var modelStateIds = model.States.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToList();
        
        // Remove states that are no longer in the model
        var statesToRemove = entity.States.Where(s => !modelStateIds.Contains(s.Id)).ToList();
        foreach (var state in statesToRemove)
        {
            entity.States.Remove(state);
        }
        
        // Update or add states
        foreach (var state in model.States)
        {
            var existingState = entity.States.FirstOrDefault(s => s.Id == state.Id);
            if (existingState != null)
            {
                // Update existing state
                existingState.Key = state.Key;
                existingState.Name = state.Name;
                existingState.Description = state.Description;
                existingState.Color = state.Color;
                existingState.Icon = state.Icon;
                existingState.SortOrder = state.SortOrder;
                existingState.IsPublished = state.IsPublished;
                existingState.IsInitial = state.IsInitial;
                existingState.IsFinal = state.IsFinal;
            }
            else
            {
                // Add new state
                entity.States.Add(new WorkflowState
                {
                    Id = state.Id != Guid.Empty ? state.Id : Guid.NewGuid(),
                    WorkflowDefinitionId = entity.Id,
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
        }

        // Update transitions - proper handling to avoid concurrency issues
        var existingTransitionIds = entity.Transitions.Select(t => t.Id).ToList();
        var modelTransitionIds = model.Transitions.Where(t => t.Id != Guid.Empty).Select(t => t.Id).ToList();
        
        // Remove transitions that are no longer in the model
        var transitionsToRemove = entity.Transitions.Where(t => !modelTransitionIds.Contains(t.Id)).ToList();
        foreach (var transition in transitionsToRemove)
        {
            entity.Transitions.Remove(transition);
        }
        
        // Update or add transitions
        foreach (var transition in model.Transitions)
        {
            var existingTransition = entity.Transitions.FirstOrDefault(t => t.Id == transition.Id);
            if (existingTransition != null)
            {
                // Update existing transition
                existingTransition.FromStateKey = transition.FromStateKey;
                existingTransition.ToStateKey = transition.ToStateKey;
                existingTransition.Name = transition.Name;
                existingTransition.Description = transition.Description;
                existingTransition.RequiredRoleId = transition.RequiredRoleId;
                existingTransition.CssClass = transition.CssClass;
                existingTransition.Icon = transition.Icon;
                existingTransition.SortOrder = transition.SortOrder;
                existingTransition.RequiresComment = transition.RequiresComment;
                existingTransition.SendNotification = transition.SendNotification;
                existingTransition.NotificationTemplate = transition.NotificationTemplate;
            }
            else
            {
                // Add new transition
                entity.Transitions.Add(new WorkflowTransition
                {
                    Id = transition.Id != Guid.Empty ? transition.Id : Guid.NewGuid(),
                    WorkflowDefinitionId = entity.Id,
                    FromStateKey = transition.FromStateKey,
                    ToStateKey = transition.ToStateKey,
                    Name = transition.Name,
                    Description = transition.Description,
                    RequiredRoleId = transition.RequiredRoleId,
                    CssClass = transition.CssClass,
                    Icon = transition.Icon,
                    SortOrder = transition.SortOrder,
                    RequiresComment = transition.RequiresComment,
                    SendNotification = transition.SendNotification,
                    NotificationTemplate = transition.NotificationTemplate
                });
            }
        }
    }

    /// <summary>
    /// Updates the state entity from the model.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <param name="model">The model</param>
    private void UpdateStateEntity(WorkflowState entity, Models.WorkflowState model)
    {
        entity.Key = model.Key;
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Color = model.Color;
        entity.Icon = model.Icon;
        entity.SortOrder = model.SortOrder;
        entity.IsPublished = model.IsPublished;
        entity.IsInitial = model.IsInitial;
        entity.IsFinal = model.IsFinal;
    }

    /// <summary>
    /// Updates the transition entity from the model.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <param name="model">The model</param>
    private void UpdateTransitionEntity(WorkflowTransition entity, Models.WorkflowTransition model)
    {
        entity.FromStateKey = model.FromStateKey;
        entity.ToStateKey = model.ToStateKey;
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.RequiredRoleId = model.RequiredRoleId;
        entity.CssClass = model.CssClass;
        entity.Icon = model.Icon;
        entity.SortOrder = model.SortOrder;
        entity.RequiresComment = model.RequiresComment;
        entity.SendNotification = model.SendNotification;
        entity.NotificationTemplate = model.NotificationTemplate;
    }
}