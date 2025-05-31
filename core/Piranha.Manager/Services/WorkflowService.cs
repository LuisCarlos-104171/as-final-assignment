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
using Piranha.Services;

namespace Piranha.Manager.Services;

/// <summary>
/// Service for handling content workflow.
/// </summary>
public class WorkflowService
{
    private readonly IApi _api;
    private readonly ManagerLocalizer _localizer;
    private readonly NotificationService _notificationService;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="api">The current API</param>
    /// <param name="localizer">The localizer</param>
    /// <param name="notificationService">The notification service</param>
    /// <param name="workflowDefinitionService">The workflow definition service</param>
    public WorkflowService(IApi api, ManagerLocalizer localizer, NotificationService notificationService, IWorkflowDefinitionService workflowDefinitionService)
    {
        _api = api;
        _localizer = localizer;
        _notificationService = notificationService;
        _workflowDefinitionService = workflowDefinitionService;
    }

    /// <summary>
    /// Gets the available workflow transitions for the current content and user.
    /// </summary>
    /// <param name="contentType">The content type</param>
    /// <param name="contentId">The content id</param>
    /// <param name="userId">The current user id</param>
    /// <returns>The workflow model</returns>
    public async Task<WorkflowModel> GetWorkflowTransitionsAsync(string contentType, Guid contentId, string userId)
    {
        var model = new WorkflowModel
        {
            ContentId = contentId,
            ContentType = contentType
        };

        // Get the content to determine its current state
        var state = ContentState.Draft;
        if (contentType == "page")
        {
            var page = await _api.Pages.GetByIdAsync(contentId);
            model.CurrentState = page?.WorkflowState ?? ContentState.Draft;
        }
        else if (contentType == "post")
        {
            var post = await _api.Posts.GetByIdAsync(contentId);
            model.CurrentState = post?.WorkflowState ?? ContentState.Draft;
        }
        else
        {
            model.CurrentState = ContentState.Draft;
        }
        
        // TODO: Get user roles from the current principal
        var userRoles = new List<Guid>(); // For now, empty list - should be populated with actual user roles

        // Get all available transitions based on the current state and user roles
        var workflowTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync(contentType, model.CurrentState, userRoles);
        model.AvailableTransitions = workflowTransitions.Select(t => new WorkflowModel.WorkflowTransition
        {
            FromState = t.FromStateKey,
            ToState = t.ToStateKey,
            Name = t.Name,
            RoleId = t.RequiredRoleId,
            CssClass = t.CssClass
        }).ToList();

        return model;
    }

    /// <summary>
    /// Performs a workflow state transition.
    /// </summary>
    /// <param name="model">The workflow model</param>
    /// <param name="userId">The current user id</param>
    /// <returns>Status message</returns>
    public async Task<StatusMessage> PerformTransitionAsync(WorkflowModel model, string userId)
    {
        try
        {
            // TODO: Get user roles from the current principal
            var userRoles = new List<Guid>(); // For now, empty list - should be populated with actual user roles

            // Validate the transition is allowed for this user
            var isValidTransition = await _workflowDefinitionService.ValidateTransitionAsync(model.ContentType, model.CurrentState, model.TargetState, userRoles);

            if (!isValidTransition)
            {
                return new StatusMessage
                {
                    Type = StatusMessage.Error,
                    Body = _localizer.General["You don't have permission to perform this workflow transition."]
                };
            }

            // Perform the transition
            if (model.ContentType == "page")
            {
                var page = await _api.Pages.GetByIdAsync(model.ContentId);
                if (page == null)
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["Page not found."]
                    };
                }

                // Update the page's workflow state
                page.WorkflowState = model.TargetState;
                page.LastReviewerId = Guid.NewGuid(); // Use a generic ID since we don't depend on identity
                page.LastReviewedOn = DateTime.Now;
                page.ReviewComment = model.Comment;

                // If the state is "Approved", we don't publish yet, just mark as approved
                // If the state is "Published", then we actually publish
                if (model.TargetState == ContentState.Published)
                {
                    page.Published = DateTime.Now;
                }

                await _api.Pages.SaveAsync(page);

                // Create notification
                var pageDisplayName = await GetStateDisplayNameAsync("page", model.TargetState);
                await _notificationService.CreateNotificationAsync(
                    page.Id, 
                    "page", 
                    page.Title,
                    Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                    "workflow",
                    $"Page workflow state updated to {pageDisplayName}");

                var displayName = await GetStateDisplayNameAsync("page", model.TargetState);
                return new StatusMessage
                {
                    Type = StatusMessage.Success,
                    Body = string.Format(_localizer.General["Page workflow state updated to {0}."], displayName)
                };
            }
            else if (model.ContentType == "post")
            {
                var post = await _api.Posts.GetByIdAsync(model.ContentId);
                if (post == null)
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["Post not found."]
                    };
                }

                // Update the post's workflow state
                post.WorkflowState = model.TargetState;
                post.LastReviewerId = Guid.NewGuid(); // Use a generic ID since we don't depend on identity
                post.LastReviewedOn = DateTime.Now;
                post.ReviewComment = model.Comment;

                // If the state is "Approved", we don't publish yet, just mark as approved
                // If the state is "Published", then we actually publish
                if (model.TargetState == ContentState.Published)
                {
                    post.Published = DateTime.Now;
                }

                await _api.Posts.SaveAsync(post);

                // Create notification
                var postDisplayName = await GetStateDisplayNameAsync("post", model.TargetState);
                await _notificationService.CreateNotificationAsync(
                    post.Id, 
                    "post", 
                    post.Title,
                    Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                    "workflow",
                    $"Post workflow state updated to {postDisplayName}");

                var postDisplayNameForMessage = await GetStateDisplayNameAsync("post", model.TargetState);
                return new StatusMessage
                {
                    Type = StatusMessage.Success,
                    Body = string.Format(_localizer.General["Post workflow state updated to {0}."], postDisplayNameForMessage)
                };
            }

            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = _localizer.General["Content type not supported."]
            };
        }
        catch (Exception ex)
        {
            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = ex.Message
            };
        }
    }


    /// <summary>
    /// Gets a user-friendly display name for a workflow state
    /// </summary>
    private async Task<string> GetStateDisplayNameAsync(string contentType, string state)
    {
        try
        {
            var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync(contentType);
            var workflowState = workflow?.States?.FirstOrDefault(s => s.Key == state);
            return workflowState?.Name ?? state;
        }
        catch
        {
            // Fallback to default names if workflow service is not available
            return state switch
            {
                "draft" => "Draft",
                "in_review" => "In Review",
                "approved" => "Approved",
                "rejected" => "Rejected",
                "published" => "Published",
                "unpublished" => "Unpublished",
                "new" => "New",
                _ => state
            };
        }
    }
}