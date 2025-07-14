namespace ProjectManagementSystem.Shared.Client.Constants;

public static class Roles
{
    // System-level roles
    public const string SystemAdmin = "SystemAdmin";
    
    // Organization roles
    public const string OrganizationOwner = "OrganizationOwner";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string OrganizationMember = "OrganizationMember";
    
    // Legacy organization roles (for backward compatibility)
    public const string OrgAdmin = "OrgAdmin";
    public const string OrgMember = "OrgMember";
    
    // Project roles
    public const string ProjectManager = "ProjectManager";
    public const string ProjectMember = "ProjectMember";
    public const string ProjectAdmin = "ProjectAdmin";
    public const string ProjectEditor = "ProjectEditor";
    public const string ProjectViewer = "ProjectViewer";
}