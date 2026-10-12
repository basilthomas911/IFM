using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal sealed record CalendarPageToken(
    string Fingerprint,
    int PartitionIndex,
    long? LastEventDateTicks,
    string? LastEventName);
