using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NorthrailCRM.Models;

namespace NorthrailCRM.Services;

public sealed class LeadService
{
    private readonly string connectionString;

    public LeadService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Die SQL-Verbindungszeichenfolge fehlt.");
    }

    public async Task<IReadOnlyList<Lead>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT LeadId, CreatedAtUtc, CompanyName, FirstName, LastName, Email, PhoneNumber, Role,
                SafetyCertificates, TractionType, LocomotivePlatform, DesiredLocomotiveCount, OtherPlatform, Corridors, SafetySystems,
                RadioRemoteControl, LastMileDiesel, MiddleCoupler, RentalModel, PlannedDuration,
                DesiredAvailability, MonthlyMileage, Route
            FROM dbo.t_CRM_Leads
            ORDER BY CreatedAtUtc DESC, LeadId DESC;
            """;

        var leads = new List<Lead>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            leads.Add(new Lead
            {
                Id = reader.GetInt64(reader.GetOrdinal("LeadId")),
                CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                SafetyCertificates = DeserializeStringList(reader.GetString(reader.GetOrdinal("SafetyCertificates"))),
                TractionType = reader.GetString(reader.GetOrdinal("TractionType")),
                LocomotivePlatform = reader.GetString(reader.GetOrdinal("LocomotivePlatform")),
                DesiredLocomotiveCount = reader.GetInt32(reader.GetOrdinal("DesiredLocomotiveCount")),
                OtherPlatform = reader.GetString(reader.GetOrdinal("OtherPlatform")),
                Corridors = DeserializeStringList(reader.GetString(reader.GetOrdinal("Corridors"))),
                SafetySystems = DeserializeStringList(reader.GetString(reader.GetOrdinal("SafetySystems"))),
                RadioRemoteControl = reader.GetBoolean(reader.GetOrdinal("RadioRemoteControl")),
                LastMileDiesel = reader.GetBoolean(reader.GetOrdinal("LastMileDiesel")),
                MiddleCoupler = reader.GetBoolean(reader.GetOrdinal("MiddleCoupler")),
                RentalModel = reader.GetString(reader.GetOrdinal("RentalModel")),
                PlannedDuration = reader.GetString(reader.GetOrdinal("PlannedDuration")),
                DesiredAvailability = reader.IsDBNull(reader.GetOrdinal("DesiredAvailability"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("DesiredAvailability")),
                MonthlyMileage = reader.GetString(reader.GetOrdinal("MonthlyMileage")),
                Route = reader.GetString(reader.GetOrdinal("Route"))
            });
        }

        return leads;
    }

    public async Task<long> CreateLeadAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        const string query = """
            INSERT INTO dbo.t_CRM_Leads (
                CompanyName, FirstName, LastName, Email, PhoneNumber, Role, SafetyCertificates,
                TractionType, LocomotivePlatform, DesiredLocomotiveCount, OtherPlatform, Corridors, SafetySystems,
                RadioRemoteControl, LastMileDiesel, MiddleCoupler, RentalModel, PlannedDuration,
                DesiredAvailability, MonthlyMileage, Route)
            OUTPUT INSERTED.LeadId
            VALUES (
                @CompanyName, @FirstName, @LastName, @Email, @PhoneNumber, @Role, @SafetyCertificates,
                @TractionType, @LocomotivePlatform, @DesiredLocomotiveCount, @OtherPlatform, @Corridors, @SafetySystems,
                @RadioRemoteControl, @LastMileDiesel, @MiddleCoupler, @RentalModel, @PlannedDuration,
                @DesiredAvailability, @MonthlyMileage, @Route);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        AddText(command, "@CompanyName", 250, lead.CompanyName);
        AddText(command, "@FirstName", 100, lead.FirstName);
        AddText(command, "@LastName", 100, lead.LastName);
        AddText(command, "@Email", 320, lead.Email);
        AddText(command, "@PhoneNumber", 50, lead.PhoneNumber);
        AddText(command, "@Role", 120, lead.Role);
        AddJson(command, "@SafetyCertificates", lead.SafetyCertificates);
        AddText(command, "@TractionType", 120, lead.TractionType);
        AddText(command, "@LocomotivePlatform", 180, lead.LocomotivePlatform);
        command.Parameters.Add("@DesiredLocomotiveCount", SqlDbType.Int).Value = lead.DesiredLocomotiveCount;
        AddText(command, "@OtherPlatform", 250, lead.OtherPlatform);
        AddJson(command, "@Corridors", lead.Corridors);
        AddJson(command, "@SafetySystems", lead.SafetySystems);
        command.Parameters.Add("@RadioRemoteControl", SqlDbType.Bit).Value = lead.RadioRemoteControl;
        command.Parameters.Add("@LastMileDiesel", SqlDbType.Bit).Value = lead.LastMileDiesel;
        command.Parameters.Add("@MiddleCoupler", SqlDbType.Bit).Value = lead.MiddleCoupler;
        AddText(command, "@RentalModel", 120, lead.RentalModel);
        AddText(command, "@PlannedDuration", 120, lead.PlannedDuration);
        command.Parameters.Add("@DesiredAvailability", SqlDbType.Date).Value = lead.DesiredAvailability?.Date ?? (object)DBNull.Value;
        AddText(command, "@MonthlyMileage", 120, lead.MonthlyMileage);
        AddText(command, "@Route", 500, lead.Route);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static void AddText(SqlCommand command, string name, int size, string value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value.Trim();

    private static void AddJson(SqlCommand command, string name, IReadOnlyCollection<string> values) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(values);

    private static List<string> DeserializeStringList(string value) =>
        JsonSerializer.Deserialize<List<string>>(value) ?? [];
}