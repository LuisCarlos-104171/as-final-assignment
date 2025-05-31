using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piranha.Data.EF.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class removedcolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredPermission",
                table: "WorkflowTransitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredPermission",
                table: "WorkflowTransitions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }
    }
}
