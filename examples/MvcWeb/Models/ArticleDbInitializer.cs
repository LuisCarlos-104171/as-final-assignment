/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using Microsoft.EntityFrameworkCore;
using Piranha.Services;

namespace MvcWeb.Models;

/// <summary>
/// Database initializer for article submissions that handles both legacy and dynamic workflow schemas.
/// </summary>
public static class ArticleDbInitializer
{
    /// <summary>
    /// Ensures the database is created and properly configured for both legacy and dynamic workflows.
    /// </summary>
    /// <param name="context">The article database context</param>
    /// <param name="workflowService">The dynamic workflow service</param>
    public static async Task InitializeAsync(ArticleDbContext context, IDynamicWorkflowService workflowService)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if we need to add workflow columns to existing table
        await UpdateSchemaForWorkflowsAsync(context);

        // Ensure default workflow exists
        await EnsureDefaultWorkflowAsync(workflowService);

        // Update existing articles to use default workflow
        await UpdateExistingArticlesAsync(context, workflowService);
    }

    /// <summary>
    /// Updates the database schema to support workflow properties if needed.
    /// </summary>
    private static async Task UpdateSchemaForWorkflowsAsync(ArticleDbContext context)
    {
        try
        {
            // Check if workflow columns exist by trying to query them
            var hasWorkflowColumns = await context.Articles
                .Where(a => a.WorkflowId != Guid.Empty)
                .AnyAsync();
        }
        catch (Exception)
        {
            // If the query fails, it means the columns don't exist
            // For SQLite, we need to recreate the table with new schema
            await RecreateTableWithWorkflowColumnsAsync(context);
        }
    }

    /// <summary>
    /// Recreates the Articles table with workflow columns for SQLite.
    /// </summary>
    private static async Task RecreateTableWithWorkflowColumnsAsync(ArticleDbContext context)
    {
        // Get existing data
        var existingArticles = new List<object>();
        try
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            // Check if Articles table exists and get data
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Articles';";
            var tableExists = await command.ExecuteScalarAsync() != null;

            if (tableExists)
            {
                // Get existing articles data
                command.CommandText = @"
                    SELECT Id, Created, LastModified, Published, Status, Title, Category, Tags, 
                           Excerpt, Content, PrimaryImageId, Email, Author, NotifyOnComment, 
                           EditorialFeedback, ReviewedById, ApprovedById, BlogId, PostId
                    FROM Articles";
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingArticles.Add(new
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Created = reader.GetDateTime(reader.GetOrdinal("Created")),
                        LastModified = reader.GetDateTime(reader.GetOrdinal("LastModified")),
                        Published = reader.IsDBNull(reader.GetOrdinal("Published")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("Published")),
                        Status = (ArticleStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                        Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? null : reader.GetString(reader.GetOrdinal("Tags")),
                        Excerpt = reader.IsDBNull(reader.GetOrdinal("Excerpt")) ? null : reader.GetString(reader.GetOrdinal("Excerpt")),
                        Content = reader.GetString(reader.GetOrdinal("Content")),
                        PrimaryImageId = reader.IsDBNull(reader.GetOrdinal("PrimaryImageId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("PrimaryImageId")),
                        Email = reader.GetString(reader.GetOrdinal("Email")),
                        Author = reader.GetString(reader.GetOrdinal("Author")),
                        NotifyOnComment = reader.GetBoolean(reader.GetOrdinal("NotifyOnComment")),
                        EditorialFeedback = reader.IsDBNull(reader.GetOrdinal("EditorialFeedback")) ? null : reader.GetString(reader.GetOrdinal("EditorialFeedback")),
                        ReviewedById = reader.IsDBNull(reader.GetOrdinal("ReviewedById")) ? null : reader.GetString(reader.GetOrdinal("ReviewedById")),
                        ApprovedById = reader.IsDBNull(reader.GetOrdinal("ApprovedById")) ? null : reader.GetString(reader.GetOrdinal("ApprovedById")),
                        BlogId = reader.GetGuid(reader.GetOrdinal("BlogId")),
                        PostId = reader.IsDBNull(reader.GetOrdinal("PostId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("PostId"))
                    });
                }

                // Drop and recreate table
                command.CommandText = "DROP TABLE Articles;";
                await command.ExecuteNonQueryAsync();
            }

            await connection.CloseAsync();
        }
        catch
        {
            // If we can't get existing data, continue with empty table
        }

        // Recreate database with new schema
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // TODO: Restore existing data with default workflow values
        // This would require the workflow service to be available
    }

    /// <summary>
    /// Ensures a default workflow exists for articles.
    /// </summary>
    private static async Task EnsureDefaultWorkflowAsync(IDynamicWorkflowService workflowService)
    {
        try
        {
            var existingWorkflow = await workflowService.GetDefaultWorkflowAsync("article");
            if (existingWorkflow == null)
            {
                await workflowService.CreateDefaultWorkflowAsync("article", "Article Approval Workflow");
            }
        }
        catch
        {
            // If workflow service is not available or fails, continue
            // The application will handle creating workflows when needed
        }
    }

    /// <summary>
    /// Updates existing articles to use the default workflow.
    /// </summary>
    private static async Task UpdateExistingArticlesAsync(ArticleDbContext context, IDynamicWorkflowService workflowService)
    {
        try
        {
            var defaultWorkflow = await workflowService.GetDefaultWorkflowAsync("article");
            if (defaultWorkflow == null) return;

            var articlesWithoutWorkflow = await context.Articles
                .Where(a => a.WorkflowId == Guid.Empty || a.WorkflowState == null)
                .ToListAsync();

            foreach (var article in articlesWithoutWorkflow)
            {
                article.WorkflowId = defaultWorkflow.Id;
                article.WorkflowState = MapStatusToWorkflowState(article.Status);
            }

            if (articlesWithoutWorkflow.Any())
            {
                await context.SaveChangesAsync();
            }
        }
        catch
        {
            // If update fails, continue - articles will work with legacy status
        }
    }

    /// <summary>
    /// Maps legacy ArticleStatus to workflow state.
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
}