using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetValueDate
{
    /// Handles a <see cref="GetValueDateQuery"/> using the API's authoritative,
    /// non-null operational value date.
    /// The calculated value date is then published back to the caller via a NATS reply.    
    /// <param name="q">The query requesting the current value date.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    public static ValueTask<ScalarReadModel<DateOnly>> ExecuteAsync(
        this GetValueDateQuery q,
        IFuturesMarketSessionAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(authority);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            new ScalarReadModel<DateOnly>(authority.Current.OperationalValueDate));
    }

    internal static ScalarReadModel<DateOnly> CalculateValueDate(DateTime today)
    {
        var unspecified = DateTime.SpecifyKind(today, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, FuturesTradingValueDate.MarketTimeZone);
        return new ScalarReadModel<DateOnly>(FuturesTradingValueDate.GetOperational(utc));
    }

    internal static ScalarReadModel<DateOnly> CalculateValueDate(DateTimeOffset instant)
        => new(FuturesTradingValueDate.GetOperational(instant));

    /// <summary>Reads and replies with the requested market-data result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetValueDateQuery query,
        TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await query.ExecuteAsync(context.MarketSessionAuthority, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<ScalarReadModel<DateOnly>>(result)).ConfigureAwait(false);
    }
}
