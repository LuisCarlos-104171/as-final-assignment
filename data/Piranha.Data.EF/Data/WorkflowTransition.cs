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
/// Entity Framework model for workflow transitions.
/// </summary>
[Serializable]
public class WorkflowTransition
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
    /// Gets/sets the from state key.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string FromStateKey { get; set; }

    /// <summary>
    /// Gets/sets the to state key.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string ToStateKey { get; set; }

    /// <summary>
    /// Gets/sets the transition name.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the transition description.
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the required permission.
    /// </summary>
    [StringLength(128)]
    public string RequiredPermission { get; set; }

    /// <summary>
    /// Gets/sets the CSS class for the transition button.
    /// </summary>
    [StringLength(64)]
    public string CssClass { get; set; } = "btn-primary";

    /// <summary>
    /// Gets/sets the icon for the transition button.
    /// </summary>
    [StringLength(64)]
    public string Icon { get; set; } = "fas fa-arrow-right";

    /// <summary>
    /// Gets/sets the sort order.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets/sets if comments are required for this transition.
    /// </summary>
    public bool RequiresComment { get; set; }

    /// <summary>
    /// Gets/sets if this transition should send notifications.
    /// </summary>
    public bool SendNotification { get; set; } = true;

    /// <summary>
    /// Gets/sets the notification template.
    /// </summary>
    [StringLength(1024)]
    public string NotificationTemplate { get; set; }

    /// <summary>
    /// Gets/sets the workflow definition.
    /// </summary>
    public WorkflowDefinition WorkflowDefinition { get; set; }
}