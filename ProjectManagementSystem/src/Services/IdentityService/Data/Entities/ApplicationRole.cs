using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.IdentityService.Data.Entities;

[Table("AspNetRoles")]
public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole() : base()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}