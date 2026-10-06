using Microsoft.Data.SqlClient;
using NorthrailCRM.Models;

namespace NorthrailCRM.Services;

public sealed class ContactService
{
    private readonly string connectionString;

    public ContactService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Die SQL-Verbindungszeichenfolge fehlt.");
    }

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                k.Nr AS ContactNumber,
                COALESCE(
                    NULLIF(k.Firma, N''),
                    NULLIF(k.Kurzbezeichnung, N''),
                    NULLIF(LTRIM(RTRIM(CONCAT(k.Firma1, N' ', k.Firma2))), N''),
                    N'') AS Company,
                COALESCE(NULLIF(k.Kurzbezeichnung, N''), N'') AS ShortName,
                COALESCE(NULLIF(k.EMail, N''), N'') AS Email,
                COALESCE(NULLIF(k.Telefon, N''), NULLIF(k.Mobil, N''), N'') AS PhoneNumber,
                COALESCE(NULLIF(k.Info, N''), N'') AS Notes,
                COALESCE(NULLIF(k.Strasse, N''), N'') AS Street,
                COALESCE(NULLIF(k.PLZ, N''), N'') AS PostalCode,
                COALESCE(NULLIF(k.Ort, N''), N'') AS City,
                COALESCE(NULLIF(l.Bezeichnung, N''), N'') AS Country,
                COALESCE(k.Kunde, CONVERT(bit, 0)) AS IsCustomer,
                COALESCE(k.Lieferant, CONVERT(bit, 0)) AS IsSupplier,
                p.Nr AS PersonNumber,
                COALESCE(NULLIF(p.Titel, N''), N'') AS PersonTitle,
                COALESCE(NULLIF(p.Vorname, N''), N'') AS PersonFirstName,
                COALESCE(NULLIF(p.Nachname, N''), N'') AS PersonLastName,
                COALESCE(NULLIF(p.Funktion, N''), N'') AS PersonJobTitle,
                COALESCE(NULLIF(p.EMail, N''), N'') AS PersonEmail,
                COALESCE(NULLIF(p.Telefon, N''), NULLIF(p.Mobil, N''), N'') AS PersonPhone
            FROM dbo.t_Kontakte AS k
            LEFT JOIN dbo.t_Personen AS p ON p.Kontakt_Nr = k.Nr
            LEFT JOIN dbo.[t_Länder] AS l ON l.Nr = k.Land_Nr
            ORDER BY Company, k.Nr, p.Nachname, p.Vorname;
            """;

        var contacts = new List<Contact>();
        Contact? currentContact = null;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var contactNumberOrdinal = reader.GetOrdinal("ContactNumber");
        var emailOrdinal = reader.GetOrdinal("Email");
        var phoneOrdinal = reader.GetOrdinal("PhoneNumber");
        var companyOrdinal = reader.GetOrdinal("Company");
        var shortNameOrdinal = reader.GetOrdinal("ShortName");
        var notesOrdinal = reader.GetOrdinal("Notes");
        var streetOrdinal = reader.GetOrdinal("Street");
        var postalCodeOrdinal = reader.GetOrdinal("PostalCode");
        var cityOrdinal = reader.GetOrdinal("City");
        var countryOrdinal = reader.GetOrdinal("Country");
        var isCustomerOrdinal = reader.GetOrdinal("IsCustomer");
        var isSupplierOrdinal = reader.GetOrdinal("IsSupplier");
        var personNumberOrdinal = reader.GetOrdinal("PersonNumber");
        var personTitleOrdinal = reader.GetOrdinal("PersonTitle");
        var personFirstNameOrdinal = reader.GetOrdinal("PersonFirstName");
        var personLastNameOrdinal = reader.GetOrdinal("PersonLastName");
        var personJobTitleOrdinal = reader.GetOrdinal("PersonJobTitle");
        var personEmailOrdinal = reader.GetOrdinal("PersonEmail");
        var personPhoneOrdinal = reader.GetOrdinal("PersonPhone");

        while (await reader.ReadAsync(cancellationToken))
        {
            var contactNumber = reader.GetInt32(contactNumberOrdinal);
            if (currentContact is null || currentContact.Number != contactNumber)
            {
                currentContact = new Contact
                {
                    Number = contactNumber,
                    Key = $"K:{contactNumber}",
                    Company = reader.GetString(companyOrdinal),
                    ShortName = reader.GetString(shortNameOrdinal),
                    Email = reader.GetString(emailOrdinal),
                    PhoneNumber = reader.GetString(phoneOrdinal),
                    Notes = reader.GetString(notesOrdinal),
                    Street = reader.GetString(streetOrdinal),
                    PostalCode = reader.GetString(postalCodeOrdinal),
                    City = reader.GetString(cityOrdinal),
                    Country = reader.GetString(countryOrdinal),
                    IsCustomer = reader.GetBoolean(isCustomerOrdinal),
                    IsSupplier = reader.GetBoolean(isSupplierOrdinal)
                };
                contacts.Add(currentContact);
            }

            if (!reader.IsDBNull(personNumberOrdinal))
            {
                currentContact.Persons.Add(new Person
                {
                    Number = reader.GetInt32(personNumberOrdinal),
                    Title = reader.GetString(personTitleOrdinal),
                    FirstName = reader.GetString(personFirstNameOrdinal),
                    LastName = reader.GetString(personLastNameOrdinal),
                    JobTitle = reader.GetString(personJobTitleOrdinal),
                    Email = reader.GetString(personEmailOrdinal),
                    PhoneNumber = reader.GetString(personPhoneOrdinal)
                });
            }
        }

        return contacts;
    }
}