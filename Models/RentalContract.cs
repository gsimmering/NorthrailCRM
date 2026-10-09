namespace NorthrailCRM.Models;

public sealed class RentalContract
{
    public int Number { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string Tenant { get; init; } = string.Empty;
    public string Lessor { get; init; } = string.Empty;
    public string VehicleHolder { get; init; } = string.Empty;
    public string Vehicle { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string RentalType { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public DateTime? ContractDate { get; init; }
    public DateTime? ContractPeriodFrom { get; init; }
    public DateTime? ContractPeriodTo { get; init; }
    public DateTime? BillingPeriodFrom { get; init; }
    public DateTime? BillingPeriodTo { get; init; }
    public decimal? DepositAmount { get; init; }
    public bool IsActive { get; init; }
    public bool IsReleased { get; init; }
    public bool IsSigned { get; init; }
}