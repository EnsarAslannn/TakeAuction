using TakeAuction.Api.Features.Chat;

namespace TakeAuction.Api.UnitTests.Features.Chat;

public sealed class KnowledgeChatServiceTests
{
    private readonly KnowledgeChatService _service = new(new StaticChatKnowledgeBase());

    [Fact]
    public void A_turkish_bid_question_returns_the_matching_site_guidance()
    {
        var response = _service.Reply(new ChatRequest(
            "Teklif vermek ve minimum artış nasıl çalışıyor?",
            "tr",
            []));

        Assert.Contains("teklif", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Sources, source => source.Url == "/#how-it-works");
        Assert.InRange(response.Suggestions.Count, 1, 3);
        Assert.False(response.UsedAi);
    }

    [Fact]
    public void A_question_about_available_questions_lists_supported_site_topics()
    {
        var response = _service.Reply(new ChatRequest(
            "Hangi soruları sorabilirim?",
            "tr",
            []));

        Assert.Contains("teklif", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hesap", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/", Assert.Single(response.Sources).Url);
        Assert.InRange(response.Suggestions.Count, 1, 3);
        Assert.False(response.UsedAi);
    }

    [Fact]
    public void An_english_seller_question_links_to_the_listing_page()
    {
        var response = _service.Reply(new ChatRequest(
            "How can a seller create a new auction listing?",
            "en",
            []));

        Assert.Contains("seller", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Sources, source => source.Url == "/auctions/new");
        Assert.All(response.Suggestions, suggestion => Assert.False(string.IsNullOrWhiteSpace(suggestion)));
    }

    [Fact]
    public void Recent_history_keeps_a_short_follow_up_on_the_same_topic()
    {
        var history = new[]
        {
            new ChatHistoryItem("user", "Teklif verirken limit nasıl çalışır?"),
            new ChatHistoryItem("assistant", "Limitiniz gizli tutulur."),
        };

        var response = _service.Reply(new ChatRequest("Peki sonra ne olur?", "tr", history));

        Assert.Contains("limit", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Sources, source => source.Url == "/#how-it-works");
    }

    [Fact]
    public void An_unknown_question_is_declared_out_of_scope_without_inventing_an_answer()
    {
        var response = _service.Reply(new ChatRequest(
            "Yarın İstanbul'da hava nasıl olacak?",
            "tr",
            []));

        Assert.Contains("kapsam", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/", Assert.Single(response.Sources).Url);
        Assert.False(response.UsedAi);
    }

    [Fact]
    public async Task A_question_about_this_lot_uses_the_current_auction_from_the_server()
    {
        var auctionId = Guid.Parse("018f6f47-4dd2-7c97-8f58-1f70edc78a21");
        var service = new KnowledgeChatService(
            new StaticChatKnowledgeBase(),
            new StubAuctionContextReader(new ChatAuctionContext(
                auctionId,
                "Osmanlı Cep Saati",
                "Active",
                12_500m,
                13_000m,
                new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero))));

        var response = await service.ReplyAsync(new ChatRequest(
            "Bu lotun durumu nedir?",
            "tr",
            [],
            new ChatPageContext($"/auctions/{auctionId}", auctionId)));

        Assert.Contains("Osmanlı Cep Saati", response.Answer, StringComparison.Ordinal);
        Assert.Contains("aktif", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("13.000", response.Answer, StringComparison.Ordinal);
        Assert.Equal($"/auctions/{auctionId}", Assert.Single(response.Sources).Url);
    }

    [Fact]
    public async Task A_signed_in_leader_gets_a_private_bid_standing_explanation()
    {
        var auctionId = Guid.Parse("018f6f47-4dd2-7c97-8f58-1f70edc78a21");
        var bidderId = Guid.Parse("018f6f47-57b8-7687-a47a-5933bc18b499");
        var service = new KnowledgeChatService(
            new StaticChatKnowledgeBase(),
            new StubAuctionContextReader(
                new ChatAuctionContext(
                    auctionId,
                    "Osmanlı Cep Saati",
                    "Active",
                    12_500m,
                    13_000m,
                    new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero)),
                new ChatBidStandingContext(
                    auctionId,
                    "Osmanlı Cep Saati",
                    12_500m,
                    true,
                    15_000m,
                    13_000m)));

        var response = await service.ReplyAsync(
            new ChatRequest(
                "Şu anda önde miyim?",
                "tr",
                [],
                new ChatPageContext($"/auctions/{auctionId}", auctionId)),
            bidderId);

        Assert.Contains("öndesiniz", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15.000", response.Answer, StringComparison.Ordinal);
        Assert.Contains("gizli tavan", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"/auctions/{auctionId}", Assert.Single(response.Sources).Url);
    }

    [Fact]
    public async Task A_guest_is_asked_to_sign_in_before_private_standing_is_read()
    {
        var auctionId = Guid.Parse("018f6f47-4dd2-7c97-8f58-1f70edc78a21");
        var service = new KnowledgeChatService(
            new StaticChatKnowledgeBase(),
            new StubAuctionContextReader(new ChatAuctionContext(
                auctionId,
                "Osmanlı Cep Saati",
                "Active",
                12_500m,
                13_000m,
                new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero))));

        var response = await service.ReplyAsync(new ChatRequest(
            "Şu anda önde miyim?",
            "tr",
            [],
            new ChatPageContext($"/auctions/{auctionId}", auctionId)));

        Assert.Contains("giriş", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/login", Assert.Single(response.Sources).Url);
    }

    [Fact]
    public async Task A_bidder_who_is_not_leading_sees_the_minimum_next_bid_without_a_private_ceiling()
    {
        var auctionId = Guid.Parse("018f6f47-4dd2-7c97-8f58-1f70edc78a21");
        var bidderId = Guid.Parse("018f6f47-57b8-7687-a47a-5933bc18b499");
        var service = new KnowledgeChatService(
            new StaticChatKnowledgeBase(),
            new StubAuctionContextReader(
                new ChatAuctionContext(
                    auctionId,
                    "Osmanlı Cep Saati",
                    "Active",
                    12_500m,
                    13_000m,
                    new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero)),
                new ChatBidStandingContext(
                    auctionId,
                    "Osmanlı Cep Saati",
                    12_500m,
                    false,
                    null,
                    13_000m)));

        var response = await service.ReplyAsync(
            new ChatRequest(
                "Teklif durumum nedir?",
                "tr",
                [],
                new ChatPageContext($"/auctions/{auctionId}", auctionId)),
            bidderId);

        Assert.Contains("önde değilsiniz", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("13.000", response.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("gizli tavan", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_message_is_rejected(string message)
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Reply(new ChatRequest(message, "tr", [])));
    }

    private sealed class StubAuctionContextReader(
        ChatAuctionContext auction,
        ChatBidStandingContext? standing = null) : IChatAuctionContextReader
    {
        public Task<ChatAuctionContext?> FindAsync(Guid auctionId, CancellationToken cancellationToken) =>
            Task.FromResult<ChatAuctionContext?>(auctionId == auction.Id ? auction : null);

        public Task<ChatBidStandingContext?> FindStandingAsync(
            Guid auctionId,
            Guid bidderId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ChatBidStandingContext?>(auctionId == standing?.AuctionId ? standing : null);
    }
}
