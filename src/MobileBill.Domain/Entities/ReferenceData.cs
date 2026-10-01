using MobileBill.Domain.Common;

namespace MobileBill.Domain.Entities;

public sealed class Factory : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Employee> Employees { get; } = new List<Employee>();
}

public sealed class Department : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Employee> Employees { get; } = new List<Employee>();
}

public sealed class Designation : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Employee> Employees { get; } = new List<Employee>();
}

public sealed class EmployeeCategory : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Employee> Employees { get; } = new List<Employee>();
}

public sealed class TelecomProvider : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<BillBatch> BillBatches { get; } = new List<BillBatch>();
}

public sealed class DeductionRule : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
