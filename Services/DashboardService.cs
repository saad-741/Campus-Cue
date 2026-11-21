using CampusCue.Data.Repositories;
using CampusCue.ViewModels;

namespace CampusCue.Services;

public sealed class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    public Task<AdminDashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
        => dashboardRepository.GetAdminDashboardAsync(cancellationToken);

    public Task<OrganizerDashboardViewModel> GetOrganizerDashboardAsync(string organizerId, CancellationToken cancellationToken = default)
        => dashboardRepository.GetOrganizerDashboardAsync(organizerId, cancellationToken);

    public Task<StudentDashboardViewModel> GetStudentDashboardAsync(string studentUserId, CancellationToken cancellationToken = default)
        => dashboardRepository.GetStudentDashboardAsync(studentUserId, cancellationToken);
}
