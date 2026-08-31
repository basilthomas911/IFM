using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

/// <summary>Deterministic committed-event to Scylla projection mapping used by durable projector descriptors and rebuild.</summary>
public sealed class PortfolioProjectionHandler(IPortfolioEventStore events, IPortfolioDbWriteContext projections)
{
    public async Task ApplyAsync(PortfolioDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.EventId <= 0) throw new InvalidOperationException("Only committed events can be projected.");
        var portfolioId = ParsePortfolioId(domainEvent.AggregateId);
        var aggregate = await events.LoadPortfolioAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        var current = aggregate.Current ?? throw new InvalidOperationException("Committed Portfolio history did not rebuild a Portfolio.");
        if (domainEvent is DraftPortfolioDeleted)
        {
            var funds = new List<DraftFundProjectionDeletion>();
            foreach (var fundId in aggregate.FundIds.Order())
            {
                var history = await events.LoadFundHistoryAsync(new PortfolioFundId(portfolioId.Id, fundId), cancellationToken).ConfigureAwait(false);
                if (history.OfType<FundCompositionReserved>().Any())
                    throw new InvalidOperationException("A Portfolio with composition history cannot be deleted.");
                var versions = history.Select(x => x switch
                {
                    FundMandateCreated created => created.Mandate.FundMandateVersion,
                    FundMandateVersionAdded added => added.Mandate.FundMandateVersion,
                    _ => 0,
                }).Where(x => x > 0).Distinct().Order().ToArray();
                funds.Add(new(fundId, versions));
            }
            await projections.DeleteDraftPortfolioAsync(
                new(current.PortfolioId, StateBucket(current.PortfolioId), [.. funds], domainEvent.EventId),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        await projections.UpsertPortfolioAsync(
            PortfolioProjection<PortfolioReadModel>.Create(current, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
            StateBucket(current.PortfolioId), cancellationToken).ConfigureAwait(false);
        if (domainEvent is FundAllocationDelegated allocation)
            await projections.UpsertAllocationAsync(
                PortfolioProjection<FundAllocationReadModel>.Create(allocation.Allocation, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
                cancellationToken).ConfigureAwait(false);
        if (domainEvent is FundRiskEnvelopeDelegated risk)
            await projections.UpsertRiskEnvelopeAsync(
                PortfolioProjection<FundRiskEnvelopeReadModel>.Create(risk.Envelope, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
                cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyAsync(PortfolioFundDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.EventId <= 0) throw new InvalidOperationException("Only committed events can be projected.");
        var fundId = ParseFundId(domainEvent.AggregateId);
        var aggregate = await events.LoadFundAsync(fundId, cancellationToken).ConfigureAwait(false);
        var current = aggregate.Current ?? throw new InvalidOperationException("Committed Fund history did not rebuild a mandate.");
        await projections.UpsertFundAsync(
            PortfolioProjection<FundMandateReadModel>.Create(current, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
            cancellationToken).ConfigureAwait(false);
        if (domainEvent is FundTradeTemplateAssigned assigned)
            await projections.UpsertAssignmentAsync(
                PortfolioProjection<FundTradeTemplateAssignmentReadModel>.Create(assigned.Assignment, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
                cancellationToken).ConfigureAwait(false);
        if (domainEvent is FundCompositionReserved reserved)
            await ApplyCompositionAsync(reserved.Reservation, domainEvent.EventId, domainEvent.ReceivedOn, cancellationToken).ConfigureAwait(false);
        if (domainEvent is FundCompositionStateChanged changed)
            await ApplyCompositionAsync(aggregate.Composition(changed.Order.OrderId), domainEvent.EventId, domainEvent.ReceivedOn, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyAsync(PortfolioFinancialPolicyDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.EventId <= 0) throw new InvalidOperationException("Only committed policy events can be projected.");
        var policyId = ParsePolicyId(domainEvent.AggregateId);
        var aggregate = await events.LoadPolicyAsync(policyId, cancellationToken).ConfigureAwait(false);
        if (domainEvent is DraftPortfolioFinancialPolicyDeleted)
        {
            await projections.DeleteDraftPolicyAsync(
                new(policyId.PortfolioId, policyId.PolicyId, domainEvent.EventId), cancellationToken).ConfigureAwait(false);
            return;
        }
        foreach (var policy in aggregate.Versions)
            await projections.UpsertPolicyAsync(
                PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(
                    policy.DefensiveCopy() with { AggregateRevision = aggregate.Revision }, aggregate.Revision, domainEvent.EventId, domainEvent.ReceivedOn),
                cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyCompositionAsync(
        FundCompositionReservationResult reservation,
        long sourceEventId,
        DateTime updatedOnUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (sourceEventId <= 0 || reservation.AggregateVersion <= 0)
            throw new InvalidOperationException("Only committed composition state can be projected.");
        if (updatedOnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Projection timestamp must be UTC.", nameof(updatedOnUtc));

        await projections.UpsertOrderAsync(
            PortfolioProjection<FundOrderProjectionReadModel>.Create(reservation.Order, reservation.AggregateVersion, sourceEventId, updatedOnUtc),
            new DateOnly(reservation.Order.CreatedOnUtc.Year, reservation.Order.CreatedOnUtc.Month, 1),
            cancellationToken).ConfigureAwait(false);
        foreach (var trade in reservation.Trades.OrderBy(x => x.LegOrdinal))
            await projections.UpsertTradeAsync(
                PortfolioProjection<FundOrderTradeProjectionReadModel>.Create(trade, reservation.AggregateVersion, sourceEventId, updatedOnUtc),
                cancellationToken).ConfigureAwait(false);
        await projections.UpsertCompositionAsync(
            PortfolioProjection<FundCompositionWorkflowProjectionReadModel>.Create(new FundCompositionWorkflowProjectionReadModel
            {
                WorkflowId = reservation.Order.WorkflowId,
                PortfolioId = reservation.Order.PortfolioId,
                FundId = reservation.Order.FundId,
                OrderId = reservation.Order.OrderId,
                CompositionResultId = reservation.Order.CompositionResultId,
                CompositionResultHash = reservation.Order.CompositionResultHash,
                Status = reservation.Order.Status,
                UpdatedOnUtc = updatedOnUtc,
                AggregateVersion = reservation.AggregateVersion,
            }, reservation.AggregateVersion, sourceEventId, updatedOnUtc),
            cancellationToken).ConfigureAwait(false);
    }

    public static int StateBucket(int portfolioId) => checked((portfolioId - 1) / 1000);

    static PortfolioId ParsePortfolioId(string value) =>
        int.TryParse(value, out var id) && id > 0 ? new PortfolioId(id) : throw new InvalidOperationException("Portfolio event aggregate identity is invalid.");

    static PortfolioFundId ParseFundId(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var fundId) && portfolioId > 0 && fundId > 0
            ? new PortfolioFundId(portfolioId, fundId)
            : throw new InvalidOperationException("PortfolioFund event aggregate identity is invalid.");
    }

    static PortfolioFinancialPolicyId ParsePolicyId(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var policyId) && portfolioId > 0 && policyId > 0
            ? new PortfolioFinancialPolicyId(portfolioId, policyId)
            : throw new InvalidOperationException("PortfolioFinancialPolicy event aggregate identity is invalid.");
    }
}
