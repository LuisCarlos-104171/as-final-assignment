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

            modelBuilder.Entity<ArticleEntity>()
                .HasIndex(a => a.Status);

            modelBuilder.Entity<ArticleEntity>()
                .HasIndex(a => a.BlogId);
        }
    }
}