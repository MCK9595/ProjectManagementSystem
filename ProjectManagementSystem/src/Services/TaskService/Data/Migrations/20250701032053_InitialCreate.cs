using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagementSystem.TaskService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    task_number = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    due_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    assigned_to_user_id = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    estimated_hours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    actual_hours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    task_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_comments_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_TaskId",
                table: "comments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "tasks",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Priority",
                table: "tasks",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_TaskNumber",
                table: "tasks",
                columns: new[] { "project_id", "task_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                table: "tasks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "tasks");
        }
    }
}
