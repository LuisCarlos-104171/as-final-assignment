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
using System.Data;
using System.Data.Common;

namespace MvcWeb.Models;

/// <summary>
/// Simple database migrator for the Articles table that handles both creation and schema updates.
/// </summary>
public static class ArticleDbMigrator
{
    /// <summary>
    /// Ensures the Articles table exists with the correct schema for dynamic workflows.
    /// </summary>
    /// <param name="context">The article database context</param>
    public static async Task EnsureArticlesTableAsync(ArticleDbContext context)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var command = (DbCommand)connection.CreateCommand();

            // Check if Articles table exists
            command.CommandText = @"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name='Articles';";
            
            var tableExists = await command.ExecuteScalarAsync() != null;

            if (!tableExists)
            {
                // Create the Articles table with full schema including workflow properties
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Articles (
                        Id TEXT PRIMARY KEY NOT NULL,
                        Created TEXT NOT NULL,
                        LastModified TEXT NOT NULL,
                        Published TEXT NULL,
                        Status INTEGER NOT NULL DEFAULT 0,
                        Title TEXT NOT NULL,
                        Category TEXT NULL,
                        Tags TEXT NULL,
                        Excerpt TEXT NULL,
                        Content TEXT NOT NULL,
                        PrimaryImageId TEXT NULL,
                        Email TEXT NOT NULL,
                        Author TEXT NOT NULL,
                        NotifyOnComment INTEGER NOT NULL DEFAULT 0,
                        EditorialFeedback TEXT NULL,
                        ReviewedById TEXT NULL,
                        ApprovedById TEXT NULL,
                        BlogId TEXT NOT NULL,
                        PostId TEXT NULL,
                        
                        -- New workflow properties (nullable for backward compatibility)
                        WorkflowId TEXT NULL,
                        WorkflowState TEXT NULL,
                        AuthorId TEXT NULL,
                        ReviewedBy TEXT NULL,
                        ReviewedAt TEXT NULL,
                        ReviewComments TEXT NULL,
                        ApprovedBy TEXT NULL,
                        ApprovedAt TEXT NULL,
                        ApprovalComments TEXT NULL,
                        Summary TEXT NULL
                    );";

                await command.ExecuteNonQueryAsync();

                // Create indexes
                command.CommandText = @"
                    CREATE INDEX IF NOT EXISTS IX_Articles_Status ON Articles (Status);
                    CREATE INDEX IF NOT EXISTS IX_Articles_BlogId ON Articles (BlogId);
                    CREATE INDEX IF NOT EXISTS IX_Articles_WorkflowId ON Articles (WorkflowId);
                    CREATE INDEX IF NOT EXISTS IX_Articles_WorkflowState ON Articles (WorkflowState);
                    CREATE INDEX IF NOT EXISTS IX_Articles_AuthorId ON Articles (AuthorId);";

                await command.ExecuteNonQueryAsync();

                Console.WriteLine("Articles table created successfully with workflow support.");
            }
            else
            {
                // Table exists, check if workflow columns exist and add them if needed
                await AddWorkflowColumnsIfNeededAsync(command);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not ensure Articles table schema: {ex.Message}");
            // For development, we can fall back to EF's EnsureCreated
            await context.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>
    /// Adds workflow columns to existing Articles table if they don't exist.
    /// </summary>
    private static async Task AddWorkflowColumnsIfNeededAsync(DbCommand command)
    {
        try
        {
            // Check if WorkflowId column exists
            command.CommandText = "PRAGMA table_info(Articles);";
            var hasWorkflowId = false;
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1); // Column name is at index 1
                if (columnName == "WorkflowId")
                {
                    hasWorkflowId = true;
                    break;
                }
            }
            reader.Close();

            if (!hasWorkflowId)
            {
                Console.WriteLine("Adding workflow columns to existing Articles table...");
                
                // Add workflow columns one by one (SQLite doesn't support multiple ADD COLUMN in one statement)
                var alterCommands = new[]
                {
                    "ALTER TABLE Articles ADD COLUMN WorkflowId TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN WorkflowState TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN AuthorId TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ReviewedBy TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ReviewedAt TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ReviewComments TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ApprovedBy TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ApprovedAt TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN ApprovalComments TEXT NULL;",
                    "ALTER TABLE Articles ADD COLUMN Summary TEXT NULL;"
                };

                foreach (var alterCommand in alterCommands)
                {
                    try
                    {
                        command.CommandText = alterCommand;
                        await command.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                        // Column might already exist, continue
                    }
                }

                // Create new indexes
                command.CommandText = @"
                    CREATE INDEX IF NOT EXISTS IX_Articles_WorkflowId ON Articles (WorkflowId);
                    CREATE INDEX IF NOT EXISTS IX_Articles_WorkflowState ON Articles (WorkflowState);
                    CREATE INDEX IF NOT EXISTS IX_Articles_AuthorId ON Articles (AuthorId);";

                await command.ExecuteNonQueryAsync();

                Console.WriteLine("Workflow columns added successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not add workflow columns: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates existing articles to use default workflow if they don't have workflow assigned.
    /// </summary>
    /// <param name="context">The article database context</param>
    /// <param name="defaultWorkflowId">The default workflow ID to assign</param>
    public static async Task UpdateExistingArticlesWithDefaultWorkflowAsync(ArticleDbContext context, Guid defaultWorkflowId)
    {
        try
        {
            var articlesWithoutWorkflow = await context.Articles
                .Where(a => a.WorkflowId == null || a.WorkflowState == null)
                .ToListAsync();

            foreach (var article in articlesWithoutWorkflow)
            {
                article.WorkflowId = defaultWorkflowId;
                article.WorkflowState = MapStatusToWorkflowState(article.Status);
                
                // Set AuthorId if not present
                if (string.IsNullOrEmpty(article.AuthorId))
                {
                    article.AuthorId = "legacy-user";
                }
            }

            if (articlesWithoutWorkflow.Any())
            {
                await context.SaveChangesAsync();
                Console.WriteLine($"Updated {articlesWithoutWorkflow.Count} existing articles with default workflow.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not update existing articles: {ex.Message}");
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