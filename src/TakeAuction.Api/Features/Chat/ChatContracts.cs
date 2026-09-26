namespace TakeAuction.Api.Features.Chat;

public sealed record ChatHistoryItem(string Role, string Content);

public sealed record ChatRequest(
    string Message,
    string Language,
    IReadOnlyList<ChatHistoryItem> History,
    ChatPageContext? Context = null);

public sealed record ChatPageContext(string Path, Guid? AuctionId);

public sealed record ChatSource(string Title, string Url);

public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<ChatSource> Sources,
    IReadOnlyList<string> Suggestions,
    bool UsedAi);

public sealed record ChatAuctionContext(
    Guid Id,
    string Title,
    string Status,
    decimal CurrentPrice,
    decimal MinimumAcceptableBid,
    DateTimeOffset EndsAtUtc);

public sealed record ChatKnowledgeEntry(
    string Topic,
    IReadOnlyList<string> TurkishKeywords,
    IReadOnlyList<string> EnglishKeywords,
    string TurkishAnswer,
    string EnglishAnswer,
    ChatSource TurkishSource,
    ChatSource EnglishSource,
    IReadOnlyList<string> TurkishSuggestions,
    IReadOnlyList<string> EnglishSuggestions);
