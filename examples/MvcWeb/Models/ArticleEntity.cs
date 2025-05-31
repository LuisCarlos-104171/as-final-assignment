using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcWeb.Models
{
    /// <summary>
    /// Database entity for article submissions
    /// </summary>
    public class ArticleEntity
    {
        /// <summary>
        /// Gets/sets the unique id.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets/sets the date when the article was submitted.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets/sets the date when the article was last modified.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets/sets the date when the article was published.
        /// </summary>
        public DateTime? Published { get; set; }

        /// <summary>
        /// Gets/sets the current status of the article.
        /// </summary>
        public ArticleStatus Status { get; set; }

        /// <summary>
        /// Gets/sets the workflow definition ID for this article.
        /// Nullable for backward compatibility with existing database.
        /// </summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>
        /// Gets/sets the current workflow state key.
        /// Nullable for backward compatibility with existing database.
        /// </summary>
        [StringLength(64)]
        public string? WorkflowState { get; set; }

        /// <summary>
        /// Gets/sets the author's user ID.
        /// Nullable for backward compatibility with existing database.
        /// </summary>
        public string? AuthorId { get; set; }

        /// <summary>
        /// Gets/sets the reviewer's name.
        /// </summary>
        [StringLength(128)]
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// Gets/sets when the article was reviewed.
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Gets/sets the review comments.
        /// </summary>
        public string? ReviewComments { get; set; }

        /// <summary>
        /// Gets/sets the approver's name.
        /// </summary>
        [StringLength(128)]
        public string? ApprovedBy { get; set; }

        /// <summary>
        /// Gets/sets when the article was approved.
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// Gets/sets the approval comments.
        /// </summary>
        public string? ApprovalComments { get; set; }

        /// <summary>
        /// Gets/sets the article summary.
        /// </summary>
        [StringLength(500)]
        public string? Summary { get; set; }

        /// <summary>
        /// Gets/sets the title.
        /// </summary>
        [Required]
        [StringLength(128)]
        public string Title { get; set; }

        /// <summary>
        /// Gets/sets the optional category.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets/sets the optional tags.
        /// </summary>
        public string Tags { get; set; }

        /// <summary>
        /// Gets/sets the optional excerpt.
        /// </summary>
        [StringLength(256)]
        public string Excerpt { get; set; }

        /// <summary>
        /// Gets/sets the content.
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Gets/sets the primary image id, if any.
        /// </summary>
        public Guid? PrimaryImageId { get; set; }

        /// <summary>
        /// Gets/sets the submitter's email for notifications.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>
        /// Gets/sets the submitter's name.
        /// </summary>
        [Required]
        [StringLength(128)]
        public string Author { get; set; }

        /// <summary>
        /// Gets/sets if the author wants to be notified of comments.
        /// </summary>
        public bool NotifyOnComment { get; set; }

        /// <summary>
        /// Gets/sets any editorial feedback for the author.
        /// </summary>
        public string EditorialFeedback { get; set; }

        /// <summary>
        /// Gets/sets the editor's id who reviewed the article.
        /// </summary>
        public string ReviewedById { get; set; }

        /// <summary>
        /// Gets/sets the approver's id who published the article.
        /// </summary>
        public string ApprovedById { get; set; }

        /// <summary>
        /// Gets/sets the blog id this article belongs to.
        /// </summary>
        public Guid BlogId { get; set; }

        /// <summary>
        /// Gets/sets the optional post id if this article was published.
        /// </summary>
        public Guid? PostId { get; set; }
    }
}