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
/// Model for workflow notifications.
/// </summary>
public class WorkflowNotification
{
    /// <summary>
    /// Gets/sets the notification id.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets/sets the content id.
    /// </summary>
    public Guid ContentId { get; set; }

    /// <summary>
    /// Gets/sets the content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets/sets the content title.
    /// </summary>
    public string ContentTitle { get; set; }

    /// <summary>
    /// Gets/sets the user id who performed the action.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets/sets the user name who performed the action.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets/sets the notification type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets/sets the notification message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets/sets the notification creation date.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets/sets if the notification has been read.
    /// </summary>
    public bool IsRead { get; set; }
}