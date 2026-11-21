using CampusCue.Common;
using CampusCue.Models;
using CampusCue.Models.Enums;
using CampusCue.ViewModels;

namespace CampusCue.Services;

public interface IRegistrationService
{
    Task<(bool Succeeded, string Error)> RegisterAsync(int eventId, string studentUserId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> CancelAsync(int registrationId, string studentUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<EventRegistration>> GetStudentRegistrationsAsync(
        string studentUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<EventRegistration>> GetEventRegistrationsAsync(
        int eventId,
        string actorUserId,
        bool isAdmin,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> MarkAttendanceAsync(
        int registrationId,
        AttendanceStatus status,
        string actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> MarkAttendanceBatchAsync(
        int eventId,
        IReadOnlyList<(int RegistrationId, AttendanceStatus Status)> items,
        string actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<EventRegistration?> GetStudentRegistrationForEventAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    Task<EventAttendanceSummary> GetAttendanceSummaryAsync(int eventId, CancellationToken cancellationToken = default);
}
