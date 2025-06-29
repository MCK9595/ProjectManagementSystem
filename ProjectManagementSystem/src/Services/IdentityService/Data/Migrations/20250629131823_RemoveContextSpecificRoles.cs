using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagementSystem.IdentityService.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContextSpecificRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove context-specific roles that should be managed in respective services
            var contextRoles = new[]
            {
                "OrganizationOwner",
                "OrganizationAdmin", 
                "OrganizationMember",
                "ProjectManager",
                "ProjectMember"
            };

            foreach (var roleName in contextRoles)
            {
                migrationBuilder.Sql($"DELETE FROM \"AspNetRoles\" WHERE \"Name\" = '{roleName}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-create context-specific roles if rollback is needed
            var contextRoles = new[]
            {
                "OrganizationOwner",
                "OrganizationAdmin", 
                "OrganizationMember",
                "ProjectManager",
                "ProjectMember"
            };

            foreach (var roleName in contextRoles)
            {
                migrationBuilder.Sql($@"
                    INSERT INTO ""AspNetRoles"" (""Id"", ""Name"", ""NormalizedName"", ""ConcurrencyStamp"")
                    VALUES ('{Guid.NewGuid()}', '{roleName}', '{roleName.ToUpper()}', '{Guid.NewGuid()}');
                ");
            }
        }
    }
}
