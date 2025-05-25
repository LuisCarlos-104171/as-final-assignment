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
/// Model for listing workflow definitions in the manager.
/// </summary>
public class WorkflowDefinitionListModel
{
    /// <summary>
    /// Gets/sets the workflow definitions.
    /// </summary>
    public List<WorkflowDefinitionItem> Items { get; set; } = new List<WorkflowDefinitionItem>();

    /// <summary>
    /// Workflow definition list item.
    /// </summary>
    public class WorkflowDefinitionItem
    {
        /// <summary>
        /// Gets/sets the unique id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets/sets the workflow name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets/sets the workflow description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets/sets the content types this workflow applies to.
        /// </summary>
        public string[] ContentTypes { get; set; } = new string[0];

        /// <summary>
        /// Gets/sets if this is the default workflow.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets/sets if this workflow is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets/sets the number of states in this workflow.
        /// </summary>
        public int StateCount { get; set; }

        /// <summary>
        /// Gets/sets the number of transitions in this workflow.
        /// </summary>
        public int TransitionCount { get; set; }

        /// <summary>
        /// Gets/sets when the workflow was created.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets/sets when the workflow was last modified.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets the content types as a display string.
        /// </summary>
        public string ContentTypesDisplay => ContentTypes != null && ContentTypes.Length > 0
            ? string.Join(", ", ContentTypes.Select(ct => ct.Substring(0, 1).ToUpper() + ct.Substring(1)))
            : "None";
    }
}