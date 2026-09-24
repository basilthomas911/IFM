using System.Collections.Concurrent;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;

/// <summary>Handles the mapped activation and portfolio assignment of one financial policy.</summary>
public static class ActivateAndAssignPortfolioFinancialPolicy
{
    static readonly ConcurrentDictionary<int, SemaphoreSlim> PortfolioAssignmentLocks = new();

    /// <summary>Serializes assignment per portfolio and applies the policy activation and assignment.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate aggregate,
        PortfolioFinancialPolicyId policyId,
        IPortfolioEventStore events,
        IPortfolioDbWriteContext projections,
        IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
        string principal,
        CancellationToken cancellationToken)
    {
        var assignmentLock = PortfolioAssignmentLocks.GetOrAdd(policyId.PortfolioId, static _ => new SemaphoreSlim(1, 1));
        await assignmentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ActivateAndAssignAsync(command, aggregate, policyId, events, projections, projector, principal, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    /// <summary>Commits policy activation and repairs any partially completed portfolio assignment on replay.</summary>
    static async ValueTask<ServiceResult<GuidResult>> ActivateAndAssignAsync(
        ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate aggregate,
        PortfolioFinancialPolicyId policyId,
        IPortfolioEventStore events,
        IPortfolioDbWriteContext projections,
        IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
        string principal,
        CancellationToken cancellationToken)
    {
        var committed = await events.FindCommittedPolicyCommandAsync(policyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        var portfolioId = new PortfolioId(policyId.PortfolioId);
        var portfolio = await events.LoadPortfolioAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        if (committed is PortfolioFinancialPolicyActivatedEvent activated)
        {
            if (portfolio.Current?.ActivePolicyId != policyId.PolicyId || portfolio.Current.ActivePolicyVersion != activated.PolicyVersion)
            {
                var replayCandidate = aggregate.Versions.Single(x => x.PolicyVersion == activated.PolicyVersion);
                var assignment = portfolio.AssignFinancialPolicy(command.CommandId, command.ExpectedPortfolioRevision, replayCandidate, DateTime.UtcNow, principal);
                await events.AppendPortfolioAsync(portfolioId, assignment, assignment.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                await projections.UpsertPortfolioAsync(
                    PortfolioProjection<PortfolioReadModel>.Create(portfolio.Current!, portfolio.Revision, Math.Max(1, assignment.EventId == 0 ? assignment.Revision : assignment.EventId), assignment.OccurredOnUtc),
                    TomasAI.IFM.Domain.Portfolio.Projection.PortfolioProjectionHandler.StateBucket(portfolioId.Id), cancellationToken).ConfigureAwait(false);
            }
            // A prior attempt may have committed the policy event and failed before
            // projection/assignment. Both operations are idempotent, so replay heals
            // every derived surface as well as the authoritative Portfolio reference.
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([activated])).ConfigureAwait(false);
            await ProjectAllAsync(aggregate, activated, projections, cancellationToken).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (committed is not null)
            return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the command was committed for a different policy operation.");

        var now = DateTime.UtcNow;
        // Validate the Portfolio assignment before committing either stream. The
        // per-Portfolio coordinator prevents distinct policy actors in this host from
        // both passing this expected-revision check.
        if (portfolio.Revision != command.ExpectedPortfolioRevision)
            throw new InvalidOperationException($"Expected Portfolio revision {command.ExpectedPortfolioRevision}, actual {portfolio.Revision}.");
        var policyEvent = aggregate.Activate(command.CommandId, command.ExpectedPolicyRevision, command.PolicyVersion, now, principal);
        var candidate = aggregate.Current!;
        var portfolioEvent = portfolio.AssignFinancialPolicy(command.CommandId, command.ExpectedPortfolioRevision, candidate, now, principal);
        await events.AppendPolicyAsync(policyId, policyEvent, policyEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await events.AppendPortfolioAsync(portfolioId, portfolioEvent, portfolioEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([policyEvent])).ConfigureAwait(false);
        await ProjectAllAsync(aggregate, policyEvent, projections, cancellationToken).ConfigureAwait(false);
        await projections.UpsertPortfolioAsync(
            PortfolioProjection<PortfolioReadModel>.Create(portfolio.Current!, portfolio.Revision, Math.Max(1, portfolioEvent.EventId == 0 ? portfolioEvent.Revision : portfolioEvent.EventId), portfolioEvent.OccurredOnUtc),
            TomasAI.IFM.Domain.Portfolio.Projection.PortfolioProjectionHandler.StateBucket(portfolioId.Id), cancellationToken).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    /// <summary>Refreshes all materialized policy versions after the authoritative event commits.</summary>
    static async Task ProjectAllAsync(PortfolioFinancialPolicyAggregate aggregate, IPortfolioFinancialPolicyDomainEvent domainEvent,
        IPortfolioDbWriteContext projections, CancellationToken cancellationToken)
    {
        foreach (var policy in aggregate.Versions)
            await projections.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(
                policy.DefensiveCopy() with { AggregateRevision = aggregate.Revision }, domainEvent.Revision, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId), domainEvent.OccurredOnUtc), cancellationToken).ConfigureAwait(false);
    }
}
