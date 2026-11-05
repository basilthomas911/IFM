using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Builds the authoritative futures-session snapshot for application clients.</summary>
public static class GetMarketSession
{

    extension(GetMarketSessionQuery query)
    {
        public ValueTask<MarketSessionReadModel> ExecuteAsync(
            IFuturesMarketSessionAuthority authority,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(authority);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(authority.Current with
            {
                AllowDevelopmentEmulator =
                string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.Ordinal)
            });
        }

        /// <summary>Reads and replies with the requested market-data result.</summary>
        public async ValueTask<ServiceResult<MarketSessionReadModel>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            var result = await query.ExecuteAsync(context.MarketSessionAuthority, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<MarketSessionReadModel>(result);
        }
    }

    internal static MarketSessionReadModel Calculate(DateTimeOffset instant)
        => Calculate(instant, FuturesTradingValueDate.GetOperational(instant));

    internal static MarketSessionReadModel Calculate(DateTimeOffset instant, DateOnly operationalValueDate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationalValueDate, default(DateOnly));

        var isOpen = FuturesTradingValueDate.TryGet(instant, out var activeValueDate);
        var endOfDayPending = isOpen && activeValueDate != operationalValueDate;
        var state = endOfDayPending ? FuturesMarketState.Closed : FuturesMarketSessionPolicy.GetState(instant);
        var marketTime = TimeZoneInfo.ConvertTime(instant, FuturesTradingValueDate.MarketTimeZone);
        return new MarketSessionReadModel
        {
            OperationalValueDate = operationalValueDate,
            ActiveValueDate = isOpen && !endOfDayPending ? activeValueDate : null,
            MarketTime = marketTime.DateTime,
            SessionStartUtc = FuturesTradingValueDate.GetSessionStartUtc(operationalValueDate).UtcDateTime,
            SessionEndUtc = FuturesTradingValueDate.GetSessionEndUtc(operationalValueDate).UtcDateTime,
            Revision = 1,
            AsOfUtc = instant.UtcDateTime,
            State = state,
            IsEndOfDayPending = endOfDayPending,
            NextTransitionUtc = FuturesMarketSessionPolicy.GetNextTransitionUtc(instant).UtcDateTime
        };
    }
}
