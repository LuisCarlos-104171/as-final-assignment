using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piranha.Data.EF.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Piranha_WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ContentTypes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitialState = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Piranha_WorkflowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInitial = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFinal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_WorkflowStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Piranha_WorkflowStates_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "Piranha_WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Piranha_WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromStateKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ToStateKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RequiredPermission = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CssClass = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresComment = table.Column<bool>(type: "INTEGER", nullable: false),
                    SendNotification = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationTemplate = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Piranha_WorkflowTransitions_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "Piranha_WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowStates_WorkflowDefinitionId",
                table: "Piranha_WorkflowStates",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowTransitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowTransitions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowDefinitions_ContentTypes",
                table: "Piranha_WorkflowDefinitions",
                column: "ContentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowDefinitions_IsDefault",
                table: "Piranha_WorkflowDefinitions",
                column: "IsDefault");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Piranha_WorkflowStates");

            migrationBuilder.DropTable(
                name: "Piranha_WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "Piranha_WorkflowDefinitions");
        }
    }
}