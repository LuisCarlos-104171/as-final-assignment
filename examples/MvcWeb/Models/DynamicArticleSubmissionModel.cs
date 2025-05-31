/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using System.ComponentModel.DataAnnotations;
using Piranha.Models;

namespace MvcWeb.Models;

/// <summary>
/// Dynamic article submission model that uses configurable workflows
/// instead of hardcoded status enums.
/// </summary>
public class DynamicArticleSubmissionModel
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; }

    [Required]
    public string Content { get; set; }

    [StringLength(500)]
    public string Summary { get; set; }

    [StringLength(100)]
    public string Author { get; set; }

    public string AuthorId { get; set; }

    public DateTime Created { get; set; }

    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition ID for this article.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets/sets the current workflow state key.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string WorkflowState { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition for this article.
    /// </summary>
    public WorkflowDefinition Workflow { get; set; }

    /// <summary>
    /// Gets/sets the current workflow state object.
    /// </summary>
    public WorkflowState CurrentState { get; set; }

    /// <summary>
    /// Gets/sets available transitions for the current user.
    /// </summary>
    public List<WorkflowTransition> AvailableTransitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>
    /// Gets/sets workflow history for this article.
    /// </summary>
    public List<WorkflowHistoryEntry> WorkflowHistory { get; set; } = new List<WorkflowHistoryEntry>();

    public string ReviewedById { get; set; }
    public string ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewComments { get; set; }

    public string ApprovedById { get; set; }
    public string ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ApprovalComments { get; set; }

    public Guid? PostId { get; set; }
    public DateTime? Published { get; set; }

    /// <summary>
    /// Gets the CSS class for the current workflow state badge.
    /// </summary>
    public string GetStateBadgeClass()
    {
        if (CurrentState != null && !string.IsNullOrEmpty(CurrentState.Color))
        {
            // Convert hex color to Bootstrap badge class
            return CurrentState.Color switch
            {
                "#6c757d" => "bg-secondary",
                "#0dcaf0" => "bg-info", 
                "#198754" => "bg-success",
                "#dc3545" => "bg-danger",
                "#ffc107" => "bg-warning",
                "#212529" => "bg-dark",
                _ => "bg-primary"
            };
        }

        return "bg-secondary";
    }

    /// <summary>
    /// Gets the icon for the current workflow state.
    /// </summary>
    public string GetStateIcon()
    {
        return CurrentState?.Icon ?? "fas fa-circle";
    }

    /// <summary>
    /// Checks if the current state allows editing.
    /// </summary>
    public bool CanEdit()
    {
        // Can edit if not in a final state
        return CurrentState?.IsFinal != true;
    }

    /// <summary>
    /// Checks if the current state represents published content.
    /// </summary>
    public bool IsPublished()
    {
        return CurrentState?.IsPublished == true;
    }
}

/// <summary>
/// Workflow history entry for tracking state changes.
/// </summary>
public class WorkflowHistoryEntry
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public string FromState { get; set; }
    public string ToState { get; set; }
    public string TransitionName { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Comments { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Request model for workflow transitions.
/// </summary>
public class WorkflowTransitionRequest
{
    public Guid ArticleId { get; set; }
    public Guid TransitionId { get; set; }
    public string Comments { get; set; }
}

/// <summary>
/// Model for workflow configuration in the admin interface.
/// </summary>
public class WorkflowConfigurationModel
{
    public List<WorkflowDefinition> AvailableWorkflows { get; set; } = new List<WorkflowDefinition>();
    public WorkflowDefinition SelectedWorkflow { get; set; }
    public List<WorkflowRole> WorkflowRoles { get; set; } = new List<WorkflowRole>();
    public Dictionary<string, List<WorkflowTransition>> TransitionsByState { get; set; } = new Dictionary<string, List<WorkflowTransition>>();
}