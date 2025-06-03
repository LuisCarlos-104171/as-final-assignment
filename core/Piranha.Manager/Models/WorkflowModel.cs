/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

namespace Piranha.Manager.Models;

/// <summary>
/// Model for workflow state transitions.
/// </summary>
public class WorkflowModel
{
    /// <summary>
    /// Gets/sets the content id.
    /// </summary>
    public Guid ContentId { get; set; }

    /// <summary>
    /// Gets/sets the content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets/sets the current workflow state.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets/sets the target workflow state.
    /// </summary>
    public string TargetState { get; set; }

    /// <summary>
    /// Gets/sets the reviewer comment.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets/sets the list of available transitions for the current user.
    /// </summary>
    public List<WorkflowTransition> AvailableTransitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>
    /// Workflow state transition.
    /// </summary>
    public class WorkflowTransition
    {
        /// <summary>
        /// Gets/sets the source state.
        /// </summary>
        public string FromState { get; set; }

        /// <summary>
        /// Gets/sets the target state.
        /// </summary>
        public string ToState { get; set; }

        /// <summary>
        /// Gets/sets the display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets/sets the role required for this transition.
        /// </summary>
        public Guid? RoleId { get; set; }

        /// <summary>
        /// Gets/sets the CSS class for the button.
        /// </summary>
        public string CssClass { get; set; }

        /// <summary>
        /// Gets/sets if comments are required for this transition.
        /// </summary>
        public bool RequiresComment { get; set; }

        /// <summary>
        /// Gets/sets the icon for the transition button.
        /// </summary>
        public string Icon { get; set; }
    }
}