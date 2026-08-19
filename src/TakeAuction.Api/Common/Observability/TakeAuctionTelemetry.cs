using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TakeAuction.Api.Common.Observability;

public sealed class TakeAuctionTelemetry
{
    public const string ServiceName = "TakeAuction.Api";

    public const string MeterName = "TakeAuction";

    public const string ActivitySourceName = "TakeAuction";

    public static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly Counter<long> _bids;
    private readonly Counter<long> _concurrencyConflicts;
    private readonly Histogram<int> _bidAttempts;
    private readonly Histogram<double> _bidDuration;
    private readonly Counter<long> _proxyAnswers;
    private readonly Counter<long> _auctionExtensions;
    private readonly Counter<long> _outboxMessages;
    private readonly Histogram<int> _outboxBatchSize;
    private readonly Counter<long> _cacheLookups;

    public TakeAuctionTelemetry(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);

        _bids = Meter.CreateCounter<long>(
            "takeauction.bids",
            unit: "{bid}",
            description: "Bid submissions, tagged with what became of them.");

        _concurrencyConflicts = Meter.CreateCounter<long>(
            "takeauction.bids.concurrency_conflicts",
            unit: "{conflict}",
            description: "Times a bid lost the row-version race and had to be re-evaluated.");

        _bidAttempts = Meter.CreateHistogram<int>(
            "takeauction.bids.attempts",
            unit: "{attempt}",
            description: "How many passes through the optimistic retry loop a bid took to settle.");

        _bidDuration = Meter.CreateHistogram<double>(
            "takeauction.bids.duration",
            unit: "ms",
            description: "Wall time from command to settled bid, retries included.");

        _proxyAnswers = Meter.CreateCounter<long>(
            "takeauction.bids.proxy_answers",
            unit: "{bid}",
            description: "Bids the house placed on a leader's behalf to answer a challenger.");

        _auctionExtensions = Meter.CreateCounter<long>(
            "takeauction.auctions.extensions",
            unit: "{extension}",
            description: "Lots whose close was pushed out by a bid landing in the closing window.");

        _outboxMessages = Meter.CreateCounter<long>(
            "takeauction.outbox.messages",
            unit: "{message}",
            description: "Outbox messages the dispatcher handled, tagged with the result.");

        _outboxBatchSize = Meter.CreateHistogram<int>(
            "takeauction.outbox.batch_size",
            unit: "{message}",
            description: "Messages claimed per sweep. A batch that keeps arriving full is a backlog.");

        _cacheLookups = Meter.CreateCounter<long>(
            "takeauction.cache.lookups",
            unit: "{lookup}",
            description: "Cache reads, tagged hit or miss.");
    }

    public Meter Meter { get; }

    public void BidSettled(string outcome, int attempts, double durationMs)
    {
        _bids.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        _bidAttempts.Record(attempts);
        _bidDuration.Record(durationMs, new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void BidConflicted() => _concurrencyConflicts.Add(1);

    public void ProxyAnswered() => _proxyAnswers.Add(1);

    public void AuctionExtended() => _auctionExtensions.Add(1);

    public void OutboxSwept(int claimed, int published)
    {
        _outboxBatchSize.Record(claimed);

        if (published > 0)
        {
            _outboxMessages.Add(published, new KeyValuePair<string, object?>("result", "published"));
        }

        if (claimed > published)
        {
            _outboxMessages.Add(claimed - published, new KeyValuePair<string, object?>("result", "failed"));
        }
    }

    public void CacheLookup(bool hit) =>
        _cacheLookups.Add(1, new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"));
}
