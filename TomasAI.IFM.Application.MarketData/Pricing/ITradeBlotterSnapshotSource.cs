using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Historical/fixture seam. Implementations provide a frozen snapshot and explicit virtual time.</summary>
public interface ITradeBlotterSnapshotSource
{
    string SourceLabel { get; }
    bool IsHistorical { get; }
    DateTimeOffset AsOfUtc { get; }
    Task<MarketCompositionSnapshot> CaptureAsync(CancellationToken cancellationToken);
}
