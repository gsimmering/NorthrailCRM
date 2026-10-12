using Microsoft.Data.SqlClient;
using NorthrailCRM.Models;

namespace NorthrailCRM.Services;

public sealed class ContactActivityService
{
    private const string PeopleLookupQuery = """
        SELECT Nr, COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(Vorname, N' ', Nachname))), N''), CONCAT(N'Person ', Nr))
        FROM dbo.t_Personen
        WHERE Kontakt_Nr = @ContactNumber
        ORDER BY Nachname, Vorname, Nr;
        """;

    private readonly string connectionString;

    public ContactActivityService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Die SQL-Verbindungszeichenfolge fehlt.");
    }

    public async Task<IReadOnlyList<ContactActivityType>> GetActivityTypesAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Nr, COALESCE(NULLIF(Bezeichnung, N''), N'Aktivität') AS Name
            FROM dbo.[t_Aktivität_Typen]
            ORDER BY Bezeichnung, Nr;
            """;

        var types = new List<ContactActivityType>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            types.Add(new ContactActivityType
            {
                Number = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return types;
    }

    public async Task<ContactActivityAssociations> GetAssociationsForContactAsync(
        int contactNumber,
        CancellationToken cancellationToken = default)
    {
        const string contactsQuery = """
            SELECT Nr, COALESCE(
                NULLIF(Firma, N''),
                NULLIF(Kurzbezeichnung, N''),
                NULLIF(LTRIM(RTRIM(CONCAT(Firma1, N' ', Firma2))), N''),
                CONCAT(N'Kontakt ', Nr))
            FROM dbo.t_Kontakte
            ORDER BY Firma, Kurzbezeichnung, Nr;
            """;
        const string employeesQuery = """
            SELECT Nr, COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(Vorname, N' ', Name))), N''), CONCAT(N'Mitarbeiter ', Nr))
            FROM dbo.t_Mitarbeiter
            ORDER BY Name, Vorname, Nr;
            """;
        const string gattungenQuery = """
            SELECT Nr, COALESCE(NULLIF(LTRIM(RTRIM(Bezeichnung)), N''), CONCAT(N'Gattung ', Nr))
            FROM dbo.t_FZG_Gattungen
            ORDER BY Bezeichnung, Nr;
            """;
        const string vehiclesQuery = """
            SELECT vehicle.Nr,
                CONCAT(
                    COALESCE(NULLIF(LTRIM(RTRIM(gattung.Bezeichnung)), N''), N''),
                    CASE WHEN NULLIF(LTRIM(RTRIM(gattung.Bezeichnung)), N'') IS NULL THEN N'' ELSE N' ' END,
                    COALESCE(NULLIF(LTRIM(RTRIM(vehicle.Bezeichnung)), N''), CONCAT(N'Fahrzeug ', vehicle.Nr)),
                    N' (', vehicle.Nr, N')')
            FROM dbo.t_FZG AS vehicle
            LEFT JOIN dbo.t_FZG_Gattungen AS gattung ON gattung.Nr = vehicle.Gattungstyp_Nr
            ORDER BY gattung.Bezeichnung, vehicle.Bezeichnung, vehicle.Nr;
            """;
        const string rentalContractsQuery = """
            SELECT Nr, COALESCE(NULLIF(LTRIM(RTRIM(Mietvertrag_Nr)), N''), CONCAT(N'Mietvertrag ', Nr))
            FROM dbo.t_Mietvertrag
            WHERE Mieter_Nr = @ContactNumber
                OR Vermieter_Nr = @ContactNumber
                OR Fahrzeughalter_Nr = @ContactNumber
            ORDER BY Aktiv DESC, Vertragszeitraum_Von DESC, Nr DESC;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return new ContactActivityAssociations
        {
            Contacts = await ReadLookupsAsync(connection, contactsQuery, null, cancellationToken),
            People = await ReadLookupsAsync(connection, PeopleLookupQuery, contactNumber, cancellationToken),
            Employees = await ReadLookupsAsync(connection, employeesQuery, null, cancellationToken),
            VehicleGattungen = await ReadLookupsAsync(connection, gattungenQuery, null, cancellationToken),
            Vehicles = await ReadLookupsAsync(connection, vehiclesQuery, null, cancellationToken),
            RentalContracts = await ReadLookupsAsync(connection, rentalContractsQuery, contactNumber, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ContactActivityLookup>> GetPeopleForContactAsync(
        int contactNumber,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await ReadLookupsAsync(connection, PeopleLookupQuery, contactNumber, cancellationToken);
    }

    private static async Task<IReadOnlyList<ContactActivityLookup>> ReadLookupsAsync(
        SqlConnection connection,
        string query,
        int? contactNumber,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(query, connection);
        if (contactNumber.HasValue)
        {
            command.Parameters.Add("@ContactNumber", System.Data.SqlDbType.Int).Value = contactNumber.Value;
        }

        var lookups = new List<ContactActivityLookup>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lookups.Add(new ContactActivityLookup
            {
                Number = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return lookups;
    }

    public async Task<IReadOnlyList<ContactActivity>> GetForContactAsync(
        int contactNumber,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                activity.Nr AS ActivityNumber,
                activity.Kontakt_Nr AS ContactNumber,
                activity.[Aktivität_Typ_Nr] AS ActivityTypeNumber,
                activity.Person_Nr AS PersonNumber,
                activity.Mitarbeiter_Nr AS EmployeeNumber,
                activity.Bearbeiter_Nr AS AssignedEmployeeNumber,
                activity.FZG_Gattung_Nr AS VehicleGattungNumber,
                activity.FZG_Nr AS VehicleNumber,
                activity.Mietvertrag_Nr AS RentalContractNumber,
                COALESCE(activity.Datum, activity.Erstellt, activity.Bearbeitet) AS ActivityDate,
                activity.Datum_Bis AS ActivityEndDate,
                COALESCE(NULLIF(activityType.Bezeichnung, N''), N'Aktivität') AS ActivityType,
                COALESCE(CONVERT(nvarchar(max), activity.Thema), N'') AS ActivitySubject,
                COALESCE(CONVERT(nvarchar(max), activity.[Text]), N'') AS ActivityText,
                COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(person.Vorname, N' ', person.Nachname))), N''), N'') AS PersonName,
                COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(employee.Vorname, N' ', employee.Name))), N''), N'') AS EmployeeName,
                COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(assignedEmployee.Vorname, N' ', assignedEmployee.Name))), N''), N'') AS AssignedEmployeeName,
                COALESCE(NULLIF(LTRIM(RTRIM(gattung.Bezeichnung)), N''), N'') AS VehicleGattungName,
                COALESCE(NULLIF(LTRIM(RTRIM(vehicle.Bezeichnung)), N''), N'') AS VehicleName,
                COALESCE(NULLIF(LTRIM(RTRIM(contract.Mietvertrag_Nr)), N''), N'') AS RentalContractName
            FROM dbo.[t_Aktivitäten] AS activity
            LEFT JOIN dbo.[t_Aktivität_Typen] AS activityType
                ON activityType.Nr = activity.[Aktivität_Typ_Nr]
            LEFT JOIN dbo.t_Personen AS person ON person.Nr = activity.Person_Nr
            LEFT JOIN dbo.t_Mitarbeiter AS employee ON employee.Nr = activity.Mitarbeiter_Nr
            LEFT JOIN dbo.t_Mitarbeiter AS assignedEmployee ON assignedEmployee.Nr = activity.Bearbeiter_Nr
            LEFT JOIN dbo.t_FZG_Gattungen AS gattung ON gattung.Nr = activity.FZG_Gattung_Nr
            LEFT JOIN dbo.t_FZG AS vehicle ON vehicle.Nr = activity.FZG_Nr
            LEFT JOIN dbo.t_Mietvertrag AS contract ON contract.Nr = activity.Mietvertrag_Nr
            WHERE activity.Kontakt_Nr = @ContactNumber
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.t_Personen AS person
                    WHERE person.Nr = activity.Person_Nr
                        AND person.Kontakt_Nr = @ContactNumber
                )
            ORDER BY COALESCE(activity.Datum, activity.Erstellt, activity.Bearbeitet) DESC, activity.Nr DESC;
            """;

        var activities = new List<ContactActivity>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@ContactNumber", contactNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var numberOrdinal = reader.GetOrdinal("ActivityNumber");
        var contactNumberOrdinal = reader.GetOrdinal("ContactNumber");
        var typeNumberOrdinal = reader.GetOrdinal("ActivityTypeNumber");
        var personNumberOrdinal = reader.GetOrdinal("PersonNumber");
        var employeeNumberOrdinal = reader.GetOrdinal("EmployeeNumber");
        var assignedEmployeeNumberOrdinal = reader.GetOrdinal("AssignedEmployeeNumber");
        var vehicleGattungNumberOrdinal = reader.GetOrdinal("VehicleGattungNumber");
        var vehicleNumberOrdinal = reader.GetOrdinal("VehicleNumber");
        var rentalContractNumberOrdinal = reader.GetOrdinal("RentalContractNumber");
        var dateOrdinal = reader.GetOrdinal("ActivityDate");
        var endDateOrdinal = reader.GetOrdinal("ActivityEndDate");
        var typeOrdinal = reader.GetOrdinal("ActivityType");
        var subjectOrdinal = reader.GetOrdinal("ActivitySubject");
        var textOrdinal = reader.GetOrdinal("ActivityText");
        var personNameOrdinal = reader.GetOrdinal("PersonName");
        var employeeNameOrdinal = reader.GetOrdinal("EmployeeName");
        var assignedEmployeeNameOrdinal = reader.GetOrdinal("AssignedEmployeeName");
        var vehicleGattungNameOrdinal = reader.GetOrdinal("VehicleGattungName");
        var vehicleNameOrdinal = reader.GetOrdinal("VehicleName");
        var rentalContractNameOrdinal = reader.GetOrdinal("RentalContractName");

        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(new ContactActivity
            {
                Number = reader.GetInt32(numberOrdinal),
                ContactNumber = reader.IsDBNull(contactNumberOrdinal) ? null : reader.GetInt32(contactNumberOrdinal),
                TypeNumber = reader.IsDBNull(typeNumberOrdinal) ? null : reader.GetInt32(typeNumberOrdinal),
                PersonNumber = reader.IsDBNull(personNumberOrdinal) ? null : reader.GetInt32(personNumberOrdinal),
                EmployeeNumber = reader.IsDBNull(employeeNumberOrdinal) ? null : reader.GetInt32(employeeNumberOrdinal),
                AssignedEmployeeNumber = reader.IsDBNull(assignedEmployeeNumberOrdinal) ? null : reader.GetInt32(assignedEmployeeNumberOrdinal),
                VehicleGattungNumber = reader.IsDBNull(vehicleGattungNumberOrdinal) ? null : reader.GetInt32(vehicleGattungNumberOrdinal),
                VehicleNumber = reader.IsDBNull(vehicleNumberOrdinal) ? null : reader.GetInt32(vehicleNumberOrdinal),
                RentalContractNumber = reader.IsDBNull(rentalContractNumberOrdinal) ? null : reader.GetInt32(rentalContractNumberOrdinal),
                ActivityDate = reader.IsDBNull(dateOrdinal) ? null : reader.GetDateTime(dateOrdinal),
                ActivityEndDate = reader.IsDBNull(endDateOrdinal) ? null : reader.GetDateTime(endDateOrdinal),
                Type = reader.GetString(typeOrdinal),
                Subject = reader.GetString(subjectOrdinal),
                Text = reader.GetString(textOrdinal),
                PersonName = reader.GetString(personNameOrdinal),
                EmployeeName = reader.GetString(employeeNameOrdinal),
                AssignedEmployeeName = reader.GetString(assignedEmployeeNameOrdinal),
                VehicleGattungName = reader.GetString(vehicleGattungNameOrdinal),
                VehicleName = reader.GetString(vehicleNameOrdinal),
                RentalContractName = reader.GetString(rentalContractNameOrdinal)
            });
        }

        return activities;
    }

    public async Task<int> CreateAsync(int contactNumber, ContactActivity activity, CancellationToken cancellationToken = default)
    {
        const string query = """
            INSERT INTO dbo.[t_Aktivitäten] (
                Datum, Datum_Bis, [Aktivität_Typ_Nr], Kontakt_Nr, Person_Nr, Mitarbeiter_Nr, Bearbeiter_Nr,
                FZG_Gattung_Nr, FZG_Nr, Mietvertrag_Nr, Thema, [Text], Erstellt)
            OUTPUT INSERTED.Nr
            VALUES (
                @ActivityDate, @ActivityEndDate, @ActivityTypeNumber, @ActivityContactNumber, @PersonNumber, @EmployeeNumber, @AssignedEmployeeNumber,
                @VehicleGattungNumber, @VehicleNumber, @RentalContractNumber, @ActivitySubject, @ActivityText, GETDATE());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        AddActivityParameters(command, contactNumber, activity);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdateAsync(int contactNumber, ContactActivity activity, CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.[t_Aktivitäten]
            SET Kontakt_Nr = @ActivityContactNumber,
                Datum = @ActivityDate,
                Datum_Bis = @ActivityEndDate,
                [Aktivität_Typ_Nr] = @ActivityTypeNumber,
                Person_Nr = @PersonNumber,
                Mitarbeiter_Nr = @EmployeeNumber,
                Bearbeiter_Nr = @AssignedEmployeeNumber,
                FZG_Gattung_Nr = @VehicleGattungNumber,
                FZG_Nr = @VehicleNumber,
                Mietvertrag_Nr = @RentalContractNumber,
                Thema = @ActivitySubject,
                [Text] = @ActivityText,
                Bearbeitet = GETDATE()
            WHERE Nr = @ActivityNumber
                AND
                (
                    Kontakt_Nr = @ContactNumber
                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.t_Personen AS person
                        WHERE person.Nr = dbo.[t_Aktivitäten].Person_Nr
                            AND person.Kontakt_Nr = @ContactNumber
                    )
                );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        AddActivityParameters(command, contactNumber, activity);
        command.Parameters.Add("@ActivityNumber", System.Data.SqlDbType.Int).Value = activity.Number;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int contactNumber, int activityNumber, CancellationToken cancellationToken = default)
    {
        const string query = """
            DELETE FROM dbo.[t_Aktivitäten]
            WHERE Nr = @ActivityNumber
                AND
                (
                    Kontakt_Nr = @ContactNumber
                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.t_Personen AS person
                        WHERE person.Nr = dbo.[t_Aktivitäten].Person_Nr
                            AND person.Kontakt_Nr = @ContactNumber
                    )
                );
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@ActivityNumber", System.Data.SqlDbType.Int).Value = activityNumber;
        command.Parameters.Add("@ContactNumber", System.Data.SqlDbType.Int).Value = contactNumber;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void AddActivityParameters(SqlCommand command, int contactNumber, ContactActivity activity)
    {
        command.Parameters.Add("@ActivityDate", System.Data.SqlDbType.DateTime).Value = activity.ActivityDate ?? (object)DBNull.Value;
        command.Parameters.Add("@ActivityEndDate", System.Data.SqlDbType.DateTime).Value = activity.ActivityEndDate ?? (object)DBNull.Value;
        command.Parameters.Add("@ActivityTypeNumber", System.Data.SqlDbType.Int).Value = activity.TypeNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@ActivityContactNumber", System.Data.SqlDbType.Int).Value = activity.ContactNumber ?? contactNumber;
        command.Parameters.Add("@ContactNumber", System.Data.SqlDbType.Int).Value = contactNumber;
        command.Parameters.Add("@PersonNumber", System.Data.SqlDbType.Int).Value = activity.PersonNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@EmployeeNumber", System.Data.SqlDbType.Int).Value = activity.EmployeeNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@AssignedEmployeeNumber", System.Data.SqlDbType.Int).Value = activity.AssignedEmployeeNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@VehicleGattungNumber", System.Data.SqlDbType.Int).Value = activity.VehicleGattungNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@VehicleNumber", System.Data.SqlDbType.Int).Value = activity.VehicleNumber ?? (object)DBNull.Value;
        command.Parameters.Add("@RentalContractNumber", System.Data.SqlDbType.Int).Value = activity.RentalContractNumber ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("@ActivitySubject", activity.Subject);
        command.Parameters.Add("@ActivityText", System.Data.SqlDbType.NText).Value = activity.Text;
    }
}