using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Application.Pdf;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillingInfrastructureTests
{
    [Fact]
    public async Task Validation_uses_structural_only_when_no_stated_total_exists()
    {
        await using var db = CreateDb();
        var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Parsed };
        db.BillBatches.Add(batch);
        db.BillLines.AddRange(new BillLine { BillBatchId = batch.Id, MobileNumber = "761499198", PageNumber = 1, RawText = "ok", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 100m }, new BillLine { BillBatchId = batch.Id, MobileNumber = "768791861", PageNumber = 1, RawText = "zero", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 0m });
        await db.SaveChangesAsync();
        var service = new EfBillBatchService(db, new NoopStorage(), new NoopParser(), new TestUser(), new TestClock(), new AllowAuthorization());

        var result = await service.ValidateAsync(batch.Id, CancellationToken.None);

        Assert.Equal(BillBatchStatus.Validated, result.Status);
        Assert.Equal(ValidationLevel.StructuralOnly, result.ValidationLevel);
        Assert.Equal(100m, result.CalculatedGrandTotal);
        Assert.Null(result.Difference);
        Assert.Contains("independently extracted", result.ValidationWarning);
    }

    [Fact]
    public async Task Failed_candidates_do_not_contribute_to_calculated_total_and_fail_validation()
    {
        await using var db = CreateDb();
        var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Parsed };
        db.BillBatches.Add(batch);
        db.BillLines.AddRange(new BillLine { BillBatchId = batch.Id, MobileNumber = "761499198", PageNumber = 1, RawText = "ok", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 100m }, new BillLine { BillBatchId = batch.Id, MobileNumber = "768791861", PageNumber = 1, RawText = "bad", ExtractionStatus = BillLineExtractionStatus.ValidationFailed, ExtractionError = "bad", TotalDueAmount = 999m });
        await db.SaveChangesAsync();
        var result = await new EfBillBatchService(db, new NoopStorage(), new NoopParser(), new TestUser(), new TestClock(), new AllowAuthorization()).ValidateAsync(batch.Id, CancellationToken.None);

        Assert.Equal(BillBatchStatus.ValidationFailed, result.Status);
        Assert.Equal(100m, result.CalculatedGrandTotal);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class TestUser : ICurrentUserService { public string UserId => "test"; public string DisplayName => "Test User"; public UserRole Role => UserRole.ITEngineer; }
    private sealed class AllowAuthorization : IBillReviewAuthorizationService { public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) => Task.FromResult(true); public Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero); }
    private sealed class NoopParser : IPdfBillParser { public Task<PdfBillParseResult> ParseAsync(Stream stream, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class NoopStorage : IBillFileStorage { public Task DeleteAsync(string path,CancellationToken t)=>Task.CompletedTask; public Task<string> FinalizeAsync(StagedBillFile s,Guid id,CancellationToken t)=>Task.FromResult("x"); public Task<Stream> OpenReadAsync(string p,CancellationToken t)=>throw new NotSupportedException(); public Task<StagedBillFile> StageAsync(Stream s,string n,string? c,long l,CancellationToken t)=>throw new NotSupportedException(); }
}
