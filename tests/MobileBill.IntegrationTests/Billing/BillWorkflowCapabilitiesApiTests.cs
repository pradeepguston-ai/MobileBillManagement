using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.IntegrationTests;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillWorkflowCapabilitiesApiTests
{
    [Theory]
    [InlineData(BillBatchStatus.Validated, UserRole.ITEngineer, true, false, false)]
    [InlineData(BillBatchStatus.Validated, UserRole.Administrator, true, false, false)]
    [InlineData(BillBatchStatus.Validated, UserRole.HeadOfIt, false, false, false)]
    [InlineData(BillBatchStatus.ITReview, UserRole.HeadOfIt, false, true, false)]
    [InlineData(BillBatchStatus.ITReview, UserRole.ITEngineer, false, false, false)]
    [InlineData(BillBatchStatus.HRApproval, UserRole.GroupHrManager, false, true, false)]
    [InlineData(BillBatchStatus.HRApproval, UserRole.HeadOfIt, false, false, false)]
    [InlineData(BillBatchStatus.FinanceApproval, UserRole.Cfo, false, true, false)]
    [InlineData(BillBatchStatus.FinanceApproval, UserRole.GroupHrManager, false, false, false)]
    [InlineData(BillBatchStatus.Completed, UserRole.Cfo, false, false, true)]
    [InlineData(BillBatchStatus.Completed, UserRole.ITEngineer, false, false, false)]
    [InlineData(BillBatchStatus.Locked, UserRole.Cfo, false, false, false)]
    public async Task Capabilities_reflect_the_caller_role_at_the_current_stage(BillBatchStatus status, UserRole role, bool canSubmit, bool canDecide, bool canLock)
    {
        using var fixture = new CapabilityFixture();
        var batchId = await fixture.SeedAsync(status);
        TestAuth.Authorize(fixture.Client, role);

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{batchId}/workflow-capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CapabilityResponse>();
        Assert.NotNull(result);
        Assert.Equal(status.ToString(), result.Status);
        Assert.Equal(canSubmit, result.CanSubmit);
        Assert.Equal(canDecide, result.CanApprove);
        Assert.Equal(canDecide, result.CanReject);
        Assert.Equal(canDecide, result.CanReturnForCorrection);
        Assert.Equal(canLock, result.CanLock);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected_and_read_does_not_change_batch()
    {
        using var fixture = new CapabilityFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed);

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{batchId}/workflow-capabilities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
        Assert.Equal(BillBatchStatus.Completed, (await db.BillBatches.SingleAsync(x => x.Id == batchId)).Status);
        Assert.Empty(await db.ApprovalHistories.Where(x => x.BillBatchId == batchId).ToListAsync());
        Assert.Empty(await db.AuditLogs.Where(x => x.EntityId == batchId).ToListAsync());
    }

    private sealed class CapabilityFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }
        public IServiceProvider Services => factory.Services;

        public CapabilityFixture()
        {
            var databaseName = $"workflow-capabilities-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public async Task<Guid> SeedAsync(BillBatchStatus status)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 8, Status = status, ValidationLevel = ValidationLevel.StructuralOnly };
            db.BillBatches.Add(batch);
            await db.SaveChangesAsync();
            return batch.Id;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }

    private sealed record CapabilityResponse(bool CanSubmit, bool CanApprove, bool CanReject, bool CanReturnForCorrection, bool CanLock, string CurrentStage, string Status);
}
