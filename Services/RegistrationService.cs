using System.Data;
using CampusCue.Common;
using CampusCue.Data;
using CampusCue.Models;
using CampusCue.Models.Enums;
using CampusCue.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CampusCue.Services;

public class RegistrationService : IRegistrationService
{
    private readonly ApplicationDbContext _db;

    public RegistrationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Succeeded, string Error)> RegisterAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var ev = await _db.Events
                .FromSqlInterpolated($"SELECT * FROM [Events] WITH (UPDLOCK, HOLDLOCK) WHERE [EventId] = {eventId}")
                .SingleOrDefaultAsync(cancellationToken);

            if (ev is null)
            {
                return (false, "Event not found.");
            }

            if (ev.Status != EventStatus.Published)
            {
                return (false, "Registration is only available for published events.");
            }

            if (DateTime.Now > ev.RegistrationDeadline)
            {
                return (false, "The registration deadline has passed.");
            }

            var existing = await _db.EventRegistrations
                .Include(r => r.Attendance)
                .FirstOrDefaultAsync(
                    r => r.EventId == eventId && r.StudentId == studentUserId,
                    cancellationToken);

            if (existing is { Status: RegistrationStatus.Registered })
            {
                return (false, "You are already registered for this event.");
            }

            var activeCount = await _db.EventRegistrations.CountAsync(
                r => r.EventId == eventId && r.Status == RegistrationStatus.Registered,
                cancellationToken);

            if (activeCount >= ev.Capacity)
            {
                return (false, "This event is full.");
            }

            if (existing is { Status: RegistrationStatus.Cancelled })
            {
                existing.Status = RegistrationStatus.Registered;
                existing.RegistrationDate = DateTime.UtcNow;
                EnsureAttendance(existing);
            }
            else
            {
                _db.EventRegistrations.Add(new EventRegistration
                {
                    EventId = eventId,
                    StudentId = studentUserId,
                    RegistrationDate = DateTime.UtcNow,
                    Status = RegistrationStatus.Registered,
                    Attendance = new Attendance { Status = AttendanceStatus.NotMarked }
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, string.Empty);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, "Unable to complete registration. The event may be full or you may already be registered.");
        }
    }

    public async Task<(bool Succeeded, string Error)> CancelAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _db.EventRegistrations
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

        if (registration is null)
        {
            return (false, "Registration not found.");
        }

        if (registration.StudentId != studentUserId)
        {
            return (false, "You are not authorized to cancel this registration.");
        }

        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return (false, "This registration is already cancelled.");
        }

        registration.Status = RegistrationStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    public async Task<PagedResult<EventRegistration>> GetStudentRegistrationsAsync(
        string studentUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e!.Category)
            .Include(r => r.Attendance)
            .Where(r => r.StudentId == studentUserId)
            .OrderByDescending(r => r.RegistrationDate);

        return await PagedResult<EventRegistration>.CreateAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<PagedResult<EventRegistration>> GetEventRegistrationsAsync(
        int eventId,
        string actorUserId,
        bool isAdmin,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        if (ev is null)
        {
            return new PagedResult<EventRegistration>
            {
                Items = [],
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        if (!isAdmin && ev.OrganizerId != actorUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view these registrations.");
        }

        var query = _db.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.Attendance)
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Registered)
            .OrderBy(r => r.Student!.FullName);

        return await PagedResult<EventRegistration>.CreateAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<(bool Succeeded, string Error)> MarkAttendanceAsync(
        int registrationId,
        AttendanceStatus status,
        string actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var registration = await _db.EventRegistrations
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);

        if (registration is null)
        {
            return (false, "Registration not found.");
        }

        var error = ValidateAttendanceChange(registration, status, actorUserId, isAdmin);
        if (error is not null)
        {
            return (false, error);
        }

        EnsureAttendance(registration);
        registration.Attendance!.Status = status;
        registration.Attendance.MarkedAt = status == AttendanceStatus.NotMarked ? null : DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> MarkAttendanceBatchAsync(
        int eventId,
        IReadOnlyList<(int RegistrationId, AttendanceStatus Status)> items,
        string actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (ev is null)
        {
            return (false, "Event not found.");
        }

        if (!isAdmin && ev.OrganizerId != actorUserId)
        {
            return (false, "You are not authorized to mark attendance for this event.");
        }

        var registrations = await _db.EventRegistrations
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Where(r => r.EventId == eventId && items.Select(i => i.RegistrationId).Contains(r.RegistrationId))
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var registration = registrations.FirstOrDefault(r => r.RegistrationId == item.RegistrationId);
            if (registration is null)
            {
                continue;
            }

            var error = ValidateAttendanceChange(registration, item.Status, actorUserId, isAdmin);
            if (error is not null)
            {
                return (false, error);
            }

            EnsureAttendance(registration);
            registration.Attendance!.Status = item.Status;
            registration.Attendance.MarkedAt = item.Status == AttendanceStatus.NotMarked ? null : DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    public Task<EventRegistration?> GetStudentRegistrationForEventAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default)
        => _db.EventRegistrations
            .Include(r => r.Attendance)
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.StudentId == studentUserId, cancellationToken);

    public async Task<EventAttendanceSummary> GetAttendanceSummaryAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        var registrations = _db.EventRegistrations.Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Registered);

        return new EventAttendanceSummary
        {
            EventId = eventId,
            Title = ev?.Title ?? string.Empty,
            Present = await registrations.CountAsync(r => r.Attendance != null && r.Attendance.Status == AttendanceStatus.Present, cancellationToken),
            Absent = await registrations.CountAsync(r => r.Attendance != null && r.Attendance.Status == AttendanceStatus.Absent, cancellationToken),
            NotMarked = await registrations.CountAsync(r => r.Attendance == null || r.Attendance.Status == AttendanceStatus.NotMarked, cancellationToken)
        };
    }

    private static string? ValidateAttendanceChange(
        EventRegistration registration,
        AttendanceStatus status,
        string actorUserId,
        bool isAdmin)
    {
        if (!Enum.IsDefined(status))
        {
            return "The selected attendance status is invalid.";
        }

        if (!isAdmin && registration.Event?.OrganizerId != actorUserId)
        {
            return "You are not authorized to mark attendance for this event.";
        }

        if (registration.Status != RegistrationStatus.Registered)
        {
            return "Attendance can only be marked for active registrations.";
        }

        if (status != AttendanceStatus.NotMarked && registration.Event is not null)
        {
            var eventEnd = registration.Event.EventDate.ToDateTime(registration.Event.EndTime);
            if (DateTime.Now < eventEnd)
            {
                return "Attendance can only be finalized after the event has taken place.";
            }
        }

        return null;
    }

    private static void EnsureAttendance(EventRegistration registration)
    {
        registration.Attendance ??= new Attendance
        {
            Status = AttendanceStatus.NotMarked
        };
    }
}
