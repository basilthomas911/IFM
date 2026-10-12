using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketDataOperationMeasurement(
    MarketDataOperationStage Stage,
    MarketDataOperationOutcome Outcome,
    MarketOutlookUpdateKind UpdateKind,
    Guid UpdateId,
    DateTime OccurredAtUtc,
    TimeSpan? Latency = null,
    DateTime? MarketDataAsOfUtc = null);
