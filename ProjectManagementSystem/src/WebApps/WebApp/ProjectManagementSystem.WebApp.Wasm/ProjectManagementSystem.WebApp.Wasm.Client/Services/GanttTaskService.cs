using ProjectManagementSystem.WebApp.Wasm.Client.Models;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Client.Constants;
using ProjectManagementSystem.WebApp.Wasm.Client.Constants;

namespace ProjectManagementSystem.WebApp.Wasm.Client.Services
{
    public class GanttTaskService
    {
        public List<GanttTask> ConvertToGanttTasks(List<TaskDto> tasks)
        {
            var ganttTasks = new List<GanttTask>();
            
            foreach (var task in tasks)
            {
                var ganttTask = new GanttTask
                {
                    Id = task.Id.ToString(),
                    Name = task.Title,
                    Start = task.StartDate ?? task.CreatedAt,
                    End = task.DueDate ?? task.CreatedAt.AddDays(1),
                    Progress = CalculateProgress(task.Status),
                    TaskId = task.Id,
                    Status = task.Status,
                    Priority = task.Priority,
                    AssignedTo = task.AssignedTo?.FirstName + " " + task.AssignedTo?.LastName,
                    ParentTaskId = task.ParentTaskId,
                    Dependencies = ConvertDependencies(task.DependsOnTaskIds)
                };

                ganttTasks.Add(ganttTask);
            }

            return ganttTasks;
        }

        public TaskDto ConvertToTaskDto(GanttTask ganttTask)
        {
            return new TaskDto
            {
                Id = ganttTask.TaskId,
                Title = ganttTask.Name,
                StartDate = ganttTask.Start,
                DueDate = ganttTask.End,
                Status = ganttTask.Status,
                Priority = ganttTask.Priority,
                ParentTaskId = ganttTask.ParentTaskId,
                // Note: Other properties would need to be populated from the original TaskDto
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ProjectId = Guid.Empty, // Would need to be set from context
                CreatedByUserId = 0, // Would need to be set from context
                AssignedToUserId = null // Would need to be resolved from AssignedTo name
            };
        }

        public List<GanttTask> CalculateTaskSchedule(List<TaskDto> tasks)
        {
            var ganttTasks = ConvertToGanttTasks(tasks);
            var taskDict = ganttTasks.ToDictionary(t => t.Id);

            // Calculate start dates based on dependencies
            foreach (var task in ganttTasks)
            {
                if (task.Dependencies.Any())
                {
                    var maxEndDate = DateTime.MinValue;
                    
                    foreach (var depId in task.Dependencies)
                    {
                        if (taskDict.ContainsKey(depId))
                        {
                            var depTask = taskDict[depId];
                            if (depTask.End > maxEndDate)
                            {
                                maxEndDate = depTask.End;
                            }
                        }
                    }

                    if (maxEndDate != DateTime.MinValue)
                    {
                        // Start the task the day after the last dependency ends
                        task.Start = maxEndDate.AddDays(1);
                        
                        // Preserve the original duration
                        var originalDuration = task.End - task.Start;
                        task.End = task.Start.Add(originalDuration);
                    }
                }
            }

            return ganttTasks;
        }

        private List<string> ConvertDependencies(ICollection<Guid> dependsOnTaskIds)
        {
            return dependsOnTaskIds?.Select(id => id.ToString()).ToList() ?? new List<string>();
        }

        private int CalculateProgress(string status)
        {
            return status switch
            {
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.ToDo => 0,
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.InProgress => 50,
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.InReview => 75,
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.Done => 100,
                _ => 0
            };
        }

        public string GetTaskColor(string priority)
        {
            return priority switch
            {
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.Priority.Low => "#28a745",      // Green
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.Priority.Medium => "#ffc107",   // Yellow
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.Priority.High => "#fd7e14",     // Orange
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.Priority.Critical => "#dc3545", // Red
                _ => "#6c757d"                  // Gray
            };
        }

        public string GetStatusColor(string status)
        {
            return status switch
            {
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.ToDo => "#6c757d",      // Gray
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.InProgress => "#007bff", // Blue
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.InReview => "#ffc107",   // Yellow
                ProjectManagementSystem.WebApp.Wasm.Client.Constants.TaskStatus.Done => "#28a745",       // Green
                _ => "#6c757d"                      // Gray
            };
        }

        public List<GanttTask> FilterTasksByDateRange(List<GanttTask> tasks, DateTime startDate, DateTime endDate)
        {
            return tasks.Where(t => 
                t.Start >= startDate && t.End <= endDate ||
                t.Start <= startDate && t.End >= startDate ||
                t.Start <= endDate && t.End >= endDate
            ).ToList();
        }

        public List<GanttTask> SortTasksByHierarchy(List<GanttTask> tasks)
        {
            var sortedTasks = new List<GanttTask>();
            var taskDict = tasks.ToDictionary(t => t.TaskId);
            var processed = new HashSet<Guid>();

            // Process parent tasks first
            foreach (var task in tasks.Where(t => t.ParentTaskId == null))
            {
                AddTaskWithChildren(task, taskDict, sortedTasks, processed);
            }

            // Add any remaining tasks (orphaned)
            foreach (var task in tasks.Where(t => !processed.Contains(t.TaskId)))
            {
                sortedTasks.Add(task);
            }

            return sortedTasks;
        }

        private void AddTaskWithChildren(GanttTask task, Dictionary<Guid, GanttTask> taskDict, 
            List<GanttTask> sortedTasks, HashSet<Guid> processed)
        {
            if (processed.Contains(task.TaskId))
                return;

            sortedTasks.Add(task);
            processed.Add(task.TaskId);

            // Add child tasks
            var childTasks = taskDict.Values.Where(t => t.ParentTaskId == task.TaskId).ToList();
            foreach (var childTask in childTasks)
            {
                AddTaskWithChildren(childTask, taskDict, sortedTasks, processed);
            }
        }
    }
}