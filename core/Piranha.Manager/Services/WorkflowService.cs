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
/// Service for handling content workflow.
/// </summary>
public class WorkflowService
{
    private readonly IApi _api;
    private readonly ManagerLocalizer _localizer;
    private readonly NotificationService _notificationService;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="api">The current API</param>
    /// <param name="localizer">The localizer</param>
    /// <param name="notificationService">The notification service</param>
    public WorkflowService(IApi api, ManagerLocalizer localizer, NotificationService notificationService)
    {
        _api = api;
        _localizer = localizer;
        _notificationService = notificationService;
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
        
        // Get user permissions from the current principal
        var permissions = App.Permissions.GetPublicPermissions()
            .Select(p => p.Name)
            .ToList();

        // Get all available transitions based on the current state and user permissions
        model.AvailableTransitions = await GetAvailableTransitionsAsync(contentType, model.CurrentState, permissions);

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
            // Get user permissions from the current principal
            var permissions = App.Permissions.GetPublicPermissions()
                .Select(p => p.Name)
                .ToList();

            // Validate the transition is allowed for this user
            var transitions = await GetAvailableTransitionsAsync(model.ContentType, model.CurrentState, permissions);
            var transition = transitions.FirstOrDefault(t => t.ToState == model.TargetState);

            if (transition == null)
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
                await _notificationService.CreateNotificationAsync(
                    page.Id, 
                    "page", 
                    page.Title,
                    Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                    "workflow",
                    $"Page workflow state updated to {GetStateDisplayName(model.TargetState)}");

                return new StatusMessage
                {
                    Type = StatusMessage.Success,
                    Body = string.Format(_localizer.General["Page workflow state updated to {0}."], GetStateDisplayName(model.TargetState))
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
                await _notificationService.CreateNotificationAsync(
                    post.Id, 
                    "post", 
                    post.Title,
                    Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                    "workflow",
                    $"Post workflow state updated to {GetStateDisplayName(model.TargetState)}");

                return new StatusMessage
                {
                    Type = StatusMessage.Success,
                    Body = string.Format(_localizer.General["Post workflow state updated to {0}."], GetStateDisplayName(model.TargetState))
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
    /// Gets the available transitions for the current state and user permissions.
    /// </summary>
    private async Task<List<WorkflowModel.WorkflowTransition>> GetAvailableTransitionsAsync(string contentType, string currentState, List<string> permissions)
    {
        var transitions = new List<WorkflowModel.WorkflowTransition>();

        // Set up permission prefixes based on content type
        string submitPermission, approvePermission, rejectPermission, publishPermission;

        if (contentType == "page")
        {
            submitPermission = WorkflowPermissions.PagesSubmitForReview;
            approvePermission = WorkflowPermissions.PagesApprove;
            rejectPermission = WorkflowPermissions.PagesReject;
            publishPermission = Permission.PagesPublish;
        }
        else if (contentType == "post")
        {
            submitPermission = WorkflowPermissions.PostsSubmitForReview;
            approvePermission = WorkflowPermissions.PostsApprove;
            rejectPermission = WorkflowPermissions.PostsReject;
            publishPermission = Permission.PostsPublish;
        }
        else
        {
            submitPermission = WorkflowPermissions.ContentSubmitForReview;
            approvePermission = WorkflowPermissions.ContentApprove;
            rejectPermission = WorkflowPermissions.ContentReject;
            publishPermission = Permission.PagesPublish; // Default to page publish permission
        }

        // Define transitions based on current state
        if (currentState == "draft" || currentState == "new")
        {
            // From Draft: can submit for review
                if (permissions.Contains(submitPermission))
                {
                    transitions.Add(new WorkflowModel.WorkflowTransition
                    {
                        FromState = currentState,
                        ToState = "in_review",
                        Name = "Submit for Review",
                        Permission = submitPermission,
                        CssClass = "btn-primary"
                    });
                }
        }
        else if (currentState == "in_review")
        {
            // From InReview: can approve or reject
            if (permissions.Contains(approvePermission))
            {
                transitions.Add(new WorkflowModel.WorkflowTransition
                {
                    FromState = currentState,
                    ToState = "approved",
                    Name = "Approve",
                    Permission = approvePermission,
                    CssClass = "btn-success"
                });
            }

            if (permissions.Contains(rejectPermission))
            {
                transitions.Add(new WorkflowModel.WorkflowTransition
                {
                    FromState = currentState,
                    ToState = "rejected",
                    Name = "Reject",
                    Permission = rejectPermission,
                    CssClass = "btn-danger"
                });
            }
        }
        else if (currentState == "approved")
        {
            // From Approved: can publish
            if (permissions.Contains(publishPermission))
            {
                transitions.Add(new WorkflowModel.WorkflowTransition
                {
                    FromState = currentState,
                    ToState = "published",
                    Name = "Publish",
                    Permission = publishPermission,
                    CssClass = "btn-success"
                });
            }
        }
        else if (currentState == "rejected")
        {
            // From Rejected: can resubmit to draft
            transitions.Add(new WorkflowModel.WorkflowTransition
            {
                FromState = currentState,
                ToState = "draft",
                Name = "Back to Draft",
                Permission = "Everyone",
                CssClass = "btn-primary"
            });
        }
        else if (currentState == "published")
        {
            // From Published: can unpublish to draft
            if (permissions.Contains(publishPermission))
            {
                transitions.Add(new WorkflowModel.WorkflowTransition
                {
                    FromState = currentState,
                    ToState = "draft",
                    Name = "Unpublish to Draft",
                    Permission = publishPermission,
                    CssClass = "btn-warning"
                });
            }
        }

        return await Task.FromResult(transitions);
    }

    /// <summary>
    /// Gets a user-friendly display name for a workflow state
    /// </summary>
    private string GetStateDisplayName(string state)
    {
        if (state == "draft")
            return "Draft";
        else if (state == "in_review")
            return "In Review";
        else if (state == "approved")
            return "Approved";
        else if (state == "rejected")
            return "Rejected";
        else if (state == "published")
            return "Published";
        else if (state == "unpublished")
            return "Unpublished";
        else if (state == "new")
            return "New";
        else
            return state;
    }
}