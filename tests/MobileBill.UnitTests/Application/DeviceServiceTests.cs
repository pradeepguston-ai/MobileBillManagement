using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Devices;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Devices;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class DeviceServiceTests
{
    private const string Imei1 = "356789010000014", Imei2 = "356789010000022", Imei3 = "356789010000030", Imei4 = "356789010000048";
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Fact]
    public async Task A_registered_device_is_in_stock_and_its_tag_and_imeis_are_unique_and_valid()
    {
        await using var db = CreateContext(); var service = Service(db);
        await SeedAsync(db);

        var device = await service.CreateDeviceAsync(Request(" GL-MOB-0001 ", Imei1, Imei2), default);

        Assert.Equal(("GL-MOB-0001", DeviceStatus.InStock, Today, true), (device.AssetTag, device.Status, device.StatusSince, device.IsActive));
        Assert.Equal(63750.00m, device.CurrentValue);   // bought 12 months ago for 85,000
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateDeviceAsync(Request("GL-MOB-0001", Imei3), default));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateDeviceAsync(Request("GL-MOB-0002", Imei2), default));   // IMEI 2 of the first device
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateDeviceAsync(Request("GL-MOB-0002", "356789010000015"), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateDeviceAsync(Request("GL-MOB-0002", Imei3, Imei3), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateDeviceAsync(Request("GL-MOB-0002", Imei3) with { PurchaseDate = Today.AddDays(1) }, default));
    }

    [Fact]
    public async Task Issue_and_return_record_the_handover_and_free_the_device()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (holder, _) = await SeedAsync(db);
        var device = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);

        var issued = await service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), "New joiner"), default);
        Assert.Equal((DeviceStatus.Issued, "EPF-1", "Holder"), (issued.Status, issued.HolderEpf, issued.HolderName));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, Today, null), default));

        var returned = await service.ReturnAsync(device.Id, new ReturnDeviceRequest(new DateOnly(2026, 9, 30), DeviceStatus.InStock, DeviceReturnReason.Upgrade, "Good condition"), default);

        Assert.Equal((DeviceStatus.InStock, (Guid?)null), (returned.Status, returned.HolderEmployeeId));
        var history = Assert.Single((await service.GetIssuesAsync(new PagedRequest(), default)).Items);
        Assert.Equal((new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 30), DeviceStatus.InStock, DeviceReturnReason.Upgrade, (decimal?)null, "it-user"),
            (history.IssuedOn, history.ReturnedOn!.Value, history.ReturnCondition!.Value, history.ReturnReason!.Value, history.RecoverableAmount, history.IssuedBy));
        Assert.Equal(["DeviceIssued", "DeviceRegistered", "DeviceReturned"], (await db.AuditLogs.Select(log => log.Action).ToListAsync()).Order());
    }

    [Fact]
    public async Task Replace_returns_the_damaged_device_and_issues_a_new_one_to_the_same_person()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (holder, _) = await SeedAsync(db);
        var broken = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        var spare = await service.CreateDeviceAsync(Request("GL-MOB-0002", Imei2), default);
        await service.IssueAsync(broken.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);

        var replacement = await service.ReplaceAsync(broken.Id, new ReplaceDeviceRequest(spare.Id, Today, DeviceStatus.Damaged, "Screen broken", ChargeEmployee: true), default);

        Assert.Equal((spare.Id, DeviceStatus.Issued, holder.Id), (replacement.Id, replacement.Status, replacement.HolderEmployeeId!.Value));
        Assert.Equal(DeviceStatus.Damaged, (await service.GetDeviceAsync(broken.Id, default)).Status);
        var closed = (await service.GetIssuesAsync(new PagedRequest(Search: "GL-MOB-0001"), default)).Items.Single();
        Assert.Equal((DeviceReturnReason.Replaced, "GL-MOB-0002", "Screen broken", 63750.00m), (closed.ReturnReason!.Value, closed.ReplacementAssetTag, closed.ReturnNotes, closed.RecoverableAmount!.Value));
        Assert.Equal("Replacement for GL-MOB-0001", (await service.GetIssuesAsync(new PagedRequest(Search: "GL-MOB-0002"), default)).Items.Single().IssueNotes);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReplaceAsync(replacement.Id, new ReplaceDeviceRequest(broken.Id, Today, DeviceStatus.Damaged, null), default));   // not In Stock
    }

    [Fact]
    public async Task A_lost_device_records_its_depreciated_value_for_recovery()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (holder, _) = await SeedAsync(db);
        var device = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        await service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);

        var lost = await service.MarkLostAsync(device.Id, new MarkDeviceLostRequest(Today, "Lost while travelling"), default);

        Assert.Equal((DeviceStatus.Lost, false, 0m), (lost.Status, lost.IsActive, lost.CurrentValue));
        Assert.Equal(63750.00m, (await service.GetIssuesAsync(new PagedRequest(), default)).Items.Single().RecoverableAmount);
        Assert.Empty((await service.GetDevicesAsync(new PagedRequest(IsActive: true), default)).Items);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.MarkLostAsync(device.Id, new MarkDeviceLostRequest(Today, null), default));
    }

    [Fact]
    public async Task A_repaired_device_comes_back_in_stock_and_an_unused_one_can_be_retired()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (holder, _) = await SeedAsync(db);
        var device = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        await service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);
        await service.ReturnAsync(device.Id, new ReturnDeviceRequest(new DateOnly(2026, 9, 1), DeviceStatus.UnderRepair, DeviceReturnReason.Other, "Battery"), default);
        Assert.Equal(36, (await service.GetDeviceAsync(device.Id, default)).DaysInStatus);

        var repaired = await service.MarkRepairedAsync(device.Id, new DeviceStatusChangeRequest(Today, "New battery"), default);
        Assert.Equal((DeviceStatus.InStock, "New battery"), (repaired.Status, repaired.Notes));
        var retired = await service.RetireAsync(device.Id, new DeviceStatusChangeRequest(Today, "End of life"), default);
        Assert.Equal(DeviceStatus.Retired, retired.Status);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.MarkRepairedAsync(device.Id, new DeviceStatusChangeRequest(Today, null), default));
    }

    [Fact]
    public async Task On_resignation_a_held_device_becomes_return_pending_and_is_listed_to_collect()
    {
        await using var db = CreateContext(); var service = Service(db); var people = new EfMasterDataService(db, new TestUser(), new TestClock());
        var (holder, other) = await SeedAsync(db);
        var device = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        var kept = await service.CreateDeviceAsync(Request("GL-MOB-0002", Imei2), default);
        await service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);
        await service.IssueAsync(kept.Id, new IssueDeviceRequest(other.Id, new DateOnly(2026, 6, 1), null), default);

        await people.ResignEmployeeAsync(holder.Id, new ResignEmployeeRequest(new DateOnly(2026, 9, 20), null), default);   // 17 days ago

        var pending = await service.GetDeviceAsync(device.Id, default);
        Assert.Equal((DeviceStatus.ReturnPending, new DateOnly(2026, 9, 20), holder.Id), (pending.Status, pending.StatusSince, pending.HolderEmployeeId!.Value));
        Assert.Equal(DeviceStatus.Issued, (await service.GetDeviceAsync(kept.Id, default)).Status);
        var collect = Assert.Single(await service.GetToCollectAsync(default));
        Assert.Equal(("GL-MOB-0001", "EPF-1", 17, true, 63750.00m, new DateOnly(2026, 9, 20)), (collect.AssetTag, collect.Epf, collect.DaysWaiting, collect.IsOverdue, collect.RecoverableAmount, collect.ResignedOn!.Value));
        Assert.Equal(["GL-MOB-0001 (Samsung Galaxy A35)"], Assert.Single(await people.GetResignationsAsync(ResignationStatus.Resigned, default)).Devices);

        await service.ReturnAsync(device.Id, new ReturnDeviceRequest(Today, DeviceStatus.InStock, DeviceReturnReason.Resigned, "Collected"), default);
        Assert.Empty(await service.GetToCollectAsync(default));
    }

    [Fact]
    public async Task Pending_resignations_list_the_devices_the_employee_holds()
    {
        await using var db = CreateContext(); var service = Service(db); var people = new EfMasterDataService(db, new TestUser(), new TestClock());
        var (holder, _) = await SeedAsync(db);
        var device = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        await service.IssueAsync(device.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);

        await people.ResignEmployeeAsync(holder.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 31), null), default);

        Assert.Equal(["GL-MOB-0001 (Samsung Galaxy A35)"], Assert.Single(await people.GetResignationsAsync(ResignationStatus.Pending, default)).Devices);
        Assert.Equal(DeviceStatus.Issued, (await service.GetDeviceAsync(device.Id, default)).Status);
    }

    [Fact]
    public async Task Register_report_lists_devices_and_can_be_filtered_by_the_holders_factory()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (holder, _) = await SeedAsync(db);
        var issued = await service.CreateDeviceAsync(Request("GL-MOB-0001", Imei1), default);
        await service.CreateDeviceAsync(Request("GL-MOB-0002", Imei3), default);
        await service.IssueAsync(issued.Id, new IssueDeviceRequest(holder.Id, new DateOnly(2026, 6, 1), null), default);

        var all = await service.ExportRegisterExcelAsync(new DeviceRegisterRequest(), default);
        var filtered = await service.ExportRegisterExcelAsync(new DeviceRegisterRequest(FactoryCode: "F"), default);
        var pdf = await service.ExportRegisterPdfAsync(new DeviceRegisterRequest(), default);

        Assert.Equal(("Mobile_Device_Register.xlsx", "Mobile_Device_Register_F.xlsx"), (all.FileName, filtered.FileName));
        using var book = new XLWorkbook(new MemoryStream(all.Content));
        var sheet = book.Worksheet("Device Register");
        Assert.Equal(("Asset ID", "GL-MOB-0002", "In Stock"), (sheet.Cell(6, 1).GetString(), sheet.Cell(7, 1).GetString(), sheet.Cell(7, 12).GetString()));
        Assert.Equal(("GL-MOB-0001", "Issued", "EPF-1"), (sheet.Cell(8, 1).GetString(), sheet.Cell(8, 12).GetString(), sheet.Cell(8, 15).GetString()));
        using var filteredBook = new XLWorkbook(new MemoryStream(filtered.Content));
        Assert.Equal("Factory: Factory (F)", filteredBook.Worksheet(1).Cell(2, 2).GetString());
        Assert.Equal(("GL-MOB-0001", ""), (filteredBook.Worksheet(1).Cell(7, 1).GetString(), filteredBook.Worksheet(1).Cell(8, 1).GetString()));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ExportRegisterExcelAsync(new DeviceRegisterRequest(FactoryCode: "NOPE"), default));
    }

    private static MobileDeviceUpsertRequest Request(string assetTag, string imei1, string? imei2 = null) =>
        new(assetTag, imei1, imei2, "Samsung", "Galaxy A35", "SN-1", new DateOnly(2025, 10, 7), 85000m, new DateOnly(2027, 10, 7), "Abans", null);

    private static EfDeviceService Service(MobileBillDbContext db) => new(db, new TestUser(), new TestClock());

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Employee Holder, Employee Other)> SeedAsync(MobileBillDbContext context)
    {
        var holder = new Employee { EPF = "EPF-1", FullName = "Holder", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        var other = new Employee { EPF = "EPF-2", FullName = "Other", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F2", DepartmentCode = "D" };
        context.AddRange(new Factory { Code = "F", Name = "Factory" }, new Factory { Code = "F2", Name = "Second Factory" }, new Department { Code = "D", Name = "Department" },
            new EmployeeCategory { Code = "C", Name = "Category" }, new Designation { Code = "DS", Name = "Designation" }, holder, other);
        await context.SaveChangesAsync();
        return (holder, other);
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "it-user"; public string DisplayName => "IT User"; public UserRole Role => UserRole.ITEngineer; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero); }
}
