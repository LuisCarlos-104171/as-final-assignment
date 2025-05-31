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

namespace Piranha.Repositories;

/// <summary>
/// Repository for workflow definitions.
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>
    /// Gets all workflow definitions.
    /// </summary>
    /// <returns>The available workflow definitions</returns>
    Task<IEnumerable<WorkflowDefinition>> GetAllAsync();

    /// <summary>
    /// Gets the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The workflow definition</returns>
    Task<WorkflowDefinition> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets the default workflow definition for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definition</returns>
    Task<WorkflowDefinition> GetDefaultByContentTypeAsync(string contentType);

    /// <summary>
    /// Gets all workflow definitions for the given content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The workflow definitions</returns>
    Task<IEnumerable<WorkflowDefinition>> GetByContentTypeAsync(string contentType);

    /// <summary>
    /// Saves the given workflow definition.
    /// </summary>
    /// <param name="model">The workflow definition</param>
    Task SaveAsync(WorkflowDefinition model);

    /// <summary>
    /// Deletes the workflow definition with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Gets all states for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow states</returns>
    Task<IEnumerable<WorkflowState>> GetStatesAsync(Guid workflowId);

    /// <summary>
    /// Gets all transitions for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow transitions</returns>
    Task<IEnumerable<WorkflowTransition>> GetTransitionsAsync(Guid workflowId);

    /// <summary>
    /// Gets available transitions from the given state.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <param name="fromState">The from state key</param>
    /// <returns>The available transitions</returns>
    Task<IEnumerable<WorkflowTransition>> GetTransitionsFromStateAsync(Guid workflowId, string fromState);

    /// <summary>
    /// Saves a workflow state.
    /// </summary>
    /// <param name="state">The workflow state</param>
    Task SaveStateAsync(WorkflowState state);

    /// <summary>
    /// Saves a workflow transition.
    /// </summary>
    /// <param name="transition">The workflow transition</param>
    Task SaveTransitionAsync(WorkflowTransition transition);

    /// <summary>
    /// Deletes a workflow state.
    /// </summary>
    /// <param name="id">The state id</param>
    Task DeleteStateAsync(Guid id);

    /// <summary>
    /// Deletes a workflow transition.
    /// </summary>
    /// <param name="id">The transition id</param>
    Task DeleteTransitionAsync(Guid id);

    /// <summary>
    /// Gets a workflow transition by its id.
    /// </summary>
    /// <param name="id">The transition id</param>
    /// <returns>The workflow transition</returns>
    Task<WorkflowTransition> GetTransitionByIdAsync(Guid id);

    /// <summary>
    /// Gets all workflow roles for the given workflow definition.
    /// </summary>
    /// <param name="workflowId">The workflow definition id</param>
    /// <returns>The workflow roles</returns>
    Task<IEnumerable<WorkflowRole>> GetRolesAsync(Guid workflowId);

    /// <summary>
    /// Saves a workflow role.
    /// </summary>
    /// <param name="role">The workflow role</param>
    Task SaveRoleAsync(WorkflowRole role);

    /// <summary>
    /// Deletes a workflow role.
    /// </summary>
    /// <param name="id">The role id</param>
    Task DeleteRoleAsync(Guid id);

    /// <summary>
    /// Gets workflow role permissions for a specific transition.
    /// </summary>
    /// <param name="transitionId">The transition id</param>
    /// <returns>The role permissions</returns>
    Task<IEnumerable<WorkflowRolePermission>> GetRolePermissionsAsync(Guid transitionId);

    /// <summary>
    /// Saves a workflow role permission.
    /// </summary>
    /// <param name="permission">The role permission</param>
    Task SaveRolePermissionAsync(WorkflowRolePermission permission);

    /// <summary>
    /// Deletes a workflow role permission.
    /// </summary>
    /// <param name="id">The permission id</param>
    Task DeleteRolePermissionAsync(Guid id);
}