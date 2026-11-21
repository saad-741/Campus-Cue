using CampusCue.Common;
using CampusCue.Data;
using CampusCue.Models;
using CampusCue.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CampusCue.Services;

public class EventService : IEventService
{
    private readonly ApplicationDbContext _db;

    public EventService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Event>> SearchPublishedAsync(
        string? search,
        int? categoryId,
        bool upcomingOnly,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Include(e => e.Registrations.Where(r => r.Status == RegistrationStatus.Registered))
            .Where(e => e.Status == EventStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.Title.Contains(term) ||
                e.Description.Contains(term) ||
                e.Venue.Contains(term) ||
                (e.Category != null && e.Category.Name.Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        if (upcomingOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            query = query.Where(e => e.EventDate >= today);
        }

        query = query.OrderBy(e => e.EventDate).ThenBy(e => e.StartTime);

        return await PagedResult<Event>.CreateAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<Event?> GetByIdAsync(int eventId, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Events
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task<PagedResult<Event>> GetOrganizerEventsAsync(
        string organizerId,
        string? search,
        EventStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Registrations.Where(r => r.Status == RegistrationStatus.Registered))
            .Where(e => e.OrganizerId == organizerId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.Title.Contains(term) ||
                e.Description.Contains(term) ||
                e.Venue.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        query = query.OrderByDescending(e => e.EventDate);

        return await PagedResult<Event>.CreateAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<(bool Succeeded, string Error, Event? Event)> CreateAsync(
        Event ev,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateEvent(ev);
        if (validationError is not null)
        {
            return (false, validationError, null);
        }

        if (!await CategoryIsUsableAsync(ev.CategoryId, cancellationToken))
        {
            return (false, "The selected category is not available.", null);
        }

        ev.Status = EventStatus.Draft;
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty, ev);
    }

    public async Task<(bool Succeeded, string Error)> UpdateAsync(
        Event ev,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Events.FirstOrDefaultAsync(e => e.EventId == ev.EventId, cancellationToken);
        if (existing is null)
        {
            return (false, "Event not found.");
        }

        if (!CanManage(existing, organizerId, isAdmin))
        {
            return (false, "You are not authorized to edit this event.");
        }

        if (existing.Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            return (false, "Completed or cancelled events cannot be edited.");
        }

        var validationError = ValidateEvent(ev);
        if (validationError is not null)
        {
            return (false, validationError);
        }

        var activeRegistrations = await _db.EventRegistrations.CountAsync(
            r => r.EventId == existing.EventId && r.Status == RegistrationStatus.Registered,
            cancellationToken);
        if (ev.Capacity < activeRegistrations)
        {
            return (false, $"Capacity cannot be lower than the {activeRegistrations} active registrations.");
        }

        if (!await CategoryIsUsableAsync(ev.CategoryId, cancellationToken))
        {
            return (false, "The selected category is not available.");
        }

        existing.Title = ev.Title;
        existing.Description = ev.Description;
        existing.EventDate = ev.EventDate;
        existing.StartTime = ev.StartTime;
        existing.EndTime = ev.EndTime;
        existing.Venue = ev.Venue;
        existing.Capacity = ev.Capacity;
        existing.RegistrationDeadline = ev.RegistrationDeadline;
        existing.CategoryId = ev.CategoryId;

        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    public Task<(bool Succeeded, string Error)> PublishAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(eventId, organizerId, isAdmin, EventStatus.Published, cancellationToken);

    public Task<(bool Succeeded, string Error)> UnpublishAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(eventId, organizerId, isAdmin, EventStatus.Draft, cancellationToken);

    public Task<(bool Succeeded, string Error)> CancelAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(eventId, organizerId, isAdmin, EventStatus.Cancelled, cancellationToken);

    public Task<(bool Succeeded, string Error)> CompleteAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
        => ChangeStatusAsync(eventId, organizerId, isAdmin, EventStatus.Completed, cancellationToken);

    public async Task<(bool Succeeded, string Error)> DeleteDraftAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        if (existing is null)
        {
            return (false, "Event not found.");
        }

        if (!CanManage(existing, organizerId, isAdmin))
        {
            return (false, "You are not authorized to delete this event.");
        }

        if (existing.Status != EventStatus.Draft)
        {
            return (false, "Only draft events can be deleted.");
        }

        if (existing.Registrations.Count > 0)
        {
            return (false, "This event cannot be deleted because registrations exist.");
        }

        _db.Events.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    private async Task<(bool Succeeded, string Error)> ChangeStatusAsync(
        int eventId,
        string organizerId,
        bool isAdmin,
        EventStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (existing is null)
        {
            return (false, "Event not found.");
        }

        if (!CanManage(existing, organizerId, isAdmin))
        {
            return (false, "You are not authorized to manage this event.");
        }

        if (existing.Status == EventStatus.Cancelled && targetStatus != EventStatus.Cancelled)
        {
            return (false, "Cancelled events cannot change status.");
        }

        if (targetStatus == EventStatus.Published)
        {
            if (existing.Status is not (EventStatus.Draft or EventStatus.Published))
            {
                return (false, "Only draft events can be published.");
            }

            var validationError = ValidateEvent(existing, requireFutureDate: true);
            if (validationError is not null)
            {
                return (false, validationError);
            }

            if (!await CategoryIsUsableAsync(existing.CategoryId, cancellationToken))
            {
                return (false, "The event category is inactive and cannot be published.");
            }
        }

        if (targetStatus == EventStatus.Draft && existing.Status != EventStatus.Published)
        {
            return (false, "Only published events can be unpublished.");
        }

        if (targetStatus == EventStatus.Completed && existing.Status != EventStatus.Published)
        {
            return (false, "Only published events can be marked completed.");
        }

        if (targetStatus == EventStatus.Completed &&
            DateTime.Now < existing.EventDate.ToDateTime(existing.EndTime))
        {
            return (false, "An event can only be marked completed after it has ended.");
        }

        existing.Status = targetStatus;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, string.Empty);
    }

    private static bool CanManage(Event ev, string organizerId, bool isAdmin)
        => isAdmin || ev.OrganizerId == organizerId;

    private async Task<bool> CategoryIsUsableAsync(int categoryId, CancellationToken cancellationToken)
    {
        return await _db.EventCategories.AnyAsync(
            c => c.CategoryId == categoryId && c.IsActive,
            cancellationToken);
    }

    private static string? ValidateEvent(Event ev, bool requireFutureDate = false)
    {
        if (string.IsNullOrWhiteSpace(ev.Title))
        {
            return "Event title is required.";
        }

        if (ev.Capacity < 1)
        {
            return "Capacity must be at least 1.";
        }

        if (ev.EndTime <= ev.StartTime)
        {
            return "End time must be after start time.";
        }

        var eventStart = ev.EventDate.ToDateTime(ev.StartTime);
        if (ev.RegistrationDeadline >= eventStart)
        {
            return "Registration deadline must be before the event start.";
        }

        if (requireFutureDate && ev.EventDate.ToDateTime(ev.StartTime) <= DateTime.Now)
        {
            return "An event cannot be published after its start time has passed.";
        }

        if (requireFutureDate && ev.RegistrationDeadline <= DateTime.Now)
        {
            return "An event cannot be published after its registration deadline has passed.";
        }

        return null;
    }

    public async Task<PagedResult<Event>> GetAdminEventsAsync(
        string? search,
        EventStatus? status,
        int? categoryId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.Title.Contains(term) ||
                e.Description.Contains(term) ||
                e.Venue.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        query = query.OrderByDescending(e => e.EventDate).ThenBy(e => e.Title);
        return await PagedResult<Event>.CreateAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> GetUpcomingPublishedAsync(int take, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return await _db.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Registrations.Where(r => r.Status == RegistrationStatus.Registered))
            .Where(e => e.Status == EventStatus.Published && e.EventDate >= today)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveRegistrationsAsync(int eventId, CancellationToken cancellationToken = default)
        => _db.EventRegistrations.CountAsync(
            r => r.EventId == eventId && r.Status == RegistrationStatus.Registered,
            cancellationToken);
}
