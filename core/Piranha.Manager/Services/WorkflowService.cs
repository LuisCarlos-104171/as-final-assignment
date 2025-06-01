/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.AspNetCore.Http;
using Piranha.Manager.Models;
using Piranha.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ILogger<WorkflowService> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="api">The current API</param>
    /// <param name="localizer">The localizer</param>
    /// <param name="notificationService">The notification service</param>
    /// <param name="workflowDefinitionService">The workflow definition service</param>
    /// <param name="httpContextAccessor">The HTTP context accessor</param>
    public WorkflowService(IApi api, ManagerLocalizer localizer, NotificationService notificationService, IWorkflowDefinitionService workflowDefinitionService, IHttpContextAccessor httpContextAccessor, ILogger<WorkflowService> logger)
    {
        _logger = logger;
        _api = api;
        _localizer = localizer;
        _notificationService = notificationService;
        _workflowDefinitionService = workflowDefinitionService;
        _httpContextAccessor = httpContextAccessor;
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
        
        // Get user roles from the current principal
        var userRoles = await GetCurrentUserRolesAsync();

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
            // Get user roles from the current principal
            var userRoles = await GetCurrentUserRolesAsync();

            // Validate the transition is allowed for this user
            _logger?.LogInformation("Validating transition for content {ContentType} from {CurrentState} to {TargetState} with user roles: {UserRoles}", 
                model.ContentType, model.CurrentState, model.TargetState, string.Join(", ", userRoles));
                
            var isValidTransition = await _workflowDefinitionService.ValidateTransitionAsync(model.ContentType, model.CurrentState, model.TargetState, userRoles);

            if (!isValidTransition)
            {
                _logger?.LogWarning("Transition validation failed for content {ContentType} from {CurrentState} to {TargetState} with user roles: {UserRoles}", 
                    model.ContentType, model.CurrentState, model.TargetState, string.Join(", ", userRoles));
                    
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

                // Get the specific transition that was performed
                var workflowTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("page", model.CurrentState, userRoles);
                
                _logger?.LogInformation("Found {TransitionCount} available transitions for page from {CurrentState}: {Transitions}", 
                    workflowTransitions.Count(), model.CurrentState, 
                    string.Join(", ", workflowTransitions.Select(t => $"{t.FromStateKey}->{t.ToStateKey} (RequiredRole: {t.RequiredRoleId})")));
                
                var transition = workflowTransitions.FirstOrDefault(t => t.ToStateKey == model.TargetState);

                if (transition == null)
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["Invalid workflow transition."]
                    };
                }

                // Validate comment requirement
                if (transition.RequiresComment && string.IsNullOrWhiteSpace(model.Comment))
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["This transition requires a comment."]
                    };
                }

                // Update the page's workflow state
                page.WorkflowState = model.TargetState;
                page.LastReviewerId = Guid.NewGuid(); // Use a generic ID since we don't depend on identity
                page.LastReviewedOn = DateTime.Now;
                page.ReviewComment = model.Comment;

                // Handle publishing logic based on target state
                if (model.TargetState == ContentState.Published)
                {
                    page.Published = DateTime.Now;
                }
                else if (model.TargetState == ContentState.Draft && page.Published.HasValue)
                {
                    // Unpublishing - clear the published date
                    page.Published = null;
                }

                await _api.Pages.SaveAsync(page);

                // Send notification if the transition requires it
                if (transition.SendNotification)
                {
                    var notificationMessage = !string.IsNullOrWhiteSpace(transition.NotificationTemplate) 
                        ? transition.NotificationTemplate 
                        : $"Page workflow state updated to {await GetStateDisplayNameAsync("page", model.TargetState)}";

                    await _notificationService.CreateNotificationAsync(
                        page.Id, 
                        "page", 
                        page.Title,
                        Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                        "workflow",
                        notificationMessage);
                }

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

                // Get the specific transition that was performed
                var workflowTransitions = await _workflowDefinitionService.GetAvailableTransitionsAsync("post", model.CurrentState, userRoles);
                
                _logger?.LogInformation("Found {TransitionCount} available transitions for post from {CurrentState}: {Transitions}", 
                    workflowTransitions.Count(), model.CurrentState, 
                    string.Join(", ", workflowTransitions.Select(t => $"{t.FromStateKey}->{t.ToStateKey} (RequiredRole: {t.RequiredRoleId})")));
                
                var transition = workflowTransitions.FirstOrDefault(t => t.ToStateKey == model.TargetState);

                if (transition == null)
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["Invalid workflow transition."]
                    };
                }

                // Validate comment requirement
                if (transition.RequiresComment && string.IsNullOrWhiteSpace(model.Comment))
                {
                    return new StatusMessage
                    {
                        Type = StatusMessage.Error,
                        Body = _localizer.General["This transition requires a comment."]
                    };
                }

                // Update the post's workflow state
                post.WorkflowState = model.TargetState;
                post.LastReviewerId = Guid.NewGuid(); // Use a generic ID since we don't depend on identity
                post.LastReviewedOn = DateTime.Now;
                post.ReviewComment = model.Comment;

                // Handle publishing logic based on target state
                if (model.TargetState == ContentState.Published)
                {
                    post.Published = DateTime.Now;
                }
                else if (model.TargetState == ContentState.Draft && post.Published.HasValue)
                {
                    // Unpublishing - clear the published date
                    post.Published = null;
                }

                await _api.Posts.SaveAsync(post);

                // Send notification if the transition requires it
                if (transition.SendNotification)
                {
                    var notificationMessage = !string.IsNullOrWhiteSpace(transition.NotificationTemplate) 
                        ? transition.NotificationTemplate 
                        : $"Post workflow state updated to {await GetStateDisplayNameAsync("post", model.TargetState)}";

                    await _notificationService.CreateNotificationAsync(
                        post.Id, 
                        "post", 
                        post.Title,
                        Guid.NewGuid(), // Generate a new ID since we don't depend on identity
                        "workflow",
                        notificationMessage);
                }

                var displayName = await GetStateDisplayNameAsync("post", model.TargetState);
                return new StatusMessage
                {
                    Type = StatusMessage.Success,
                    Body = string.Format(_localizer.General["Post workflow state updated to {0}."], displayName)
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
    /// Gets the current user's role IDs from the claims principal
    /// </summary>
    private async Task<List<Guid>> GetCurrentUserRolesAsync()
    {
        var roleIds = new List<Guid>();
        
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger?.LogWarning("User is not authenticated");
                return roleIds;
            }

            _logger?.LogInformation("Getting roles for user: {UserName}", user.Identity.Name);

            // Get role claims from the user's identity
            var roleClaims = user.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").ToList();
            
            _logger?.LogInformation("Found {ClaimCount} role claims for user {UserName}: {Claims}", 
                roleClaims.Count, user.Identity.Name, string.Join(", ", roleClaims.Select(c => $"{c.Type}={c.Value}")));
            
            foreach (var roleClaim in roleClaims)
            {
                _logger?.LogDebug("Processing role claim: {Type}={Value}", roleClaim.Type, roleClaim.Value);
                
                // Try to parse role value as GUID (if it's stored as role ID)
                if (Guid.TryParse(roleClaim.Value, out var roleId))
                {
                    _logger?.LogDebug("Role claim value is a GUID: {RoleId}", roleId);
                    roleIds.Add(roleId);
                }
                else
                {
                    _logger?.LogDebug("Role claim value is a name, looking up ID for: {RoleName}", roleClaim.Value);
                    // If it's stored as role name, try to get the role ID
                    var roleIdFromName = await GetRoleIdByNameAsync(roleClaim.Value);
                    if (roleIdFromName.HasValue)
                    {
                        _logger?.LogDebug("Found role ID {RoleId} for role name {RoleName}", roleIdFromName.Value, roleClaim.Value);
                        roleIds.Add(roleIdFromName.Value);
                    }
                    else
                    {
                        _logger?.LogWarning("Could not find role ID for role name: {RoleName}", roleClaim.Value);
                    }
                }
            }
            
            _logger?.LogInformation("Resolved {RoleCount} role IDs for user {UserName}: {RoleIds}", 
                roleIds.Count, user.Identity.Name, string.Join(", ", roleIds));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting user roles");
            // If there's any error getting roles, return empty list (safer default)
            return new List<Guid>();
        }

        return roleIds;
    }

    /// <summary>
    /// Gets a role ID by name from the Identity database
    /// </summary>
    private async Task<Guid?> GetRoleIdByNameAsync(string roleName)
    {
        try
        {
            // Get the Identity database context using reflection (similar to WorkflowDefinitionService)
            var piranhaIdentityAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Piranha.AspNetCore.Identity");
            
            if (piranhaIdentityAssembly == null)
            {
                return null;
            }

            // Get the IDb interface type
            var idbType = piranhaIdentityAssembly.GetType("Piranha.AspNetCore.Identity.IDb");
            if (idbType == null)
            {
                return null;
            }

            // Get the Identity DB from HttpContext services
            var identityDb = _httpContextAccessor.HttpContext?.RequestServices.GetService(idbType);
            if (identityDb == null)
            {
                return null;
            }

            // Get the Roles property
            var rolesProperty = idbType.GetProperty("Roles");
            if (rolesProperty == null)
            {
                return null;
            }

            var rolesDbSet = rolesProperty.GetValue(identityDb);
            if (rolesDbSet == null)
            {
                return null;
            }

            // Get the role type
            var roleType = piranhaIdentityAssembly.GetType("Piranha.AspNetCore.Identity.Data.Role");
            if (roleType == null)
            {
                return null;
            }

            // Use LINQ to find the role by name
            var whereMethod = typeof(Enumerable).GetMethods()
                .Where(m => m.Name == "Where" && m.GetParameters().Length == 2)
                .First().MakeGenericMethod(roleType);

            var nameProperty = roleType.GetProperty("Name");
            if (nameProperty == null)
            {
                return null;
            }

            // Create a predicate to find role by name
            var parameterExpression = System.Linq.Expressions.Expression.Parameter(roleType, "r");
            var propertyExpression = System.Linq.Expressions.Expression.Property(parameterExpression, nameProperty);
            var constantExpression = System.Linq.Expressions.Expression.Constant(roleName);
            var equalExpression = System.Linq.Expressions.Expression.Equal(propertyExpression, constantExpression);
            var lambdaExpression = System.Linq.Expressions.Expression.Lambda(equalExpression, parameterExpression);

            var filteredRoles = whereMethod.Invoke(null, new[] { rolesDbSet, lambdaExpression.Compile() });

            // Get first or default
            var firstOrDefaultMethod = typeof(Enumerable).GetMethods()
                .Where(m => m.Name == "FirstOrDefault" && m.GetParameters().Length == 1)
                .First().MakeGenericMethod(roleType);

            var role = firstOrDefaultMethod.Invoke(null, new[] { filteredRoles });

            if (role != null)
            {
                var idProperty = roleType.GetProperty("Id");
                if (idProperty != null)
                {
                    var id = idProperty.GetValue(role);
                    if (id is Guid guidId)
                    {
                        return guidId;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Return null if there's any error
        }

        return null;
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