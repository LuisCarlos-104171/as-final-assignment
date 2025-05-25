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
/// Model for editing workflow transitions in the manager.
/// </summary>
public class WorkflowTransitionEditModel
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
    [Required(ErrorMessage = "From state is required")]
    public string FromStateKey { get; set; }

    /// <summary>
    /// Gets/sets the to state key.
    /// </summary>
    [Required(ErrorMessage = "To state is required")]
    public string ToStateKey { get; set; }

    /// <summary>
    /// Gets/sets the transition name.
    /// </summary>
    [Required(ErrorMessage = "Transition name is required")]
    [StringLength(128, ErrorMessage = "Transition name cannot exceed 128 characters")]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the transition description.
    /// </summary>
    [StringLength(512, ErrorMessage = "Description cannot exceed 512 characters")]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the required permission.
    /// </summary>
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
    [StringLength(1024, ErrorMessage = "Notification template cannot exceed 1024 characters")]
    public string NotificationTemplate { get; set; }

    /// <summary>
    /// Gets/sets if this transition is being deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets/sets available CSS class options.
    /// </summary>
    public List<CssClassOption> AvailableCssClasses { get; set; } = new List<CssClassOption>
    {
        new CssClassOption { Value = "btn-primary", Text = "Primary (Blue)" },
        new CssClassOption { Value = "btn-secondary", Text = "Secondary (Gray)" },
        new CssClassOption { Value = "btn-success", Text = "Success (Green)" },
        new CssClassOption { Value = "btn-danger", Text = "Danger (Red)" },
        new CssClassOption { Value = "btn-warning", Text = "Warning (Yellow)" },
        new CssClassOption { Value = "btn-info", Text = "Info (Cyan)" },
        new CssClassOption { Value = "btn-light", Text = "Light" },
        new CssClassOption { Value = "btn-dark", Text = "Dark" }
    };

    /// <summary>
    /// Gets/sets available icon options.
    /// </summary>
    public List<IconOption> AvailableIcons { get; set; } = new List<IconOption>
    {
        new IconOption { Value = "fas fa-arrow-right", Text = "Arrow Right" },
        new IconOption { Value = "fas fa-paper-plane", Text = "Paper Plane" },
        new IconOption { Value = "fas fa-check", Text = "Check" },
        new IconOption { Value = "fas fa-times", Text = "Times" },
        new IconOption { Value = "fas fa-globe", Text = "Globe" },
        new IconOption { Value = "fas fa-eye-slash", Text = "Eye Slash" },
        new IconOption { Value = "fas fa-undo", Text = "Undo" },
        new IconOption { Value = "fas fa-edit", Text = "Edit" },
        new IconOption { Value = "fas fa-save", Text = "Save" },
        new IconOption { Value = "fas fa-trash", Text = "Trash" }
    };

    /// <summary>
    /// CSS class option for UI selection.
    /// </summary>
    public class CssClassOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// Icon option for UI selection.
    /// </summary>
    public class IconOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }
}