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

namespace Piranha.Services;

/// <summary>
/// Service for managing workflow definitions.
/// </summary>
public interface IWorkflowDefinitionService
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
    /// Creates a default workflow definition.
    /// </summary>
    /// <param name="name">The workflow name</param>
    /// <param name="contentTypes">The content types</param>
    /// <returns>The default workflow definition</returns>
    Task<WorkflowDefinition> CreateDefaultWorkflowAsync(string name, string[] contentTypes);

    /// <summary>
    /// Validates a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition</param>
    /// <returns>Validation errors, if any</returns>
    Task<IEnumerable<string>> ValidateAsync(WorkflowDefinition workflow);

    /// <summary>
    /// Gets available workflow transitions for content in the given state.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="currentState">The current state</param>
    /// <param name="permissions">The user permissions</param>
    /// <returns>The available transitions</returns>
    Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(string contentType, string currentState, IEnumerable<string> permissions);

    /// <summary>
    /// Performs a workflow transition validation.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="fromState">The from state</param>
    /// <param name="toState">The to state</param>
    /// <param name="permissions">The user permissions</param>
    /// <returns>True if the transition is valid</returns>
    Task<bool> ValidateTransitionAsync(string contentType, string fromState, string toState, IEnumerable<string> permissions);
}