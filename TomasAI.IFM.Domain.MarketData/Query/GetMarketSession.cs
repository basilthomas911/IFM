using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Builds the authoritative futures-session snapshot for application clients.</summary>
public static class GetMarketSession
{
    public static ValueTask<MarketSessionReadModel> GetMarketSessionAsync(
        this GetMarketSessionQuery query,
        IFuturesMarketSessionAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(authority);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(authority.Current);
    }

    internal static MarketSessionReadModel Calculate(DateTimeOffset instant)
    {
        var operationalValueDate = FuturesTradingValueDate.GetOperational(instant);
        var isOpen = FuturesTradingValueDate.TryGet(instant, out var activeValueDate);
        var state = FuturesMarketSessionPolicy.GetState(instant);
        var marketTime = TimeZoneInfo.ConvertTime(instant, FuturesTradingValueDate.MarketTimeZone);
        return new MarketSessionReadModel
        {
            OperationalValueDate = operationalValueDate,
            ActiveValueDate = isOpen ? activeValueDate : null,
            MarketTime = marketTime.DateTime,
            SessionStartUtc = FuturesTradingValueDate.GetSessionStartUtc(operationalValueDate).UtcDateTime,
            SessionEndUtc = FuturesTradingValueDate.GetSessionEndUtc(operationalValueDate).UtcDateTime,
            Revision = 1,
            AsOfUtc = instant.UtcDateTime,
            State = state,
            NextTransitionUtc = FuturesMarketSessionPolicy.GetNextTransitionUtc(instant).UtcDateTime
        };
    }
}
