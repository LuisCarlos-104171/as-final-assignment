using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piranha.Data.EF.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class LetsgoooMig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStates_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowStates");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowTransitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowTransitions",
                table: "WorkflowTransitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStates",
                table: "WorkflowStates");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStates_WorkflowDefinitionId",
                table: "WorkflowStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions");

            migrationBuilder.RenameTable(
                name: "WorkflowTransitions",
                newName: "Piranha_WorkflowTransitions");

            migrationBuilder.RenameTable(
                name: "WorkflowStates",
                newName: "Piranha_WorkflowStates");

            migrationBuilder.RenameTable(
                name: "WorkflowDefinitions",
                newName: "Piranha_WorkflowDefinitions");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowTransitions",
                newName: "IX_Piranha_WorkflowTransitions_WorkflowDefinitionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Piranha_WorkflowTransitions",
                table: "Piranha_WorkflowTransitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Piranha_WorkflowStates",
                table: "Piranha_WorkflowStates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Piranha_WorkflowDefinitions",
                table: "Piranha_WorkflowDefinitions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Piranha_WorkflowRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CanCreate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanEdit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanDelete = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanViewAll = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedFromStates = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AllowedToStates = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_WorkflowRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Piranha_WorkflowRoles_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "Piranha_WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Piranha_WorkflowRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowTransitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanExecute = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    Conditions = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Piranha_WorkflowRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Piranha_WorkflowRolePermissions_Piranha_WorkflowRoles_WorkflowRoleId",
                        column: x => x.WorkflowRoleId,
                        principalTable: "Piranha_WorkflowRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Piranha_WorkflowRolePermissions_Piranha_WorkflowTransitions_WorkflowTransitionId",
                        column: x => x.WorkflowTransitionId,
                        principalTable: "Piranha_WorkflowTransitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowStates_WorkflowDefinitionId_Key",
                table: "Piranha_WorkflowStates",
                columns: new[] { "WorkflowDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowRolePermissions_WorkflowRoleId_WorkflowTransitionId",
                table: "Piranha_WorkflowRolePermissions",
                columns: new[] { "WorkflowRoleId", "WorkflowTransitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowRolePermissions_WorkflowTransitionId",
                table: "Piranha_WorkflowRolePermissions",
                column: "WorkflowTransitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Piranha_WorkflowRoles_WorkflowDefinitionId_RoleKey",
                table: "Piranha_WorkflowRoles",
                columns: new[] { "WorkflowDefinitionId", "RoleKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Piranha_WorkflowStates_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowStates",
                column: "WorkflowDefinitionId",
                principalTable: "Piranha_WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Piranha_WorkflowTransitions_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowTransitions",
                column: "WorkflowDefinitionId",
                principalTable: "Piranha_WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Piranha_WorkflowStates_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowStates");

            migrationBuilder.DropForeignKey(
                name: "FK_Piranha_WorkflowTransitions_Piranha_WorkflowDefinitions_WorkflowDefinitionId",
                table: "Piranha_WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "Piranha_WorkflowRolePermissions");

            migrationBuilder.DropTable(
                name: "Piranha_WorkflowRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Piranha_WorkflowTransitions",
                table: "Piranha_WorkflowTransitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Piranha_WorkflowStates",
                table: "Piranha_WorkflowStates");

            migrationBuilder.DropIndex(
                name: "IX_Piranha_WorkflowStates_WorkflowDefinitionId_Key",
                table: "Piranha_WorkflowStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Piranha_WorkflowDefinitions",
                table: "Piranha_WorkflowDefinitions");

            migrationBuilder.RenameTable(
                name: "Piranha_WorkflowTransitions",
                newName: "WorkflowTransitions");

            migrationBuilder.RenameTable(
                name: "Piranha_WorkflowStates",
                newName: "WorkflowStates");

            migrationBuilder.RenameTable(
                name: "Piranha_WorkflowDefinitions",
                newName: "WorkflowDefinitions");

            migrationBuilder.RenameIndex(
                name: "IX_Piranha_WorkflowTransitions_WorkflowDefinitionId",
                table: "WorkflowTransitions",
                newName: "IX_WorkflowTransitions_WorkflowDefinitionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowTransitions",
                table: "WorkflowTransitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStates",
                table: "WorkflowStates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_WorkflowDefinitionId",
                table: "WorkflowStates",
                column: "WorkflowDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStates_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowStates",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitions_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowTransitions",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
