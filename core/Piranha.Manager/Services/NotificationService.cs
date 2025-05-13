/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Piranha.Manager.Models;

namespace Piranha.Manager.Services;

/// <summary>
/// Service for handling notifications.
/// </summary>
public class NotificationService
{
    private readonly IApi _api;
    private readonly ManagerLocalizer _localizer;
    
    // In-memory storage for notifications (in a real application, this would be a database)
    private static List<WorkflowNotification> _notifications = new List<WorkflowNotification>();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="api">The current API</param>
    /// <param name="localizer">The localizer</param>
    public NotificationService(IApi api, ManagerLocalizer localizer)
    {
        _api = api;
        _localizer = localizer;
    }

    /// <summary>
    /// Creates a new workflow notification.
    /// </summary>
    /// <param name="contentId">The content id</param>
    /// <param name="contentType">The content type</param>
    /// <param name="contentTitle">The content title</param>
    /// <param name="userId">The user id</param>
    /// <param name="type">The notification type</param>
    /// <param name="message">The notification message</param>
    /// <returns>The created notification</returns>
    public Task<WorkflowNotification> CreateNotificationAsync(
        Guid contentId, string contentType, string contentTitle,
        Guid userId, string type, string message)
    {
        var notification = new WorkflowNotification
        {
            ContentId = contentId,
            ContentType = contentType,
            ContentTitle = contentTitle,
            UserId = userId,
            UserName = userId.ToString(),
            Type = type,
            Message = message,
            Created = DateTime.Now,
            IsRead = false
        };
        
        _notifications.Add(notification);
        
        return Task.FromResult(notification);
    }

    /// <summary>
    /// Gets all unread notifications for a user.
    /// </summary>
    /// <param name="userId">The user id</param>
    /// <returns>The list of notifications</returns>
    public async Task<List<WorkflowNotification>> GetUnreadNotificationsAsync(Guid userId)
    {
        return await Task.FromResult(
            _notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.Created)
                .ToList()
        );
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="notificationId">The notification id</param>
    /// <returns>True if successful</returns>
    public async Task<bool> MarkAsReadAsync(Guid notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }
}