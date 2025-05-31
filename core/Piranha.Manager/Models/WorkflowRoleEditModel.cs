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

namespace Piranha.Manager.Models;

/// <summary>
/// Model for editing workflow roles in the manager.
/// </summary>
public class WorkflowRoleEditModel
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
    [Required(ErrorMessage = "Role key is required")]
    [StringLength(64, ErrorMessage = "Role key cannot exceed 64 characters")]
    public string RoleKey { get; set; }

    /// <summary>
    /// Gets/sets the display name for the role.
    /// </summary>
    [Required(ErrorMessage = "Display name is required")]
    [StringLength(128, ErrorMessage = "Display name cannot exceed 128 characters")]
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets/sets the role description.
    /// </summary>
    [StringLength(512, ErrorMessage = "Description cannot exceed 512 characters")]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the role priority for inheritance.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Priority must be between 1 and 100")]
    public int Priority { get; set; } = 1;

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
    [StringLength(256, ErrorMessage = "Allowed from states cannot exceed 256 characters")]
    public string AllowedFromStates { get; set; }

    /// <summary>
    /// Gets/sets the comma-separated list of states this role can transition to.
    /// </summary>
    [StringLength(256, ErrorMessage = "Allowed to states cannot exceed 256 characters")]
    public string AllowedToStates { get; set; }

    /// <summary>
    /// Gets/sets if this role is deleted (for UI handling).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets/sets the sort order for display.
    /// </summary>
    public int SortOrder { get; set; }
}