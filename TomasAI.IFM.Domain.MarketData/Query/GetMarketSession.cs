using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Builds the authoritative futures-session snapshot for application clients.</summary>
public static class GetMarketSession
{
    public static ValueTask<MarketSessionReadModel> GetMarketSessionAsync(
        this GetMarketSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Calculate(TimeProvider.System.GetUtcNow()));
    }

    internal static MarketSessionReadModel Calculate(DateTimeOffset instant)
    {
        var operationalValueDate = FuturesTradingValueDate.GetOperational(instant);
        var isOpen = FuturesTradingValueDate.TryGet(instant, out var activeValueDate);
        var marketTime = TimeZoneInfo.ConvertTime(instant, FuturesTradingValueDate.MarketTimeZone);
        return new MarketSessionReadModel
        {
            OperationalValueDate = operationalValueDate,
            ActiveValueDate = isOpen ? activeValueDate : null,
            IsLiveSessionOpen = isOpen,
            MarketTime = marketTime.DateTime,
            SessionStartUtc = FuturesTradingValueDate.GetSessionStartUtc(operationalValueDate).UtcDateTime,
            SessionEndUtc = FuturesTradingValueDate.GetSessionEndUtc(operationalValueDate).UtcDateTime
        };
    }
}
