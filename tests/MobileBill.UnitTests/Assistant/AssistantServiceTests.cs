using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MobileBill.Application.Assistant;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Assistant;
using MobileBill.Infrastructure.Devices;
using MobileBill.Infrastructure.Insights;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Assistant;

public sealed class AssistantServiceTests
{
    private static readonly string[] RealValues = ["Sam Perera", "211464", "761499198"];

    [Fact]
    public async Task Names_epf_and_mobile_numbers_never_reach_the_model_but_the_answer_shows_them()
    {
        await using var db = await SeedAsync();
        var model = new ScriptedModel((messages, _) => messages[^1].Role == "user"
            ? new ChatModelResult(null, [new ChatToolCall("call-1", "find_allocations", """{"search":"MOBILE-1"}""")])
            : new ChatModelResult("MOBILE-1 is held by PERSON-1 (EPF EPF-1) on PPU23_700.", []));

        var reply = await Service(db, model).AskAsync(new AssistantAskRequest("Who holds 0761499198? Is it sam perera?"), default);

        Assert.Equal("761499198 is held by Sam Perera (EPF 211464) on PPU23_700.", reply.Reply);
        Assert.Equal("Who holds MOBILE-1? Is it PERSON-1?", model.Calls[0].Messages[^1].Content);
        var toolAnswer = model.Calls[1].Messages[^1];
        Assert.Equal(("tool", "call-1"), (toolAnswer.Role, toolAnswer.ToolCallId));
        Assert.Contains("PERSON-1", toolAnswer.Content);
        Assert.Contains("EPF-1", toolAnswer.Content);
        Assert.Contains("PPU23_700", toolAnswer.Content);
        foreach (var message in model.Calls.SelectMany(call => call.Messages))
            foreach (var value in RealValues) Assert.DoesNotContain(value, message.Content ?? "", StringComparison.OrdinalIgnoreCase);

        var audit = Assert.Single(await db.AuditLogs.Where(log => log.Action == "AssistantQuestion").ToListAsync());
        Assert.Equal((reply.ConversationId, "it-user"), (audit.EntityId, audit.PerformedBy));
        Assert.Contains("find_allocations", audit.AfterDataJson);
    }

    [Fact]
    public async Task A_conversation_remembers_earlier_messages_and_placeholders_for_its_own_user_only()
    {
        await using var db = await SeedAsync();
        var model = new ScriptedModel((messages, _) => new ChatModelResult($"Answer {messages.Count}", []));
        var cache = new MemoryCache(new MemoryCacheOptions());

        var first = await Service(db, model, cache).AskAsync(new AssistantAskRequest("Tell me about Sam Perera"), default);
        await Service(db, model, cache).AskAsync(new AssistantAskRequest("And Sam Perera's rental?", first.ConversationId), default);
        await Service(db, model, cache, new TestUser("hr-user")).AskAsync(new AssistantAskRequest("Hello", first.ConversationId), default);

        Assert.Equal(["system", "user", "assistant", "user"], model.Calls[1].Messages.Select(message => message.Role));
        Assert.Equal(["Tell me about PERSON-1", "And PERSON-1's rental?"], model.Calls[1].Messages.Where(message => message.Role == "user").Select(message => message.Content));
        Assert.Equal(2, model.Calls[2].Messages.Count);   // another user's request with the same id starts afresh
    }

    [Fact]
    public async Task After_the_tool_rounds_run_out_the_model_must_answer_without_tools()
    {
        await using var db = await SeedAsync();
        var model = new ScriptedModel((_, tools) => tools.Count > 0
            ? new ChatModelResult(null, [new ChatToolCall(Guid.NewGuid().ToString(), "get_sim_pool", "{}")])
            : new ChatModelResult("Here is what I found.", []));

        var reply = await Service(db, model).AskAsync(new AssistantAskRequest("Keep looking"), default);

        Assert.Equal("Here is what I found.", reply.Reply);
        Assert.Equal(7, model.Calls.Count);
        Assert.Empty(model.Calls[^1].Tools);
    }

    [Fact]
    public async Task An_empty_or_too_long_question_or_a_missing_key_is_refused_before_calling_the_model()
    {
        await using var db = await SeedAsync();
        var model = new ScriptedModel((_, _) => new ChatModelResult("x", []));

        await Assert.ThrowsAsync<AssistantValidationException>(() => Service(db, model).AskAsync(new AssistantAskRequest("  "), default));
        await Assert.ThrowsAsync<AssistantValidationException>(() => Service(db, model).AskAsync(new AssistantAskRequest(new string('a', 2001)), default));
        Assert.False(Service(db, new ScriptedModel((_, _) => new ChatModelResult("x", []), configured: false)).GetStatus().IsEnabled);
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => Service(db, new ScriptedModel((_, _) => new ChatModelResult("x", []), configured: false)).AskAsync(new AssistantAskRequest("Hi"), default));
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task Bill_history_is_masked_and_an_unknown_month_or_tool_comes_back_as_an_error_the_model_can_read()
    {
        await using var db = await SeedAsync();
        var toolbox = Toolbox(db);
        var masker = new PersonalDataMasker();

        using var history = JsonDocument.Parse(await toolbox.ExecuteAsync("get_number_bill_history", """{"mobile_number":"0761499198"}""", masker, default));
        var month = history.RootElement.GetProperty("months")[0];
        Assert.Equal(("2026-08", "PERSON-1", "EPF-1", 2800m, "ByUser"), (month.GetProperty("period").GetString(), month.GetProperty("holder").GetString(), month.GetProperty("epf").GetString(), month.GetProperty("calculatedExcess").GetDecimal(), month.GetProperty("responsibility").GetString()));
        Assert.Equal("MOBILE-1", history.RootElement.GetProperty("mobileNumber").GetString());

        Assert.Contains("no matched billing batch for 2026-01", await toolbox.ExecuteAsync("get_batch_summary", """{"period":"2026-01"}""", masker, default));
        Assert.Contains("not a billing month", await toolbox.ExecuteAsync("get_top_over_limit", """{"period":"August"}""", masker, default));
        Assert.Contains("no tool called", await toolbox.ExecuteAsync("delete_everything", "{}", masker, default));
        using var summary = JsonDocument.Parse(await toolbox.ExecuteAsync("get_batch_summary", "{}", masker, default));
        Assert.Equal(2800m, summary.RootElement.GetProperty("totals").GetProperty("totalCalculatedExcess").GetDecimal());
    }

    private static AssistantService Service(MobileBillDbContext db, IChatModelClient model, IMemoryCache? cache = null, TestUser? user = null) =>
        new(model, Toolbox(db, user), db, user ?? new TestUser("it-user"), new TestClock(), cache ?? new MemoryCache(new MemoryCacheOptions()));

    private static AssistantToolbox Toolbox(MobileBillDbContext db, TestUser? user = null)
    {
        var currentUser = user ?? new TestUser("it-user");
        return new(db, new EfBillingInsightsService(db, new TestClock()), new EfMasterDataService(db, currentUser, new TestClock()), new EfDeviceService(db, currentUser, new TestClock()));
    }

    private static async Task<MobileBillDbContext> SeedAsync()
    {
        var db = new MobileBillDbContext(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = new TelecomProvider { Code = "P01", Name = "Dialog" };
        var employee = new Employee { EPF = "211464", FullName = "Sam Perera", CallingName = "Sam", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        var package = new MobilePackage { Code = "PPU23_700", ProviderId = provider.Id, Description = "Plan", MonthlyRental = 700m, TotalWithTax = 940m, DefaultCreditLimit = 1000m };
        var account = new MobileAccount { MobileNumber = "761499198", EmployeeId = employee.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 700m, PackageId = package.Id };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 8, Status = BillBatchStatus.Completed };
        var line = new BillLine { BillBatchId = batch.Id, BillBatch = batch, MobileNumber = "761499198", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 4500m };
        db.AddRange(provider, new Factory { Code = "F", Name = "Head Office" }, new Department { Code = "D", Name = "IT" }, new EmployeeCategory { Code = "C", Name = "Staff" },
            new Designation { Code = "DS", Name = "Engineer" }, employee, package, account, batch, line,
            new MonthlyBill
            {
                EmployeeId = employee.Id, MobileAccountId = account.Id, BillLineId = line.Id, BillLine = line,
                EmployeeEpfSnapshot = "211464", EmployeeNameSnapshot = "Sam Perera", MobileNumberSnapshot = "761499198", PackageCodeSnapshot = "PPU23_700",
                CategoryCodeSnapshot = "C", CategoryNameSnapshot = "Staff", DesignationCodeSnapshot = "DS", DesignationNameSnapshot = "Engineer",
                FactoryCodeSnapshot = "F", FactoryNameSnapshot = "Head Office", DepartmentCodeSnapshot = "D", DepartmentNameSnapshot = "IT",
                EntitlementEffectiveFromSnapshot = new DateOnly(2026, 1, 1), CreditLimit = 1000m, MonthlyRental = 700m, ActualBill = 4500m, Variance = -2800m,
                CalculatedExcess = 2800m, Responsibility = Responsibility.ByUser, FinalDeduction = 2800m, AssessedAt = DateTimeOffset.UnixEpoch, Status = MonthlyBillStatus.Approved,
            });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class ScriptedModel(Func<IReadOnlyList<ChatModelMessage>, IReadOnlyList<ChatToolDefinition>, ChatModelResult> respond, bool configured = true) : IChatModelClient
    {
        public List<(IReadOnlyList<ChatModelMessage> Messages, IReadOnlyList<ChatToolDefinition> Tools)> Calls { get; } = [];
        public bool IsConfigured => configured;

        public Task<ChatModelResult> CompleteAsync(IReadOnlyList<ChatModelMessage> messages, IReadOnlyList<ChatToolDefinition> tools, CancellationToken cancellationToken)
        {
            Calls.Add((messages.ToList(), tools));
            return Task.FromResult(respond(messages, tools));
        }
    }

    private sealed class TestUser(string userId) : ICurrentUserService { public string UserId => userId; public string DisplayName => userId; public UserRole Role => UserRole.ITEngineer; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero); }
}
