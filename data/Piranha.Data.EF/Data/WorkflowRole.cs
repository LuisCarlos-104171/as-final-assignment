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
/// Entity Framework model for workflow roles.
/// </summary>
[Serializable]
public class WorkflowRole
{
    /// <summary>
    /// Gets/sets the unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition id.
    /// </summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// Gets/sets the role key that maps to application roles.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string RoleKey { get; set; }

    /// <summary>
    /// Gets/sets the display name for the role.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets/sets the role description.
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the role priority for inheritance.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets/sets if the role can create content.
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Gets/sets if the role can edit content.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets/sets if the role can delete content.
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Gets/sets if the role can view all content.
    /// </summary>
    public bool CanViewAll { get; set; }

    /// <summary>
    /// Gets/sets the comma-separated list of states this role can transition from.
    /// </summary>
    [StringLength(256)]
    public string AllowedFromStates { get; set; }

    /// <summary>
    /// Gets/sets the comma-separated list of states this role can transition to.
    /// </summary>
    [StringLength(256)]
    public string AllowedToStates { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition.
    /// </summary>
    public WorkflowDefinition WorkflowDefinition { get; set; }

    /// <summary>
    /// Gets/sets the role permissions for transitions.
    /// </summary>
    public ICollection<WorkflowRolePermission> RolePermissions { get; set; } = new List<WorkflowRolePermission>();
}