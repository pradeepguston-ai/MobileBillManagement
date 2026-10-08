using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Assistant;
using MobileBill.Application.Common;
using MobileBill.Application.Devices;
using MobileBill.Application.Insights;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Assistant;

// The read-only tools the assistant may call. Each one uses the same services as the screens, so it shows only
// what any signed-in user can already see, and masks names, EPF numbers and mobile numbers in what it returns.
public sealed class AssistantToolbox(MobileBillDbContext db, IBillingInsightsService insights, IMasterDataService masterData, IDeviceService devices)
{
    private const int MaxHistoryMonths = 24;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string PeriodSchema = """{ "type": "string", "description": "Billing month as YYYY-MM, for example 2026-08. Leave out for the latest batch." }""";

    public static readonly IReadOnlyList<ChatToolDefinition> Definitions =
    [
        Tool("list_billing_batches", "Lists the billing batches that have been matched to employees: billing month, provider, workflow status, whether approved, and how many numbers.", "{}"),
        Tool("get_batch_summary",
            "Totals for one billing batch: actual bill, entitlement, calculated excess, deducted from employees, borne by company, not yet assigned, roaming, numbers over limit, comparison with the previous approved batch, the excess split, bill ranges, the charge mix and cost by factory, department and category.",
            $$"""{ "period": {{PeriodSchema}} }"""),
        Tool("get_top_over_limit", "The 10 numbers with the highest calculated excess in a billing batch, with holder, actual bill, entitlement, responsibility and reason.", $$"""{ "period": {{PeriodSchema}} }"""),
        Tool("get_repeat_over_limit", "Numbers over their limit in 3 or more of the last 6 billing months up to a batch.", $$"""{ "period": {{PeriodSchema}} }"""),
        Tool("get_number_bill_history",
            "Month-by-month bills of one mobile number: holder, package, actual bill, entitlement, calculated excess, final deduction and responsibility.",
            """{ "mobile_number": { "type": "string", "description": "The mobile number or its placeholder, for example MOBILE-2." }, "months": { "type": "integer", "description": "How many recent months, 1 to 24. Default 6." } }""",
            "mobile_number"),
        Tool("find_allocations",
            "Finds mobile allocations by mobile number, EPF, employee name or package code: holder, factory, department, package, SIM type, credit limit, rental and SIM status.",
            """{ "search": { "type": "string", "description": "A mobile number, EPF, employee name, package code, or a placeholder such as PERSON-1." } }""",
            "search"),
        Tool("get_sim_pool", "SIMs waiting in the SIM Pool after their holder resigned, with the previous holder and days in the pool.", "{}"),
        Tool("get_devices_to_collect", "Company mobile devices still to be collected from employees who left, with days waiting and the amount that may be recovered.", "{}"),
        Tool("get_assets_summary", "Active allocations by SIM type, and company devices by status with the depreciated value still with employees.", "{}"),
    ];

    // Returns the tool's answer as JSON. A problem the model can recover from (an unknown period, a bad argument)
    // comes back as {"error": ...} rather than an exception.
    public async Task<string> ExecuteAsync(string name, string argumentsJson, PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try { arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement; }
        catch (JsonException) { return Error("The tool arguments were not valid JSON."); }

        try
        {
            object result = name switch
            {
                "list_billing_batches" => await ListBatchesAsync(cancellationToken),
                "get_batch_summary" => await BatchSummaryAsync(Text(arguments, "period"), cancellationToken),
                "get_top_over_limit" => await TopOverLimitAsync(Text(arguments, "period"), masker, cancellationToken),
                "get_repeat_over_limit" => await RepeatOverLimitAsync(Text(arguments, "period"), masker, cancellationToken),
                "get_number_bill_history" => await NumberHistoryAsync(masker.Unmask(Text(arguments, "mobile_number")), Number(arguments, "months") ?? 6, masker, cancellationToken),
                "find_allocations" => await FindAllocationsAsync(masker.Unmask(Text(arguments, "search")), masker, cancellationToken),
                "get_sim_pool" => await SimPoolAsync(masker, cancellationToken),
                "get_devices_to_collect" => await DevicesToCollectAsync(masker, cancellationToken),
                "get_assets_summary" => (await insights.GetAsync(null, 1, cancellationToken)).Assets,
                _ => new { error = $"There is no tool called {name}." },
            };
            return JsonSerializer.Serialize(result, Json);
        }
        catch (AssistantToolException exception) { return Error(exception.Message); }
    }

    private async Task<object> ListBatchesAsync(CancellationToken cancellationToken) => await MatchedBatches()
        .Take(MaxHistoryMonths)
        .Select(batch => new
        {
            period = $"{batch.BillingYear:D4}-{batch.BillingMonth:D2}",
            provider = batch.Provider.Name,
            status = batch.Status.ToString(),
            approved = batch.Status == BillBatchStatus.Completed || batch.Status == BillBatchStatus.Locked,
            numbers = db.MonthlyBills.Count(bill => bill.BillLine.BillBatchId == batch.Id),
        })
        .ToListAsync(cancellationToken);

    private async Task<object> BatchSummaryAsync(string? period, CancellationToken cancellationToken)
    {
        var data = await insights.GetAsync(await ResolveBatchAsync(period, cancellationToken), 6, cancellationToken);
        if (data.Selected is null) return new { error = "No billing batch has been matched to employees yet." };
        return new
        {
            batch = Describe(data.Selected),
            totals = data.Current,
            previousApprovedBatch = data.PreviousBatch is null ? null : Describe(data.PreviousBatch),
            previousTotals = data.Previous,
            excessSplit = data.ExcessSplit,
            billRanges = data.BillRanges,
            chargeMix = data.ChargeMix,
            costByFactory = data.Groups.Factory,
            costByDepartment = data.Groups.Department.Take(15),
            costByCategory = data.Groups.Category,
        };
    }

    private async Task<object> TopOverLimitAsync(string? period, PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        var data = await insights.GetAsync(await ResolveBatchAsync(period, cancellationToken), 1, cancellationToken);
        if (data.Selected is null) return new { error = "No billing batch has been matched to employees yet." };
        return new
        {
            batch = Describe(data.Selected),
            numbers = data.TopOverLimit.Select(item => new
            {
                mobileNumber = masker.Mobile(item.MobileNumber), epf = masker.Epf(item.Epf), employee = masker.Person(item.EmployeeName),
                item.Factory, item.Department, item.ActualBill, item.Entitlement, item.CalculatedExcess,
                responsibility = item.Responsibility?.ToString() ?? "Not yet assigned", reason = item.Remark,
            }),
        };
    }

    private async Task<object> RepeatOverLimitAsync(string? period, PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        var data = await insights.GetAsync(await ResolveBatchAsync(period, cancellationToken), 1, cancellationToken);
        if (data.Selected is null) return new { error = "No billing batch has been matched to employees yet." };
        var repeat = data.RepeatOverLimit;
        return new
        {
            batch = Describe(data.Selected),
            rule = $"Over the limit in {repeat.MinMonthsOver} or more of the last {repeat.WindowMonths} billing months",
            repeat.TotalCount,
            shown = repeat.Accounts.Select(item => new
            {
                mobileNumber = masker.Mobile(item.MobileNumber), epf = masker.Epf(item.Epf), employee = masker.Person(item.EmployeeName),
                item.Factory, item.PackageCode, item.MonthsOverLimit, item.MonthsBilled, item.TotalExcess, item.AverageExcess, thisBatchExcess = item.SelectedBatchExcess,
            }),
        };
    }

    private async Task<object> NumberHistoryAsync(string? mobileNumber, int months, PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber)) throw new AssistantToolException("Give a mobile number.");
        var normalized = PersonalDataMasker.NormalizeMobile(mobileNumber);
        var bills = await db.MonthlyBills.AsNoTracking()
            .Where(bill => (bill.MobileNumberSnapshot == normalized || bill.MobileNumberSnapshot == mobileNumber.Trim()) && bill.Status != MonthlyBillStatus.Excluded)
            .OrderByDescending(bill => bill.BillLine.BillBatch.BillingYear).ThenByDescending(bill => bill.BillLine.BillBatch.BillingMonth)
            .Take(Math.Clamp(months, 1, MaxHistoryMonths))
            .Select(bill => new
            {
                bill.BillLine.BillBatch.BillingYear, bill.BillLine.BillBatch.BillingMonth, Provider = bill.BillLine.BillBatch.Provider.Name, BatchStatus = bill.BillLine.BillBatch.Status,
                bill.EmployeeNameSnapshot, bill.EmployeeEpfSnapshot, Factory = bill.FactoryNameSnapshot ?? bill.FactoryCodeSnapshot, bill.PackageCodeSnapshot,
                bill.ActualBill, Entitlement = bill.CreditLimit + bill.MonthlyRental, bill.CalculatedExcess, bill.FinalDeduction, bill.Responsibility,
            })
            .ToListAsync(cancellationToken);
        if (bills.Count == 0) return new { error = $"No bills were found for mobile number {masker.Mobile(mobileNumber)}." };
        return new
        {
            mobileNumber = masker.Mobile(mobileNumber),
            months = bills.Select(bill => new
            {
                period = $"{bill.BillingYear:D4}-{bill.BillingMonth:D2}", provider = bill.Provider,
                approved = bill.BatchStatus is BillBatchStatus.Completed or BillBatchStatus.Locked,
                holder = masker.Person(bill.EmployeeNameSnapshot), epf = masker.Epf(bill.EmployeeEpfSnapshot), factory = bill.Factory, package = bill.PackageCodeSnapshot,
                bill.ActualBill, bill.Entitlement, bill.CalculatedExcess, bill.FinalDeduction,
                responsibility = bill.Responsibility?.ToString() ?? "Not yet assigned",
            }),
        };
    }

    private async Task<object> FindAllocationsAsync(string? search, PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search)) throw new AssistantToolException("Give a mobile number, EPF, name or package code to search for.");
        var term = search.Trim();
        // A mobile number written with a leading 0 or +94 is stored without it.
        if (term.Any(char.IsAsciiDigit) && term.All(character => char.IsAsciiDigit(character) || character is ' ' or '+' or '-')) term = PersonalDataMasker.NormalizeMobile(term);
        var page = await masterData.GetMobileAccountsAsync(new PagedRequest(1, 15, term), cancellationToken);
        return new
        {
            page.TotalCount,
            shown = page.Items.Select(account => new
            {
                mobileNumber = masker.Mobile(account.MobileNumber), epf = masker.Epf(account.Epf), holder = masker.Person(account.EmployeeName),
                account.Factory, account.Department, package = account.PackageCode, simType = account.SimType?.ToString() ?? "Not set",
                account.MonthlyCreditLimit, account.MonthlyRental, simStatus = account.Status.ToString(), account.IsActive,
                account.PooledOn, account.DisconnectedOn, account.StatusReason,
            }),
        };
    }

    private async Task<object> SimPoolAsync(PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        var pool = await masterData.GetSimPoolAsync(cancellationToken);
        return new
        {
            count = pool.Count,
            shown = pool.Take(50).Select(item => new
            {
                mobileNumber = masker.Mobile(item.MobileNumber), previousEpf = masker.Epf(item.PreviousEpf), previousHolder = masker.Person(item.PreviousEmployeeName),
                item.Factory, item.Department, item.PooledOn, item.Reason, item.DaysInPool, item.IsLongIdle, item.MonthlyCreditLimit, item.MonthlyRental,
            }),
        };
    }

    private async Task<object> DevicesToCollectAsync(PersonalDataMasker masker, CancellationToken cancellationToken)
    {
        var toCollect = await devices.GetToCollectAsync(cancellationToken);
        return new
        {
            count = toCollect.Count,
            devices = toCollect.Select(item => new
            {
                assetId = item.AssetTag, device = $"{item.Brand} {item.Model}", epf = masker.Epf(item.Epf), employee = masker.Person(item.EmployeeName),
                item.Factory, item.Department, item.ResignedOn, item.DaysWaiting, item.IsOverdue, item.RecoverableAmount,
            }),
        };
    }

    // Batches with monthly bills, latest first, as on the Dashboard.
    private IQueryable<Domain.Entities.BillBatch> MatchedBatches() => db.BillBatches.AsNoTracking()
        .Where(batch => db.MonthlyBills.Any(bill => bill.BillLine.BillBatchId == batch.Id))
        .OrderByDescending(batch => batch.BillingYear).ThenByDescending(batch => batch.BillingMonth).ThenByDescending(batch => batch.CreatedAtUtc);

    private async Task<Guid?> ResolveBatchAsync(string? period, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(period)) return null;
        var parts = period.Trim().Split('-', '/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) || month is < 1 or > 12)
            throw new AssistantToolException($"'{period}' is not a billing month; use YYYY-MM.");
        var batchId = await MatchedBatches().Where(batch => batch.BillingYear == year && batch.BillingMonth == month).Select(batch => (Guid?)batch.Id).FirstOrDefaultAsync(cancellationToken);
        return batchId ?? throw new AssistantToolException($"There is no matched billing batch for {year:D4}-{month:D2}. Use list_billing_batches to see the available months.");
    }

    private static object Describe(InsightsBatchOption batch) => new
    {
        period = $"{batch.BillingYear:D4}-{batch.BillingMonth:D2}", provider = batch.Provider, status = batch.Status.ToString(), approved = batch.IsApproved,
    };

    private static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null
            : null;

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, Json);

    private static ChatToolDefinition Tool(string name, string description, string properties, params string[] required) =>
        new(name, description, JsonDocument.Parse($$"""{ "type": "object", "properties": {{properties}}, "required": {{JsonSerializer.Serialize(required)}} }""").RootElement.Clone());

    private sealed class AssistantToolException(string message) : Exception(message);
}
