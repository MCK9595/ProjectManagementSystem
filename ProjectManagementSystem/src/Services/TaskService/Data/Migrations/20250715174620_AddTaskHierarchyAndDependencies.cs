using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagementSystem.TaskService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskHierarchyAndDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_task_id",
                table: "tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_dependencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    task_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dependent_on_task_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_dependent_on_task_id",
                        column: x => x.dependent_on_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ParentTaskId",
                table: "tasks",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_DependentOnTaskId",
                table: "task_dependencies",
                column: "dependent_on_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId",
                table: "task_dependencies",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId_DependentOnTaskId",
                table: "task_dependencies",
                columns: new[] { "task_id", "dependent_on_task_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_tasks_parent_task_id",
                table: "tasks",
                column: "parent_task_id",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_tasks_parent_task_id",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_dependencies");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ParentTaskId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "parent_task_id",
                table: "tasks");
        }
    }
}
