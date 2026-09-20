namespace TakeAuction.Api.Features.Chat;

public interface IChatKnowledgeBase
{
    IReadOnlyList<ChatKnowledgeEntry> Entries { get; }
}
