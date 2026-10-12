using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.Portfolio.Projection;

/// <summary>Captures projection values from the accepted command state before source persistence.</summary>
public static class PortfolioAcceptedProjection
{
    /// <summary>Captures the accepted Portfolio definition and delegated values.</summary>
    public static void Capture(IPortfolioDomainEvent source, PortfolioAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(aggregate);
        var facts = new PortfolioProjectionFacts { Revision=source.Revision, UpdatedAtUtc=source.OccurredOnUtc,
            Portfolio=source is DraftPortfolioDeletedEvent ? null : aggregate.Current?.DefensiveCopy(),
            DeletedPortfolioId=source is DraftPortfolioDeletedEvent ? aggregate.Current!.PortfolioId : 0,
            Allocation=(source as FundAllocationDelegatedEvent)?.Allocation,
            RiskEnvelope=(source as FundRiskEnvelopeDelegatedEvent)?.Envelope };
        EventInitHelper.SetProperty(source, "ProjectionFacts", facts);
    }

    /// <summary>Captures the accepted mandate, assignment and composition without reading persistence.</summary>
    public static void Capture(IPortfolioFundDomainEvent source, PortfolioFundAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(aggregate);
        var facts=new PortfolioProjectionFacts { Revision=source.Revision, UpdatedAtUtc=source.OccurredOnUtc,
            Fund=aggregate.Current?.DefensiveCopy(), Assignment=(source as FundTradeTemplateAssignedEvent)?.Assignment };
        var reservation=source switch {
            FundCompositionReservedEvent reserved => reserved.Reservation,
            FundCompositionStateChangedEvent changed => aggregate.Composition(changed.Order.OrderId),
            FundManualOrderChangedEvent manual => manual.Reservation,
            _ => null };
        if (reservation is not null) facts=WithComposition(facts,reservation);
        if (source is FundManualOrderChangedEvent { RemovedTradeId: > 0 } changedOrder)
            facts=facts with { DeletedTradeIds=[changedOrder.RemovedTradeId] };
        if (source is FundManualOrderDeletedEvent deleted)
            facts=facts with { DeletedTradeIds=[..deleted.RemovedTradeIds],DeletedOrderId=deleted.OrderId };
        EventInitHelper.SetProperty(source,"ProjectionFacts",facts);
    }

    /// <summary>Captures all accepted policy versions, including superseded state.</summary>
    public static void Capture(IPortfolioFinancialPolicyDomainEvent source, PortfolioFinancialPolicyAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(aggregate);
        var facts=new PortfolioProjectionFacts { Revision=source.Revision,UpdatedAtUtc=source.OccurredOnUtc,
            Policies=source is DraftPortfolioFinancialPolicyDeletedEvent ? [] : aggregate.Versions.Select(x=>x.DefensiveCopy() with { AggregateRevision=aggregate.Revision }).ToArray() };
        if (source is DraftPortfolioFinancialPolicyDeletedEvent)
            facts=facts with { DeletedPolicyId=aggregate.Current!.PolicyId,DeletedPolicyPortfolioId=aggregate.Current.PortfolioId };
        EventInitHelper.SetProperty(source,"ProjectionFacts",facts);
    }

    static PortfolioProjectionFacts WithComposition(PortfolioProjectionFacts facts,FundCompositionReservationResult reservation)
        => facts with { Order=reservation.Order,Trades=[..reservation.Trades],OrderRevision=reservation.AggregateVersion,
            Composition=new FundCompositionWorkflowProjectionReadModel { WorkflowId=reservation.Order.WorkflowId,
                PortfolioId=reservation.Order.PortfolioId,FundId=reservation.Order.FundId,OrderId=reservation.Order.OrderId,
                CompositionResultId=reservation.Order.CompositionResultId,CompositionResultHash=reservation.Order.CompositionResultHash,
                Status=reservation.Order.Status,UpdatedOnUtc=facts.UpdatedAtUtc,AggregateVersion=reservation.AggregateVersion } };
}
