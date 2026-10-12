using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum MarketDataOperationOutcome : byte
{
    Received,
    Enqueued,
    Applied,
    Changed,
    Composed,
    Published,
    Failed,
    Coalesced,
    Requested,
    Started,
    Completed
}
