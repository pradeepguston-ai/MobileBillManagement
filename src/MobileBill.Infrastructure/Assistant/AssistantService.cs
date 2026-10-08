using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MobileBill.Application.Assistant;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Assistant;

// Runs one question through the AI model: masks personal data in it, lets the model call the read-only tools
// (at most MaxToolRounds times), and unmasks the answer. Conversations are kept in memory for a while after
// their last message and belong to the user who started them.
public sealed partial class AssistantService(
    IChatModelClient model, AssistantToolbox toolbox, MobileBillDbContext db, ICurrentUserService currentUser, IClock clock, IMemoryCache cache) : IAssistantService
{
    public const int MaxMessageLength = 2000;
    private const int MaxToolRounds = 6;
    private const int HistoryMessages = 20;
    private static readonly TimeSpan ConversationLifetime = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan NameListLifetime = TimeSpan.FromMinutes(10);
    private static readonly Lock CacheGate = new();

    public AssistantStatusDto GetStatus() => new(model.IsConfigured);

    public async Task<AssistantReplyDto> AskAsync(AssistantAskRequest request, CancellationToken cancellationToken)
    {
        var question = request.Message?.Trim() ?? string.Empty;
        if (question.Length == 0) throw new AssistantValidationException("Type a question.");
        if (question.Length > MaxMessageLength) throw new AssistantValidationException($"Keep the question under {MaxMessageLength} characters.");
        if (!model.IsConfigured) throw new AssistantUnavailableException("The assistant is not set up yet. Ask an administrator to add the OpenRouter API key.");

        var conversationId = request.ConversationId ?? Guid.NewGuid();
        Conversation conversation;
        // An expired or unknown conversation simply starts again.
        lock (CacheGate) conversation = cache.GetOrCreate($"assistant:{currentUser.UserId}:{conversationId}", entry => { entry.SlidingExpiration = ConversationLifetime; return new Conversation(); })!;
        // One question at a time per conversation, so its history and placeholders stay in order.
        await conversation.Gate.WaitAsync(cancellationToken);
        try
        {
            var maskedQuestion = conversation.Masker.MaskText(question, await FindPersonalDataAsync(question, cancellationToken));

            var messages = new List<ChatModelMessage> { ChatModelMessage.System(SystemPrompt()) };
            messages.AddRange(conversation.History.TakeLast(HistoryMessages));
            messages.Add(ChatModelMessage.User(maskedQuestion));

            var toolsUsed = new List<string>();
            string? answer = null;
            for (var round = 0; round <= MaxToolRounds && answer is null; round++)
            {
                // After the last tool round the model must answer with what it has.
                var tools = round < MaxToolRounds ? AssistantToolbox.Definitions : [];
                var result = await model.CompleteAsync(messages, tools, cancellationToken);
                if (result.ToolCalls.Count == 0 || tools.Count == 0) { answer = result.Content; break; }

                messages.Add(ChatModelMessage.Assistant(result.Content, result.ToolCalls));
                foreach (var call in result.ToolCalls)
                {
                    toolsUsed.Add(call.Name);
                    messages.Add(ChatModelMessage.Tool(call.Id, await toolbox.ExecuteAsync(call.Name, call.ArgumentsJson, conversation.Masker, cancellationToken)));
                }
            }
            if (string.IsNullOrWhiteSpace(answer)) answer = "I couldn't put an answer together for that. Try asking it another way.";

            conversation.History.Add(ChatModelMessage.User(maskedQuestion));
            conversation.History.Add(ChatModelMessage.Assistant(answer.Trim()));
            await AuditAsync(conversationId, question, toolsUsed, cancellationToken);
            return new AssistantReplyDto(conversationId, conversation.Masker.Unmask(answer.Trim()));
        }
        finally { conversation.Gate.Release(); }
    }

    // Mobile numbers and EPF numbers that exist in the system, and employees' full names, written in the question.
    // A first name on its own is not recognised, as it is too easily an ordinary word or a month.
    private async Task<IReadOnlyList<(PersonalDataKind Kind, string Value)>> FindPersonalDataAsync(string question, CancellationToken cancellationToken)
    {
        var found = new List<(PersonalDataKind, string)>();
        var numbers = NumberRegex().Matches(question).Select(match => match.Value).Distinct().ToList();
        if (numbers.Count > 0)
        {
            var mobiles = numbers.Select(PersonalDataMasker.NormalizeMobile).Concat(numbers).Distinct().ToList();
            var knownMobiles = await db.MobileAccounts.AsNoTracking().Where(account => mobiles.Contains(account.MobileNumber)).Select(account => account.MobileNumber).ToListAsync(cancellationToken);
            var knownEpfs = await db.Employees.AsNoTracking().Where(employee => numbers.Contains(employee.EPF)).Select(employee => employee.EPF).ToListAsync(cancellationToken);
            found.AddRange(knownMobiles.Select(mobile => (PersonalDataKind.Mobile, mobile)));
            found.AddRange(knownEpfs.Where(epf => !knownMobiles.Contains(epf)).Select(epf => (PersonalDataKind.Epf, epf)));
        }

        var names = await cache.GetOrCreateAsync("assistant:employee-names", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NameListLifetime;
            return await db.Employees.AsNoTracking().Select(employee => employee.FullName).Distinct().ToListAsync(cancellationToken);
        }) ?? [];
        found.AddRange(names
            .Where(name => name.Contains(' ') && question.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(name => (PersonalDataKind.Person, name.Trim())));
        return found;
    }

    private async Task AuditAsync(Guid conversationId, string question, IReadOnlyList<string> toolsUsed, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "AssistantConversation", EntityId = conversationId, Action = "AssistantQuestion",
            AfterDataJson = JsonSerializer.Serialize(new { question, tools = toolsUsed.Distinct() }),
            PerformedBy = currentUser.UserId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = currentUser.UserId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private string SystemPrompt() => $"""
        You are the assistant inside the Mobile Bill Management system of Concord. Today is {clock.UtcNow:yyyy-MM-dd}. The user is signed in as a {currentUser.Role} user.

        The system processes the company's monthly corporate telecom bills: a provider's PDF bill is uploaded, parsed, validated and matched to employees by mobile number; each number's entitlement is its monthly credit limit plus monthly rental.
        - Actual Bill = the PDF Total Due Amount.
        - Variance = credit limit + rental - actual bill. Calculated Excess = max(0, actual bill - credit limit - rental).
        - Calculated excess is not automatically deducted. A reviewer assigns responsibility: By User (deducted from the employee, possibly reduced by an override) or By Company. Unassigned rows are "not yet assigned".
        - A batch goes through IT Review, HR Approval and Finance Approval; Completed or Locked batches are approved, the others are provisional.
        - Resigned employees' numbers go to the SIM Pool; company devices they hold must be collected.
        Screens: Dashboard; Billing (Monthly Bill Review, Billing Batches, Exception Review); Masters (Employees, Mobile Allocations, Mobile Devices, Factories, Departments, Designations, Categories, Mobile Packages, Telecom Providers); Reports (Monthly Bill Report, VAS Report).

        Rules:
        - Use the tools for any figure or record. Never guess or invent numbers; if a tool cannot answer, say so.
        - You can only read. To change anything (assess bills, approve, edit allocations, issue devices), tell the user which screen to use.
        - Employee names, EPF numbers and mobile numbers appear as placeholders such as PERSON-1, EPF-1 and MOBILE-1. Write them exactly as they appear; they are replaced with the real values before the user reads your answer. You can pass them to tools.
        - Amounts are in LKR; write them with thousands separators and two decimals.
        - Answer briefly in plain text. Use short "-" bullet lists rather than tables. Do not mention tools, JSON or placeholders.
        - Only help with this system and its data; politely decline unrelated requests.
        """;

    [GeneratedRegex(@"\+?\d{4,12}")]
    private static partial Regex NumberRegex();

    private sealed class Conversation
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public PersonalDataMasker Masker { get; } = new();
        public List<ChatModelMessage> History { get; } = [];
    }
}
