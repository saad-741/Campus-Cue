using CampusCue.Constants;
using CampusCue.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CampusCue.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager, logger);
        await SeedAdminAsync(userManager, configuration, logger);
        await SeedCategoriesAsync(context, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var roleName in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                logger.LogError("Failed to seed role {Role}: {Errors}",
                    roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        var fullName = configuration["AdminSeed:FullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin seed credentials are missing. Skipping admin user seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                logger.LogError("Failed to seed admin user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            existing = admin;
        }

        if (!await userManager.IsInRoleAsync(existing, AppRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(existing, AppRoles.Admin);
            if (!roleResult.Succeeded)
            {
                logger.LogError("Failed to assign the seeded Admin role to {Email}: {Errors}",
                    email,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.EventCategories.AnyAsync())
        {
            return;
        }

        var categories = new[]
        {
            new EventCategory { Name = "Workshops", Description = "Hands-on skill building sessions.", IsActive = true },
            new EventCategory { Name = "Seminars", Description = "Talks and academic seminars.", IsActive = true },
            new EventCategory { Name = "Competitions", Description = "Academic and technical contests.", IsActive = true },
            new EventCategory { Name = "Sports", Description = "Athletic and recreational events.", IsActive = true },
            new EventCategory { Name = "Cultural Events", Description = "Arts, music, and cultural programs.", IsActive = true },
            new EventCategory { Name = "Career Fairs", Description = "Employer and career networking events.", IsActive = true },
            new EventCategory { Name = "Hackathons", Description = "Intensive coding and innovation events.", IsActive = true }
        };

        context.EventCategories.AddRange(categories);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} event categories.", categories.Length);
    }
}
