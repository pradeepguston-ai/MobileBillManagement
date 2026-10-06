using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.Reports;

namespace MobileBill.UnitTests.Reports;

public sealed class VasReportServiceTests
{
    [Fact]
    public async Task Lists_numbers_with_vas_largest_first_with_share_of_bill_and_repeat_use()
    {
        var (db, batches) = await SeedAsync();
        await using var _ = db;

        var report = await Service(db).GetAsync(new VasReportRequest(batches.September), default);

        Assert.Equal(["0771", "0773", "0772", "0774"], report.Rows.Select(row => row.MobileNumber));
        var top = report.Rows[0];
        Assert.Equal(("EPF-1", "Factory A", 300m, 1000m, 30.0m), (top.Epf, top.Factory, top.Vas, top.ActualBill, top.VasShareOfBill));
        Assert.Equal((3, 3, true), (top.MonthsWithVas, top.MonthsConsidered, top.IsRepeat));          // VAS in July, August and September
        Assert.Equal((2, true), (report.Rows[1].MonthsWithVas, report.Rows[1].IsRepeat));            // 0773: August and September
        Assert.Equal((1, false), (report.Rows[2].MonthsWithVas, report.Rows[2].IsRepeat));           // 0772: September only
        Assert.Equal((4, 650m, 2), (report.Users, report.TotalVas, report.RepeatUsers));
        Assert.Equal((2, 300m), (report.PreviousUsers!.Value, report.PreviousTotalVas!.Value));      // August
        Assert.True(report.IsPreliminary);
    }

    [Fact]
    public async Task Unmatched_lines_are_listed_without_employee_details_and_grouped_separately()
    {
        var (db, batches) = await SeedAsync();
        await using var _ = db;

        var report = await Service(db).GetAsync(new VasReportRequest(batches.September), default);

        var unmatched = report.Rows.Single(row => row.MobileNumber == "0774");
        Assert.Equal((false, null, null), (unmatched.IsMatched, unmatched.Epf, unmatched.Responsibility));
        Assert.Contains(report.ByFactory, group => group.Name == VasReportService.NotMatched && group.Users == 1 && group.TotalVas == 50m);
        Assert.Equal(("Factory A", 2, 500m), (report.ByFactory[0].Name, report.ByFactory[0].Users, report.ByFactory[0].TotalVas));
    }

    [Fact]
    public async Task Filters_narrow_the_list_and_the_previous_month_comparison()
    {
        var (db, batches) = await SeedAsync();
        await using var _ = db;

        var factoryB = await Service(db).GetAsync(new VasReportRequest(batches.September, FactoryCodes: ["FB"]), default);
        var repeatOnly = await Service(db).GetAsync(new VasReportRequest(batches.September, RepeatOnly: true), default);
        var minimum = await Service(db).GetAsync(new VasReportRequest(batches.September, MinimumVas: 150m), default);

        Assert.Equal(["0772"], factoryB.Rows.Select(row => row.MobileNumber));
        Assert.Equal((0, 0m), (factoryB.PreviousUsers!.Value, factoryB.PreviousTotalVas!.Value));
        Assert.Equal(["0771", "0773"], repeatOnly.Rows.Select(row => row.MobileNumber));
        Assert.Equal(["0771", "0773"], minimum.Rows.Select(row => row.MobileNumber));
        await Assert.ThrowsAsync<BillingReportFilterNotFoundException>(() => Service(db).GetAsync(new VasReportRequest(batches.September, FactoryCodes: ["NOPE"]), default));
    }

    [Fact]
    public async Task Only_matched_batches_are_offered_and_reported()
    {
        var (db, batches) = await SeedAsync();
        await using var _ = db;

        var offered = await Service(db).GetBatchesAsync(default);

        Assert.Equal([batches.September, batches.August, batches.July], offered.Select(batch => batch.BatchId));
        Assert.Equal((true, false), (offered[0].IsPreliminary, offered[2].IsPreliminary));          // September is in IT review; July is Locked
        await Assert.ThrowsAsync<BillingReportConflictException>(() => Service(db).GetAsync(new VasReportRequest(batches.Unmatched), default));
    }

    [Fact]
    public async Task Excel_and_pdf_contain_the_report()
    {
        var (db, batches) = await SeedAsync();
        await using var _ = db;

        var excel = await Service(db).ExportExcelAsync(new VasReportRequest(batches.September), default);
        var pdf = await Service(db).ExportPdfAsync(new VasReportRequest(batches.September), default);

        Assert.Equal("VAS_Report_2026_09.xlsx", excel.FileName);
        using var book = new XLWorkbook(new MemoryStream(excel.Content));
        var sheet = book.Worksheet("VAS Report");
        Assert.Equal("0771", sheet.Cell(7, 2).GetString());
        Assert.Equal(300m, sheet.Cell(7, 10).GetValue<decimal>());
        Assert.Equal("Yes", sheet.Cell(7, 16).GetString());
        Assert.Equal(650m, sheet.Cell(11, 10).GetValue<decimal>());
        Assert.Contains(book.Worksheets, worksheet => worksheet.Name == "Summary");
        Assert.Equal(("VAS_Report_2026_09.pdf", "%PDF"), (pdf.FileName, System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4)));
    }

    private static VasReportService Service(MobileBillDbContext db) => new(db, new TestClock());

    private sealed record Batches(Guid July, Guid August, Guid September, Guid Unmatched);

    // July (Locked): 0771 has VAS. August (Completed): 0771 and 0773. September (ITReview): 0771, 0772, 0773 and
    // the unmatched 0774; 0775 has no VAS. A fourth batch has VAS lines but was never matched.
    private static async Task<(MobileBillDbContext Db, Batches Batches)> SeedAsync()
    {
        var db = new MobileBillDbContext(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog" };
        db.Add(provider);
        var july = Batch(provider, 7, BillBatchStatus.Locked);
        var august = Batch(provider, 8, BillBatchStatus.Completed);
        var september = Batch(provider, 9, BillBatchStatus.ITReview);
        var unmatched = Batch(provider, 10, BillBatchStatus.Validated);
        db.AddRange(july, august, september, unmatched);
        Line(db, july, "0771", 100m, 500m, "EPF-1", "FA");
        Line(db, august, "0771", 200m, 800m, "EPF-1", "FA");
        Line(db, august, "0773", 100m, 400m, "EPF-3", "FA");
        Line(db, september, "0771", 300m, 1000m, "EPF-1", "FA");
        Line(db, september, "0772", 100m, 200m, "EPF-2", "FB");
        Line(db, september, "0773", 200m, 500m, "EPF-3", "FA");
        Line(db, september, "0775", 0m, 900m, "EPF-5", "FA");
        var lost = Line(db, september, "0774", 50m, 100m, null, null);
        db.Add(new BillException { BillBatchId = september.Id, BillLineId = lost.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "Not found" });
        Line(db, unmatched, "0771", 999m, 1000m, null, null);
        db.AddRange(new Factory { Code = "FA", Name = "Factory A" }, new Factory { Code = "FB", Name = "Factory B" });
        await db.SaveChangesAsync();
        return (db, new Batches(july.Id, august.Id, september.Id, unmatched.Id));
    }

    private static BillBatch Batch(TelecomProvider provider, int month, BillBatchStatus status) =>
        new() { ProviderId = provider.Id, Provider = provider, CorporateCode = "PR1", BillingYear = 2026, BillingMonth = month, Status = status };

    private static BillLine Line(MobileBillDbContext db, BillBatch batch, string mobile, decimal vas, decimal totalDue, string? epf, string? factory)
    {
        var line = new BillLine { BillBatchId = batch.Id, MobileNumber = mobile, PageNumber = 1, RawText = mobile, ExtractionStatus = BillLineExtractionStatus.Extracted, ValueAddedServices = vas, TotalDueAmount = totalDue };
        db.Add(line);
        if (epf is not null)
            db.Add(new MonthlyBill
            {
                EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, ActualBill = totalDue, CalculatedExcess = 0m, FinalDeduction = 0m,
                Responsibility = Responsibility.ByUser, AssessedAt = DateTimeOffset.UnixEpoch,
                EmployeeEpfSnapshot = epf, EmployeeNameSnapshot = $"Name {epf}", CategoryCodeSnapshot = "C", CategoryNameSnapshot = "Staff",
                DesignationCodeSnapshot = "D", FactoryCodeSnapshot = factory!, FactoryNameSnapshot = factory == "FA" ? "Factory A" : "Factory B",
                DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = "Department", MobileNumberSnapshot = mobile
            });
        return line;
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero); }
}
