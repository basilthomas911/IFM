using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public interface IMarketCompositionSnapshotProvider
{
    Task<CompositionSnapshotResult> CaptureAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken);
}
