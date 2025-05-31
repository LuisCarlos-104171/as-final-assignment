using Microsoft.EntityFrameworkCore;

namespace MvcWeb.Models
{
    /// <summary>
    /// Database context for article submissions
    /// </summary>
    public class ArticleDbContext : DbContext
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ArticleDbContext(DbContextOptions<ArticleDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Gets/sets the article submissions.
        /// </summary>
        public DbSet<ArticleEntity> Articles { get; set; }

        /// <summary>
        /// Model configuration.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the Articles table
            modelBuilder.Entity<ArticleEntity>(entity =>
            {
                entity.ToTable("Articles");
                entity.HasKey(e => e.Id);

                // Configure properties
                entity.Property(e => e.Id).IsRequired();
                entity.Property(e => e.Title).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.Author).IsRequired().HasMaxLength(128);
                entity.Property(e => e.BlogId).IsRequired();
                
                // Configure nullable properties (for backward compatibility)
                entity.Property(e => e.Category).IsRequired(false);
                entity.Property(e => e.Tags).IsRequired(false);
                entity.Property(e => e.Excerpt).IsRequired(false).HasMaxLength(256);
                entity.Property(e => e.EditorialFeedback).IsRequired(false);
                entity.Property(e => e.ReviewedById).IsRequired(false);
                entity.Property(e => e.ApprovedById).IsRequired(false);
                entity.Property(e => e.PrimaryImageId).IsRequired(false);
                entity.Property(e => e.PostId).IsRequired(false);
                entity.Property(e => e.Published).IsRequired(false);
                
                // Configure new workflow properties as nullable
                entity.Property(e => e.WorkflowId).IsRequired(false);
                entity.Property(e => e.WorkflowState).IsRequired(false).HasMaxLength(64);
                entity.Property(e => e.AuthorId).IsRequired(false);
                entity.Property(e => e.ReviewedBy).IsRequired(false).HasMaxLength(128);
                entity.Property(e => e.ReviewedAt).IsRequired(false);
                entity.Property(e => e.ReviewComments).IsRequired(false);
                entity.Property(e => e.ApprovedBy).IsRequired(false).HasMaxLength(128);
                entity.Property(e => e.ApprovedAt).IsRequired(false);
                entity.Property(e => e.ApprovalComments).IsRequired(false);
                entity.Property(e => e.Summary).IsRequired(false).HasMaxLength(500);

                // Create indexes
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.BlogId);
                entity.HasIndex(e => e.WorkflowId);
                entity.HasIndex(e => e.WorkflowState);
                entity.HasIndex(e => e.AuthorId);
            });
        }
    }
}