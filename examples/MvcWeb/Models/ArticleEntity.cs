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