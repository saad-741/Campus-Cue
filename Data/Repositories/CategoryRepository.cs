using CampusCue.Common;
using CampusCue.Models;
using Dapper;

namespace CampusCue.Data.Repositories;

public sealed class CategoryRepository(IDbConnectionFactory connectionFactory) : ICategoryRepository
{
    private const string Columns = "[CategoryId], [Name], [Description], [IsActive]";

    public async Task<PagedResult<EventCategory>> SearchAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        await using var connection = connectionFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [EventCategories] WHERE (@Term IS NULL OR [Name] LIKE @Term OR [Description] LIKE @Term)",
            new { Term = term }, cancellationToken: cancellationToken));
        var categories = await connection.QueryAsync<EventCategory>(new CommandDefinition(
            $"SELECT {Columns} FROM [EventCategories] WHERE (@Term IS NULL OR [Name] LIKE @Term OR [Description] LIKE @Term) ORDER BY [Name] OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Term = term, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));

        return new PagedResult<EventCategory> { Items = categories.AsList(), PageNumber = pageNumber, PageSize = pageSize, TotalCount = total };
    }

    public async Task<EventCategory?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EventCategory>(new CommandDefinition(
            $"SELECT {Columns} FROM [EventCategories] WHERE [CategoryId] = @CategoryId", new { CategoryId = categoryId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsByNameAsync(string name, int? exceptCategoryId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM [EventCategories] WHERE [Name] = @Name AND (@ExceptCategoryId IS NULL OR [CategoryId] <> @ExceptCategoryId)) THEN 1 ELSE 0 END",
            new { Name = name, ExceptCategoryId = exceptCategoryId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(EventCategory category, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO [EventCategories] ([Name], [Description], [IsActive]) OUTPUT INSERTED.[CategoryId] VALUES (@Name, @Description, @IsActive)",
            category, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(EventCategory category, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [EventCategories] SET [Name] = @Name, [Description] = @Description, [IsActive] = @IsActive WHERE [CategoryId] = @CategoryId",
            category, cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task<(bool Found, bool HasEvents)> HasEventsAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM [EventCategories] WHERE [CategoryId] = @CategoryId) THEN 1 ELSE 0 END", new { CategoryId = categoryId }, cancellationToken: cancellationToken));
        var hasEvents = exists && await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM [Events] WHERE [CategoryId] = @CategoryId) THEN 1 ELSE 0 END", new { CategoryId = categoryId }, cancellationToken: cancellationToken));
        return (exists, hasEvents);
    }

    public async Task<bool> DeleteAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [EventCategories] WHERE [CategoryId] = @CategoryId", new { CategoryId = categoryId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> SetActiveAsync(int categoryId, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [EventCategories] SET [IsActive] = @IsActive WHERE [CategoryId] = @CategoryId", new { CategoryId = categoryId, IsActive = isActive }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<EventCategory>> GetForEventFormAsync(int? includeCategoryId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var categories = await connection.QueryAsync<EventCategory>(new CommandDefinition(
            $"SELECT {Columns} FROM [EventCategories] WHERE [IsActive] = 1 OR [CategoryId] = @IncludeCategoryId ORDER BY [Name]",
            new { IncludeCategoryId = includeCategoryId }, cancellationToken: cancellationToken));
        return categories.AsList();
    }

    public async Task<IReadOnlyList<EventCategory>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var categories = await connection.QueryAsync<EventCategory>(new CommandDefinition(
            $"SELECT {Columns} FROM [EventCategories] WHERE [IsActive] = 1 ORDER BY [Name]", cancellationToken: cancellationToken));
        return categories.AsList();
    }
}
