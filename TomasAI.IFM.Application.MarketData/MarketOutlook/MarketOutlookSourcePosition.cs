using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>
/// Describes where a latest-arrival cache value came from. These values are diagnostic only and
/// never participate in cache admission or ordering decisions.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketOutlookSourcePosition(
    Guid SourceId,
    long SourceSequence,
    DateTime SourceTimestampUtc,
    Guid StreamEpochId = default,
    long StreamOrdinal = 0);
