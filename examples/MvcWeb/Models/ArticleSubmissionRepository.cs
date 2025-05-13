using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Piranha;
using Piranha.AspNetCore.Identity.Data;
using Piranha.Extend.Blocks;
using Piranha.Extend.Fields;
using Piranha.Models;

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

        /// <summary>
        /// Constructor
        /// </summary>
        public ArticleSubmissionRepository(IApi api, 
            ILogger<ArticleSubmissionRepository> logger, 
            UserManager<User> userManager,
            ArticleDbContext dbContext)
        {
            _api = api;
            _logger = logger;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Adds a new article submission
        /// </summary>
        public async Task<SubmittedArticle> AddSubmissionAsync(ArticleSubmissionModel model, Guid blogId)
        {
            // Create entity from model
            var entity = new ArticleEntity
            {
                Id = Guid.NewGuid(),
                Created = DateTime.Now,
                LastModified = DateTime.Now,
                Status = ArticleStatus.Draft,
                Title = model.Title,
                Category = model.Category,
                Tags = model.Tags,
                Excerpt = model.Excerpt,
                Content = model.Content,
                Email = model.Email,
                Author = model.Author,
                NotifyOnComment = model.NotifyOnComment,
                BlogId = blogId
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
                }
            }

            // Save to database
            _dbContext.Articles.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Convert to SubmittedArticle for API consistency
            return ConvertToSubmittedArticle(entity);
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
                Status = entity.Status,
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
                PostId = entity.PostId
            };
        }

        /// <summary>
        /// Gets a list of all submissions, filtered by status and/or user
        /// </summary>
        public async Task<List<SubmittedArticle>> GetSubmissionsAsync(ArticleStatus? status = null, string userId = null)
        {
            // Start with base query
            IQueryable<ArticleEntity> query = _dbContext.Articles;
            
            // Apply status filter if provided
            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }
            
            // Apply user filter if provided
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(a => 
                    (a.Status == ArticleStatus.Draft && a.Author == userId) || 
                    (a.Status == ArticleStatus.InReview && a.ReviewedById == userId) ||
                    (a.Status == ArticleStatus.Approved && a.ApprovedById == userId));
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
            var entity = await _dbContext.Articles.FindAsync(id);
            if (entity == null)
            {
                return null;
            }
            
            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Updates the status of a submission
        /// </summary>
        public async Task<SubmittedArticle> UpdateSubmissionStatusAsync(
            Guid id, 
            ArticleStatus status, 
            string userId, 
            string feedback = null)
        {
            var entity = await _dbContext.Articles.FindAsync(id);
            if (entity == null)
            {
                return null;
            }

            entity.Status = status;
            entity.LastModified = DateTime.Now;
            entity.EditorialFeedback = feedback;

            // Update the appropriate reviewer based on status
            if (status == ArticleStatus.InReview || status == ArticleStatus.Rejected)
            {
                entity.ReviewedById = userId;
            }
            else if (status == ArticleStatus.Approved || status == ArticleStatus.Published)
            {
                entity.ApprovedById = userId;
                entity.Published = DateTime.Now;
                
                // Create an actual post in Piranha when approved or published
                // Only if the article hasn't been published yet
                if (!entity.PostId.HasValue) 
                {
                    var article = ConvertToSubmittedArticle(entity);
                    await CreatePostFromSubmissionAsync(article);
                    entity.PostId = article.PostId;
                }
            }

            // Save changes to database
            await _dbContext.SaveChangesAsync();

            return ConvertToSubmittedArticle(entity);
        }

        /// <summary>
        /// Creates a published post from a submitted article
        /// </summary>
        private async Task CreatePostFromSubmissionAsync(SubmittedArticle article)
        {
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
                
                post.Published = DateTime.Now; // Ensure the post is published immediately

                // Process tags
                if (!string.IsNullOrWhiteSpace(article.Submission.Tags))
                {
                    var tags = article.Submission.Tags
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t));
                    
                    foreach (var tag in tags)
                    {
                        post.Tags.Add(tag);
                    }
                }

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
                    
                    _logger.LogInformation("Successfully published article {ArticleId} as post {PostId}", 
                        article.Id, post.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save post for article {ArticleId}. Title: {Title}, Category: {Category}", 
                        article.Id, post.Title, post.Category);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post from article submission {ArticleId}", article.Id);
                throw;
            }
        }
    }
}