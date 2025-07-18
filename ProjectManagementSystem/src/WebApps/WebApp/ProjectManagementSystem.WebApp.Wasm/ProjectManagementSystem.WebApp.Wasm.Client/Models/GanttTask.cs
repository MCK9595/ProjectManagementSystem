namespace ProjectManagementSystem.WebApp.Wasm.Client.Models
{
    public class GanttTask
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Progress { get; set; } = 0;
        public List<string> Dependencies { get; set; } = new();
        public bool CustomClass { get; set; } = false;
        
        // Additional properties for integration with our task system
        public Guid TaskId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public Guid? ParentTaskId { get; set; }
        
        // Flag to indicate if this task has valid start/end dates
        public bool HasDates { get; set; } = true;
    }
}