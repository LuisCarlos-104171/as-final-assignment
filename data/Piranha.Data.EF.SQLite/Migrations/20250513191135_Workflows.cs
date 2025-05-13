using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piranha.Data.EF.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class Workflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedOn",
                table: "Piranha_Posts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastReviewerId",
                table: "Piranha_Posts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Piranha_Posts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowState",
                table: "Piranha_Posts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedOn",
                table: "Piranha_Pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastReviewerId",
                table: "Piranha_Pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Piranha_Pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowState",
                table: "Piranha_Pages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReviewedOn",
                table: "Piranha_Posts");

            migrationBuilder.DropColumn(
                name: "LastReviewerId",
                table: "Piranha_Posts");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "Piranha_Posts");

            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "Piranha_Posts");

            migrationBuilder.DropColumn(
                name: "LastReviewedOn",
                table: "Piranha_Pages");

            migrationBuilder.DropColumn(
                name: "LastReviewerId",
                table: "Piranha_Pages");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "Piranha_Pages");

            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "Piranha_Pages");
        }
    }
}
