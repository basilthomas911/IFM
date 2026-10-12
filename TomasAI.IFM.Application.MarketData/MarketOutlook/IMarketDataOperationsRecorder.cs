using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>
/// Minimal cross-stage recording boundary. Stage 3 replaces the Stage 1 collector with the complete
/// central operational-health implementation without reopening producers.
/// </summary>
public interface IMarketDataOperationsRecorder
{
    void Record(in MarketDataOperationMeasurement measurement);
}
