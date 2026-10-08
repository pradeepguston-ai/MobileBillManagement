using System.Text.Json;

namespace MobileBill.Application.Assistant;

// The in-app AI assistant. It answers questions about the system and, through read-only tools, about its data.
// Names, EPF numbers and mobile numbers are masked before anything is sent to the AI model (see PersonalDataMasker).
public interface IAssistantService
{
    AssistantStatusDto GetStatus();

    // ConversationId null starts a new conversation; a conversation expires after a period without messages.
    Task<AssistantReplyDto> AskAsync(AssistantAskRequest request, CancellationToken cancellationToken);
}

public sealed record AssistantStatusDto(bool IsEnabled);

public sealed record AssistantAskRequest(string Message, Guid? ConversationId = null);

public sealed record AssistantReplyDto(Guid ConversationId, string Reply);

// The AI model is not configured, busy, over its free limit or could not be reached. The message is safe to show.
public sealed class AssistantUnavailableException(string message) : Exception(message);

public sealed class AssistantValidationException(string message) : Exception(message);

// The AI model behind the assistant (OpenRouter in production, a fake in tests).
public interface IChatModelClient
{
    bool IsConfigured { get; }

    Task<ChatModelResult> CompleteAsync(IReadOnlyList<ChatModelMessage> messages, IReadOnlyList<ChatToolDefinition> tools, CancellationToken cancellationToken);
}

// Role is system, user, assistant or tool. An assistant message may ask for tool calls; a tool message answers one.
public sealed record ChatModelMessage(string Role, string? Content, IReadOnlyList<ChatToolCall>? ToolCalls = null, string? ToolCallId = null)
{
    public static ChatModelMessage System(string content) => new("system", content);
    public static ChatModelMessage User(string content) => new("user", content);
    public static ChatModelMessage Assistant(string? content, IReadOnlyList<ChatToolCall>? toolCalls = null) => new("assistant", content, toolCalls);
    public static ChatModelMessage Tool(string toolCallId, string content) => new("tool", content, ToolCallId: toolCallId);
}

public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

// Parameters is a JSON schema object describing the tool's arguments.
public sealed record ChatToolDefinition(string Name, string Description, JsonElement Parameters);

public sealed record ChatModelResult(string? Content, IReadOnlyList<ChatToolCall> ToolCalls);
