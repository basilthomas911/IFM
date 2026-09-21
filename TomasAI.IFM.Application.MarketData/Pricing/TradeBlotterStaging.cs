using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public enum TradeBlotterStrategy : byte { IronCondor = 1, VerticalSpread = 2, FuturesOutright = 3 }
public enum TradeBlotterDirection : byte { Short = 1, Long = 2 }
public enum TradeBlotterOptionRight : byte { Call = 1, Put = 2 }
public enum TradeBlotterPositionIntent : byte { Short = 1, Long = 2 }

/// <summary>Selection settings are explicit; a deviation never silently changes the strategy.</summary>
public sealed record TradeBlotterStagingRequest(TradeBlotterStrategy Strategy, TradeBlotterDirection Direction,
    double TargetAbsoluteDelta = 0.16, decimal WingWidth = 25m, double DeltaTolerance = 0.03,
    decimal WidthTolerance = 5m, TradeBlotterOptionRight VerticalRight = TradeBlotterOptionRight.Call);

public sealed record StagedTradeLeg(Guid StagedLegId, string ContractId, TradeBlotterOptionRight? Right,
    decimal? Strike, double? Delta, TradeBlotterPositionIntent PositionIntent, int SignedQuantity,
    decimal Bid, decimal Ask, DateTimeOffset QuoteAtUtc, string SnapshotDigest, bool ManualReviewRequired,
    string ReviewReason)
{
    public string LegLabel => PositionIntent == TradeBlotterPositionIntent.Short ? "SL-" : "LL+";
    public string DeltaLabel => Delta is null ? "N/A" : Delta.Value.ToString("+0.000;-0.000;0.000", System.Globalization.CultureInfo.InvariantCulture);
    public string SideLabel => SignedQuantity > 0 ? "Buy" : "Sell";
}

public sealed record TradeBlotterStagingResult(ImmutableArray<StagedTradeLeg> Legs, bool ManualReviewRequired,
    string Status, Guid SnapshotId, Guid GenerationId, DateTimeOffset EvaluatedAtUtc, string SnapshotDigest);

/// <summary>Pure deterministic staging over the Stage 2 immutable composition snapshot.</summary>
public static class TradeBlotterLegStager
{
    public static TradeBlotterStagingResult Stage(MarketCompositionSnapshot snapshot, TradeBlotterStagingRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetAbsoluteDelta is <= 0 or >= 1 || request.WingWidth <= 0
            || request.DeltaTolerance < 0 || request.WidthTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(request));

        if (request.Strategy == TradeBlotterStrategy.FuturesOutright)
        {
            var future = snapshot.Instruments
                .Where(x => x.Instrument.Pricing is null && x.Instrument.FutureDefinition is not null)
                .OrderBy(x => x.Instrument.FutureDefinition!.LastTradingUtc)
                .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal)
                .FirstOrDefault() ?? throw new InvalidOperationException("No qualified exact future exists in the snapshot.");
            var intent = request.Direction == TradeBlotterDirection.Long ? TradeBlotterPositionIntent.Long : TradeBlotterPositionIntent.Short;
            var leg = Create(snapshot, future, null, intent, false, string.Empty, 0);
            return Result(snapshot, [leg]);
        }

        var options = snapshot.Instruments
            .Where(x => x.Instrument.Pricing is not null && x.Instrument.Strike is not null
                && x.Instrument.IsCall is not null && x.Instrument.Selection is not null)
            .ToArray();
        if (options.Length == 0) throw new InvalidOperationException("No qualified option selection values exist in the snapshot.");

        var rights = request.Strategy == TradeBlotterStrategy.IronCondor
            ? new[] { TradeBlotterOptionRight.Call, TradeBlotterOptionRight.Put }
            : [request.VerticalRight];
        var legs = new List<StagedTradeLeg>(rights.Length * 2);
        foreach (var right in rights)
        {
            var candidates = options.Where(x => x.Instrument.IsCall == (right == TradeBlotterOptionRight.Call)).ToArray();
            if (candidates.Length < 2) throw new InvalidOperationException($"At least two qualified {right} strikes are required.");
            var anchor = candidates
                .OrderBy(x => Math.Abs(Math.Abs(x.Instrument.Selection!.Delta) - request.TargetAbsoluteDelta))
                .ThenBy(x => x.Instrument.Strike)
                .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).First();
            var targetStrike = anchor.Instrument.Strike!.Value + (right == TradeBlotterOptionRight.Call ? request.WingWidth : -request.WingWidth);
            var wing = candidates.Where(x => x.Instrument.ContractId != anchor.Instrument.ContractId)
                .OrderBy(x => Math.Abs(x.Instrument.Strike!.Value - targetStrike))
                .ThenBy(x => x.Instrument.Strike)
                .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).First();
            var deltaDeviation = Math.Abs(Math.Abs(anchor.Instrument.Selection!.Delta) - request.TargetAbsoluteDelta);
            var widthDeviation = Math.Abs(Math.Abs(wing.Instrument.Strike!.Value - anchor.Instrument.Strike.Value) - request.WingWidth);
            var review = deltaDeviation > request.DeltaTolerance || widthDeviation > request.WidthTolerance;
            var reason = review ? $"Delta deviation {deltaDeviation:0.000}; width deviation {widthDeviation:0.###}." : string.Empty;
            var anchorIntent = request.Direction == TradeBlotterDirection.Short ? TradeBlotterPositionIntent.Short : TradeBlotterPositionIntent.Long;
            var wingIntent = anchorIntent == TradeBlotterPositionIntent.Short ? TradeBlotterPositionIntent.Long : TradeBlotterPositionIntent.Short;
            legs.Add(Create(snapshot, anchor, right, anchorIntent, review, reason, legs.Count));
            legs.Add(Create(snapshot, wing, right, wingIntent, review, reason, legs.Count));
        }

        var ordered = legs.OrderBy(x => x.Right == TradeBlotterOptionRight.Call ? 0 : 1)
            .ThenBy(x => x.Delta).ThenBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray();
        return Result(snapshot, ordered);
    }

    private static TradeBlotterStagingResult Result(MarketCompositionSnapshot snapshot, ImmutableArray<StagedTradeLeg> legs) =>
        new(legs, legs.Any(x => x.ManualReviewRequired),
            legs.Any(x => x.ManualReviewRequired) ? "Manual review required" : "Structure staged",
            snapshot.SnapshotId, snapshot.GenerationId, snapshot.EvaluatedAtUtc, snapshot.Digest);

    private static StagedTradeLeg Create(MarketCompositionSnapshot snapshot, CompositionInstrumentSnapshot value,
        TradeBlotterOptionRight? right, TradeBlotterPositionIntent intent, bool review, string reason, int ordinal)
    {
        var identity = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{snapshot.SnapshotId:N}|{snapshot.GenerationId:N}|{value.Instrument.ContractId}|{ordinal}"))[..16]);
        return new(identity, value.Instrument.ContractId, right, value.Instrument.Strike,
            value.Instrument.Selection?.Delta, intent, intent == TradeBlotterPositionIntent.Long ? 1 : -1,
            value.Instrument.Quote.Bid, value.Instrument.Quote.Ask, value.Instrument.Quote.EventAtUtc,
            snapshot.Digest, review, reason);
    }
}

/// <summary>Historical/fixture seam. Implementations provide a frozen snapshot and explicit virtual time.</summary>
public interface ITradeBlotterSnapshotSource
{
    string SourceLabel { get; }
    bool IsHistorical { get; }
    DateTimeOffset AsOfUtc { get; }
    Task<MarketCompositionSnapshot> CaptureAsync(CancellationToken cancellationToken);
}

public sealed class FixtureTradeBlotterSnapshotSource(string sourceLabel, MarketCompositionSnapshot snapshot,
    bool historical = true) : ITradeBlotterSnapshotSource
{
    public string SourceLabel { get; } = string.IsNullOrWhiteSpace(sourceLabel) ? "Historical fixture" : sourceLabel;
    public bool IsHistorical { get; } = historical;
    public DateTimeOffset AsOfUtc => snapshot.EvaluatedAtUtc;
    public Task<MarketCompositionSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(snapshot);
    }
}
