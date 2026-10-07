using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using MobileBill.Application.Common;
using MobileBill.Application.Devices;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.Reports;

namespace MobileBill.Infrastructure.Devices;

// Company mobile devices: the register, every handover to and from employees, and devices to collect from leavers.
// Every change is written to the audit log.
public sealed class EfDeviceService(MobileBillDbContext db, ICurrentUserService currentUser, IClock clock) : IDeviceService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly DeviceStatus[] Held = [DeviceStatus.Issued, DeviceStatus.ReturnPending];
    private static readonly DeviceStatus[] OutOfUse = [DeviceStatus.Lost, DeviceStatus.Retired];

    static EfDeviceService() => ReportFonts.Configure();

    private DateTimeOffset Now => clock.UtcNow;
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    public async Task<PagedResult<MobileDeviceDto>> GetDevicesAsync(PagedRequest request, CancellationToken cancellationToken, DeviceStatus? status = null)
    {
        var query = db.MobileDevices.AsNoTracking();
        if (request.IsActive is { } active) query = active ? query.Where(device => !OutOfUse.Contains(device.Status)) : query.Where(device => OutOfUse.Contains(device.Status));
        if (status is not null) query = query.Where(device => device.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(device => device.AssetTag.Contains(search) || device.Imei1.Contains(search) || (device.Imei2 != null && device.Imei2.Contains(search))
                || device.Brand.Contains(search) || device.Model.Contains(search)
                || (device.CurrentEmployee != null && (device.CurrentEmployee.EPF.Contains(search) || device.CurrentEmployee.FullName.Contains(search))));
        }
        var total = await query.CountAsync(cancellationToken);
        var page = request.NormalizedPageNumber; var size = request.NormalizedPageSize;
        var rows = await Project(query.OrderBy(device => device.AssetTag).Skip((page - 1) * size).Take(size)).ToListAsync(cancellationToken);
        return new PagedResult<MobileDeviceDto>(rows.Select(ToDto).ToList(), page, size, total);
    }

    public async Task<MobileDeviceDto> GetDeviceAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await Project(db.MobileDevices.AsNoTracking().Where(device => device.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new MasterDataNotFoundException("Mobile device", id));

    public async Task<MobileDeviceDto> CreateDeviceAsync(MobileDeviceUpsertRequest request, CancellationToken cancellationToken)
    {
        var values = await ValidateAsync(request, null, cancellationToken);
        var device = new MobileDevice { AssetTag = values.AssetTag, Imei1 = values.Imei1, Brand = values.Brand, Model = values.Model, StatusSince = Today, CreatedAtUtc = Now, CreatedBy = currentUser.UserId };
        Apply(device, values);
        db.MobileDevices.Add(device);
        Audit(device.Id, "DeviceRegistered", new { }, Snapshot(device));
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(device.Id, cancellationToken);
    }

    public async Task<MobileDeviceDto> UpdateDeviceAsync(Guid id, MobileDeviceUpsertRequest request, CancellationToken cancellationToken)
    {
        var device = await FindAsync(id, cancellationToken);
        var values = await ValidateAsync(request, id, cancellationToken);
        var before = Snapshot(device);
        Apply(device, values);
        Touch(device);
        Audit(device.Id, "DeviceUpdated", before, Snapshot(device));
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<MobileDeviceDto> IssueAsync(Guid id, IssueDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await FindAsync(id, cancellationToken);
        if (device.Status != DeviceStatus.InStock) throw new MasterDataValidationException("Only a device In Stock can be issued.");
        await IssueToAsync(device, request.EmployeeId, request.IssuedOn, request.Notes, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<MobileDeviceDto> ReturnAsync(Guid id, ReturnDeviceRequest request, CancellationToken cancellationToken)
    {
        if (request.Condition is not (DeviceStatus.InStock or DeviceStatus.UnderRepair or DeviceStatus.Damaged))
            throw new MasterDataValidationException("A returned device is In Stock, Under Repair or Damaged.");
        if (request.Reason == DeviceReturnReason.Lost) throw new MasterDataValidationException("Use Mark Lost for a lost device.");
        var device = await FindAsync(id, cancellationToken);
        var issue = await OpenIssueAsync(device, cancellationToken);
        CloseIssue(device, issue, request.ReturnedOn, request.Condition, request.Reason, request.Notes, request.ChargeEmployee, null);
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<MobileDeviceDto> ReplaceAsync(Guid id, ReplaceDeviceRequest request, CancellationToken cancellationToken)
    {
        if (request.OldCondition is not (DeviceStatus.UnderRepair or DeviceStatus.Damaged))
            throw new MasterDataValidationException("The replaced device goes Under Repair or is Damaged.");
        if (request.NewDeviceId == id) throw new MasterDataValidationException("Choose a different device as the replacement.");
        var device = await FindAsync(id, cancellationToken);
        if (device.Status != DeviceStatus.Issued) throw new MasterDataValidationException("Only an issued device can be replaced.");
        var replacement = await FindAsync(request.NewDeviceId, cancellationToken);
        if (replacement.Status != DeviceStatus.InStock) throw new MasterDataValidationException("The replacement device must be In Stock.");
        var issue = await OpenIssueAsync(device, cancellationToken);

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        CloseIssue(device, issue, request.ReplacedOn, request.OldCondition, DeviceReturnReason.Replaced, request.Notes, request.ChargeEmployee, replacement.Id);
        await IssueToAsync(replacement, issue.EmployeeId, request.ReplacedOn, $"Replacement for {device.AssetTag}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetDeviceAsync(replacement.Id, cancellationToken);
    }

    public async Task<MobileDeviceDto> MarkLostAsync(Guid id, MarkDeviceLostRequest request, CancellationToken cancellationToken)
    {
        var device = await FindAsync(id, cancellationToken);
        if (OutOfUse.Contains(device.Status)) throw new MasterDataValidationException("This device is already lost or retired.");
        if (Held.Contains(device.Status))
        {
            var issue = await OpenIssueAsync(device, cancellationToken);
            CloseIssue(device, issue, request.LostOn, DeviceStatus.Lost, DeviceReturnReason.Lost, request.Notes, request.ChargeEmployee, null);
        }
        else SetStatus(device, DeviceStatus.Lost, request.LostOn, request.Notes, "DeviceLost");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<MobileDeviceDto> MarkRepairedAsync(Guid id, DeviceStatusChangeRequest request, CancellationToken cancellationToken)
    {
        var device = await FindAsync(id, cancellationToken);
        if (device.Status is not (DeviceStatus.UnderRepair or DeviceStatus.Damaged)) throw new MasterDataValidationException("Only a device Under Repair or Damaged can come back In Stock.");
        SetStatus(device, DeviceStatus.InStock, request.On, request.Notes, "DeviceRepaired");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<MobileDeviceDto> RetireAsync(Guid id, DeviceStatusChangeRequest request, CancellationToken cancellationToken)
    {
        var device = await FindAsync(id, cancellationToken);
        if (device.Status is not (DeviceStatus.InStock or DeviceStatus.Damaged or DeviceStatus.UnderRepair)) throw new MasterDataValidationException("Only a device that is not with an employee can be retired.");
        SetStatus(device, DeviceStatus.Retired, request.On, request.Notes, "DeviceRetired");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDeviceAsync(id, cancellationToken);
    }

    public async Task<PagedResult<DeviceIssueDto>> GetIssuesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = db.DeviceIssues.AsNoTracking();
        if (request.IsActive is { } open) query = query.Where(issue => (issue.ReturnedOn == null) == open);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(issue => issue.Device.AssetTag.Contains(search) || issue.Device.Imei1.Contains(search) || issue.Employee.EPF.Contains(search) || issue.Employee.FullName.Contains(search));
        }
        var total = await query.CountAsync(cancellationToken);
        var page = request.NormalizedPageNumber; var size = request.NormalizedPageSize;
        var items = await query.OrderByDescending(issue => issue.IssuedOn).ThenByDescending(issue => issue.CreatedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(issue => new DeviceIssueDto(issue.Id, issue.DeviceId, issue.Device.AssetTag, issue.Device.Brand, issue.Device.Model,
                issue.EmployeeId, issue.Employee.EPF, issue.Employee.FullName, issue.Employee.Factory.Name, issue.Employee.Department.Name,
                issue.IssuedOn, issue.CreatedBy, issue.IssueNotes, issue.ReturnedOn, issue.ReturnCondition, issue.ReturnReason, issue.ReturnNotes,
                issue.ReplacementDevice != null ? issue.ReplacementDevice.AssetTag : null, issue.RecoverableAmount))
            .ToListAsync(cancellationToken);
        return new PagedResult<DeviceIssueDto>(items, page, size, total);
    }

    public async Task<IReadOnlyList<DeviceToCollectDto>> GetToCollectAsync(CancellationToken cancellationToken)
    {
        var today = Today;
        var rows = await db.MobileDevices.AsNoTracking()
            .Where(device => device.Status == DeviceStatus.ReturnPending && device.CurrentEmployee != null)
            .OrderBy(device => device.StatusSince).ThenBy(device => device.AssetTag)
            .Select(device => new
            {
                device.Id, device.AssetTag, device.Brand, device.Model, device.Imei1, device.PurchaseCost, device.PurchaseDate, device.StatusSince,
                EmployeeId = device.CurrentEmployeeId!.Value, device.CurrentEmployee!.EPF, device.CurrentEmployee.FullName,
                Factory = device.CurrentEmployee.Factory.Name, Department = device.CurrentEmployee.Department.Name, device.CurrentEmployee.ResignedOn
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new DeviceToCollectDto(row.Id, row.AssetTag, row.Brand, row.Model, row.Imei1, row.EmployeeId, row.EPF, row.FullName, row.Factory, row.Department,
            row.ResignedOn, row.StatusSince, DeviceRules.DaysWaiting(row.StatusSince, today), DeviceRules.IsCollectionOverdue(row.StatusSince, today),
            row.PurchaseCost, DeviceRules.RecoverableAmount(row.PurchaseCost, row.PurchaseDate, today))).ToList();
    }

    public async Task<DeviceReportFile> ExportRegisterExcelAsync(DeviceRegisterRequest request, CancellationToken cancellationToken)
    {
        var (devices, filter) = await RegisterAsync(request, cancellationToken);
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Device Register");
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;
        sheet.Cell(1, 1).Value = "Mobile Device Register";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Filters"; sheet.Cell(2, 2).Value = filter;
        sheet.Cell(3, 1).Value = "Generated (UTC)"; sheet.Cell(3, 2).Value = Now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Range(2, 1, 3, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(4, 1).Style.Font.Italic = true;
        const int headerRow = 6;
        for (var column = 0; column < RegisterHeadings.Length; column++) sheet.Cell(headerRow, column + 1).Value = RegisterHeadings[column];
        var header = sheet.Range(headerRow, 1, headerRow, RegisterHeadings.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        for (var index = 0; index < devices.Count; index++)
        {
            var values = RegisterValues(devices[index]);
            for (var column = 0; column < values.Length; column++)
            {
                var cell = sheet.Cell(headerRow + 1 + index, column + 1);
                if (values[column] is decimal amount) { cell.Value = amount; cell.Style.NumberFormat.Format = "#,##0.00"; }
                else if (values[column] is int number) cell.Value = number;
                else cell.Value = values[column]?.ToString() ?? string.Empty;
            }
        }
        var totalRow = headerRow + devices.Count + 2;
        sheet.Cell(totalRow, 1).Value = "Devices"; sheet.Cell(totalRow, 2).Value = devices.Count;
        sheet.Cell(totalRow + 1, 1).Value = "Purchase cost"; sheet.Cell(totalRow + 1, 2).Value = devices.Sum(device => device.PurchaseCost);
        sheet.Cell(totalRow + 2, 1).Value = "Current value"; sheet.Cell(totalRow + 2, 2).Value = devices.Sum(device => device.CurrentValue);
        sheet.Range(totalRow + 1, 2, totalRow + 2, 2).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(totalRow, 1, totalRow + 2, 1).Style.Font.Bold = true;
        foreach (var group in devices.GroupBy(device => device.Status).OrderBy(group => group.Key))
        {
            totalRow++;
            sheet.Cell(totalRow + 2, 1).Value = StatusLabel(group.Key); sheet.Cell(totalRow + 2, 2).Value = group.Count();
        }
        sheet.Columns().AdjustToContents(headerRow, headerRow + devices.Count, 10, 40);
        sheet.SheetView.FreezeRows(headerRow);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return new DeviceReportFile(FileName(request, "xlsx"), ExcelContentType, stream.ToArray());
    }

    public async Task<DeviceReportFile> ExportRegisterPdfAsync(DeviceRegisterRequest request, CancellationToken cancellationToken)
    {
        var (devices, filter) = await RegisterAsync(request, cancellationToken);
        var document = new Document();
        document.Info.Title = "Mobile Device Register";
        document.Info.Subject = BillingReportData.SystemGeneratedNotice;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 6.5;
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromMillimeter(10);
        section.PageSetup.TopMargin = Unit.FromMillimeter(10);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(16);
        section.PageSetup.FooterDistance = Unit.FromMillimeter(6);
        var title = section.AddParagraph("Mobile Device Register");
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        var info = section.AddParagraph();
        info.AddFormattedText("Filters: ", TextFormat.Bold); info.AddText(filter);
        info.AddFormattedText("    Generated (UTC): ", TextFormat.Bold); info.AddText(Now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        info.AddFormattedText("    Devices: ", TextFormat.Bold); info.AddText(devices.Count.ToString(CultureInfo.InvariantCulture));
        info.Format.SpaceAfter = Unit.FromPoint(6);

        // Widths in millimetres; they add up to the 277mm printable width of A4 landscape.
        double[] widths = [18, 25, 18, 32, 16, 18, 17, 14, 15, 30, 26, 24, 24];
        string[] headings = ["Asset ID", "IMEI 1", "Brand", "Model", "Purchased", "Cost", "Current Value", "Status", "Since", "Holder", "Factory", "Department", "Notes"];
        var table = section.AddTable();
        table.Borders.Bottom.Width = 0.25;
        table.Borders.Bottom.Color = Color.Parse("#D9E2F3");
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);
        foreach (var width in widths) table.AddColumn(Unit.FromMillimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.Parse("#1F4E78");
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        for (var column = 0; column < headings.Length; column++) header.Cells[column].AddParagraph(headings[column]);
        foreach (var device in devices)
        {
            var row = table.AddRow();
            string?[] values = [device.AssetTag, device.Imei1, device.Brand, device.Model, Date(device.PurchaseDate), Money(device.PurchaseCost), Money(device.CurrentValue),
                StatusLabel(device.Status), Date(device.StatusSince), device.HolderName is null ? null : $"{device.HolderEpf} — {device.HolderName}", device.HolderFactory, device.HolderDepartment, device.Notes];
            for (var column = 0; column < values.Length; column++)
                if (!string.IsNullOrEmpty(values[column]))
                {
                    var paragraph = row.Cells[column].AddParagraph(values[column]!);
                    if (column is 5 or 6) paragraph.Format.Alignment = ParagraphAlignment.Right;
                }
        }
        var totals = table.AddRow();
        totals.Format.Font.Bold = true;
        totals.Cells[0].AddParagraph("Totals");
        totals.Cells[5].AddParagraph(Money(devices.Sum(device => device.PurchaseCost))).Format.Alignment = ParagraphAlignment.Right;
        totals.Cells[6].AddParagraph(Money(devices.Sum(device => device.CurrentValue))).Format.Alignment = ParagraphAlignment.Right;
        var closing = section.AddParagraph(BillingReportData.SystemGeneratedNotice);
        closing.Format.Font.Italic = true;
        closing.Format.SpaceBefore = Unit.FromPoint(10);
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText(BillingReportData.SystemGeneratedNotice + "   |   Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return new DeviceReportFile(FileName(request, "pdf"), PdfBillingReportService.PdfContentType, stream.ToArray());
    }

    private static readonly string[] RegisterHeadings =
    [
        "Asset ID", "IMEI 1", "IMEI 2", "Brand", "Model", "Serial No.", "Purchase Date", "Purchase Cost", "Current Value", "Warranty Until", "Supplier",
        "Status", "Status Since", "Days in Status", "Holder EPF", "Holder Name", "Factory", "Department", "Notes"
    ];

    private static object?[] RegisterValues(MobileDeviceDto device) =>
    [
        device.AssetTag, device.Imei1, device.Imei2, device.Brand, device.Model, device.SerialNumber, Date(device.PurchaseDate), device.PurchaseCost, device.CurrentValue,
        device.WarrantyUntil is { } warranty ? Date(warranty) : null, device.Supplier, StatusLabel(device.Status), Date(device.StatusSince), device.DaysInStatus,
        device.HolderEpf, device.HolderName, device.HolderFactory, device.HolderDepartment, device.Notes
    ];

    // Filtering by factory or department lists only the devices held by employees there.
    private async Task<(List<MobileDeviceDto> Devices, string Filter)> RegisterAsync(DeviceRegisterRequest request, CancellationToken token)
    {
        var factory = string.IsNullOrWhiteSpace(request.FactoryCode) ? null : request.FactoryCode.Trim();
        var department = string.IsNullOrWhiteSpace(request.DepartmentCode) ? null : request.DepartmentCode.Trim();
        var query = db.MobileDevices.AsNoTracking();
        var filters = new List<string>();
        if (factory is not null)
        {
            var name = await db.Factories.AsNoTracking().Where(item => item.Code == factory).Select(item => item.Name).SingleOrDefaultAsync(token)
                ?? throw new MasterDataValidationException($"Factory '{factory}' was not found.");
            query = query.Where(device => device.CurrentEmployee != null && device.CurrentEmployee.FactoryCode == factory);
            filters.Add($"Factory: {name} ({factory})");
        }
        if (department is not null)
        {
            var name = await db.Departments.AsNoTracking().Where(item => item.Code == department).Select(item => item.Name).SingleOrDefaultAsync(token)
                ?? throw new MasterDataValidationException($"Department '{department}' was not found.");
            query = query.Where(device => device.CurrentEmployee != null && device.CurrentEmployee.DepartmentCode == department);
            filters.Add($"Department: {name} ({department})");
        }
        var rows = await Project(query.OrderBy(device => device.Status).ThenBy(device => device.AssetTag)).ToListAsync(token);
        return (rows.Select(ToDto).ToList(), filters.Count == 0 ? "All devices" : string.Join(", ", filters));
    }

    private static string FileName(DeviceRegisterRequest request, string extension)
    {
        var suffix = string.Concat(new[] { request.FactoryCode, request.DepartmentCode }.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => "_" + code!.Trim()));
        return $"Mobile_Device_Register{suffix}.{extension}";
    }

    // Issues a device to an active employee (the caller has checked it is In Stock); the caller saves.
    private async Task IssueToAsync(MobileDevice device, Guid employeeId, DateOnly issuedOn, string? notes, CancellationToken token)
    {
        if (employeeId == Guid.Empty || !await db.Employees.AnyAsync(employee => employee.Id == employeeId && employee.IsActive, token))
            throw new MasterDataValidationException("An active employee is required.");
        if (issuedOn > Today) throw new MasterDataValidationException("The issue date cannot be in the future.");
        var issue = new DeviceIssue { DeviceId = device.Id, EmployeeId = employeeId, IssuedOn = issuedOn, IssueNotes = Trim(notes, 1000, "Notes"), CreatedAtUtc = Now, CreatedBy = currentUser.UserId };
        db.DeviceIssues.Add(issue);
        device.Status = DeviceStatus.Issued;
        device.StatusSince = issuedOn;
        device.CurrentEmployeeId = employeeId;
        Touch(device);
        Audit(device.Id, "DeviceIssued", new { Status = nameof(DeviceStatus.InStock) }, new { EmployeeId = employeeId, IssuedOn = issuedOn, Notes = issue.IssueNotes });
    }

    // Closes the open handover and moves the device to its new state; the caller saves.
    private void CloseIssue(MobileDevice device, DeviceIssue issue, DateOnly on, DeviceStatus condition, DeviceReturnReason reason, string? notes, bool chargeEmployee, Guid? replacementId)
    {
        if (on < issue.IssuedOn) throw new MasterDataValidationException("The date cannot be before the device was issued.");
        if (on > Today) throw new MasterDataValidationException("The date cannot be in the future.");
        issue.ReturnedOn = on;
        issue.ReturnCondition = condition;
        issue.ReturnReason = reason;
        issue.ReturnNotes = Trim(notes, 1000, "Notes");
        issue.ReplacementDeviceId = replacementId;
        issue.RecoverableAmount = chargeEmployee ? DeviceRules.RecoverableAmount(device.PurchaseCost, device.PurchaseDate, on) : null;
        issue.UpdatedAtUtc = Now;
        issue.UpdatedBy = currentUser.UserId;
        var before = new { Status = device.Status.ToString(), device.CurrentEmployeeId };
        device.Status = condition;
        device.StatusSince = on;
        device.CurrentEmployeeId = null;
        Touch(device);
        Audit(device.Id, condition == DeviceStatus.Lost ? "DeviceLost" : reason == DeviceReturnReason.Replaced ? "DeviceReplaced" : "DeviceReturned", before,
            new { Status = condition.ToString(), Reason = reason.ToString(), ReturnedOn = on, issue.ReturnNotes, issue.RecoverableAmount, ReplacementDeviceId = replacementId });
    }

    private void SetStatus(MobileDevice device, DeviceStatus status, DateOnly on, string? notes, string action)
    {
        if (on > Today) throw new MasterDataValidationException("The date cannot be in the future.");
        var before = new { Status = device.Status.ToString() };
        device.Status = status;
        device.StatusSince = on;
        if (Trim(notes, 1000, "Notes") is { } text) device.Notes = text;
        Touch(device);
        Audit(device.Id, action, before, new { Status = status.ToString(), On = on, Notes = Trim(notes, 1000, "Notes") });
    }

    private async Task<DeviceIssue> OpenIssueAsync(MobileDevice device, CancellationToken token)
    {
        if (!Held.Contains(device.Status)) throw new MasterDataValidationException("This device is not with an employee.");
        return await db.DeviceIssues.Where(issue => issue.DeviceId == device.Id && issue.ReturnedOn == null).OrderByDescending(issue => issue.IssuedOn).FirstOrDefaultAsync(token)
            ?? throw new MasterDataValidationException("This device has no open issue record.");
    }

    private async Task<MobileDevice> FindAsync(Guid id, CancellationToken token) =>
        await db.MobileDevices.FindAsync([id], token) ?? throw new MasterDataNotFoundException("Mobile device", id);

    private sealed record DeviceValues(string AssetTag, string Imei1, string? Imei2, string Brand, string Model, string? SerialNumber, DateOnly PurchaseDate, decimal PurchaseCost, DateOnly? WarrantyUntil, string? Supplier, string? Notes);

    private async Task<DeviceValues> ValidateAsync(MobileDeviceUpsertRequest request, Guid? id, CancellationToken token)
    {
        var assetTag = Required(request.AssetTag, 50, "Asset ID");
        var imei1 = Required(request.Imei1, 15, "IMEI 1");
        var imei2 = string.IsNullOrWhiteSpace(request.Imei2) ? null : request.Imei2.Trim();
        if (!DeviceRules.IsValidImei(imei1)) throw new MasterDataValidationException("IMEI 1 must be a valid 15-digit IMEI.");
        if (imei2 is not null && !DeviceRules.IsValidImei(imei2)) throw new MasterDataValidationException("IMEI 2 must be a valid 15-digit IMEI.");
        if (imei2 == imei1) throw new MasterDataValidationException("IMEI 2 must differ from IMEI 1.");
        MasterDataValidation.RequireCurrencyAmount(request.PurchaseCost, "Purchase Cost");
        if (request.PurchaseDate > Today) throw new MasterDataValidationException("The purchase date cannot be in the future.");
        if (request.WarrantyUntil is { } warranty && warranty < request.PurchaseDate) throw new MasterDataValidationException("Warranty Until cannot be before the purchase date.");
        if (await db.MobileDevices.AnyAsync(device => device.Id != id && device.AssetTag == assetTag, token)) throw new MasterDataConflictException("This Asset ID is already registered.");
        var imeis = imei2 is null ? new[] { imei1 } : new[] { imei1, imei2 };
        if (await db.MobileDevices.AnyAsync(device => device.Id != id && (imeis.Contains(device.Imei1) || (device.Imei2 != null && imeis.Contains(device.Imei2))), token))
            throw new MasterDataConflictException("This IMEI is already registered to another device.");
        return new DeviceValues(assetTag, imei1, imei2, Required(request.Brand, 100, "Brand"), Required(request.Model, 100, "Model"), Trim(request.SerialNumber, 100, "Serial No."),
            request.PurchaseDate, request.PurchaseCost, request.WarrantyUntil, Trim(request.Supplier, 200, "Supplier"), Trim(request.Notes, 1000, "Notes"));
    }

    private static void Apply(MobileDevice device, DeviceValues values)
    {
        device.AssetTag = values.AssetTag; device.Imei1 = values.Imei1; device.Imei2 = values.Imei2; device.Brand = values.Brand; device.Model = values.Model;
        device.SerialNumber = values.SerialNumber; device.PurchaseDate = values.PurchaseDate; device.PurchaseCost = values.PurchaseCost;
        device.WarrantyUntil = values.WarrantyUntil; device.Supplier = values.Supplier; device.Notes = values.Notes;
    }

    private static string Required(string? value, int maxLength, string field)
    {
        MasterDataValidation.RequireText(value ?? string.Empty, field);
        return Trim(value, maxLength, field)!;
    }

    private static string? Trim(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > maxLength) throw new MasterDataValidationException($"{field} must be {maxLength} characters or fewer.");
        return text;
    }

    private void Touch(MobileDevice device)
    {
        device.UpdatedAtUtc = Now;
        device.UpdatedBy = currentUser.UserId;
    }

    private void Audit(Guid deviceId, string action, object before, object after) =>
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(MobileDevice), EntityId = deviceId, Action = action,
            BeforeDataJson = JsonSerializer.Serialize(before), AfterDataJson = JsonSerializer.Serialize(after),
            PerformedBy = currentUser.UserId, PerformedAt = Now, CreatedAtUtc = Now, CreatedBy = currentUser.UserId
        });

    private static object Snapshot(MobileDevice device) => new { device.AssetTag, device.Imei1, device.Imei2, device.Brand, device.Model, device.SerialNumber, device.PurchaseDate, device.PurchaseCost, device.WarrantyUntil, device.Supplier, device.Notes };

    private sealed record DeviceRow(
        Guid Id, string AssetTag, string Imei1, string? Imei2, string Brand, string Model, string? SerialNumber, DateOnly PurchaseDate, decimal PurchaseCost,
        DateOnly? WarrantyUntil, string? Supplier, string? Notes, DeviceStatus Status, DateOnly StatusSince,
        Guid? HolderId, string? HolderEpf, string? HolderName, string? HolderFactory, string? HolderDepartment);

    private static IQueryable<DeviceRow> Project(IQueryable<MobileDevice> query) => query.Select(device => new DeviceRow(
        device.Id, device.AssetTag, device.Imei1, device.Imei2, device.Brand, device.Model, device.SerialNumber, device.PurchaseDate, device.PurchaseCost,
        device.WarrantyUntil, device.Supplier, device.Notes, device.Status, device.StatusSince, device.CurrentEmployeeId,
        device.CurrentEmployee != null ? device.CurrentEmployee.EPF : null, device.CurrentEmployee != null ? device.CurrentEmployee.FullName : null,
        device.CurrentEmployee != null ? device.CurrentEmployee.Factory.Name : null, device.CurrentEmployee != null ? device.CurrentEmployee.Department.Name : null));

    private MobileDeviceDto ToDto(DeviceRow row)
    {
        var today = Today;
        return new MobileDeviceDto(row.Id, row.AssetTag, row.Imei1, row.Imei2, row.Brand, row.Model, row.SerialNumber, row.PurchaseDate, row.PurchaseCost,
            row.WarrantyUntil, row.Supplier, row.Notes, row.Status, row.StatusSince, DeviceRules.DaysWaiting(row.StatusSince, today),
            row.HolderId, row.HolderEpf, row.HolderName, row.HolderFactory, row.HolderDepartment, !OutOfUse.Contains(row.Status),
            OutOfUse.Contains(row.Status) ? 0m : DeviceRules.RecoverableAmount(row.PurchaseCost, row.PurchaseDate, today));
    }

    internal static string StatusLabel(DeviceStatus status) => status switch
    {
        DeviceStatus.InStock => "In Stock",
        DeviceStatus.ReturnPending => "Return Pending",
        DeviceStatus.UnderRepair => "Under Repair",
        _ => status.ToString(),
    };

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
