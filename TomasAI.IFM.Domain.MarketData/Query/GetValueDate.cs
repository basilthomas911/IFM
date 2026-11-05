using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetValueDate
{

    /// <param name="q">
    /// The query requesting the current value date.
    /// </param>
    extension(GetValueDateQuery q)
    {
        /// Handles a <see cref="GetValueDateQuery"/> using the API's authoritative,
        /// non-null operational value date.
        /// The calculated value date is then published back to the caller via a NATS reply.
        ///
        /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
        /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
        public ValueTask<ScalarReadModel<DateOnly>> ExecuteAsync(
            IFuturesMarketSessionAuthority authority,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(q);
            ArgumentNullException.ThrowIfNull(authority);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new ScalarReadModel<DateOnly>(authority.Current.OperationalValueDate));
        }
    }

    extension(GetValueDateQuery query)
    {
        /// <summary>Reads and replies with the requested market-data result.</summary>
        public async ValueTask<ServiceResult<ScalarReadModel<DateOnly>>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            var result = await query.ExecuteAsync(context.MarketSessionAuthority, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<ScalarReadModel<DateOnly>>(result);
        }
    }

    internal static ScalarReadModel<DateOnly> CalculateValueDate(DateTime today)
    {
        var unspecified = DateTime.SpecifyKind(today, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, FuturesTradingValueDate.MarketTimeZone);
        return new ScalarReadModel<DateOnly>(FuturesTradingValueDate.GetOperational(new DateTimeOffset(utc, TimeSpan.Zero)));
    }

    internal static ScalarReadModel<DateOnly> CalculateValueDate(DateTimeOffset instant)
        => new(FuturesTradingValueDate.GetOperational(instant));
}
