using CampusCue.Common;
using CampusCue.Constants;
using CampusCue.Data;
using CampusCue.Models;
using CampusCue.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CampusCue.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<PagedResult<UserListItemViewModel>> SearchUsersAsync(
        string? search,
        string? role,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ApplicationUser> query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)) ||
                (u.StudentId != null && u.StudentId.Contains(term)) ||
                (u.Department != null && u.Department.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query =
                from user in query
                join userRole in _db.UserRoles on user.Id equals userRole.UserId
                join identityRole in _db.Roles on userRole.RoleId equals identityRole.Id
                where identityRole.Name == role
                select user;
        }

        query = query.Distinct().OrderBy(u => u.FullName);
        var page = await PagedResult<ApplicationUser>.CreateAsync(query, pageNumber, pageSize, cancellationToken);

        var items = new List<UserListItemViewModel>();
        foreach (var user in page.Items)
        {
            items.Add(await MapAsync(user));
        }

        return new PagedResult<UserListItemViewModel>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    public async Task<UserDetailsViewModel?> GetDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new UserDetailsViewModel
        {
            User = await MapAsync(user),
            OrganizedEventCount = await _db.Events.CountAsync(e => e.OrganizerId == userId, cancellationToken),
            RegistrationCount = await _db.EventRegistrations.CountAsync(r => r.StudentId == userId, cancellationToken)
        };
    }

    public async Task<(bool Succeeded, string Error)> CreateOrganizerAsync(
        CreateOrganizerViewModel model,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            return (false, "An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            return (false, string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Organizer);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return (false, string.Join(" ", roleResult.Errors.Select(e => e.Description)));
        }

        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> AssignOrganizerAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (false, "User not found.");
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return (false, "Admin accounts cannot be converted to organizers.");
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Organizer))
        {
            return (false, "This user is already an organizer.");
        }

        var result = await _userManager.AddToRoleAsync(user, AppRoles.Organizer);
        if (!result.Succeeded)
        {
            return (false, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Student))
        {
            var removeStudentRole = await _userManager.RemoveFromRoleAsync(user, AppRoles.Student);
            if (!removeStudentRole.Succeeded)
            {
                await _userManager.RemoveFromRoleAsync(user, AppRoles.Organizer);
                return (false, string.Join(" ", removeStudentRole.Errors.Select(e => e.Description)));
            }
        }

        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> SetActiveAsync(
        string userId,
        bool isActive,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
        {
            return (false, "You cannot change the activation status of your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (false, "User not found.");
        }

        user.IsActive = isActive;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return (false, string.Join(" ", update.Errors.Select(e => e.Description)));
        }

        if (!isActive)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return (true, string.Empty);
    }

    private async Task<UserListItemViewModel> MapAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserListItemViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            StudentId = user.StudentId,
            Department = user.Department,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        };
    }
}
