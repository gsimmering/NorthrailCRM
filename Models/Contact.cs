namespace NorthrailCRM.Models;

public sealed class Contact
{
    public string Key { get; init; } = string.Empty;
    public int Number { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string ShortName { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public List<Person> Persons { get; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Company) ? $"Kontakt {Number}" : Company;

    public string Subtitle => string.Join(" · ", new[]
    {
        Persons.Count == 1 ? "1 Person" : $"{Persons.Count} Personen",
        IsCustomer ? "Kunde" : null,
        IsSupplier ? "Lieferant" : null
    }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public string Address => string.Join(", ", new[]
    {
        Street,
        $"{PostalCode} {City}".Trim(),
        Country
    }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed class Person
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;

    public string DisplayName => string.Join(" ", new[] { Title, FirstName, LastName }
        .Where(part => !string.IsNullOrWhiteSpace(part)));
}
