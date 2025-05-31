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
/// Model for workflow roles that define what permissions are available for workflow operations.
/// This replaces hardcoded role checks with configurable role definitions.
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
    /// Gets/sets the role key/name that maps to ASP.NET Identity roles.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string RoleKey { get; set; }

    /// <summary>
    /// Gets/sets the display name for this role.
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
    /// Gets/sets the priority/hierarchy level. Higher numbers = higher priority.
    /// This allows role inheritance (e.g., Approver can do Editor actions).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets/sets whether this role can create new content.
    /// </summary>
    public bool CanCreate { get; set; } = true;

    /// <summary>
    /// Gets/sets whether this role can edit content.
    /// </summary>
    public bool CanEdit { get; set; } = true;

    /// <summary>
    /// Gets/sets whether this role can delete content.
    /// </summary>
    public bool CanDelete { get; set; } = false;

    /// <summary>
    /// Gets/sets whether this role can view all content or only owned content.
    /// </summary>
    public bool CanViewAll { get; set; } = false;

    /// <summary>
    /// Gets/sets the comma-separated list of workflow states this role can transition from.
    /// Empty means can transition from any state (based on transition permissions).
    /// </summary>
    [StringLength(512)]
    public string AllowedFromStates { get; set; }

    /// <summary>
    /// Gets/sets the comma-separated list of workflow states this role can transition to.
    /// Empty means can transition to any state (based on transition permissions).
    /// </summary>
    [StringLength(512)]
    public string AllowedToStates { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition.
    /// </summary>
    public WorkflowDefinition WorkflowDefinition { get; set; }

    /// <summary>
    /// Gets the allowed from states as an array.
    /// </summary>
    public string[] GetAllowedFromStates()
    {
        return !string.IsNullOrEmpty(AllowedFromStates) 
            ? AllowedFromStates.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray()
            : new string[0];
    }

    /// <summary>
    /// Sets the allowed from states from an array.
    /// </summary>
    /// <param name="states">The allowed from states</param>
    public void SetAllowedFromStates(string[] states)
    {
        AllowedFromStates = states != null ? string.Join(",", states) : "";
    }

    /// <summary>
    /// Gets the allowed to states as an array.
    /// </summary>
    public string[] GetAllowedToStates()
    {
        return !string.IsNullOrEmpty(AllowedToStates) 
            ? AllowedToStates.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray()
            : new string[0];
    }

    /// <summary>
    /// Sets the allowed to states from an array.
    /// </summary>
    /// <param name="states">The allowed to states</param>
    public void SetAllowedToStates(string[] states)
    {
        AllowedToStates = states != null ? string.Join(",", states) : "";
    }
}