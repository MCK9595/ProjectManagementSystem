namespace ProjectManagementSystem.Shared.Models.DTOs;

public class ProjectDashboardStatsDto
{
    public StatusBreakdownDto StatusBreakdown { get; set; } = new();
    public PriorityBreakdownDto PriorityBreakdown { get; set; } = new();
    public int OverdueTasksCount { get; set; }
    public int TodayDueTasksCount { get; set; }
    public decimal CompletionRate { get; set; }
    public List<RecentTaskActivityDto> RecentActivities { get; set; } = new();
    
    // 具体的なタスクリスト
    public List<DashboardTaskDto> TodayDueTasks { get; set; } = new();
    public List<DashboardTaskDto> OverdueTasks { get; set; } = new();
    public List<DashboardTaskDto> ActiveTasksInPeriod { get; set; } = new();
    public List<DashboardTaskDto> UserAssignedTasks { get; set; } = new();
}

public class StatusBreakdownDto
{
    public int TodoCount { get; set; }
    public int InProgressCount { get; set; }
    public int InReviewCount { get; set; }
    public int DoneCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalCount { get; set; }
}

public class PriorityBreakdownDto
{
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int TotalCount { get; set; }
}

public class RecentTaskActivityDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string ActivityType { get; set; } = string.Empty; // "Created" or "Updated"
    public DateTime ActivityDate { get; set; }
    public string? AssignedToUserName { get; set; }
}

public class DashboardTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public string? AssignedToUserName { get; set; }
    public int? AssignedToUserId { get; set; }
    public int DaysOverdue { get; set; } // 遅延日数（遅延タスクの場合）
}