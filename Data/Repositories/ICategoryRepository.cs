using CampusCue.Common;
using CampusCue.Models;

namespace CampusCue.Data.Repositories;

public interface ICategoryRepository
{
    Task<PagedResult<EventCategory>> SearchAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<EventCategory?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, int? exceptCategoryId = null, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(EventCategory category, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(EventCategory category, CancellationToken cancellationToken = default);
    Task<(bool Found, bool HasEvents)> HasEventsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(int categoryId, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventCategory>> GetForEventFormAsync(int? includeCategoryId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventCategory>> GetActiveAsync(CancellationToken cancellationToken = default);
}
