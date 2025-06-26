namespace ProjectManagementSystem.Shared.Common.Constants;

public static class ProjectStatus
{
    public const string Planning = "Planning";
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] AllStatuses = {
        Planning,
        Active,
        OnHold,
        Completed,
        Cancelled
    };
}

public static class TaskStatus
{
    public const string ToDo = "ToDo";
    public const string InProgress = "InProgress";
    public const string InReview = "InReview";
    public const string Done = "Done";
    public const string Cancelled = "Cancelled";

    public static readonly string[] AllStatuses = {
        ToDo,
        InProgress,
        InReview,
        Done,
        Cancelled
    };
}

public static class Priority
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly string[] AllPriorities = {
        Low,
        Medium,
        High,
        Critical
    };
}