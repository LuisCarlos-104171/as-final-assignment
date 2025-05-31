/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.EntityFrameworkCore;
using Piranha;
using Piranha.Models;
using Piranha.Services;
using Piranha.Repositories;

namespace MvcWeb.Models;

/// <summary>
/// Repository for dynamic article submissions that works with configurable workflows
/// instead of hardcoded status enums.
/// </summary>
public class DynamicArticleSubmissionRepository
{
    private readonly ArticleDbContext _context;
    private readonly IApi _api;
    private readonly IDynamicWorkflowService _workflowService;
    private readonly IWorkflowRepository _workflowRepository;

    public DynamicArticleSubmissionRepository(ArticleDbContext context, IApi api, IDynamicWorkflowService workflowService, IWorkflowRepository workflowRepository)
    {
        _context = context;
        _api = api;
        _workflowService = workflowService;
        _workflowRepository = workflowRepository;
    }

    /// <summary>
    /// Gets all article submissions with workflow information.
    /// </summary>
    /// <param name="userRoles">User roles for filtering content visibility</param>
    /// <param name="userId">User ID for ownership checks</param>
    /// <param name="workflowState">Optional state filter</param>
    /// <returns>Filtered article submissions</returns>
    public async Task<List<DynamicArticleSubmissionModel>> GetSubmissionsAsync(
        IEnumerable<string> userRoles = null, 
        string userId = null, 
        string workflowState = null)
    {
        var query = _context.Articles.AsQueryable();

        // Apply workflow state filter if specified
        if (!string.IsNullOrEmpty(workflowState))
        {
            query = query.Where(a => a.WorkflowState == workflowState);
        }

        var submissions = await query.ToListAsync();
        var results = new List<DynamicArticleSubmissionModel>();

        foreach (var submission in submissions)
        {
            // Get workflow information
            if (!submission.WorkflowId.HasValue) continue; // Skip articles without workflow
            var workflow = await _workflowRepository.GetByIdAsync(submission.WorkflowId.Value);
            if (workflow == null) continue;

            // Check if user can view this content
            if (userRoles != null && userId != null)
            {
                var canView = await _workflowService.CanViewContentAsync(
                    workflow.Id, submission.WorkflowState ?? "draft", userRoles, submission.AuthorId ?? "unknown", userId);
                
                if (!canView) continue;
            }

            var model = await MapToModelAsync(submission, workflow, userRoles);
            results.Add(model);
        }

        return results.OrderByDescending(r => r.Created).ToList();
    }

    /// <summary>
    /// Gets an article submission by ID with workflow information.
    /// </summary>
    /// <param name="id">Article ID</param>
    /// <param name="userRoles">User roles for permission checks</param>
    /// <param name="userId">User ID for ownership checks</param>
    /// <returns>Article submission with workflow data</returns>
    public async Task<DynamicArticleSubmissionModel> GetByIdAsync(Guid id, IEnumerable<string> userRoles = null, string userId = null)
    {
        var submission = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (submission == null) return null;

        if (!submission.WorkflowId.HasValue) return null; // No workflow assigned
        var workflow = await _workflowRepository.GetByIdAsync(submission.WorkflowId.Value);
        if (workflow == null) return null;

        // Check viewing permissions
        if (userRoles != null && userId != null)
        {
            var canView = await _workflowService.CanViewContentAsync(
                workflow.Id, submission.WorkflowState ?? "draft", userRoles, submission.AuthorId ?? "unknown", userId);
            
            if (!canView) return null;
        }

        return await MapToModelAsync(submission, workflow, userRoles);
    }

    /// <summary>
    /// Creates a new article submission with the default workflow.
    /// </summary>
    /// <param name="model">Article submission data</param>
    /// <returns>Created article with workflow information</returns>
    public async Task<DynamicArticleSubmissionModel> CreateAsync(DynamicArticleSubmissionModel model)
    {
        // Get default workflow for articles
        var workflow = await _workflowService.GetDefaultWorkflowAsync("article");
        if (workflow == null)
        {
            // Create default workflow if it doesn't exist
            workflow = await _workflowService.CreateDefaultWorkflowAsync("article", "Article Approval Workflow");
        }

        var entity = new ArticleEntity
        {
            Id = Guid.NewGuid(),
            Title = model.Title,
            Content = model.Content,
            Summary = model.Summary ?? "",
            Author = model.Author,
            AuthorId = model.AuthorId ?? "system",
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            Status = ArticleStatus.Draft, // Set legacy status for compatibility
            Email = model.Author + "@example.com", // Required field
            BlogId = Guid.NewGuid(), // Required field - would normally be set properly
            WorkflowId = workflow.Id,
            WorkflowState = workflow.InitialState
        };

        _context.Articles.Add(entity);
        await _context.SaveChangesAsync();

        // Log workflow history
        await LogWorkflowTransitionAsync(entity.Id, null, workflow.InitialState, "Created", model.AuthorId, "Article created");

        return await MapToModelAsync(entity, workflow, null);
    }

    /// <summary>
    /// Executes a workflow transition on an article.
    /// </summary>
    /// <param name="request">Transition request</param>
    /// <param name="userRoles">User roles for permission checks</param>
    /// <param name="userId">User performing the transition</param>
    /// <param name="userName">Name of user performing the transition</param>
    /// <returns>Updated article model</returns>
    public async Task<DynamicArticleSubmissionModel> ExecuteTransitionAsync(
        WorkflowTransitionRequest request, 
        IEnumerable<string> userRoles, 
        string userId, 
        string userName)
    {
        var submission = await _context.Articles.FirstOrDefaultAsync(a => a.Id == request.ArticleId);
        if (submission == null)
            throw new ArgumentException("Article not found");

        // Check if user can execute this transition
        var canExecute = await _workflowService.CanExecuteTransitionAsync(
            request.TransitionId, userRoles, request.ArticleId, userId);
        
        if (!canExecute)
            throw new UnauthorizedAccessException("You do not have permission to execute this transition");

        // Get transition details
        var transition = await _workflowRepository.GetTransitionByIdAsync(request.TransitionId);
        if (transition == null)
            throw new ArgumentException("Transition not found");

        // Validate current state matches transition
        if (transition.FromStateKey != submission.WorkflowState)
            throw new InvalidOperationException($"Cannot execute transition from current state: {submission.WorkflowState}");

        var currentState = submission.WorkflowState;
        
        // Update article state
        submission.WorkflowState = transition.ToStateKey;
        submission.LastModified = DateTime.Now;

        // Update reviewer/approver information based on transition
        await UpdateSubmissionForTransitionAsync(submission, transition, userId, userName, request.Comments);

        await _context.SaveChangesAsync();

        // Log workflow history
        await LogWorkflowTransitionAsync(
            submission.Id, currentState, transition.ToStateKey, 
            transition.Name, userId, request.Comments ?? userName);

        // Get updated model
        var workflow = await _workflowRepository.GetByIdAsync(submission.WorkflowId.Value);
        return await MapToModelAsync(submission, workflow, userRoles);
    }

    /// <summary>
    /// Gets workflow analytics for the articles.
    /// </summary>
    /// <param name="workflowId">Workflow ID</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>Analytics data</returns>
    public async Task<WorkflowAnalytics> GetAnalyticsAsync(Guid workflowId, DateTime fromDate, DateTime toDate)
    {
        return await _workflowService.GetWorkflowAnalyticsAsync(workflowId, fromDate, toDate);
    }

    private async Task<DynamicArticleSubmissionModel> MapToModelAsync(
        ArticleEntity entity, 
        WorkflowDefinition workflow, 
        IEnumerable<string> userRoles)
    {
        var currentState = workflow.States.FirstOrDefault(s => s.Key == entity.WorkflowState);
        
        var model = new DynamicArticleSubmissionModel
        {
            Id = entity.Id,
            Title = entity.Title,
            Content = entity.Content,
            Summary = entity.Summary,
            Author = entity.Author,
            AuthorId = entity.AuthorId,
            Created = entity.Created,
            LastModified = entity.LastModified,
            WorkflowId = entity.WorkflowId ?? Guid.Empty,
            WorkflowState = entity.WorkflowState ?? "draft",
            Workflow = workflow,
            CurrentState = currentState,
            ReviewedById = entity.ReviewedById,
            ReviewedBy = entity.ReviewedBy,
            ReviewedAt = entity.ReviewedAt,
            ReviewComments = entity.ReviewComments,
            ApprovedById = entity.ApprovedById,
            ApprovedBy = entity.ApprovedBy,
            ApprovedAt = entity.ApprovedAt,
            ApprovalComments = entity.ApprovalComments,
            PostId = entity.PostId,
            Published = entity.Published
        };

        // Get available transitions for current user
        if (userRoles != null)
        {
            var transitions = await _workflowService.GetAvailableTransitionsAsync(
                workflow.Id, entity.WorkflowState ?? "draft", userRoles, entity.Id, entity.AuthorId ?? "unknown");
            model.AvailableTransitions = transitions.ToList();
        }

        // Get workflow history
        model.WorkflowHistory = await GetWorkflowHistoryAsync(entity.Id);

        return model;
    }

    private async Task UpdateSubmissionForTransitionAsync(
        ArticleEntity submission, 
        WorkflowTransition transition, 
        string userId, 
        string userName, 
        string comments)
    {
        var targetState = transition.ToStateKey.ToLower();

        switch (targetState)
        {
            case "in_review":
            case "rejected":
                submission.ReviewedById = userId;
                submission.ReviewedBy = userName;
                submission.ReviewedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(comments))
                    submission.ReviewComments = comments;
                break;

            case "approved":
            case "published":
                submission.ApprovedById = userId;
                submission.ApprovedBy = userName;
                submission.ApprovedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(comments))
                    submission.ApprovalComments = comments;

                // Create Piranha post when published
                if (targetState == "published" && !submission.PostId.HasValue)
                {
                    await CreatePiranhaPostAsync(submission);
                }
                break;
        }
    }

    private async Task CreatePiranhaPostAsync(ArticleEntity submission)
    {
        var post = await StandardPost.CreateAsync(_api);
        post.Title = submission.Title;
        post.MetaKeywords = submission.Summary;
        post.Excerpt = submission.Summary;
        // Note: StandardPost content would be set through blocks or regions
        // For now, we'll just set the basic properties
        post.Published = DateTime.Now;

        await _api.Posts.SaveAsync(post);
        
        submission.PostId = post.Id;
        submission.Published = DateTime.Now;
    }

    private async Task LogWorkflowTransitionAsync(
        Guid articleId, 
        string fromState, 
        string toState, 
        string transitionName, 
        string userId, 
        string comments)
    {
        var historyEntry = new WorkflowHistoryEntry
        {
            Id = Guid.NewGuid(),
            ArticleId = articleId,
            FromState = fromState,
            ToState = toState,
            TransitionName = transitionName,
            UserId = userId,
            Comments = comments,
            Timestamp = DateTime.Now
        };

        // In a real implementation, this would be saved to a WorkflowHistory table
        // For now, we'll just log it (this could be extended to save to database)
    }

    private async Task<List<WorkflowHistoryEntry>> GetWorkflowHistoryAsync(Guid articleId)
    {
        // In a real implementation, this would query a WorkflowHistory table
        // For now, return an empty list
        return new List<WorkflowHistoryEntry>();
    }
}