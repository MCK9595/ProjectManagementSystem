namespace ProjectManagementSystem.WebApp.Wasm.Client.Constants;

public static class ProjectStatus
{
    public const string Planning = "Planning";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string OnHold = "OnHold";
    public const string Cancelled = "Cancelled";

    public static readonly string[] AllStatuses = { Planning, Active, Completed, OnHold, Cancelled };
}