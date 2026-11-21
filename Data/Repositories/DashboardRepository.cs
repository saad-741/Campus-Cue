using CampusCue.Models.Enums;
using CampusCue.ViewModels;
using Dapper;

namespace CampusCue.Data.Repositories;

public sealed class DashboardRepository(IDbConnectionFactory connectionFactory) : IDashboardRepository
{
    public async Task<AdminDashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        const string countsSql = """
            SELECT
                (SELECT COUNT(1) FROM [AspNetUserRoles] ur INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId] WHERE r.[Name] = @StudentRole) AS [TotalStudents],
                (SELECT COUNT(1) FROM [AspNetUserRoles] ur INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId] WHERE r.[Name] = @OrganizerRole) AS [TotalOrganizers],
                (SELECT COUNT(1) FROM [Events]) AS [TotalEvents],
                (SELECT COUNT(1) FROM [Events] WHERE [Status] = 'Published') AS [PublishedEvents],
                (SELECT COUNT(1) FROM [Events] WHERE [Status] = 'Completed') AS [CompletedEvents],
                (SELECT COUNT(1) FROM [Events] WHERE [Status] = 'Cancelled') AS [CancelledEvents];
            """;
        await using var connection = connectionFactory.CreateConnection();
        var model = await connection.QuerySingleAsync<AdminDashboardViewModel>(new CommandDefinition(
            countsSql, new { StudentRole = "Student", OrganizerRole = "Organizer" }, cancellationToken: cancellationToken));
        model.RecentEvents = (await connection.QueryAsync<RecentEventItem>(new CommandDefinition("""
            SELECT TOP (5) e.[EventId], e.[Title], e.[Status], e.[EventDate], COALESCE(u.[FullName], 'Unknown') AS [OrganizerName]
            FROM [Events] e LEFT JOIN [AspNetUsers] u ON u.[Id] = e.[OrganizerId]
            ORDER BY e.[EventId] DESC;
            """, cancellationToken: cancellationToken))).AsList();
        model.RecentUsers = (await connection.QueryAsync<RecentUserItem>(new CommandDefinition("""
            SELECT TOP (5) [FullName], COALESCE([Email], '') AS [Email], TODATETIMEOFFSET([CreatedAt], '+00:00') AS [CreatedOn]
            FROM [AspNetUsers] ORDER BY [CreatedAt] DESC;
            """, cancellationToken: cancellationToken))).AsList();
        return model;
    }

    public async Task<OrganizerDashboardViewModel> GetOrganizerDashboardAsync(string organizerId, CancellationToken cancellationToken = default)
    {
        const string countsSql = """
            SELECT
                COUNT(1) AS [TotalEvents],
                COALESCE(SUM(CASE WHEN [EventDate] >= @Today AND [Status] = 'Published' THEN 1 ELSE 0 END), 0) AS [UpcomingEvents],
                COALESCE(SUM(CASE WHEN [Status] = 'Published' THEN 1 ELSE 0 END), 0) AS [PublishedEvents],
                COALESCE(SUM(CASE WHEN [Status] = 'Completed' THEN 1 ELSE 0 END), 0) AS [CompletedEvents]
            FROM [Events] WHERE [OrganizerId] = @OrganizerId;
            """;
        await using var connection = connectionFactory.CreateConnection();
        var model = await connection.QuerySingleAsync<OrganizerDashboardViewModel>(new CommandDefinition(
            countsSql, new { OrganizerId = organizerId, Today = DateOnly.FromDateTime(DateTime.Now) }, cancellationToken: cancellationToken));
        model.ActiveRegistrations = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM [EventRegistrations] r INNER JOIN [Events] e ON e.[EventId] = r.[EventId]
            WHERE e.[OrganizerId] = @OrganizerId AND r.[Status] = 'Registered';
            """, new { OrganizerId = organizerId }, cancellationToken: cancellationToken));
        model.AttendanceByEvent = (await connection.QueryAsync<EventAttendanceSummary>(new CommandDefinition("""
            SELECT e.[EventId], e.[Title],
                SUM(CASE WHEN r.[Status] = 'Registered' AND a.[Status] = 'Present' THEN 1 ELSE 0 END) AS [Present],
                SUM(CASE WHEN r.[Status] = 'Registered' AND a.[Status] = 'Absent' THEN 1 ELSE 0 END) AS [Absent],
                SUM(CASE WHEN r.[Status] = 'Registered' AND (a.[Status] IS NULL OR a.[Status] = 'NotMarked') THEN 1 ELSE 0 END) AS [NotMarked]
            FROM [Events] e LEFT JOIN [EventRegistrations] r ON r.[EventId] = e.[EventId]
            LEFT JOIN [Attendances] a ON a.[RegistrationId] = r.[RegistrationId]
            WHERE e.[OrganizerId] = @OrganizerId GROUP BY e.[EventId], e.[Title] ORDER BY MIN(e.[EventDate]);
            """, new { OrganizerId = organizerId }, cancellationToken: cancellationToken))).AsList();
        model.RecentRegistrations = (await connection.QueryAsync<RecentRegistrationItem>(new CommandDefinition("""
            SELECT TOP (8) COALESCE(u.[FullName], 'Unknown') AS [StudentName], COALESCE(e.[Title], 'Unknown') AS [EventTitle],
                r.[RegistrationDate], r.[Status]
            FROM [EventRegistrations] r LEFT JOIN [Events] e ON e.[EventId] = r.[EventId]
            LEFT JOIN [AspNetUsers] u ON u.[Id] = r.[StudentId]
            WHERE e.[OrganizerId] = @OrganizerId ORDER BY r.[RegistrationDate] DESC;
            """, new { OrganizerId = organizerId }, cancellationToken: cancellationToken))).AsList();
        return model;
    }

    public async Task<StudentDashboardViewModel> GetStudentDashboardAsync(string studentUserId, CancellationToken cancellationToken = default)
    {
        const string countsSql = """
            SELECT
                (SELECT COUNT(1) FROM [EventRegistrations] WHERE [StudentId] = @StudentId) AS [TotalRegistrations],
                (SELECT COUNT(1) FROM [EventRegistrations] r INNER JOIN [Attendances] a ON a.[RegistrationId] = r.[RegistrationId] WHERE r.[StudentId] = @StudentId AND a.[Status] = 'Present') AS [AttendedCount],
                (SELECT COUNT(1) FROM [EventRegistrations] WHERE [StudentId] = @StudentId AND [Status] = 'Cancelled') AS [CancelledCount];
            """;
        await using var connection = connectionFactory.CreateConnection();
        var model = await connection.QuerySingleAsync<StudentDashboardViewModel>(new CommandDefinition(
            countsSql, new { StudentId = studentUserId }, cancellationToken: cancellationToken));
        model.UpcomingEvents = (await connection.QueryAsync<EventListItemViewModel>(new CommandDefinition("""
            SELECT TOP (5) e.[EventId], e.[Title], e.[Description], COALESCE(c.[Name], '') AS [CategoryName], e.[EventDate],
                e.[StartTime], e.[EndTime], e.[Venue], e.[Capacity],
                (SELECT COUNT(1) FROM [EventRegistrations] r WHERE r.[EventId] = e.[EventId] AND r.[Status] = 'Registered') AS [ActiveRegistrations],
                e.[Status]
            FROM [Events] e LEFT JOIN [EventCategories] c ON c.[CategoryId] = e.[CategoryId]
            WHERE e.[Status] = 'Published' AND e.[EventDate] >= @Today
            ORDER BY e.[EventDate], e.[StartTime];
            """, new { Today = DateOnly.FromDateTime(DateTime.Now) }, cancellationToken: cancellationToken))).AsList();
        model.MyRegistrations = (await connection.QueryAsync<StudentRegistrationItem>(new CommandDefinition("""
            SELECT TOP (8) r.[RegistrationId], r.[EventId], COALESCE(e.[Title], '') AS [EventTitle],
                COALESCE(e.[EventDate], CONVERT(date, '0001-01-01')) AS [EventDate], COALESCE(e.[Venue], '') AS [Venue],
                r.[Status] AS [RegistrationStatus], COALESCE(a.[Status], 'NotMarked') AS [AttendanceStatus]
            FROM [EventRegistrations] r LEFT JOIN [Events] e ON e.[EventId] = r.[EventId]
            LEFT JOIN [Attendances] a ON a.[RegistrationId] = r.[RegistrationId]
            WHERE r.[StudentId] = @StudentId ORDER BY r.[RegistrationDate] DESC;
            """, new { StudentId = studentUserId }, cancellationToken: cancellationToken))).AsList();
        return model;
    }
}
