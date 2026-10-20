using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Query;

/// <summary>Read-only draft calculation handler; never applies events or authorizes an order.</summary>
public static class GetTradeOrderPreview
{
    /// <summary>Replies with calculated strategy economics or a structured invalid-draft failure.</summary>
    /// <param name="query">The draft and exact quote inputs to compute.</param>
    /// <param name="context">The actor reply context; persisted state is not accessed.</param>
    /// <param name="cancellationToken">Cancellation of the read-only request.</param>
    /// <returns>Completion after the typed success or invalid-input reply is sent.</returns>
    public static async ValueTask ExecuteAsync(this GetTradeOrderPreviewQuery query, ITradeOrderQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ServiceResult<TradeOrderPreview> result;
        try { result = new ServiceOk<TradeOrderPreview>(query.Compute()); }
        catch (ArgumentException exception) { result = new ServiceFailed<TradeOrderPreview>(400, exception.Message); }
        catch (OverflowException) { result = new ServiceFailed<TradeOrderPreview>(400, "TradeOrder.Preview.NUMERIC_OVERFLOW: draft values exceed supported numeric limits."); }
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }

    /// <summary>Computes the preview without changing actor or persisted state.</summary>
    /// <param name="query">The provisional strategy preview request.</param>
    /// <returns>Backend-calculated strategy totals.</returns>
    internal static TradeOrderPreview Compute(this GetTradeOrderPreviewQuery query) => TradeOrderPreviewCompute.Create(query.TradeOrderPreviewRequest);
}
