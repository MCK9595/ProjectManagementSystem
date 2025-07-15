namespace ProjectManagementSystem.WebApp.Wasm.Client.Constants;

public static class TaskStatus
{
    public const string ToDo = "ToDo";
    public const string InProgress = "InProgress";
    public const string InReview = "InReview";
    public const string Done = "Done";

    public static readonly string[] AllStatuses = { ToDo, InProgress, InReview, Done };
}