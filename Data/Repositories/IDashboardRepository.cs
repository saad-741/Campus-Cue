using CampusCue.ViewModels;

namespace CampusCue.Data.Repositories;

public interface IDashboardRepository
{
    Task<AdminDashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<OrganizerDashboardViewModel> GetOrganizerDashboardAsync(string organizerId, CancellationToken cancellationToken = default);
    Task<StudentDashboardViewModel> GetStudentDashboardAsync(string studentUserId, CancellationToken cancellationToken = default);
}
