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
/// Dynamic workflow service that evaluates workflow permissions and transitions
/// based on configurable role definitions rather than hardcoded logic.
/// </summary>
public interface IDynamicWorkflowService
{
    /// <summary>
    /// Gets all available workflows for a specific content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>Available workflows</returns>
    Task<IEnumerable<WorkflowDefinition>> GetWorkflowsForContentTypeAsync(string contentType);

    /// <summary>
    /// Gets the default workflow for a content type.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <returns>The default workflow</returns>
    Task<WorkflowDefinition> GetDefaultWorkflowAsync(string contentType);

    /// <summary>
    /// Gets available transitions for a user based on their roles and the current content state.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="currentState">The current workflow state</param>
    /// <param name="userRoles">The user's roles</param>
    /// <param name="contentId">The content ID (for ownership checks)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Available transitions</returns>
    Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(Guid workflowId, string currentState, 
        IEnumerable<string> userRoles, Guid contentId, string userId);

    /// <summary>
    /// Checks if a user can execute a specific workflow transition.
    /// </summary>
    /// <param name="transitionId">The transition ID</param>
    /// <param name="userRoles">The user's roles</param>
    /// <param name="contentId">The content ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if the user can execute the transition</returns>
    Task<bool> CanExecuteTransitionAsync(Guid transitionId, IEnumerable<string> userRoles, 
        Guid contentId, string userId);

    /// <summary>
    /// Evaluates whether a user can view content based on workflow state and role permissions.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="contentState">The content's workflow state</param>
    /// <param name="userRoles">The user's roles</param>
    /// <param name="contentOwnerId">The content owner's ID</param>
    /// <param name="userId">The current user's ID</param>
    /// <returns>True if the user can view the content</returns>
    Task<bool> CanViewContentAsync(Guid workflowId, string contentState, IEnumerable<string> userRoles, 
        string contentOwnerId, string userId);

    /// <summary>
    /// Gets the effective roles for a user in a specific workflow.
    /// This considers role hierarchy and inheritance.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="userRoles">The user's roles</param>
    /// <returns>Effective workflow roles</returns>
    Task<IEnumerable<WorkflowRole>> GetEffectiveRolesAsync(Guid workflowId, IEnumerable<string> userRoles);

    /// <summary>
    /// Creates a default dynamic workflow for a content type.
    /// This replaces hardcoded workflow creation with configurable defaults.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="workflowName">The workflow name</param>
    /// <returns>The created workflow</returns>
    Task<WorkflowDefinition> CreateDefaultWorkflowAsync(string contentType, string workflowName);

    /// <summary>
    /// Validates a workflow definition to ensure it has valid states, transitions, and role mappings.
    /// </summary>
    /// <param name="workflow">The workflow to validate</param>
    /// <returns>Validation result with any errors</returns>
    Task<WorkflowValidationResult> ValidateWorkflowAsync(WorkflowDefinition workflow);

    /// <summary>
    /// Gets workflow analytics for monitoring and optimization.
    /// </summary>
    /// <param name="workflowId">The workflow definition ID</param>
    /// <param name="fromDate">Start date for analytics</param>
    /// <param name="toDate">End date for analytics</param>
    /// <returns>Workflow analytics data</returns>
    Task<WorkflowAnalytics> GetWorkflowAnalyticsAsync(Guid workflowId, DateTime fromDate, DateTime toDate);
}

/// <summary>
/// Result of workflow validation.
/// </summary>
public class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// Workflow analytics data.
/// </summary>
public class WorkflowAnalytics
{
    public Dictionary<string, int> StateDistribution { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, TimeSpan> AverageStateTime { get; set; } = new Dictionary<string, TimeSpan>();
    public Dictionary<string, int> TransitionCounts { get; set; } = new Dictionary<string, int>();
    public List<WorkflowBottleneck> Bottlenecks { get; set; } = new List<WorkflowBottleneck>();
}

/// <summary>
/// Workflow bottleneck information.
/// </summary>
public class WorkflowBottleneck
{
    public string StateKey { get; set; }
    public TimeSpan AverageWaitTime { get; set; }
    public int ContentCount { get; set; }
    public string Description { get; set; }
}