using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Extend.Blocks;
using Piranha.Extend.Fields;
using Piranha.Models;
using Piranha.Services;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MvcWeb.Models
{
    /// <summary>
    /// Repository for handling article submissions
    /// </summary>
    public class ArticleSubmissionRepository
    {
        private readonly IApi _api;
        private readonly ILogger<ArticleSubmissionRepository> _logger;
        private readonly UserManager<User> _userManager;
        private readonly ArticleDbContext _dbContext;
        private readonly IDynamicWorkflowService _workflowService;
        
        private static readonly ActivitySource ActivitySource = new("MvcWeb.ArticleSubmissionRepository");
        private static readonly Meter Meter = new("MvcWeb.ArticleSubmissionRepository");
        private static readonly Counter<int> RepositoryOperationsCounter = Meter.CreateCounter<int>("repository_operations_total", "Total number of repository operations");
        private static readonly Histogram<double> DatabaseQueryDuration = Meter.CreateHistogram<double>("database_query_duration_ms", "Duration of database queries in milliseconds");
        private static readonly Counter<int> PostCreationsCounter = Meter.CreateCounter<int>("post_creations_total", "Total number of post creations from submissions");

        /// <summary>
        /// Constructor
        /// </summary>
        public ArticleSubmissionRepository(IApi api, 
            ILogger<ArticleSubmissionRepository> logger, 
            UserManager<User> userManager,
            ArticleDbContext dbContext,
            IDynamicWorkflowService workflowService)
        {
            _api = api;
            _logger = logger;
            _userManager = userManager;
            _dbContext = dbContext;
            _workflowService = workflowService;
        }

        /// <summary>
        /// Adds a new article submission
        /// </summary>
        public async Task<SubmittedArticle> AddSubmissionAsync(ArticleSubmissionModel model, Guid blogId)
        {
            using var activity = ActivitySource.StartActivity("ArticleSubmissionRepository.AddSubmission");
            var stopwatch = Stopwatch.StartNew();
            
            activity?.SetTag("blogId", blogId.ToString());
            activity?.SetTag("hasImage", model.PrimaryImage != null);
            activity?.SetTag("author", model.Author);
            
            try
            {
                // Get default workflow for articles
                var workflow = await _workflowService.GetDefaultWorkflowAsync("post");
                if (workflow == null)
                {
                    // Check if any workflows exist for articles before creating a new one
                    var existingWorkflows = await _workflowService.GetWorkflowsForContentTypeAsync("post");
                    if (existingWorkflows.Any())
                    {
                        // Check if the existing workflow has roles - if not, it's incomplete
                        var firstWorkflow = existingWorkflows.First();
                        var roles = await _workflowService.GetEffectiveRolesAsync(firstWorkflow.Id, new[] { "SysAdmin" });
                        
                        if (roles.Any())
                        {
                            // Use the existing complete workflow
                            workflow = firstWorkflow;
                            _logger.LogWarning("No default workflow found for posts, using existing workflow: {WorkflowName} (ID: {WorkflowId})", 
                                workflow.Name, workflow.Id);
                        }
                        else
                        {
                            // Existing workflow is incomplete, create a new one
                            _logger.LogWarning("Found incomplete workflow for posts (no roles), creating new complete workflow");
                            workflow = await _workflowService.CreateDefaultWorkflowAsync("post", "Article Approval Workflow");
                            _logger.LogInformation("Created new default workflow for articles: {WorkflowName} (ID: {WorkflowId})", 
                                workflow.Name, workflow.Id);
                        }
                    }
                    else
                    {
                        // Only create a new workflow if none exist at all
                        workflow = await _workflowService.CreateDefaultWorkflowAsync("post", "Article Approval Workflow");
                        _logger.LogInformation("Created new default workflow for articles: {WorkflowName} (ID: {WorkflowId})", 
                            workflow.Name, workflow.Id);
                    }
                }

                // Create entity from model
                var entity = new ArticleEntity
                {
                    Id = Guid.NewGuid(),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now,
                    Status = ArticleStatus.Draft, // Keep legacy status for backward compatibility
                    Title = model.Title,
                    Category = model.Category,
                    Tags = model.Tags,
                    Excerpt = model.Excerpt,
                    Content = model.Content,
                    Email = model.Email,
                    Author = model.Author,
                    NotifyOnComment = model.NotifyOnComment,
                    BlogId = blogId,
                    // Set dynamic workflow properties
                    WorkflowId = workflow.Id,
                    WorkflowState = workflow.InitialState // Use workflow's initial state instead of hardcoded draft
                };

                // If a primary image was provided, save it to media
                if (model.PrimaryImage != null)
                {
                    using var stream = model.PrimaryImage.OpenReadStream();
                    // Save media and get the media content
                    var mediaContent = new Piranha.Models.StreamMediaContent
                    {
                        Filename = model.PrimaryImage.FileName,
                        Data = stream
                    };
                    await _api.Media.SaveAsync(mediaContent);
                    
                    // Store the media ID if it was created successfully
                    if (mediaContent.Id.HasValue)
                    {
                        entity.PrimaryImageId = mediaContent.Id.Value;
                        activity?.SetTag("mediaId", mediaContent.Id.Value.ToString());
                    }
                }

                // Save to database
                _dbContext.Articles.Add(entity);
                await _dbContext.SaveChangesAsync();

                stopwatch.Stop();
                DatabaseQueryDuration.Record(stopwatch.ElapsedMilliseconds);
                RepositoryOperationsCounter.Add(1, 
                    new KeyValuePair<string, object?>("operation", "add_submission"), 
                    new KeyValuePair<string, object?>("status", "success"));
                
                activity?.SetTag("submissionId", entity.Id.ToString());
                activity?.SetTag("outcome", "success");

                // Log workflow transition
                await LogWorkflowTransitionAsync(entity.Id, null, workflow.InitialState, "Article created", "system", $"Article '{entity.Title}' created by {entity.Author}");

                // Convert to SubmittedArticle for API consistency
                return ConvertToSubmittedArticle(entity);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RepositoryOperationsCounter.Add(1, 
                    new KeyValuePair<string, object?>("operation", "add_submission"), 
                    new KeyValuePair<string, object?>("status", "error"));
                
                activity?.SetTag("outcome", "error");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _logger.LogError(ex, "Failed to add submission for author {Author}", model.Author);
                throw;
            }
        }
        
        /// <summary>
        /// Converts a database entity to a submitted article
        /// </summary>
        private SubmittedArticle ConvertToSubmittedArticle(ArticleEntity entity)
        {
            return new SubmittedArticle
            {
                Id = entity.Id,
                Created = entity.Created,
                LastModified = entity.LastModified,
                Published = entity.Published,
                // Use workflow state to determine status, fallback to entity status for legacy compatibility
                Status = !string.IsNullOrEmpty(entity.WorkflowState) 
                    ? MapWorkflowStateToStatus(entity.WorkflowState) 
                    : entity.Status,
                Submission = new ArticleSubmissionModel
                {
                    Title = entity.Title,
                    Category = entity.Category,
                    Tags = entity.Tags,
                    Excerpt = entity.Excerpt,
                    Content = entity.Content,
                    Email = entity.Email,
                    Author = entity.Author,
                    NotifyOnComment = entity.NotifyOnComment
                },
                EditorialFeedback = entity.EditorialFeedback,
                ReviewedById = entity.ReviewedById,
                ApprovedById = entity.ApprovedById,
                BlogId = entity.BlogId,
                PostId = entity.PostId,
                // Include workflow properties
                WorkflowId = entity.WorkflowId,
                WorkflowState = entity.WorkflowState
            };
        }

        /// <summary>
        /// Gets a list of all submissions, filtered by workflow state and/or user
        /// </summary>
        public async Task<List<SubmittedArticle>> GetSubmissionsAsync(string workflowState = null, string userId = null)
        {
            // Start with base query
            IQueryable<ArticleEntity> query = _dbContext.Articles;
            
            // Apply workflow state filter if provided
            if (!string.IsNullOrEmpty(workflowState))
            {
                query = query.Where(a => a.WorkflowState == workflowState);
            }
            
            // Apply user filter if provided
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(a => 
                    (a.WorkflowState == "draft" && a.AuthorId == userId) || 
                    (a.WorkflowState == "in_review" && a.ReviewedById == userId) ||
                    (a.WorkflowState == "approved" && a.ApprovedById == userId) ||
                    a.AuthorId == userId); // User can always see their own articles
            }
            
            // Order by last modified
            query = query.OrderByDescending(a => a.LastModified);
            
            // Execute query and convert to SubmittedArticle objects
            var entities = await query.ToListAsync();
            return entities.Select(ConvertToSubmittedArticle).ToList();
        }

        /// <summary>
        /// Legacy method for backward compatibility - converts status to workflow state
        /// </summary>
        [Obsolete("Use GetSubmissionsByWorkflowStateAsync with workflowState parameter instead")]
        public async Task<List<SubmittedArticle>> GetSubmissionsByStatusAsync(ArticleStatus? status = null, string userId = null)
        {
            var workflowState = status.HasValue ? MapStatusToWorkflowState(status.Value) : null;
            return await GetSubmissionsAsync(workflowState, userId);
        }

        /// <summary>
        /// Gets a specific submission by id
        /// </summary>
        public async Task<SubmittedArticle> GetSubmissionByIdAsync(Guid id)
        {
            var entity = await _dbContext.Articles.FindAsync(id);
            if (entity == null)
            {
                return null;
            }
            
            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Updates the workflow state of a submission using dynamic workflow transitions
        /// </summary>
        public async Task<SubmittedArticle> UpdateSubmissionWorkflowStateAsync(
            Guid id, 
            string newWorkflowState, 
            string userId, 
            string feedback = null)
        {
            var entity = await _dbContext.Articles.FindAsync(id);
            if (entity == null)
            {
                return null;
            }

            // Update workflow state
            entity.WorkflowState = newWorkflowState;
            entity.LastModified = DateTime.Now;
            entity.EditorialFeedback = feedback;

            // Map workflow state to legacy status for backward compatibility
            entity.Status = MapWorkflowStateToStatus(newWorkflowState);

            // Update reviewer/approver information based on workflow state
            await UpdateEntityForWorkflowStateAsync(entity, newWorkflowState, userId, feedback);

            // Save changes to database
            await _dbContext.SaveChangesAsync();

            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Legacy method for backward compatibility - redirects to workflow-based updates
        /// </summary>
        [Obsolete("Use UpdateSubmissionWorkflowStateAsync instead")]
        public async Task<SubmittedArticle> UpdateSubmissionStatusAsync(
            Guid id, 
            ArticleStatus status, 
            string userId, 
            string feedback = null)
        {
            // Convert legacy status to workflow state
            var workflowState = MapStatusToWorkflowState(status);
            return await UpdateSubmissionWorkflowStateAsync(id, workflowState, userId, feedback);
        }

        /// <summary>
        /// Creates a published post from a submitted article
        /// </summary>
        private async Task CreatePostFromSubmissionAsync(SubmittedArticle article)
        {
            using var activity = ActivitySource.StartActivity("ArticleSubmissionRepository.CreatePost");
            var stopwatch = Stopwatch.StartNew();
            
            activity?.SetTag("articleId", article.Id.ToString());
            activity?.SetTag("blogId", article.BlogId.ToString());
            activity?.SetTag("title", article.Submission.Title);
            
            try
            {
                // Start by creating a new post
                var post = await StandardPost.CreateAsync(_api);

                // Set the basic fields
                post.BlogId = article.BlogId;
                post.Title = article.Submission.Title;
                
                // Excerpt might be optional in our form but required in Piranha
                post.Excerpt = !string.IsNullOrWhiteSpace(article.Submission.Excerpt) 
                    ? article.Submission.Excerpt 
                    : article.Submission.Title; // Use title as fallback
                
                // Category is required in Piranha, so set a default if none provided
                if (string.IsNullOrWhiteSpace(article.Submission.Category))
                {
                    post.Category = "General";
                }
                else
                {
                    post.Category = article.Submission.Category;
                }
                
                activity?.SetTag("category", post.Category);
                post.Published = DateTime.Now; // Ensure the post is published immediately

                // Process tags
                var tagCount = 0;
                if (!string.IsNullOrWhiteSpace(article.Submission.Tags))
                {
                    var tags = article.Submission.Tags
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t));
                    
                    foreach (var tag in tags)
                    {
                        post.Tags.Add(tag);
                        tagCount++;
                    }
                }
                activity?.SetTag("tagCount", tagCount);

                // Create the content block
                var htmlBlock = new HtmlBlock
                {
                    Body = new HtmlField
                    {
                        Value = article.Submission.Content
                    }
                };

                post.Blocks.Add(htmlBlock);

                // Set metadata
                post.MetaTitle = article.Submission.Title;
                post.MetaDescription = article.Submission.Excerpt;

                try
                {
                    // Save the post
                    await _api.Posts.SaveAsync(post);

                    // Update the article with the post id
                    article.PostId = post.Id;
                    
                    stopwatch.Stop();
                    PostCreationsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "success"), 
                        new KeyValuePair<string, object?>("category", post.Category));
                    
                    activity?.SetTag("postId", post.Id.ToString());
                    activity?.SetTag("outcome", "success");
                    
                    _logger.LogInformation("Successfully published article {ArticleId} as post {PostId}", 
                        article.Id, post.Id);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    PostCreationsCounter.Add(1, 
                        new KeyValuePair<string, object?>("status", "save_error"), 
                        new KeyValuePair<string, object?>("category", post.Category));
                    
                    activity?.SetTag("outcome", "save_error");
                    activity?.SetTag("error.message", ex.Message);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    
                    _logger.LogError(ex, "Failed to save post for article {ArticleId}. Title: {Title}, Category: {Category}", 
                        article.Id, post.Title, post.Category);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PostCreationsCounter.Add(1, new KeyValuePair<string, object?>("status", "error"));
                
                activity?.SetTag("outcome", "error");
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _logger.LogError(ex, "Error creating post from article submission {ArticleId}", article.Id);
                throw;
            }
        }

        /// <summary>
        /// Updates entity properties based on workflow state
        /// </summary>
        private async Task UpdateEntityForWorkflowStateAsync(ArticleEntity entity, string workflowState, string userId, string feedback)
        {
            switch (workflowState.ToLower())
            {
                case "in_review":
                    entity.ReviewedById = userId;
                    if (!string.IsNullOrEmpty(feedback))
                        entity.EditorialFeedback = feedback;
                    break;

                case "approved":
                    entity.ApprovedById = userId;
                    if (!string.IsNullOrEmpty(feedback))
                        entity.EditorialFeedback = feedback;
                    break;

                case "published":
                    entity.ApprovedById = userId;
                    entity.Published = DateTime.Now;
                    if (!string.IsNullOrEmpty(feedback))
                        entity.EditorialFeedback = feedback;
                    
                    // Create an actual post in Piranha when published
                    if (!entity.PostId.HasValue)
                    {
                        var article = ConvertToSubmittedArticle(entity);
                        await CreatePostFromSubmissionAsync(article);
                        entity.PostId = article.PostId;
                    }
                    break;

                case "rejected":
                    entity.ReviewedById = userId;
                    if (!string.IsNullOrEmpty(feedback))
                        entity.EditorialFeedback = feedback;
                    break;
            }
        }

        /// <summary>
        /// Maps workflow state to legacy ArticleStatus for backward compatibility
        /// </summary>
        private static ArticleStatus MapWorkflowStateToStatus(string workflowState)
        {
            return workflowState?.ToLower() switch
            {
                "draft" => ArticleStatus.Draft,
                "in_review" => ArticleStatus.InReview,
                "rejected" => ArticleStatus.Rejected,
                "approved" => ArticleStatus.Approved,
                "published" => ArticleStatus.Published,
                "archived" => ArticleStatus.Archived,
                _ => ArticleStatus.Draft
            };
        }

        /// <summary>
        /// Maps legacy ArticleStatus to workflow state
        /// </summary>
        private static string MapStatusToWorkflowState(ArticleStatus status)
        {
            return status switch
            {
                ArticleStatus.Draft => "draft",
                ArticleStatus.InReview => "in_review",
                ArticleStatus.Rejected => "rejected",
                ArticleStatus.Approved => "approved",
                ArticleStatus.Published => "published",
                ArticleStatus.Archived => "archived",
                _ => "draft"
            };
        }

        /// <summary>
        /// Logs workflow transitions for audit purposes
        /// </summary>
        private async Task LogWorkflowTransitionAsync(Guid articleId, string fromState, string toState, string transitionName, string userId, string comments)
        {
            // In a full implementation, this would save to a WorkflowHistory table
            // For now, just log to the application logger
            _logger.LogInformation("Workflow transition for article {ArticleId}: {FromState} -> {ToState} ({TransitionName}) by user {UserId}. Comments: {Comments}", 
                articleId, fromState ?? "[initial]", toState, transitionName, userId, comments ?? "[none]");
        }
    }
}