using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>
/// Authoritative application handler used by the PortfolioFund command actor. It allocates integer identities,
/// appends exactly one Fund event per accepted transition, and has no execution or TradeDb dependency.
/// </summary>
public sealed class PortfolioFundCompositionCommandHandler(
    IPortfolioEventStore eventStore,
    IPortfolioBusinessIdAllocator allocator)
{
    readonly IPortfolioEventStore _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IPortfolioBusinessIdAllocator _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));

    public async Task<FundCompositionReservationResult> ReserveAsync(
        Guid commandId, ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot,
        DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
    {
        var fundId = new PortfolioFundId(request.PortfolioId, request.FundId);
        var aggregate = await _eventStore.LoadFundAsync(fundId, cancellationToken).ConfigureAwait(false);
        if (aggregate.TryComposition(request.IdempotencyKey, out var prior))
        {
            var requestHash = PortfolioCanonicalHash.Compute(request.DefensiveCopy());
            if (!string.Equals(prior.CanonicalRequestSha256, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return prior with { Disposition = ReservationDisposition.IdempotentReplay };
        }

        var orderId = await _allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++)
            tradeIds[i] = await _allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
        var domainEvent = aggregate.ReserveComposition(commandId, aggregate.Revision, request, snapshot, orderId, tradeIds, nowUtc, principal);
        await _eventStore.AppendFundAsync(fundId, domainEvent, domainEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ((Command.Model.FundCompositionReserved)domainEvent).Reservation;
    }

    public Task<FundOrderProjectionReadModel> MarkComposingAsync(Guid commandId, PortfolioFundOrderId id, long expectedOrderVersion,
        DateTime nowUtc, string principal, CancellationToken cancellationToken = default) =>
        TransitionAsync(commandId, id, nowUtc, principal,
            aggregate => aggregate.MarkCompositionComposing(commandId, aggregate.Revision, id.OrderId, expectedOrderVersion, nowUtc, principal), cancellationToken);

    public Task<FundOrderProjectionReadModel> RecordComposedAsync(Guid commandId, PortfolioFundOrderId id, long expectedOrderVersion,
        OrderCompositionResultReference result, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) =>
        TransitionAsync(commandId, id, nowUtc, principal,
            aggregate => aggregate.RecordCompositionResult(commandId, aggregate.Revision, id.OrderId, expectedOrderVersion, result, nowUtc, principal), cancellationToken);

    public Task<FundOrderProjectionReadModel> RecordRiskAsync(Guid commandId, PortfolioFundOrderId id, long expectedOrderVersion,
        RiskManagementResultReference result, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) =>
        TransitionAsync(commandId, id, nowUtc, principal,
            aggregate => aggregate.RecordRiskResult(commandId, aggregate.Revision, id.OrderId, expectedOrderVersion, result, nowUtc, principal), cancellationToken);

    public Task<FundOrderProjectionReadModel> CancelAsync(Guid commandId, PortfolioFundOrderId id, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) =>
        TransitionAsync(commandId, id, nowUtc, principal,
            aggregate => aggregate.CancelComposition(commandId, aggregate.Revision, id.OrderId, expectedOrderVersion, reason, nowUtc, principal), cancellationToken);

    public Task<FundOrderProjectionReadModel> ExpireAsync(Guid commandId, PortfolioFundOrderId id, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) =>
        TransitionAsync(commandId, id, nowUtc, principal,
            aggregate => aggregate.ExpireComposition(commandId, aggregate.Revision, id.OrderId, expectedOrderVersion, reason, nowUtc, principal), cancellationToken);

    async Task<FundOrderProjectionReadModel> TransitionAsync(Guid commandId, PortfolioFundOrderId id, DateTime nowUtc, string principal,
        Func<State.PortfolioFundAggregate, Model.PortfolioFundDomainEvent> transition, CancellationToken cancellationToken)
    {
        var fundId = new PortfolioFundId(id.PortfolioId, id.FundId);
        var aggregate = await _eventStore.LoadFundAsync(fundId, cancellationToken).ConfigureAwait(false);
        if (await _eventStore.FindCommittedFundCommandAsync(fundId, commandId, cancellationToken).ConfigureAwait(false) is Model.FundCompositionStateChanged committed)
            return committed.Order;
        var domainEvent = transition(aggregate);
        await _eventStore.AppendFundAsync(fundId, domainEvent, domainEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ((Model.FundCompositionStateChanged)domainEvent).Order;
    }
}
