using CampusCue.Common;
using CampusCue.Models;
using CampusCue.Models.Enums;

namespace CampusCue.Services;

public interface IEventService
{
    Task<PagedResult<Event>> SearchPublishedAsync(
        string? search,
        int? categoryId,
        bool upcomingOnly,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(int eventId, bool tracking = false, CancellationToken cancellationToken = default);

    Task<PagedResult<Event>> GetOrganizerEventsAsync(
        string organizerId,
        string? search,
        EventStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error, Event? Event)> CreateAsync(Event ev, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> UpdateAsync(Event ev, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> PublishAsync(int eventId, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> UnpublishAsync(int eventId, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> CancelAsync(int eventId, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> CompleteAsync(int eventId, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Error)> DeleteDraftAsync(int eventId, string organizerId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<PagedResult<Event>> GetAdminEventsAsync(
        string? search,
        EventStatus? status,
        int? categoryId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Event>> GetUpcomingPublishedAsync(int take, CancellationToken cancellationToken = default);

    Task<int> CountActiveRegistrationsAsync(int eventId, CancellationToken cancellationToken = default);
}
