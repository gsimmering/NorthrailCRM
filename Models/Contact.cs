using System.ComponentModel.DataAnnotations;

namespace NorthrailCRM.Models;

public class Contact
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(40)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Company { get; set; } = string.Empty;

    [MaxLength(150)]
    public string JobTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;
}