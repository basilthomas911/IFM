using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Workflow;

public sealed class PortfolioFundCompositionService(
    IPortfolioBusinessIdAllocator allocator,
    PortfolioFundCompositionAggregate aggregate)
{
    readonly IPortfolioBusinessIdAllocator _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    readonly PortfolioFundCompositionAggregate _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));

    public async ValueTask<FundCompositionReservationResult> ReserveAsync(
        ReserveFundOrderCompositionRequest request,
        PortfolioFundStrategySnapshot snapshot,
        DateTime committedOnUtc,
        string principal,
        CancellationToken cancellationToken = default)
    {
        if (_aggregate.TryGetReservation(request.IdempotencyKey, out var prior))
            return _aggregate.Reserve(request, snapshot, prior.Order.OrderId, prior.Trades.Select(x => x.TradeId).ToArray(), committedOnUtc, principal);

        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++)
            tradeIds[i] = await _allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
        return _aggregate.Reserve(request, snapshot, orderId, tradeIds, committedOnUtc, principal);
    }
}
