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
        /// Gets/sets the content type (always "post" for workflow purposes)
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
            if (SubmittedArticle != null)
            {
                // For submitted articles, use the article review URL
                return $"/article/review/{Id}";
            }
            else if (Post != null)
            {
                // For Piranha posts, use the post review URL
                return $"/article/review-post/{Id}";
            }
            return "";
        }
        
        /// <summary>
        /// Gets the display status for this item based on workflow state name
        /// </summary>
        public string GetDisplayStatus()
        {
            if (string.IsNullOrEmpty(WorkflowState))
                return "Unknown";
                
            // Use the workflow state as-is, with proper capitalization
            return char.ToUpper(WorkflowState[0]) + WorkflowState.Substring(1).ToLower();
        }
        
        /// <summary>
        /// Gets the CSS class for the status badge based on workflow state properties
        /// </summary>
        public string GetStatusBadgeClass()
        {
            if (string.IsNullOrEmpty(WorkflowState))
                return "bg-secondary";

            // We need a way to access workflow definition here
            // For now, use pattern-based approach as fallback
            // TODO: Inject workflow service or pass workflow metadata
            return GetStatusBadgeClassByPattern();
        }

        /// <summary>
        /// Fallback method for getting badge class based on patterns
        /// </summary>
        private string GetStatusBadgeClassByPattern()
        {
            var state = WorkflowState.ToLower();
            
            // Pattern-based mapping for common state types
            if (state.Contains("draft") || state.Contains("initial") || state.Contains("new"))
                return "bg-secondary";
            if (state.Contains("review") || state.Contains("pending") || state.Contains("submitted"))
                return "bg-info";
            if (state.Contains("rejected") || state.Contains("denied") || state.Contains("failed"))
                return "bg-danger";
            if (state.Contains("approved") || state.Contains("ready") || state.Contains("accepted"))
                return "bg-warning";
            if (state.Contains("published") || state.Contains("pub") || state.Contains("live") || state.Contains("final") || state.Contains("complete"))
                return "bg-success";
                
            return "bg-primary"; // Default for unknown states
        }
    }
}