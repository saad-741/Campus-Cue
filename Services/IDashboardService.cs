using CampusCue.ViewModels;

namespace CampusCue.Services;

public interface IDashboardService
{
    Task<AdminDashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<OrganizerDashboardViewModel> GetOrganizerDashboardAsync(string organizerId, CancellationToken cancellationToken = default);
    Task<StudentDashboardViewModel> GetStudentDashboardAsync(string studentUserId, CancellationToken cancellationToken = default);
}
