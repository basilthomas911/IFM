using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Model;

/// <summary>The computed terminal download outcome and its content hash.</summary>
internal sealed record DownloadLogEntry(MarketDataDownloadOutcome Outcome, string PayloadSha256)
{
    /// <summary>Checks exact replay without modifying the previously recorded outcome.</summary>
    public bool Matches(MarketDataDownloadOutcome? recordedOutcome, string? recordedPayloadSha256)
        => recordedOutcome == Outcome && recordedPayloadSha256 == PayloadSha256;
}
