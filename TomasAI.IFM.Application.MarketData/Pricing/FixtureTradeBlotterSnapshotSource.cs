using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

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
