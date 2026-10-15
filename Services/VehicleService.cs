using Microsoft.Data.SqlClient;
using NorthrailCRM.Models;

namespace NorthrailCRM.Services;

public sealed class VehicleService
{
    private readonly string connectionString;

    public VehicleService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Die SQL-Verbindungszeichenfolge fehlt.");
    }

    public async Task<IReadOnlyList<Vehicle>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                f.Nr AS VehicleNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(f.Bezeichnung)), N''), N'') AS Name,
                COALESCE(NULLIF(LTRIM(RTRIM(g.Bezeichnung)), N''), N'') AS VehicleClass,
                owner.Nr AS OwnerNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(owner.Kurzbezeichnung)), N''), NULLIF(owner.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(owner.Firma1, N' ', owner.Firma2))), N''), N'') AS Owner,
                m.Nr AS ContractId,
                COALESCE(NULLIF(LTRIM(RTRIM(m.Mietvertrag_Nr)), N''), CONVERT(nvarchar(20), m.Nr), N'') AS ContractNumber,
                tenant.Nr AS TenantNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(tenant.Kurzbezeichnung)), N''), NULLIF(tenant.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(tenant.Firma1, N' ', tenant.Firma2))), N''), N'') AS Tenant,
                holder.Nr AS VehicleHolderNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(holder.Kurzbezeichnung)), N''), NULLIF(holder.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(holder.Firma1, N' ', holder.Firma2))), N''), N'') AS VehicleHolder,
                m.Vertragszeitraum_Von AS ContractPeriodFrom,
                m.Vertragszeitraum_Bis AS ContractPeriodTo,
                COALESCE(m.Aktiv, CONVERT(bit, 0)) AS IsActive
            FROM dbo.t_FZG AS f
            LEFT JOIN dbo.t_FZG_Gattungen AS g ON g.Nr = f.Gattungstyp_Nr
            LEFT JOIN dbo.t_Kontakte AS owner ON owner.Nr = f.Fahrzeuginhaber_Nr
            LEFT JOIN dbo.t_Mietvertrag AS m ON m.Fahrzeug_Nr = f.Nr
            LEFT JOIN dbo.t_Kontakte AS tenant ON tenant.Nr = m.Mieter_Nr
            LEFT JOIN dbo.t_Kontakte AS holder ON holder.Nr = m.Fahrzeughalter_Nr
            ORDER BY VehicleClass, Name, f.Nr, m.Vertragszeitraum_Von DESC, m.Nr DESC;
            """;

        var vehicles = new List<Vehicle>();
        Vehicle? currentVehicle = null;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var vehicleNumberOrdinal = reader.GetOrdinal("VehicleNumber");
        var nameOrdinal = reader.GetOrdinal("Name");
        var vehicleClassOrdinal = reader.GetOrdinal("VehicleClass");
        var ownerNumberOrdinal = reader.GetOrdinal("OwnerNumber");
        var ownerOrdinal = reader.GetOrdinal("Owner");
        var contractIdOrdinal = reader.GetOrdinal("ContractId");
        var contractNumberOrdinal = reader.GetOrdinal("ContractNumber");
        var tenantNumberOrdinal = reader.GetOrdinal("TenantNumber");
        var tenantOrdinal = reader.GetOrdinal("Tenant");
        var vehicleHolderNumberOrdinal = reader.GetOrdinal("VehicleHolderNumber");
        var vehicleHolderOrdinal = reader.GetOrdinal("VehicleHolder");
        var contractPeriodFromOrdinal = reader.GetOrdinal("ContractPeriodFrom");
        var contractPeriodToOrdinal = reader.GetOrdinal("ContractPeriodTo");
        var isActiveOrdinal = reader.GetOrdinal("IsActive");

        while (await reader.ReadAsync(cancellationToken))
        {
            var vehicleNumber = reader.GetInt32(vehicleNumberOrdinal);
            if (currentVehicle is null || currentVehicle.Number != vehicleNumber)
            {
                currentVehicle = new Vehicle
                {
                    Number = vehicleNumber,
                    Key = $"F:{vehicleNumber}",
                    Name = reader.GetString(nameOrdinal),
                    VehicleClass = reader.GetString(vehicleClassOrdinal),
                    OwnerNumber = ReadNullableInt(reader, ownerNumberOrdinal),
                    Owner = reader.GetString(ownerOrdinal)
                };
                vehicles.Add(currentVehicle);
            }

            if (!reader.IsDBNull(contractIdOrdinal))
            {
                currentVehicle.Contracts.Add(new VehicleContract
                {
                    Number = reader.GetInt32(contractIdOrdinal),
                    ContractNumber = reader.GetString(contractNumberOrdinal),
                    TenantNumber = ReadNullableInt(reader, tenantNumberOrdinal),
                    Tenant = reader.GetString(tenantOrdinal),
                    VehicleHolderNumber = ReadNullableInt(reader, vehicleHolderNumberOrdinal),
                    VehicleHolder = reader.GetString(vehicleHolderOrdinal),
                    ContractPeriodFrom = ReadNullableDate(reader, contractPeriodFromOrdinal),
                    ContractPeriodTo = ReadNullableDate(reader, contractPeriodToOrdinal),
                    IsActive = reader.GetBoolean(isActiveOrdinal)
                });
            }
        }

        return vehicles;
    }

    private static int? ReadNullableInt(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static DateTime? ReadNullableDate(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
