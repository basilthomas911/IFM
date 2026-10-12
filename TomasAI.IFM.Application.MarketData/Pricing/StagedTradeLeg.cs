using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record StagedTradeLeg(Guid StagedLegId, string ContractId, TradeBlotterOptionRight? Right,
    decimal? Strike, double? Delta, TradeBlotterPositionIntent PositionIntent, int SignedQuantity,
    decimal Bid, decimal Ask, DateTimeOffset QuoteAtUtc, string SnapshotDigest, bool ManualReviewRequired,
    string ReviewReason)
{
    public string LegLabel => PositionIntent == TradeBlotterPositionIntent.Short ? "SL-" : "LL+";
    public string DeltaLabel => Delta is null ? "N/A" : Delta.Value.ToString("+0.000;-0.000;0.000", System.Globalization.CultureInfo.InvariantCulture);
    public string SideLabel => SignedQuantity > 0 ? "Buy" : "Sell";
}
