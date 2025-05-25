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
/// Entity Framework model for workflow definitions.
/// </summary>
[Serializable]
public class WorkflowDefinition
{
    /// <summary>
    /// Gets/sets the unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets/sets the workflow name.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the workflow description.
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the content types this workflow applies to.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string ContentTypes { get; set; }

    /// <summary>
    /// Gets/sets if this is the default workflow.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets/sets if this workflow is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets/sets the initial state for new content.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string InitialState { get; set; }

    /// <summary>
    /// Gets/sets when the workflow was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets/sets when the workflow was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets/sets the list of states in this workflow.
    /// </summary>
    public ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();

    /// <summary>
    /// Gets/sets the list of transitions in this workflow.
    /// </summary>
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}