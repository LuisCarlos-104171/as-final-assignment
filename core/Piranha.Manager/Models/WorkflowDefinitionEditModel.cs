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

namespace Piranha.Manager.Models.Workflow;

/// <summary>
/// Model for editing workflow definitions in the manager.
/// </summary>
public class WorkflowDefinitionEditModel
{
    /// <summary>
    /// Gets/sets the unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets/sets the workflow name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(128, ErrorMessage = "Name cannot exceed 128 characters")]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the workflow description.
    /// </summary>
    [StringLength(512, ErrorMessage = "Description cannot exceed 512 characters")]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the content types this workflow applies to.
    /// </summary>
    [Required(ErrorMessage = "At least one content type must be selected")]
    public string[] ContentTypes { get; set; } = new string[0];

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
    [Required(ErrorMessage = "Initial state is required")]
    public string InitialState { get; set; }

    /// <summary>
    /// Gets/sets the list of states in this workflow.
    /// </summary>
    public List<WorkflowStateEditModel> States { get; set; } = new List<WorkflowStateEditModel>();

    /// <summary>
    /// Gets/sets the list of transitions in this workflow.
    /// </summary>
    public List<WorkflowTransitionEditModel> Transitions { get; set; } = new List<WorkflowTransitionEditModel>();

    /// <summary>
    /// Gets/sets the available content type options.
    /// </summary>
    public List<ContentTypeOption> AvailableContentTypes { get; set; } = new List<ContentTypeOption>();

    /// <summary>
    /// Gets/sets the available permission options.
    /// </summary>
    public List<PermissionOption> AvailablePermissions { get; set; } = new List<PermissionOption>();

    /// <summary>
    /// Gets/sets when the workflow was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets/sets when the workflow was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Content type option for UI selection.
    /// </summary>
    public class ContentTypeOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public bool Selected { get; set; }
    }

    /// <summary>
    /// Permission option for UI selection.
    /// </summary>
    public class PermissionOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }
}