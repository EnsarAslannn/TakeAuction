using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TakeAuction.Api.Features.Chat;

public interface IChatService
{
    ChatResponse Reply(ChatRequest request);
}

public sealed partial class KnowledgeChatService(IChatKnowledgeBase knowledgeBase) : IChatService
{
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

        var match = knowledgeBase.Entries
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
