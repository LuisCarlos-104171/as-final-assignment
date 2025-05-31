/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Piranha.Models;
using Piranha.Repositories;

namespace Piranha.Services;

/// <summary>
/// Implementation of dynamic workflow service that provides role-based workflow management
/// without hardcoded permissions or workflow logic.
/// </summary>
public class DynamicWorkflowService : IDynamicWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;

    public DynamicWorkflowService(IWorkflowRepository workflowRepository)
    {
        _workflowRepository = workflowRepository;
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetWorkflowsForContentTypeAsync(string contentType)
    {
        var workflows = await _workflowRepository.GetAllAsync();
        return workflows.Where(w => w.IsActive && w.GetContentTypes().Contains(contentType));
    }

    public async Task<WorkflowDefinition> GetDefaultWorkflowAsync(string contentType)
    {
        var workflows = await GetWorkflowsForContentTypeAsync(contentType);
        return workflows.FirstOrDefault(w => w.IsDefault) ?? workflows.FirstOrDefault();
    }

    public async Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(Guid workflowId, string currentState,
        IEnumerable<string> userRoles, Guid contentId, string userId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null) return Enumerable.Empty<WorkflowTransition>();

        var effectiveRoles = await GetEffectiveRolesAsync(workflowId, userRoles);
        var availableTransitions = new List<WorkflowTransition>();

        foreach (var transition in workflow.Transitions.Where(t => t.FromStateKey == currentState))
        {
            if (await CanExecuteTransitionAsync(transition.Id, userRoles, contentId, userId))
            {
                availableTransitions.Add(transition);
            }
        }

        return availableTransitions.OrderBy(t => t.SortOrder);
    }

    public async Task<bool> CanExecuteTransitionAsync(Guid transitionId, IEnumerable<string> userRoles, 
        Guid contentId, string userId)
    {
        var transition = await _workflowRepository.GetTransitionByIdAsync(transitionId);
        if (transition == null) return false;

        var workflow = await _workflowRepository.GetByIdAsync(transition.WorkflowDefinitionId);
        if (workflow == null) return false;
        
        var effectiveRoles = await GetEffectiveRolesAsync(workflow.Id, userRoles);

        // Primary check: Use role-based permissions (the new dynamic system)
        if (transition.RolePermissions.Any())
        {
            foreach (var role in effectiveRoles)
            {
                var permission = transition.RolePermissions.FirstOrDefault(rp => rp.WorkflowRoleId == role.Id);
                if (permission?.CanExecute == true)
                {
                    // Check additional conditions if specified
                    if (await EvaluateConditionsAsync(permission, contentId, userId))
                    {
                        return true;
                    }
                }
            }
            // If role-based permissions exist but none match, deny access
            return false;
        }

        // Fallback: Legacy permission check (for backward compatibility with old workflows)
        if (!string.IsNullOrEmpty(transition.RequiredPermission))
        {
            // This is the old hardcoded permission system - should be avoided
            return userRoles.Contains(transition.RequiredPermission);
        }

        // If no permissions are defined at all, deny access for security
        return false;
    }

    public async Task<bool> CanViewContentAsync(Guid workflowId, string contentState, IEnumerable<string> userRoles,
        string contentOwnerId, string userId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null) return false;

        var effectiveRoles = await GetEffectiveRolesAsync(workflowId, userRoles);

        // Content owner can always view their own content
        if (contentOwnerId == userId) return true;

        // Check if any role allows viewing all content
        if (effectiveRoles.Any(r => r.CanViewAll)) return true;

        // Check if the content state allows viewing for any of the user's roles
        var state = workflow.States.FirstOrDefault(s => s.Key == contentState);
        if (state?.IsPublished == true) return true; // Published content is viewable by everyone

        // Check role-specific state access
        foreach (var role in effectiveRoles)
        {
            var allowedStates = role.GetAllowedFromStates();
            if (allowedStates.Length == 0 || allowedStates.Contains(contentState))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IEnumerable<WorkflowRole>> GetEffectiveRolesAsync(Guid workflowId, IEnumerable<string> userRoles)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null) return Enumerable.Empty<WorkflowRole>();

        var effectiveRoles = new List<WorkflowRole>();
        var userRoleSet = userRoles.ToHashSet();

        // Get direct role matches
        var directRoles = workflow.Roles.Where(r => userRoleSet.Contains(r.RoleKey)).ToList();
        effectiveRoles.AddRange(directRoles);

        // Apply role hierarchy - users with higher priority roles inherit permissions from lower priority roles
        if (directRoles.Any())
        {
            var maxPriority = directRoles.Max(r => r.Priority);
            var inheritedRoles = workflow.Roles.Where(r => r.Priority < maxPriority && !effectiveRoles.Contains(r));
            effectiveRoles.AddRange(inheritedRoles);
        }

        return effectiveRoles.OrderByDescending(r => r.Priority);
    }

    public async Task<WorkflowDefinition> CreateDefaultWorkflowAsync(string contentType, string workflowName)
    {
        var workflow = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            Name = workflowName,
            Description = $"Default workflow for {contentType}",
            ContentTypes = contentType,
            IsDefault = true,
            IsActive = true,
            InitialState = "draft"
        };

        // Create default states
        workflow.States = new List<WorkflowState>
        {
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "draft",
                Name = "Draft",
                Description = "Content is being written",
                Color = "#6c757d",
                Icon = "fas fa-edit",
                SortOrder = 1,
                IsInitial = true
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "in_review",
                Name = "In Review",
                Description = "Content is under review",
                Color = "#0dcaf0",
                Icon = "fas fa-search",
                SortOrder = 2
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "approved",
                Name = "Approved",
                Description = "Content has been approved",
                Color = "#198754",
                Icon = "fas fa-check-circle",
                SortOrder = 3
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "published",
                Name = "Published",
                Description = "Content is live",
                Color = "#198754",
                Icon = "fas fa-globe",
                SortOrder = 4,
                IsPublished = true,
                IsFinal = true
            },
            new WorkflowState
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                Key = "rejected",
                Name = "Rejected",
                Description = "Content has been rejected",
                Color = "#dc3545",
                Icon = "fas fa-times-circle",
                SortOrder = 5
            }
        };

        // Create default roles
        workflow.Roles = new List<WorkflowRole>
        {
            new WorkflowRole
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                RoleKey = "Writer",
                DisplayName = "Content Writer",
                Description = "Can create and edit draft content",
                Priority = 1,
                CanCreate = true,
                CanEdit = true,
                CanDelete = false,
                CanViewAll = false,
                AllowedFromStates = "draft",
                AllowedToStates = "in_review"
            },
            new WorkflowRole
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                RoleKey = "Editor",
                DisplayName = "Content Editor",
                Description = "Can review and approve content",
                Priority = 2,
                CanCreate = true,
                CanEdit = true,
                CanDelete = false,
                CanViewAll = true,
                AllowedFromStates = "in_review,draft",
                AllowedToStates = "approved,rejected,draft"
            },
            new WorkflowRole
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                RoleKey = "Approver",
                DisplayName = "Content Approver",
                Description = "Can publish approved content",
                Priority = 3,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true,
                CanViewAll = true,
                AllowedFromStates = "approved,published",
                AllowedToStates = "published,rejected"
            },
            new WorkflowRole
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                RoleKey = "SysAdmin",
                DisplayName = "System Administrator",
                Description = "Full workflow access",
                Priority = 10,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true,
                CanViewAll = true
            }
        };

        // Create default transitions using role-based permissions instead of hardcoded permission strings
        var writerRole = workflow.Roles.First(r => r.RoleKey == "Writer");
        var editorRole = workflow.Roles.First(r => r.RoleKey == "Editor");
        var approverRole = workflow.Roles.First(r => r.RoleKey == "Approver");

        workflow.Transitions = new List<WorkflowTransition>
        {
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "draft",
                ToStateKey = "in_review",
                Name = "Submit for Review",
                Description = "Submit content for editorial review",
                CssClass = "btn-primary",
                Icon = "fas fa-paper-plane",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                RequiredPermission = writerRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = writerRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "in_review",
                ToStateKey = "approved",
                Name = "Approve",
                Description = "Approve content for publication",
                CssClass = "btn-success",
                Icon = "fas fa-check",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                RequiredPermission = editorRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = editorRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "in_review",
                ToStateKey = "rejected",
                Name = "Reject",
                Description = "Reject content and return to draft",
                CssClass = "btn-danger",
                Icon = "fas fa-times",
                SortOrder = 2,
                RequiresComment = true,
                SendNotification = true,
                RequiredPermission = editorRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = editorRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "approved",
                ToStateKey = "published",
                Name = "Publish",
                Description = "Publish content to the website",
                CssClass = "btn-success",
                Icon = "fas fa-globe",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = true,
                RequiredPermission = approverRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = approverRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "rejected",
                ToStateKey = "draft",
                Name = "Revise",
                Description = "Return to draft for revision",
                CssClass = "btn-secondary",
                Icon = "fas fa-edit",
                SortOrder = 1,
                RequiresComment = false,
                SendNotification = false,
                RequiredPermission = writerRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = writerRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            // Add additional transitions to support role hierarchy
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "draft",
                ToStateKey = "approved",
                Name = "Direct Approve",
                Description = "Directly approve content (Editor+ only)",
                CssClass = "btn-warning",
                Icon = "fas fa-check-double",
                SortOrder = 2,
                RequiresComment = false,
                SendNotification = true,
                RequiredPermission = editorRole.RoleKey, // Primary role for direct approval
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = editorRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    },
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = approverRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            },
            new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = workflow.Id,
                FromStateKey = "published",
                ToStateKey = "draft",
                Name = "Unpublish",
                Description = "Unpublish content and return to draft",
                CssClass = "btn-outline-danger",
                Icon = "fas fa-undo",
                SortOrder = 1,
                RequiresComment = true,
                SendNotification = true,
                RequiredPermission = approverRole.RoleKey, // Use role key instead of hardcoded permission
                RolePermissions = new List<WorkflowRolePermission>
                {
                    new WorkflowRolePermission
                    {
                        Id = Guid.NewGuid(),
                        WorkflowRoleId = approverRole.Id,
                        CanExecute = true,
                        RequiresApproval = false
                    }
                }
            }
        };

        await _workflowRepository.SaveAsync(workflow);
        return workflow;
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowAsync(WorkflowDefinition workflow)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        // Validate basic structure
        if (string.IsNullOrEmpty(workflow.Name))
            result.Errors.Add("Workflow name is required");

        if (string.IsNullOrEmpty(workflow.ContentTypes))
            result.Errors.Add("At least one content type must be specified");

        if (string.IsNullOrEmpty(workflow.InitialState))
            result.Errors.Add("Initial state is required");

        // Validate states
        if (!workflow.States.Any())
        {
            result.Errors.Add("At least one workflow state is required");
        }
        else
        {
            var initialState = workflow.States.FirstOrDefault(s => s.Key == workflow.InitialState);
            if (initialState == null)
                result.Errors.Add($"Initial state '{workflow.InitialState}' not found in workflow states");

            var duplicateKeys = workflow.States.GroupBy(s => s.Key).Where(g => g.Count() > 1);
            foreach (var duplicate in duplicateKeys)
                result.Errors.Add($"Duplicate state key: {duplicate.Key}");
        }

        // Validate transitions
        foreach (var transition in workflow.Transitions)
        {
            if (!workflow.States.Any(s => s.Key == transition.FromStateKey))
                result.Errors.Add($"Transition '{transition.Name}' references unknown from state: {transition.FromStateKey}");

            if (!workflow.States.Any(s => s.Key == transition.ToStateKey))
                result.Errors.Add($"Transition '{transition.Name}' references unknown to state: {transition.ToStateKey}");
        }

        // Validate roles
        var duplicateRoleKeys = workflow.Roles.GroupBy(r => r.RoleKey).Where(g => g.Count() > 1);
        foreach (var duplicate in duplicateRoleKeys)
            result.Errors.Add($"Duplicate role key: {duplicate.Key}");

        result.IsValid = !result.Errors.Any();
        return result;
    }

    public async Task<WorkflowAnalytics> GetWorkflowAnalyticsAsync(Guid workflowId, DateTime fromDate, DateTime toDate)
    {
        // This would be implemented with actual database queries to content tracking tables
        // For now, return a placeholder implementation
        return new WorkflowAnalytics
        {
            StateDistribution = new Dictionary<string, int>
            {
                { "draft", 25 },
                { "in_review", 15 },
                { "approved", 5 },
                { "published", 100 },
                { "rejected", 8 }
            },
            AverageStateTime = new Dictionary<string, TimeSpan>
            {
                { "draft", TimeSpan.FromDays(2.5) },
                { "in_review", TimeSpan.FromDays(1.2) },
                { "approved", TimeSpan.FromHours(4) }
            },
            TransitionCounts = new Dictionary<string, int>
            {
                { "Submit for Review", 40 },
                { "Approve", 32 },
                { "Reject", 8 },
                { "Publish", 30 }
            }
        };
    }

    private async Task<bool> EvaluateConditionsAsync(WorkflowRolePermission permission, Guid contentId, string userId)
    {
        // This method would evaluate any additional conditions specified in the permission
        // For now, return true if no conditions are specified
        if (string.IsNullOrEmpty(permission.Conditions))
            return true;

        // TODO: Implement JSON-based condition evaluation
        // Example conditions might include:
        // - Minimum review time
        // - Required number of reviewers
        // - Content metadata requirements
        // - Time-based constraints

        return true;
    }
}