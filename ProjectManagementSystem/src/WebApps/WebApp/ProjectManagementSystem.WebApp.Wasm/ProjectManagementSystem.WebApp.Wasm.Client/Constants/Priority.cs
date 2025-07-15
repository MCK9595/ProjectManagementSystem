namespace ProjectManagementSystem.WebApp.Wasm.Client.Constants;

public static class Priority
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static readonly string[] AllPriorities = { Critical, High, Medium, Low };
}