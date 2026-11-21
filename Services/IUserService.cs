using CampusCue.Common;
using CampusCue.ViewModels;

namespace CampusCue.Services;

public interface IUserService
{
    Task<PagedResult<UserListItemViewModel>> SearchUsersAsync(
        string? search,
        string? role,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserDetailsViewModel?> GetDetailsAsync(string userId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> CreateOrganizerAsync(CreateOrganizerViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> AssignOrganizerAsync(string userId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> SetActiveAsync(string userId, bool isActive, string currentUserId, CancellationToken cancellationToken = default);
}
