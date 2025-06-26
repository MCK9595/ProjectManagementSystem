namespace ProjectManagementSystem.Shared.Common.Constants;

public static class Roles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string OrganizationOwner = "OrganizationOwner";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string OrganizationMember = "OrganizationMember";
    public const string ProjectManager = "ProjectManager";
    public const string ProjectMember = "ProjectMember";

    public static readonly string[] AllRoles = {
        SystemAdmin,
        OrganizationOwner,
        OrganizationAdmin,
        OrganizationMember,
        ProjectManager,
        ProjectMember
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