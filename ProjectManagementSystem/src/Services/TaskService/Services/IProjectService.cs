using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.TaskService.Services;

public interface IProjectService
{
    Task<ProjectDto?> GetProjectAsync(Guid projectId);
}