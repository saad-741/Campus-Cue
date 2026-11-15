using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CampusCue.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? StudentId { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();

    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
}
