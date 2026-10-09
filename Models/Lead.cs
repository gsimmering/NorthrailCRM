using System.ComponentModel.DataAnnotations;

namespace NorthrailCRM.Models;

public sealed class Lead
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    [Required(ErrorMessage = "Bitte den Firmennamen angeben.")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitte den Vornamen angeben.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitte den Nachnamen angeben.")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Bitte eine gültige E-Mail-Adresse angeben.")]
    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitte die Rolle des Leads auswählen.")]
    public string Role { get; set; } = string.Empty;

    public List<string> SafetyCertificates { get; set; } = [];
    public string TractionType { get; set; } = string.Empty;
    public string LocomotivePlatform { get; set; } = string.Empty;
    [Range(1, int.MaxValue, ErrorMessage = "Bitte mindestens eine Lok angeben.")]
    public int DesiredLocomotiveCount { get; set; } = 1;
    public string OtherPlatform { get; set; } = string.Empty;
    public List<string> Corridors { get; set; } = [];
    public List<string> SafetySystems { get; set; } = [];
    public bool RadioRemoteControl { get; set; }
    public bool LastMileDiesel { get; set; }
    public bool MiddleCoupler { get; set; }
    public string RentalModel { get; set; } = string.Empty;
    public string PlannedDuration { get; set; } = string.Empty;
    public DateTime? DesiredAvailability { get; set; }
    public string MonthlyMileage { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
}