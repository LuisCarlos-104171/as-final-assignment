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
/// Entity Framework model for workflow states.
/// </summary>
[Serializable]
public class WorkflowState
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
    /// Gets/sets the state key.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string Key { get; set; }

    /// <summary>
    /// Gets/sets the display name.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the state description.
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the state color for UI display.
    /// </summary>
    [StringLength(16)]
    public string Color { get; set; } = "#6c757d";

    /// <summary>
    /// Gets/sets the state icon for UI display.
    /// </summary>
    [StringLength(64)]
    public string Icon { get; set; } = "fas fa-circle";

    /// <summary>
    /// Gets/sets the sort order.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets/sets if this is a published state.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Gets/sets if this is an initial state.
    /// </summary>
    public bool IsInitial { get; set; }

    /// <summary>
    /// Gets/sets if this is a final state.
    /// </summary>
    public bool IsFinal { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition.
    /// </summary>
    public WorkflowDefinition WorkflowDefinition { get; set; }
}