using CampusCue.Common;
using CampusCue.Data.Repositories;
using CampusCue.Models;

namespace CampusCue.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;

    public CategoryService(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<PagedResult<EventCategory>> SearchAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _categories.SearchAsync(search, pageNumber, pageSize, cancellationToken);
    }

    public Task<EventCategory?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default)
        => _categories.GetByIdAsync(categoryId, cancellationToken);

    public async Task<(bool Succeeded, string Error)> CreateAsync(
        EventCategory category,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            return (false, "Category name is required.");
        }

        if (await _categories.ExistsByNameAsync(category.Name, cancellationToken: cancellationToken))
        {
            return (false, "A category with this name already exists.");
        }

        category.IsActive = true;
        category.CategoryId = await _categories.CreateAsync(category, cancellationToken);
        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> UpdateAsync(
        EventCategory category,
        CancellationToken cancellationToken = default)
    {
        var existing = await _categories.GetByIdAsync(category.CategoryId, cancellationToken);

        if (existing is null)
        {
            return (false, "Category not found.");
        }

        if (await _categories.ExistsByNameAsync(category.Name, category.CategoryId, cancellationToken))
        {
            return (false, "A category with this name already exists.");
        }

        return await _categories.UpdateAsync(category, cancellationToken)
            ? (true, string.Empty)
            : (false, "Category not found.");
    }

    public async Task<(bool Succeeded, string Error, bool DeactivatedInstead)> DeleteOrDeactivateAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var (found, hasEvents) = await _categories.HasEventsAsync(categoryId, cancellationToken);
        if (!found)
        {
            return (false, "Category not found.", false);
        }

        if (hasEvents)
        {
            await _categories.SetActiveAsync(categoryId, false, cancellationToken);
            return (true, "This category has existing events, so it was deactivated instead of deleted.", true);
        }

        await _categories.DeleteAsync(categoryId, cancellationToken);
        return (true, string.Empty, false);
    }

    public async Task<IReadOnlyList<EventCategory>> GetForEventFormAsync(
        int? includeCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        return await _categories.GetForEventFormAsync(includeCategoryId, cancellationToken);
    }

    public async Task<IReadOnlyList<EventCategory>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _categories.GetActiveAsync(cancellationToken);
}
