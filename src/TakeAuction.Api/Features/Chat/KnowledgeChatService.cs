using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TakeAuction.Api.Features.Chat;

public interface IChatService
{
    Task<ChatResponse> ReplyAsync(
        ChatRequest request,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}

public sealed partial class KnowledgeChatService : IChatService
{
    private readonly IChatKnowledgeBase _knowledgeBase;
    private readonly IChatAuctionContextReader? _auctionContextReader;

    public KnowledgeChatService(IChatKnowledgeBase knowledgeBase) => _knowledgeBase = knowledgeBase;

    public KnowledgeChatService(
        IChatKnowledgeBase knowledgeBase,
        IChatAuctionContextReader auctionContextReader)
    {
        _knowledgeBase = knowledgeBase;
        _auctionContextReader = auctionContextReader;
    }

    public async Task<ChatResponse> ReplyAsync(
        ChatRequest request,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Context?.AuctionId is { } standingAuctionId && AsksAboutBidStanding(request.Message))
        {
            if (userId is null)
            {
                return SignInForStandingReply(request.Language);
            }

            var standing = _auctionContextReader is null
                ? null
                : await _auctionContextReader.FindStandingAsync(
                    standingAuctionId,
                    userId.Value,
                    cancellationToken);

            if (standing is not null)
            {
                return BidStandingReply(standing, request.Language);
            }
        }

        if (request.Context?.AuctionId is { } auctionId && RefersToCurrentAuction(request.Message))
        {
            var auction = _auctionContextReader is null
                ? null
                : await _auctionContextReader.FindAsync(auctionId, cancellationToken);

            if (auction is not null)
            {
                return AuctionReply(auction, request.Language);
            }
        }

        return Reply(request);
    }

    private static bool AsksAboutBidStanding(string message)
    {
        var normalized = Normalize(message);
        return normalized.Contains("onde miyim", StringComparison.Ordinal) ||
               normalized.Contains("lider miyim", StringComparison.Ordinal) ||
               normalized.Contains("teklif durumum", StringComparison.Ordinal) ||
               normalized.Contains("am i leading", StringComparison.Ordinal) ||
               normalized.Contains("am i winning", StringComparison.Ordinal) ||
               normalized.Contains("my bid status", StringComparison.Ordinal);
    }

    private static ChatResponse SignInForStandingReply(string language)
    {
        var english = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        return new ChatResponse(
            english
                ? "Sign in to see your private bid standing for this auction."
                : "Bu açık artırmadaki kişisel teklif durumunuzu görmek için giriş yapın.",
            [new ChatSource(english ? "Sign in" : "Giriş yap", "/login")],
            english ? ["How does automatic bidding work?"] : ["Otomatik teklif nasıl çalışır?"],
            false);
    }

    private static ChatResponse BidStandingReply(ChatBidStandingContext standing, string language)
    {
        var english = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "tr-TR");
        string answer;

        if (standing.IsLeading)
        {
            var ceiling = standing.MaxAmount?.ToString("C", culture);
            answer = english
                ? $"You are currently leading {standing.AuctionTitle}. The current price is {standing.CurrentPrice.ToString("C", culture)} and your private ceiling is {ceiling}. The system will bid only as much as needed unless another bidder exceeds that ceiling."
                : $"{standing.AuctionTitle} lotunda şu anda öndesiniz. Güncel fiyat {standing.CurrentPrice.ToString("C", culture)}, gizli tavanınız {ceiling}. Başka bir teklif tavanınızı aşmadıkça sistem yalnızca gerektiği kadar otomatik teklif verir.";
        }
        else
        {
            answer = english
                ? $"You are not currently leading {standing.AuctionTitle}. The current price is {standing.CurrentPrice.ToString("C", culture)} and your next bid must be at least {standing.MinimumAcceptableBid.ToString("C", culture)}."
                : $"{standing.AuctionTitle} lotunda şu anda önde değilsiniz. Güncel fiyat {standing.CurrentPrice.ToString("C", culture)}; yeni teklifiniz en az {standing.MinimumAcceptableBid.ToString("C", culture)} olmalı.";
        }

        return new ChatResponse(
            answer,
            [new ChatSource(english ? "Auction details" : "Lot detayı", $"/auctions/{standing.AuctionId}")],
            english
                ? ["How does my private ceiling work?", "What is the minimum next bid?"]
                : ["Gizli tavanım nasıl çalışır?", "Sonraki minimum teklif nedir?"],
            false);
    }

    public ChatResponse Reply(ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("A chat message is required.", nameof(request));
        }

        var english = string.Equals(request.Language, "en", StringComparison.OrdinalIgnoreCase);
        var current = Normalize(request.Message);
        var context = string.Join(' ', request.History
            .Where(item => string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase))
            .TakeLast(6)
            .Select(item => Normalize(item.Content)));

        var match = _knowledgeBase.Entries
            .Select(entry => new { Entry = entry, Score = Score(entry, current, context, english) })
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();

        if (match is null || match.Score < 2)
        {
            return english
                ? new ChatResponse(
                    "I can only answer questions about TakeAuction and how to use this site. I could not find this topic in the verified site guide, so I will not guess.",
                    [new ChatSource("TakeAuction home", "/")],
                    ["How do I place a bid?", "How do I find an auction?", "How do I create an account?"],
                    false)
                : new ChatResponse(
                    "Yalnızca TakeAuction ve bu sitenin kullanımı hakkındaki soruları yanıtlayabilirim. Bu konuyu doğrulanmış site rehberinde bulamadım; bu nedenle bilgi uydurmayacağım ve sorunun kapsam dışında olduğunu belirtiyorum.",
                    [new ChatSource("TakeAuction ana sayfa", "/")],
                    ["Nasıl teklif veririm?", "Açık artırmaları nasıl bulurum?", "Nasıl hesap açarım?"],
                    false);
        }

        return new ChatResponse(
            english ? match.Entry.EnglishAnswer : match.Entry.TurkishAnswer,
            [english ? match.Entry.EnglishSource : match.Entry.TurkishSource],
            (english ? match.Entry.EnglishSuggestions : match.Entry.TurkishSuggestions).Take(3).ToArray(),
            false);
    }

    private static bool RefersToCurrentAuction(string message)
    {
        var normalized = Normalize(message);
        return normalized.Contains("bu lot", StringComparison.Ordinal) ||
               normalized.Contains("bu muzayede", StringComparison.Ordinal) ||
               normalized.Contains("bu acik artirma", StringComparison.Ordinal) ||
               normalized.Contains("this lot", StringComparison.Ordinal) ||
               normalized.Contains("this auction", StringComparison.Ordinal);
    }

    private static ChatResponse AuctionReply(ChatAuctionContext auction, string language)
    {
        var english = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "tr-TR");
        var status = LocalizeStatus(auction.Status, english);
        var answer = english
            ? $"{auction.Title} is currently {status}. The current price is {auction.CurrentPrice.ToString("C", culture)} and the minimum acceptable bid is {auction.MinimumAcceptableBid.ToString("C", culture)}. It ends at {auction.EndsAtUtc.ToString("g", culture)}."
            : $"{auction.Title} lotu şu anda {status}. Güncel fiyat {auction.CurrentPrice.ToString("C", culture)}; kabul edilecek en düşük teklif {auction.MinimumAcceptableBid.ToString("C", culture)}. Kapanış zamanı {auction.EndsAtUtc.ToString("g", culture)}.";

        return new ChatResponse(
            answer,
            [new ChatSource(english ? "Auction details" : "Lot detayı", $"/auctions/{auction.Id}")],
            english
                ? ["Am I leading?", "How does automatic bidding work?"]
                : ["Şu anda önde miyim?", "Otomatik teklif nasıl çalışır?"],
            false);
    }

    private static string LocalizeStatus(string status, bool english) => status.ToLowerInvariant() switch
    {
        "active" => english ? "active" : "aktif",
        "scheduled" => english ? "scheduled" : "planlanmış",
        "ended" => english ? "ended" : "sona ermiş",
        "cancelled" => english ? "cancelled" : "iptal edilmiş",
        _ => status
    };

    private static int Score(ChatKnowledgeEntry entry, string current, string context, bool english)
    {
        var keywords = english ? entry.EnglishKeywords : entry.TurkishKeywords;
        var score = keywords.Sum(keyword => KeywordScore(current, keyword, 3));

        if (score < 2 && context.Length > 0)
        {
            score += keywords.Sum(keyword => KeywordScore(context, keyword, 1));
        }

        return score;
    }

    private static int KeywordScore(string text, string keyword, int weight)
    {
        var normalizedKeyword = Normalize(keyword);
        if (normalizedKeyword.Length == 0 || !text.Contains(normalizedKeyword, StringComparison.Ordinal))
        {
            return 0;
        }

        return Math.Max(1, normalizedKeyword.Count(character => character == ' ') + 1) * weight;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ç', 'c')
            .Replace('ö', 'o')
            .Replace('ü', 'u')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            builder.Append(category == UnicodeCategory.NonSpacingMark
                ? ' '
                : char.IsLetterOrDigit(character) ? character : ' ');
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
