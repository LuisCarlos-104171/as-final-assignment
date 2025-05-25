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
/// Model for editing workflow states in the manager.
/// </summary>
public class WorkflowStateEditModel
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
    [Required(ErrorMessage = "State key is required")]
    [StringLength(64, ErrorMessage = "State key cannot exceed 64 characters")]
    [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "State key must contain only lowercase letters, numbers, and underscores")]
    public string Key { get; set; }

    /// <summary>
    /// Gets/sets the display name.
    /// </summary>
    [Required(ErrorMessage = "State name is required")]
    [StringLength(128, ErrorMessage = "State name cannot exceed 128 characters")]
    public string Name { get; set; }

    /// <summary>
    /// Gets/sets the state description.
    /// </summary>
    [StringLength(512, ErrorMessage = "Description cannot exceed 512 characters")]
    public string Description { get; set; }

    /// <summary>
    /// Gets/sets the state color for UI display.
    /// </summary>
    [StringLength(16)]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code")]
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
    /// Gets/sets if this state is being deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets/sets available icon options.
    /// </summary>
    public List<IconOption> AvailableIcons { get; set; } = new List<IconOption>
    {
        new IconOption { Value = "fas fa-circle", Text = "Circle" },
        new IconOption { Value = "fas fa-edit", Text = "Edit" },
        new IconOption { Value = "fas fa-eye", Text = "Eye" },
        new IconOption { Value = "fas fa-check", Text = "Check" },
        new IconOption { Value = "fas fa-times", Text = "Times" },
        new IconOption { Value = "fas fa-globe", Text = "Globe" },
        new IconOption { Value = "fas fa-clock", Text = "Clock" },
        new IconOption { Value = "fas fa-pause", Text = "Pause" },
        new IconOption { Value = "fas fa-play", Text = "Play" },
        new IconOption { Value = "fas fa-stop", Text = "Stop" }
    };

    /// <summary>
    /// Icon option for UI selection.
    /// </summary>
    public class IconOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }
}