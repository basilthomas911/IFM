using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
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
    /// <summary>Handles new at the command boundary.</summary>
    /// <returns>The operation result.</returns>
    static readonly ConcurrentDictionary<int, SemaphoreSlim> PortfolioAssignmentLocks = new();

    /// <summary>Serializes assignment per portfolio and applies the policy activation and assignment.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="aggregate">The current authoritative business state.</param>
    /// <param name="policyId">The exact Portfolio financial-policy identity.</param>
    /// <param name="events">The authoritative event store used for lookup and commit.</param>
    /// <param name="projections">The committed-event read-model projection writer.</param>
    /// <param name="projector">The command-owned committed-event projector.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
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
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="aggregate">The current authoritative business state.</param>
    /// <param name="policyId">The exact Portfolio financial-policy identity.</param>
    /// <param name="events">The authoritative event store used for lookup and commit.</param>
    /// <param name="projections">The committed-event read-model projection writer.</param>
    /// <param name="projector">The command-owned committed-event projector.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
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
                if (!command.ComputeAssignment(portfolio, replayCandidate, DateTime.UtcNow, principal, out var portfolioChange))
                    return command.UpdateFailed($"{portfolioChange.RejectionCode};{portfolioChange.RejectionReason}");
                var assignment = command.CreatePortfolioFinancialPolicyAssignedEvent(portfolioChange);
                if (!portfolio.Update(assignment, command))
                    return command.UpdateFailed("Portfolio policy assignment event could not be applied during repair");
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
        if (!command.Compute(aggregate, now, principal, out var financialPolicyChange))
            return command.UpdateFailed($"{financialPolicyChange.RejectionCode};{financialPolicyChange.RejectionReason}");
        var candidate = aggregate.Versions.Single(policy => policy.PolicyVersion == command.PolicyVersion)
            with { OperatingState = TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFinancialPolicyState.Active };
        if (!command.ComputeAssignment(portfolio, candidate, now, principal, out var portfolioAssignment))
            return command.UpdateFailed($"{portfolioAssignment.RejectionCode};{portfolioAssignment.RejectionReason}");
        var policyEvent = command.CreatePortfolioFinancialPolicyActivatedEvent(financialPolicyChange);
        var portfolioEvent = command.CreatePortfolioFinancialPolicyAssignedEvent(portfolioAssignment);
        var errorMsg = $"{command.CommandName}: unable to apply policy activation and assignment events";
        var updated = true switch
        {
            _ when financialPolicyChange.Revision != aggregate.Revision + 1 || portfolioAssignment.Revision != portfolio.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed policy or Portfolio revision is invalid"),
            _ when !financialPolicyChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{financialPolicyChange.RejectionCode};{financialPolicyChange.RejectionReason}"),
            _ when financialPolicyChange.CommandId != command.CommandId || portfolioAssignment.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed originating command identity is invalid"),
            _ => aggregate.Update(policyEvent, command) && portfolio.Update(portfolioEvent, command)
        };
        if (!updated) return command.UpdateFailed(errorMsg);
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
    /// <param name="aggregate">The current authoritative business state.</param>
    /// <param name="domainEvent">The domain event business input.</param>
    /// <param name="projections">The committed-event read-model projection writer.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    static async Task ProjectAllAsync(PortfolioFinancialPolicyAggregate aggregate, IPortfolioFinancialPolicyDomainEvent domainEvent,
        IPortfolioDbWriteContext projections, CancellationToken cancellationToken)
    {
        foreach (var policy in aggregate.Versions)
            await projections.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(
                policy.DefensiveCopy() with { AggregateRevision = aggregate.Revision }, domainEvent.Revision, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId), domainEvent.OccurredOnUtc), cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Computes the accepted Activate business values without mutating state.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this ActivateAndAssignPortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyAggregate state,
        DateTime now, string principal, out PortfolioFinancialPolicyActivatedCompute financialPolicyChange)
    {
        try
        {
            financialPolicyChange = (PortfolioFinancialPolicyActivatedCompute)state.ComputeActivate(command.CommandId, command.ExpectedPolicyRevision, command.PolicyVersion, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            financialPolicyChange = new PortfolioFinancialPolicyActivatedCompute { Accepted = false, RejectionCode = "PortfolioFinancialPolicy.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the accepted PortfolioFinancialPolicyActivatedEvent with the originating command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static PortfolioFinancialPolicyActivatedEvent CreatePortfolioFinancialPolicyActivatedEvent(this ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyActivatedCompute financialPolicyChange) => new()
    {
        Id = financialPolicyChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = financialPolicyChange.OccurredOnUtc,
        Revision = financialPolicyChange.Revision,
        OccurredOnUtc = financialPolicyChange.OccurredOnUtc,
        Principal = financialPolicyChange.Principal,
        OriginatedOnUtc = financialPolicyChange.OccurredOnUtc,
        PolicyVersion = financialPolicyChange.PolicyVersion,
    };

    /// <summary>Computes the accepted AssignFinancialPolicy business values without mutating state.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="financialPolicy">The financial policy business input.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool ComputeAssignment(this ActivateAndAssignPortfolioFinancialPolicyCommand command, PortfolioAggregate state, PortfolioFinancialPolicyReadModel financialPolicy,
        DateTime now, string principal, out PortfolioFinancialPolicyAssignedCompute portfolioChange)
    {
        try
        {
            portfolioChange = (PortfolioFinancialPolicyAssignedCompute)state.ComputeAssignFinancialPolicy(command.CommandId, command.ExpectedPortfolioRevision, financialPolicy, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            portfolioChange = new PortfolioFinancialPolicyAssignedCompute { Accepted = false, RejectionCode = "PortfolioFinancialPolicy.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the accepted PortfolioFinancialPolicyAssignedEvent with the originating command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static PortfolioFinancialPolicyAssignedEvent CreatePortfolioFinancialPolicyAssignedEvent(this ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAssignedCompute portfolioChange) => new()
    {
        Id = portfolioChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = portfolioChange.OccurredOnUtc,
        Revision = portfolioChange.Revision,
        OccurredOnUtc = portfolioChange.OccurredOnUtc,
        Principal = portfolioChange.Principal,
        OriginatedOnUtc = portfolioChange.OccurredOnUtc,
        PolicyId = portfolioChange.PolicyId,
        PolicyVersion = portfolioChange.PolicyVersion,
    };

}
