namespace MobileBill.Domain.Enums;

// A company mobile device's state. Issued and ReturnPending devices are with an employee; ReturnPending means the
// employee has left and the device still has to be collected.
public enum DeviceStatus
{
    InStock,
    Issued,
    ReturnPending,
    UnderRepair,
    Damaged,
    Lost,
    Retired
}

// Why a device came back from an employee.
public enum DeviceReturnReason
{
    Resigned,
    Replaced,
    Upgrade,
    Lost,
    Other
}
