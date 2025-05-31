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

namespace Piranha.Data;

/// <summary>
/// Entity Framework model for workflow role permissions.
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
    /// Gets/sets if this role can execute the transition.
    /// </summary>
    public bool CanExecute { get; set; }

    /// <summary>
    /// Gets/sets if executing this transition requires approval.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Gets/sets additional conditions for executing the transition (JSON format).
    /// </summary>
    [StringLength(1024)]
    public string Conditions { get; set; }

    /// <summary>
    /// Gets/sets the workflow role.
    /// </summary>
    public WorkflowRole WorkflowRole { get; set; }

    /// <summary>
    /// Gets/sets the workflow transition.
    /// </summary>
    public WorkflowTransition WorkflowTransition { get; set; }
}