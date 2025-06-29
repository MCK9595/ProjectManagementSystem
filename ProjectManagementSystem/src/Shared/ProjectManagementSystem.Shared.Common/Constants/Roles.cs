namespace ProjectManagementSystem.Shared.Common.Constants;

public static class Roles
{
    // Global roles (assigned to users directly in IdentityService)
    public const string SystemAdmin = "SystemAdmin";
    public const string User = "User";

    // Context-specific roles (managed in respective services)
    // Organization roles (managed in OrganizationService)
    public const string OrganizationOwner = "OrganizationOwner";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string OrganizationMember = "OrganizationMember";
    
    // Project roles (managed in ProjectService)
    public const string ProjectManager = "ProjectManager";
    public const string ProjectMember = "ProjectMember";

    // Global roles that can be assigned during user creation/editing
    public static readonly string[] GlobalRoles = {
        SystemAdmin,
        User
    };

    public static readonly string[] OrganizationRoles = {
        OrganizationOwner,
        OrganizationAdmin,
        OrganizationMember
    };

    public static readonly string[] ProjectRoles = {
        ProjectManager,
        ProjectMember
    };
}