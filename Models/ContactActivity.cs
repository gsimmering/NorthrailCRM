namespace NorthrailCRM.Models;

public sealed class ContactActivity
{
    public int Number { get; init; }
    public int? ContactNumber { get; set; }
    public int? TypeNumber { get; set; }
    public int? PersonNumber { get; set; }
    public int? EmployeeNumber { get; set; }
    public int? AssignedEmployeeNumber { get; set; }
    public int? VehicleGattungNumber { get; set; }
    public int? VehicleNumber { get; set; }
    public int? RentalContractNumber { get; set; }
    public DateTime? ActivityDate { get; set; }
    public DateTime? ActivityEndDate { get; set; }
    public string Type { get; init; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string PersonName { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string AssignedEmployeeName { get; init; } = string.Empty;
    public string VehicleGattungName { get; init; } = string.Empty;
    public string VehicleName { get; init; } = string.Empty;
    public string RentalContractName { get; init; } = string.Empty;
}

public sealed class ContactActivityType
{
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ContactActivityLookup
{
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ContactActivityAssociations
{
    public IReadOnlyList<ContactActivityLookup> Contacts { get; init; } = [];
    public IReadOnlyList<ContactActivityLookup> People { get; init; } = [];
    public IReadOnlyList<ContactActivityLookup> Employees { get; init; } = [];
    public IReadOnlyList<ContactActivityLookup> VehicleGattungen { get; init; } = [];
    public IReadOnlyList<ContactActivityLookup> Vehicles { get; init; } = [];
    public IReadOnlyList<ContactActivityLookup> RentalContracts { get; init; } = [];
}