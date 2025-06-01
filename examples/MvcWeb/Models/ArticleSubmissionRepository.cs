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
        private readonly IDb _db;
        private readonly IWorkflowDefinitionService _workflowDefinitionService;
        
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
            IDb db,
            IWorkflowDefinitionService workflowDefinitionService)
        {
            _api = api;
            _logger = logger;
            _userManager = userManager;
            _db = db;
            _workflowDefinitionService = workflowDefinitionService;
        }

        /// <summary>
        /// Adds a new article submission
        /// </summary>
        public async Task<SubmittedArticle> AddSubmissionAsync(ArticleSubmissionModel model, Guid blogId, string submittedById = null)
        {
            using var activity = ActivitySource.StartActivity("ArticleSubmissionRepository.AddSubmission");
            var stopwatch = Stopwatch.StartNew();
            
            activity?.SetTag("blogId", blogId.ToString());
            activity?.SetTag("hasImage", model.PrimaryImage != null);
            activity?.SetTag("author", model.Author);
            
            try
            {
                // Get the initial workflow state from the workflow definition
                var initialState = await GetInitialWorkflowStateAsync();
                
                // Create entity from model
                var entity = new Piranha.Data.ArticleSubmission
                {
                    Id = Guid.NewGuid(),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now,
                    Status = (int)ArticleStatus.Draft, // Keep for backward compatibility
                    WorkflowState = initialState,
                    Title = model.Title,
                    Category = model.Category,
                    Tags = model.Tags,
                    Excerpt = model.Excerpt,
                    Content = model.Content,
                    Email = model.Email,
                    Author = model.Author,
                    NotifyOnComment = model.NotifyOnComment,
                    BlogId = blogId,
                    SubmittedById = submittedById
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
                _db.ArticleSubmissions.Add(entity);
                await _db.SaveChangesAsync();

                stopwatch.Stop();
                DatabaseQueryDuration.Record(stopwatch.ElapsedMilliseconds);
                RepositoryOperationsCounter.Add(1, 
                    new KeyValuePair<string, object?>("operation", "add_submission"), 
                    new KeyValuePair<string, object?>("status", "success"));
                
                activity?.SetTag("submissionId", entity.Id.ToString());
                activity?.SetTag("outcome", "success");

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
        private SubmittedArticle ConvertToSubmittedArticle(Piranha.Data.ArticleSubmission entity)
        {
            return new SubmittedArticle
            {
                Id = entity.Id,
                Created = entity.Created,
                LastModified = entity.LastModified,
                Published = entity.Published,
                Status = (ArticleStatus)entity.Status,
                WorkflowState = entity.WorkflowState,
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
                SubmittedById = entity.SubmittedById
            };
        }

        /// <summary>
        /// Gets the initial workflow state from the workflow definition
        /// </summary>
        private async Task<string> GetInitialWorkflowStateAsync()
        {
            try
            {
                var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync("post");
                if (workflow?.States == null || !workflow.States.Any())
                {
                    return "draft"; // Fallback
                }

                // Find the initial state
                var initialState = workflow.States.FirstOrDefault(s => s.IsInitial);
                if (initialState != null)
                {
                    return initialState.Key;
                }

                // If no initial state is explicitly marked, use the first state
                return workflow.States.First().Key;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting initial workflow state");
                return "draft"; // Fallback
            }
        }

        /// <summary>
        /// Maps workflow state to ArticleStatus for backward compatibility using workflow metadata
        /// </summary>
        private ArticleStatus MapWorkflowStateToArticleStatus(string workflowState)
        {
            if (string.IsNullOrEmpty(workflowState))
                return ArticleStatus.Draft;

            try
            {
                var workflow = _workflowDefinitionService.GetDefaultByContentTypeAsync("post").Result;
                if (workflow?.States == null || !workflow.States.Any())
                {
                    return ArticleStatus.Draft;
                }

                var state = workflow.States.FirstOrDefault(s => s.Key == workflowState);
                if (state == null)
                {
                    return ArticleStatus.Draft;
                }

                // Use workflow state properties to determine ArticleStatus
                if (state.IsInitial)
                    return ArticleStatus.Draft;
                if (state.IsPublished)
                    return ArticleStatus.Published;
                if (state.IsFinal)
                    return ArticleStatus.Published; // Final states are typically published

                // Pattern-based fallback for other states
                var stateName = state.Name?.ToLower() ?? state.Key.ToLower();
                if (stateName.Contains("review") || stateName.Contains("pending"))
                    return ArticleStatus.InReview;
                if (stateName.Contains("approved") || stateName.Contains("ready"))
                    return ArticleStatus.Approved;
                if (stateName.Contains("rejected") || stateName.Contains("denied"))
                    return ArticleStatus.Rejected;

                return ArticleStatus.InReview; // Default for non-initial, non-final states
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping workflow state {WorkflowState} to ArticleStatus", workflowState);
                return ArticleStatus.Draft;
            }
        }

        /// <summary>
        /// Gets a list of all submissions, filtered by status and/or user
        /// </summary>
        public async Task<List<SubmittedArticle>> GetSubmissionsAsync(ArticleStatus? status = null, string userId = null)
        {
            // Start with base query
            IQueryable<Piranha.Data.ArticleSubmission> query = _db.ArticleSubmissions;
            
            // Apply status filter if provided
            if (status.HasValue)
            {
                query = query.Where(a => a.Status == (int)status.Value);
            }
            
            // Apply user filter if provided
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(a => 
                    (a.Status == (int)ArticleStatus.Draft && a.SubmittedById == userId) || 
                    (a.Status == (int)ArticleStatus.InReview && (a.ReviewedById == userId || a.SubmittedById == userId)) ||
                    (a.Status == (int)ArticleStatus.Approved && (a.ApprovedById == userId || a.SubmittedById == userId)) ||
                    (a.Status == (int)ArticleStatus.Rejected && a.SubmittedById == userId) ||
                    (a.Status == (int)ArticleStatus.Published && a.SubmittedById == userId));
            }
            
            // Order by last modified
            query = query.OrderByDescending(a => a.LastModified);
            
            // Execute query and convert to SubmittedArticle objects
            var entities = await query.ToListAsync();
            return entities.Select(ConvertToSubmittedArticle).ToList();
        }

        /// <summary>
        /// Gets a specific submission by id
        /// </summary>
        public async Task<SubmittedArticle> GetSubmissionByIdAsync(Guid id)
        {
            var entity = await _db.ArticleSubmissions.FindAsync(id);
            if (entity == null)
            {
                return null;
            }
            
            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Updates the workflow state of a submission
        /// </summary>
        public async Task<SubmittedArticle> UpdateSubmissionStateAsync(
            Guid id, 
            string workflowState, 
            string userId, 
            string feedback = null)
        {
            var entity = await _db.ArticleSubmissions.FindAsync(id);
            if (entity == null)
            {
                return null;
            }

            entity.WorkflowState = workflowState;
            entity.LastModified = DateTime.Now;
            entity.EditorialFeedback = feedback;

            // Update status based on workflow state patterns for backward compatibility
            entity.Status = (int)MapWorkflowStateToArticleStatus(workflowState);

            // Update the appropriate reviewer based on workflow state properties
            try
            {
                var workflow = await _workflowDefinitionService.GetDefaultByContentTypeAsync("post");
                var state = workflow?.States?.FirstOrDefault(s => s.Key == workflowState);
                
                _logger.LogInformation("Processing workflow state '{WorkflowState}' - Found state: {StateFound}, IsPublished: {IsPublished}, IsFinal: {IsFinal}", 
                    workflowState, state != null, state?.IsPublished ?? false, state?.IsFinal ?? false);
                    
                if (state != null)
                {
                    _logger.LogInformation("State details - Key: '{StateKey}', Name: '{StateName}', IsInitial: {IsInitial}, IsPublished: {IsPublished}, IsFinal: {IsFinal}", 
                        state.Key, state.Name, state.IsInitial, state.IsPublished, state.IsFinal);
                }
                
                if (state != null)
                {
                    // Use workflow state properties to determine reviewer assignment
                    if (state.IsPublished || state.IsFinal)
                    {
                        _logger.LogInformation("Article {ArticleId} transitioning to published/final state '{WorkflowState}'", entity.Id, workflowState);
                        
                        entity.ApprovedById = userId;
                        entity.Published = DateTime.Now;
                        
                        // Create or update the Piranha post when published or final
                        if (!entity.PostId.HasValue) 
                        {
                            _logger.LogInformation("Creating new Piranha post for article {ArticleId}", entity.Id);
                            var article = ConvertToSubmittedArticle(entity);
                            await CreatePostFromSubmissionAsync(article);
                            entity.PostId = article.PostId;
                            _logger.LogInformation("Created Piranha post {PostId} for article {ArticleId}", article.PostId, entity.Id);
                        }
                        else
                        {
                            _logger.LogInformation("Updating existing Piranha post {PostId} for article {ArticleId}", entity.PostId, entity.Id);
                            // Update existing post to set published date
                            await UpdatePostPublishedStatusAsync(entity.PostId.Value, true);
                        }
                    }
                    else if (!state.IsInitial)
                    {
                        _logger.LogInformation("Article {ArticleId} transitioning to review state '{WorkflowState}'", entity.Id, workflowState);
                        // Non-initial, non-published states are review states
                        entity.ReviewedById = userId;
                        
                        // If post exists and we're moving to non-published state, unpublish it
                        if (entity.PostId.HasValue)
                        {
                            await UpdatePostPublishedStatusAsync(entity.PostId.Value, false);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find workflow state '{WorkflowState}' in workflow definition", workflowState);
                    
                    // Fallback to pattern-based approach
                    var stateLower = workflowState.ToLower();
                    if (stateLower.Contains("review") || stateLower.Contains("pending") || stateLower.Contains("rejected"))
                    {
                        entity.ReviewedById = userId;
                    }
                    else if (stateLower.Contains("approved") || stateLower.Contains("published") || stateLower.Contains("pub") || stateLower.Contains("final"))
                    {
                        entity.ApprovedById = userId;
                        entity.Published = DateTime.Now;
                        
                        if (!entity.PostId.HasValue) 
                        {
                            var article = ConvertToSubmittedArticle(entity);
                            await CreatePostFromSubmissionAsync(article);
                            entity.PostId = article.PostId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining reviewer assignment for workflow state {WorkflowState}", workflowState);
                // Use pattern-based fallback
                var stateLower = workflowState.ToLower();
                if (stateLower.Contains("review") || stateLower.Contains("pending") || stateLower.Contains("rejected"))
                {
                    entity.ReviewedById = userId;
                }
            }

            // Save changes to database
            await _db.SaveChangesAsync();

            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Updates the published status of an existing Piranha post
        /// </summary>
        private async Task UpdatePostPublishedStatusAsync(Guid postId, bool isPublished)
        {
            try
            {
                var post = await _api.Posts.GetByIdAsync(postId);
                if (post != null)
                {
                    if (isPublished)
                    {
                        post.Published = DateTime.Now;
                        _logger.LogInformation("Setting post {PostId} published date to {PublishedDate}", postId, post.Published);
                    }
                    else
                    {
                        post.Published = null;
                        _logger.LogInformation("Removing published date from post {PostId}", postId);
                    }
                    
                    await _api.Posts.SaveAsync(post);
                    _logger.LogInformation("Updated post {PostId} published status to {IsPublished}", postId, isPublished);
                }
                else
                {
                    _logger.LogError("Post {PostId} not found when trying to update published status", postId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating published status for post {PostId}", postId);
                throw;
            }
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
                
                // Generate a unique slug by appending the article ID and timestamp
                var baseSlug = Utils.GenerateSlug(article.Submission.Title);
                var uniqueSuffix = $"{article.Id.ToString("N")[..8]}-{DateTime.Now.Ticks.ToString()[..8]}";
                post.Slug = $"{baseSlug}-{uniqueSuffix}";
                
                // Ensure slug is unique by checking if it exists
                var existingPost = await _api.Posts.GetBySlugAsync(post.BlogId, post.Slug);
                if (existingPost != null)
                {
                    // If still not unique, add random suffix
                    post.Slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..12]}";
                }
                
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
                activity?.SetTag("slug", post.Slug);
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
                    
                    _logger.LogInformation("Successfully published article {ArticleId} as post {PostId} with slug {Slug}, published: {Published}, blogId: {BlogId}", 
                        article.Id, post.Id, post.Slug, post.Published, post.BlogId);
                        
                    // Verify the post was saved correctly by trying to retrieve it
                    var savedPost = await _api.Posts.GetByIdAsync(post.Id);
                    if (savedPost != null)
                    {
                        _logger.LogInformation("Verified post {PostId} exists: Title='{Title}', Published={Published}, Slug='{Slug}'", 
                            savedPost.Id, savedPost.Title, savedPost.Published, savedPost.Slug);
                    }
                    else
                    {
                        _logger.LogError("Post {PostId} was not found after saving!", post.Id);
                    }
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
        /// Updates the PostId for an article submission
        /// </summary>
        public async Task UpdateArticlePostIdAsync(Guid articleId, Guid postId)
        {
            try
            {
                var entity = await _db.ArticleSubmissions.FindAsync(articleId);
                if (entity != null)
                {
                    entity.PostId = postId;
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Updated article {ArticleId} with PostId {PostId}", articleId, postId);
                }
                else
                {
                    _logger.LogWarning("Article {ArticleId} not found when trying to update PostId", articleId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PostId {PostId} for article {ArticleId}", postId, articleId);
                throw;
            }
        }
    }
}