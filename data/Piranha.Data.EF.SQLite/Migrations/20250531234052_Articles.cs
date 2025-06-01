using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piranha.Data.EF.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class Articles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Piranha_ArticleSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Published = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Excerpt = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NotifyOnComment = table.Column<bool>(type: "INTEGER", nullable: false),
                    EditorialFeedback = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedById = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedById = table.Column<string>(type: "TEXT", nullable: true),
                    BlogId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_ArticleSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_ArticleSubmissions_BlogId",
                table: "Piranha_ArticleSubmissions",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_ArticleSubmissions_Status",
                table: "Piranha_ArticleSubmissions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Piranha_ArticleSubmissions");
        }
    }
}
