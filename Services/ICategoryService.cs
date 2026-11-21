using CampusCue.Common;
using CampusCue.Models;

namespace CampusCue.Services;

public interface ICategoryService
{
    Task<PagedResult<EventCategory>> SearchAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<EventCategory?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> CreateAsync(EventCategory category, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> UpdateAsync(EventCategory category, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error, bool DeactivatedInstead)> DeleteOrDeactivateAsync(
        int categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventCategory>> GetForEventFormAsync(int? includeCategoryId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventCategory>> GetActiveAsync(CancellationToken cancellationToken = default);
}
