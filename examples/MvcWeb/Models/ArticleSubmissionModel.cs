using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Piranha.Models;

namespace MvcWeb.Models
{
    /// <summary>
    /// Model for frontend article submissions
    /// </summary>
    public class ArticleSubmissionModel
    {
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
        /// Gets/sets the content. For simplicity, we'll use plain text
        /// that will be converted to a Html Block.
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Gets/sets the optional primary image.
        /// </summary>
        public IFormFile PrimaryImage { get; set; }

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
        public bool NotifyOnComment { get; set; } = false;
    }

    /// <summary>
    /// Represents the workflow status of a submitted article
    /// </summary>
    public enum ArticleStatus
    {
        Draft,      // Initial status when submitted
        InReview,   // Being reviewed by an Editor
        Rejected,   // Rejected by an Editor
        Approved,   // Approved by an Editor, pending final approval
        Published,  // Approved by an Approver and published
        Archived    // No longer displayed, but kept for records
    }

    /// <summary>
    /// Represents a submitted article with workflow status
    /// </summary>
    public class SubmittedArticle
    {
        /// <summary>
        /// Gets/sets the unique id.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets/sets the date when the article was submitted.
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets/sets the date when the article was last modified.
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets/sets the date when the article was published.
        /// </summary>
        public DateTime? Published { get; set; }

        /// <summary>
        /// Gets/sets the current status of the article.
        /// </summary>
        public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

        /// <summary>
        /// Gets/sets the submission info.
        /// </summary>
        public ArticleSubmissionModel Submission { get; set; }

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