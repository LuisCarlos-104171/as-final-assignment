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
                .ThenInclude(t => t.RolePermissions)
            .Include(w => w.Roles)
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
                .ThenInclude(t => t.RolePermissions)
            .Include(w => w.Roles)
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
        WorkflowDefinition workflow = null;
        
        // Only try to load existing workflow if we have a valid non-empty GUID
        if (model.Id != Guid.Empty)
        {
            workflow = await _db.WorkflowDefinitions
                .Include(w => w.States)
                .Include(w => w.Transitions)
                    .ThenInclude(t => t.RolePermissions)
                .Include(w => w.Roles)
                .FirstOrDefaultAsync(w => w.Id == model.Id);
        }

        if (workflow == null)
        {
            // Create new workflow
            workflow = new WorkflowDefinition
            {
                Id = model.Id != Guid.Empty ? model.Id : Guid.NewGuid(),
                Created = DateTime.Now
            };
            _db.WorkflowDefinitions.Add(workflow);
        }

        UpdateEntity(workflow, model);
        
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Handle concurrency conflicts by reloading and retrying once
            foreach (var entry in ex.Entries)
            {
                if (entry.Entity is WorkflowDefinition)
                {
                    await entry.ReloadAsync();
                }
            }
            // Retry the update
            UpdateEntity(workflow, model);
            await _db.SaveChangesAsync();
        }
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
    /// Gets a workflow transition by its id.
    /// </summary>
    /// <param name="id">The transition id</param>
    /// <returns>The workflow transition</returns>
    public async Task<Models.WorkflowTransition> GetTransitionByIdAsync(Guid id)
    {
        var transition = await _db.WorkflowTransitions
            .AsNoTracking()
            .Include(t => t.RolePermissions)
            .FirstOrDefaultAsync(t => t.Id == id);

        return transition != null ? CreateTransitionModel(transition) : null;
    }

    /// <summary>
    /// Gets all workflow roles for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow roles</returns>
    public async Task<IEnumerable<Models.WorkflowRole>> GetRolesAsync(Guid workflowId)
    {
        var roles = await _db.WorkflowRoles
            .AsNoTracking()
            .Where(r => r.WorkflowDefinitionId == workflowId)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        return roles.Select(r => CreateRoleModel(r));
    }

    /// <summary>
    /// Saves a workflow role.
    /// </summary>
    /// <param name="role">The workflow role</param>
    public async Task SaveRoleAsync(Models.WorkflowRole role)
    {
        var entity = await _db.WorkflowRoles
            .FirstOrDefaultAsync(r => r.Id == role.Id);

        if (entity == null)
        {
            entity = new WorkflowRole
            {
                Id = role.Id != Guid.Empty ? role.Id : Guid.NewGuid(),
                WorkflowDefinitionId = role.WorkflowDefinitionId
            };
            _db.WorkflowRoles.Add(entity);
        }

        UpdateRoleEntity(entity, role);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a workflow role.
    /// </summary>
    /// <param name="id">The role id</param>
    public async Task DeleteRoleAsync(Guid id)
    {
        var role = await _db.WorkflowRoles
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role != null)
        {
            _db.WorkflowRoles.Remove(role);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets workflow role permissions for a specific transition.
    /// </summary>
    /// <param name="transitionId">The transition id</param>
    /// <returns>The role permissions</returns>
    public async Task<IEnumerable<Models.WorkflowRolePermission>> GetRolePermissionsAsync(Guid transitionId)
    {
        var permissions = await _db.WorkflowRolePermissions
            .AsNoTracking()
            .Include(p => p.WorkflowRole)
            .Where(p => p.WorkflowTransitionId == transitionId)
            .ToListAsync();

        return permissions.Select(p => CreateRolePermissionModel(p));
    }

    /// <summary>
    /// Saves a workflow role permission.
    /// </summary>
    /// <param name="permission">The role permission</param>
    public async Task SaveRolePermissionAsync(Models.WorkflowRolePermission permission)
    {
        var entity = await _db.WorkflowRolePermissions
            .FirstOrDefaultAsync(p => p.Id == permission.Id);

        if (entity == null)
        {
            entity = new WorkflowRolePermission
            {
                Id = permission.Id != Guid.Empty ? permission.Id : Guid.NewGuid(),
                WorkflowRoleId = permission.WorkflowRoleId,
                WorkflowTransitionId = permission.WorkflowTransitionId
            };
            _db.WorkflowRolePermissions.Add(entity);
        }

        UpdateRolePermissionEntity(entity, permission);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a workflow role permission.
    /// </summary>
    /// <param name="id">The permission id</param>
    public async Task DeleteRolePermissionAsync(Guid id)
    {
        var permission = await _db.WorkflowRolePermissions
            .FirstOrDefaultAsync(p => p.Id == id);

        if (permission != null)
        {
            _db.WorkflowRolePermissions.Remove(permission);
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
            Transitions = entity.Transitions?.Select(CreateTransitionModel).ToList() ?? new List<Models.WorkflowTransition>(),
            Roles = entity.Roles?.Select(CreateRoleModel).ToList() ?? new List<Models.WorkflowRole>()
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
            RequiredPermission = entity.RequiredPermission,
            CssClass = entity.CssClass,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            RequiresComment = entity.RequiresComment,
            SendNotification = entity.SendNotification,
            NotificationTemplate = entity.NotificationTemplate,
            RolePermissions = entity.RolePermissions?.Select(CreateRolePermissionModel).ToList() ?? new List<Models.WorkflowRolePermission>()
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

        // Update states
        var existingStates = entity.States?.ToList() ?? new List<WorkflowState>();
        entity.States.Clear();
        
        foreach (var stateModel in model.States)
        {
            WorkflowState stateEntity;
            
            if (stateModel.Id != Guid.Empty)
            {
                // Try to reuse existing entity if it exists
                stateEntity = existingStates.FirstOrDefault(s => s.Id == stateModel.Id);
                if (stateEntity != null)
                {
                    // Update existing entity
                    stateEntity.Key = stateModel.Key;
                    stateEntity.Name = stateModel.Name;
                    stateEntity.Description = stateModel.Description;
                    stateEntity.Color = stateModel.Color;
                    stateEntity.Icon = stateModel.Icon;
                    stateEntity.SortOrder = stateModel.SortOrder;
                    stateEntity.IsPublished = stateModel.IsPublished;
                    stateEntity.IsInitial = stateModel.IsInitial;
                    stateEntity.IsFinal = stateModel.IsFinal;
                }
                else
                {
                    // Create new entity with specified ID
                    stateEntity = new WorkflowState
                    {
                        Id = stateModel.Id,
                        WorkflowDefinitionId = entity.Id,
                        Key = stateModel.Key,
                        Name = stateModel.Name,
                        Description = stateModel.Description,
                        Color = stateModel.Color,
                        Icon = stateModel.Icon,
                        SortOrder = stateModel.SortOrder,
                        IsPublished = stateModel.IsPublished,
                        IsInitial = stateModel.IsInitial,
                        IsFinal = stateModel.IsFinal
                    };
                }
            }
            else
            {
                // Create new entity with new ID
                stateEntity = new WorkflowState
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = entity.Id,
                    Key = stateModel.Key,
                    Name = stateModel.Name,
                    Description = stateModel.Description,
                    Color = stateModel.Color,
                    Icon = stateModel.Icon,
                    SortOrder = stateModel.SortOrder,
                    IsPublished = stateModel.IsPublished,
                    IsInitial = stateModel.IsInitial,
                    IsFinal = stateModel.IsFinal
                };
            }
            
            entity.States.Add(stateEntity);
        }

        // Update transitions
        var existingTransitions = entity.Transitions?.ToList() ?? new List<WorkflowTransition>();
        entity.Transitions.Clear();
        
        foreach (var transitionModel in model.Transitions)
        {
            WorkflowTransition transitionEntity;
            
            if (transitionModel.Id != Guid.Empty)
            {
                // Try to reuse existing entity if it exists
                transitionEntity = existingTransitions.FirstOrDefault(t => t.Id == transitionModel.Id);
                if (transitionEntity != null)
                {
                    // Update existing entity
                    transitionEntity.FromStateKey = transitionModel.FromStateKey;
                    transitionEntity.ToStateKey = transitionModel.ToStateKey;
                    transitionEntity.Name = transitionModel.Name;
                    transitionEntity.Description = transitionModel.Description;
                    transitionEntity.RequiredPermission = transitionModel.RequiredPermission;
                    transitionEntity.CssClass = transitionModel.CssClass;
                    transitionEntity.Icon = transitionModel.Icon;
                    transitionEntity.SortOrder = transitionModel.SortOrder;
                    transitionEntity.RequiresComment = transitionModel.RequiresComment;
                    transitionEntity.SendNotification = transitionModel.SendNotification;
                    transitionEntity.NotificationTemplate = transitionModel.NotificationTemplate;
                    
                    // Update role permissions
                    transitionEntity.RolePermissions.Clear();
                }
                else
                {
                    // Create new entity with specified ID
                    transitionEntity = new WorkflowTransition
                    {
                        Id = transitionModel.Id,
                        WorkflowDefinitionId = entity.Id,
                        FromStateKey = transitionModel.FromStateKey,
                        ToStateKey = transitionModel.ToStateKey,
                        Name = transitionModel.Name,
                        Description = transitionModel.Description,
                        RequiredPermission = transitionModel.RequiredPermission,
                        CssClass = transitionModel.CssClass,
                        Icon = transitionModel.Icon,
                        SortOrder = transitionModel.SortOrder,
                        RequiresComment = transitionModel.RequiresComment,
                        SendNotification = transitionModel.SendNotification,
                        NotificationTemplate = transitionModel.NotificationTemplate
                    };
                }
            }
            else
            {
                // Create new entity with new ID
                transitionEntity = new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = entity.Id,
                    FromStateKey = transitionModel.FromStateKey,
                    ToStateKey = transitionModel.ToStateKey,
                    Name = transitionModel.Name,
                    Description = transitionModel.Description,
                    RequiredPermission = transitionModel.RequiredPermission,
                    CssClass = transitionModel.CssClass,
                    Icon = transitionModel.Icon,
                    SortOrder = transitionModel.SortOrder,
                    RequiresComment = transitionModel.RequiresComment,
                    SendNotification = transitionModel.SendNotification,
                    NotificationTemplate = transitionModel.NotificationTemplate
                };
            }

            // Add role permissions
            foreach (var permission in transitionModel.RolePermissions)
            {
                transitionEntity.RolePermissions.Add(new WorkflowRolePermission
                {
                    Id = permission.Id != Guid.Empty ? permission.Id : Guid.NewGuid(),
                    WorkflowRoleId = permission.WorkflowRoleId,
                    WorkflowTransitionId = transitionEntity.Id,
                    CanExecute = permission.CanExecute,
                    RequiresApproval = permission.RequiresApproval,
                    Conditions = permission.Conditions
                });
            }

            entity.Transitions.Add(transitionEntity);
        }

        // Update roles
        var existingRoles = entity.Roles?.ToList() ?? new List<WorkflowRole>();
        entity.Roles.Clear();
        
        foreach (var roleModel in model.Roles)
        {
            WorkflowRole roleEntity;
            
            if (roleModel.Id != Guid.Empty)
            {
                // Try to reuse existing entity if it exists
                roleEntity = existingRoles.FirstOrDefault(r => r.Id == roleModel.Id);
                if (roleEntity != null)
                {
                    // Update existing entity
                    roleEntity.RoleKey = roleModel.RoleKey;
                    roleEntity.DisplayName = roleModel.DisplayName;
                    roleEntity.Description = roleModel.Description;
                    roleEntity.Priority = roleModel.Priority;
                    roleEntity.CanCreate = roleModel.CanCreate;
                    roleEntity.CanEdit = roleModel.CanEdit;
                    roleEntity.CanDelete = roleModel.CanDelete;
                    roleEntity.CanViewAll = roleModel.CanViewAll;
                    roleEntity.AllowedFromStates = roleModel.AllowedFromStates;
                    roleEntity.AllowedToStates = roleModel.AllowedToStates;
                }
                else
                {
                    // Create new entity with specified ID
                    roleEntity = new WorkflowRole
                    {
                        Id = roleModel.Id,
                        WorkflowDefinitionId = entity.Id,
                        RoleKey = roleModel.RoleKey,
                        DisplayName = roleModel.DisplayName,
                        Description = roleModel.Description,
                        Priority = roleModel.Priority,
                        CanCreate = roleModel.CanCreate,
                        CanEdit = roleModel.CanEdit,
                        CanDelete = roleModel.CanDelete,
                        CanViewAll = roleModel.CanViewAll,
                        AllowedFromStates = roleModel.AllowedFromStates,
                        AllowedToStates = roleModel.AllowedToStates
                    };
                }
            }
            else
            {
                // Create new entity with new ID
                roleEntity = new WorkflowRole
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = entity.Id,
                    RoleKey = roleModel.RoleKey,
                    DisplayName = roleModel.DisplayName,
                    Description = roleModel.Description,
                    Priority = roleModel.Priority,
                    CanCreate = roleModel.CanCreate,
                    CanEdit = roleModel.CanEdit,
                    CanDelete = roleModel.CanDelete,
                    CanViewAll = roleModel.CanViewAll,
                    AllowedFromStates = roleModel.AllowedFromStates,
                    AllowedToStates = roleModel.AllowedToStates
                };
            }
            
            entity.Roles.Add(roleEntity);
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
        entity.RequiredPermission = model.RequiredPermission;
        entity.CssClass = model.CssClass;
        entity.Icon = model.Icon;
        entity.SortOrder = model.SortOrder;
        entity.RequiresComment = model.RequiresComment;
        entity.SendNotification = model.SendNotification;
        entity.NotificationTemplate = model.NotificationTemplate;

        // Update role permissions
        entity.RolePermissions.Clear();
        foreach (var permission in model.RolePermissions)
        {
            entity.RolePermissions.Add(new WorkflowRolePermission
            {
                Id = permission.Id != Guid.Empty ? permission.Id : Guid.NewGuid(),
                WorkflowRoleId = permission.WorkflowRoleId,
                WorkflowTransitionId = entity.Id,
                CanExecute = permission.CanExecute,
                RequiresApproval = permission.RequiresApproval,
                Conditions = permission.Conditions
            });
        }
    }

    /// <summary>
    /// Creates a role model from the data entity.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <returns>The model</returns>
    private Models.WorkflowRole CreateRoleModel(WorkflowRole entity)
    {
        return new Models.WorkflowRole
        {
            Id = entity.Id,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            RoleKey = entity.RoleKey,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Priority = entity.Priority,
            CanCreate = entity.CanCreate,
            CanEdit = entity.CanEdit,
            CanDelete = entity.CanDelete,
            CanViewAll = entity.CanViewAll,
            AllowedFromStates = entity.AllowedFromStates,
            AllowedToStates = entity.AllowedToStates
        };
    }

    /// <summary>
    /// Creates a role permission model from the data entity.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <returns>The model</returns>
    private Models.WorkflowRolePermission CreateRolePermissionModel(WorkflowRolePermission entity)
    {
        return new Models.WorkflowRolePermission
        {
            Id = entity.Id,
            WorkflowRoleId = entity.WorkflowRoleId,
            WorkflowTransitionId = entity.WorkflowTransitionId,
            CanExecute = entity.CanExecute,
            RequiresApproval = entity.RequiresApproval,
            Conditions = entity.Conditions
        };
    }

    /// <summary>
    /// Updates the role entity from the model.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <param name="model">The model</param>
    private void UpdateRoleEntity(WorkflowRole entity, Models.WorkflowRole model)
    {
        entity.RoleKey = model.RoleKey;
        entity.DisplayName = model.DisplayName;
        entity.Description = model.Description;
        entity.Priority = model.Priority;
        entity.CanCreate = model.CanCreate;
        entity.CanEdit = model.CanEdit;
        entity.CanDelete = model.CanDelete;
        entity.CanViewAll = model.CanViewAll;
        entity.AllowedFromStates = model.AllowedFromStates;
        entity.AllowedToStates = model.AllowedToStates;
    }

    /// <summary>
    /// Updates the role permission entity from the model.
    /// </summary>
    /// <param name="entity">The data entity</param>
    /// <param name="model">The model</param>
    private void UpdateRolePermissionEntity(WorkflowRolePermission entity, Models.WorkflowRolePermission model)
    {
        entity.CanExecute = model.CanExecute;
        entity.RequiresApproval = model.RequiresApproval;
        entity.Conditions = model.Conditions;
    }
}