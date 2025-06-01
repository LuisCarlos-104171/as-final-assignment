using Piranha.Models;

namespace MvcWeb.Models
{
    /// <summary>
    /// Unified model representing content items in the workflow (both posts and submitted articles)
    /// </summary>
    public class WorkflowItem
    {
        /// <summary>
        /// Gets/sets the unique ID
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Gets/sets the title
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Gets/sets the author
        /// </summary>
        public string Author { get; set; }
        
        /// <summary>
        /// Gets/sets the creation date
        /// </summary>
        public DateTime Created { get; set; }
        
        /// <summary>
        /// Gets/sets the last modified date
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Gets/sets the current workflow state
        /// </summary>
        public string WorkflowState { get; set; }
        
        /// <summary>
        /// Gets/sets the content type (post or article)
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// Gets/sets the blog ID
        /// </summary>
        public Guid BlogId { get; set; }
        
        /// <summary>
        /// Gets/sets the available actions for this item
        /// </summary>
        public List<ArticleAction> AvailableActions { get; set; } = new List<ArticleAction>();
        
        /// <summary>
        /// Gets/sets whether this is published
        /// </summary>
        public bool IsPublished { get; set; }
        
        /// <summary>
        /// Gets/sets the submitted article (if this is an article submission)
        /// </summary>
        public SubmittedArticle SubmittedArticle { get; set; }
        
        /// <summary>
        /// Gets/sets the post (if this is a Piranha post)
        /// </summary>
        public PostBase Post { get; set; }
        
        /// <summary>
        /// Gets the review URL for this item
        /// </summary>
        public string GetReviewUrl()
        {
            if (ContentType == "article")
            {
                return $"/article/review/{Id}";
            }
            else if (ContentType == "post")
            {
                return $"/article/review-post/{Id}";
            }
            return "";
        }
        
        /// <summary>
        /// Gets the display status for this item
        /// </summary>
        public string GetDisplayStatus()
        {
            return WorkflowState switch
            {
                "draft" => "Draft",
                "in_review" => "In Review", 
                "approved" => "Approved",
                "published" => "Published",
                "rejected" => "Rejected",
                _ => WorkflowState ?? "Unknown"
            };
        }
        
        /// <summary>
        /// Gets the CSS class for the status badge
        /// </summary>
        public string GetStatusBadgeClass()
        {
            return WorkflowState switch
            {
                "draft" => "bg-secondary",
                "in_review" => "bg-info",
                "rejected" => "bg-danger",
                "approved" => "bg-warning",
                "published" => "bg-success",
                _ => "bg-secondary"
            };
        }
    }
}