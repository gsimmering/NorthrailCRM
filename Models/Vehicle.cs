namespace NorthrailCRM.Models;

public sealed class Vehicle
{
    public string Key { get; init; } = string.Empty;
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
    public string VehicleClass { get; init; } = string.Empty;
    public int? OwnerNumber { get; init; }
    public string Owner { get; init; } = string.Empty;
    public List<VehicleContract> Contracts { get; } = [];

    public string DisplayName => string.Join(" ", new[] { VehicleClass, Name }
        .Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } displayName
            ? displayName
            : $"Fahrzeug {Number}";

    public VehicleContract? CurrentContract => Contracts.FirstOrDefault(contract => contract.IsRunning);

    public string Subtitle => string.Join(" · ", new[]
    {
        Contracts.Count == 1 ? "1 Mietvertrag" : $"{Contracts.Count} Mietverträge",
        CurrentContract is null ? "Verfügbar" : "Vermietet"
    });
}

public sealed class VehicleContract
{
    public int Number { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public int? TenantNumber { get; init; }
    public string Tenant { get; init; } = string.Empty;
    public int? VehicleHolderNumber { get; init; }
    public string VehicleHolder { get; init; } = string.Empty;
    public DateTime? ContractPeriodFrom { get; init; }
    public DateTime? ContractPeriodTo { get; init; }
    public bool IsActive { get; init; }

    public bool IsRunning
    {
        get
        {
            var today = DateTime.Today;
            return ContractPeriodFrom is { } from
                && ContractPeriodTo is { } to
                && from.Date <= today
                && today <= to.Date;
        }
    }
}
