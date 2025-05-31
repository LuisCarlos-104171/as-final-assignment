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

namespace Piranha.Models;

/// <summary>
/// Model for mapping workflow roles to specific workflow transitions.
/// This replaces hardcoded permission strings with configurable role-based permissions.
/// </summary>
[Serializable]
public class WorkflowRolePermission
{
    /// <summary>
    /// Gets/sets the unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets/sets the workflow role id.
    /// </summary>
    public Guid WorkflowRoleId { get; set; }

    /// <summary>
    /// Gets/sets the workflow transition id.
    /// </summary>
    public Guid WorkflowTransitionId { get; set; }

    /// <summary>
    /// Gets/sets whether this role can execute this transition.
    /// </summary>
    public bool CanExecute { get; set; } = true;

    /// <summary>
    /// Gets/sets whether this role requires additional approval for this transition.
    /// </summary>
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// Gets/sets the role that can approve this transition if RequiresApproval is true.
    /// </summary>
    public Guid? ApprovalRoleId { get; set; }

    /// <summary>
    /// Gets/sets additional conditions that must be met for this permission (JSON format).
    /// For example: {"minimumReviewTime": "24:00:00", "requiredReviewers": 2}
    /// </summary>
    [StringLength(1024)]
    public string Conditions { get; set; }

    /// <summary>
    /// Gets/sets when this permission was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets/sets the workflow role.
    /// </summary>
    public WorkflowRole WorkflowRole { get; set; }

    /// <summary>
    /// Gets/sets the workflow transition.
    /// </summary>
    public WorkflowTransition WorkflowTransition { get; set; }

    /// <summary>
    /// Gets/sets the approval role if required.
    /// </summary>
    public WorkflowRole ApprovalRole { get; set; }
}