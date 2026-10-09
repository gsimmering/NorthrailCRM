using Microsoft.Data.SqlClient;
using NorthrailCRM.Models;

namespace NorthrailCRM.Services;

public sealed class RentalContractService
{
    private readonly string connectionString;

    public RentalContractService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Die SQL-Verbindungszeichenfolge fehlt.");
    }

    public async Task<IReadOnlyList<RentalContract>> GetRentalContractsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                m.Nr AS ContractId,
                COALESCE(NULLIF(LTRIM(RTRIM(m.Mietvertrag_Nr)), N''), CONVERT(nvarchar(20), m.Nr)) AS ContractNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(tenant.Kurzbezeichnung)), N''), NULLIF(tenant.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(tenant.Firma1, N' ', tenant.Firma2))), N''), N'') AS Tenant,
                COALESCE(NULLIF(LTRIM(RTRIM(lessor.Kurzbezeichnung)), N''), NULLIF(lessor.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(lessor.Firma1, N' ', lessor.Firma2))), N''), N'') AS Lessor,
                COALESCE(NULLIF(LTRIM(RTRIM(holder.Kurzbezeichnung)), N''), NULLIF(holder.Firma, N''), NULLIF(LTRIM(RTRIM(CONCAT(holder.Firma1, N' ', holder.Firma2))), N''), N'') AS VehicleHolder,
                COALESCE(NULLIF(CONCAT_WS(N' ', NULLIF(LTRIM(RTRIM(vehicleGattung.Bezeichnung)), N''), NULLIF(LTRIM(RTRIM(vehicle.Bezeichnung)), N'')), N''), CASE WHEN m.Fahrzeug_Nr IS NULL THEN N'' ELSE CONCAT(N'Fahrzeug ', m.Fahrzeug_Nr) END) AS Vehicle,
                COALESCE(NULLIF(contractType.Bezeichnung, N''), N'') AS ContractType,
                COALESCE(NULLIF(rentalType.Bezeichnung, N''), N'') AS RentalType,
                COALESCE(NULLIF(m.Bestell_Nr, N''), N'') AS OrderNumber,
                m.Vertragsdatum AS ContractDate,
                m.Vertragszeitraum_Von AS ContractPeriodFrom,
                m.Vertragszeitraum_Bis AS ContractPeriodTo,
                m.Abrechnungszeitraum_Von AS BillingPeriodFrom,
                m.Abrechnungszeitraum_Bis AS BillingPeriodTo,
                m.Kautionshöhe AS DepositAmount,
                m.Aktiv AS IsActive,
                m.Freigegeben AS IsReleased,
                m.Unterschrieben AS IsSigned
            FROM dbo.t_Mietvertrag AS m
            LEFT JOIN dbo.t_Kontakte AS tenant ON tenant.Nr = m.Mieter_Nr
            LEFT JOIN dbo.t_Kontakte AS lessor ON lessor.Nr = m.Vermieter_Nr
            LEFT JOIN dbo.t_Kontakte AS holder ON holder.Nr = m.Fahrzeughalter_Nr
            LEFT JOIN dbo.t_FZG AS vehicle ON vehicle.Nr = m.Fahrzeug_Nr
            LEFT JOIN dbo.t_FZG_Gattungen AS vehicleGattung ON vehicleGattung.Nr = vehicle.Gattungstyp_Nr
            LEFT JOIN dbo.t_Mietvertrag_Arten AS contractType ON contractType.Nr = m.Vertragsart_Nr
            LEFT JOIN dbo.t_Mietvertrag_Abrechnungsarten AS rentalType ON rentalType.Nr = m.Vermietungsart_Nr
            ORDER BY m.Aktiv DESC, m.Vertragszeitraum_Von DESC, m.Nr DESC;
            """;

        var contracts = new List<RentalContract>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var contractIdOrdinal = reader.GetOrdinal("ContractId");
        var contractNumberOrdinal = reader.GetOrdinal("ContractNumber");
        var tenantOrdinal = reader.GetOrdinal("Tenant");
        var lessorOrdinal = reader.GetOrdinal("Lessor");
        var vehicleHolderOrdinal = reader.GetOrdinal("VehicleHolder");
        var vehicleOrdinal = reader.GetOrdinal("Vehicle");
        var contractTypeOrdinal = reader.GetOrdinal("ContractType");
        var rentalTypeOrdinal = reader.GetOrdinal("RentalType");
        var orderNumberOrdinal = reader.GetOrdinal("OrderNumber");
        var contractDateOrdinal = reader.GetOrdinal("ContractDate");
        var contractPeriodFromOrdinal = reader.GetOrdinal("ContractPeriodFrom");
        var contractPeriodToOrdinal = reader.GetOrdinal("ContractPeriodTo");
        var billingPeriodFromOrdinal = reader.GetOrdinal("BillingPeriodFrom");
        var billingPeriodToOrdinal = reader.GetOrdinal("BillingPeriodTo");
        var depositAmountOrdinal = reader.GetOrdinal("DepositAmount");
        var isActiveOrdinal = reader.GetOrdinal("IsActive");
        var isReleasedOrdinal = reader.GetOrdinal("IsReleased");
        var isSignedOrdinal = reader.GetOrdinal("IsSigned");

        while (await reader.ReadAsync(cancellationToken))
        {
            contracts.Add(new RentalContract
            {
                Number = reader.GetInt32(contractIdOrdinal),
                ContractNumber = reader.GetString(contractNumberOrdinal),
                Tenant = reader.GetString(tenantOrdinal),
                Lessor = reader.GetString(lessorOrdinal),
                VehicleHolder = reader.GetString(vehicleHolderOrdinal),
                Vehicle = reader.GetString(vehicleOrdinal),
                ContractType = reader.GetString(contractTypeOrdinal),
                RentalType = reader.GetString(rentalTypeOrdinal),
                OrderNumber = reader.GetString(orderNumberOrdinal),
                ContractDate = ReadNullableDate(reader, contractDateOrdinal),
                ContractPeriodFrom = ReadNullableDate(reader, contractPeriodFromOrdinal),
                ContractPeriodTo = ReadNullableDate(reader, contractPeriodToOrdinal),
                BillingPeriodFrom = ReadNullableDate(reader, billingPeriodFromOrdinal),
                BillingPeriodTo = ReadNullableDate(reader, billingPeriodToOrdinal),
                DepositAmount = reader.IsDBNull(depositAmountOrdinal) ? null : reader.GetDecimal(depositAmountOrdinal),
                IsActive = reader.GetBoolean(isActiveOrdinal),
                IsReleased = reader.GetBoolean(isReleasedOrdinal),
                IsSigned = reader.GetBoolean(isSignedOrdinal)
            });
        }

        return contracts;
    }

    private static DateTime? ReadNullableDate(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}