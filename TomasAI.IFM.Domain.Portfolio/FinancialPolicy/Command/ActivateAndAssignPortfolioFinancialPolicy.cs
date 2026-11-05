using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
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

    /// <param name="command">
    /// The concrete business command and originating identity.
    /// </param>
    extension(ActivateAndAssignPortfolioFinancialPolicyCommand command)
    {
        /// <summary>Serializes assignment per portfolio and applies the policy activation and assignment.</summary>
        ///
        /// <param name="aggregate">The current authoritative business state.</param>
        /// <param name="policyId">The exact Portfolio financial-policy identity.</param>
        /// <param name="events">The authoritative event store used for lookup and commit.</param>
        /// <param name="portfolioProjector">The Portfolio owner's committed-event projector.</param>
        /// <param name="projector">The command-owned committed-event projector.</param>
        /// <param name="logger">Reports submission failures recoverable from committed projection markers.</param>
        /// <param name="principal">The authenticated and authorized operator.</param>
        /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
        /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
        /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
        public async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
            PortfolioFinancialPolicyAggregate aggregate,
            PortfolioFinancialPolicyId policyId,
            IPortfolioEventStore events,
            IEventProjector<PortfolioCommandActor> portfolioProjector,
            IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
            ILogger logger,
            string principal,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(aggregate);
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(policyId);
            ArgumentNullException.ThrowIfNull(portfolioProjector);
            ArgumentNullException.ThrowIfNull(projector);
            var assignmentLock = PortfolioAssignmentLocks.GetOrAdd(policyId.PortfolioId, static _ => new SemaphoreSlim(1, 1));
            await assignmentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ActivateAndAssignAsync(command, aggregate, policyId, events, portfolioProjector, projector, logger, principal, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                assignmentLock.Release();
            }
        }
    }

    /// <summary>Commits policy activation and repairs any partially completed portfolio assignment on replay.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="aggregate">The current authoritative business state.</param>
    /// <param name="policyId">The exact Portfolio financial-policy identity.</param>
    /// <param name="events">The authoritative event store used for lookup and commit.</param>
    /// <param name="portfolioProjector">The Portfolio owner's committed-event projector.</param>
    /// <param name="projector">The command-owned committed-event projector.</param>
    /// <param name="logger">Reports recoverable post-commit submission failures.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    static async ValueTask<ServiceResult<GuidResult>> ActivateAndAssignAsync(
        ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate aggregate,
        PortfolioFinancialPolicyId policyId,
        IPortfolioEventStore events,
        IEventProjector<PortfolioCommandActor> portfolioProjector,
        IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
        ILogger logger,
        string principal,
        CancellationToken cancellationToken)
    {
        var committed = await events.FindCommittedPolicyCommandAsync(policyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        var portfolioId = new PortfolioId(policyId.PortfolioId);
        if (committed is PortfolioFinancialPolicyActivatedEvent activated)
            return await CompleteAcceptedAssignmentAsync(command, aggregate, portfolioId, activated, events, portfolioProjector, projector, logger, principal, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
            return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the command was committed for a different policy operation.");

        var now = DateTime.UtcNow;
        if (!command.Compute(aggregate, now, principal, out var financialPolicyChange))
            return command.UpdateFailed($"{financialPolicyChange.RejectionCode};{financialPolicyChange.RejectionReason}");
        var candidate = aggregate.Versions.Single(policy => policy.PolicyVersion == command.PolicyVersion)
            with
        { OperatingState = TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFinancialPolicyState.Active };
        var policyEvent = command.CreatePortfolioFinancialPolicyActivatedEvent(financialPolicyChange);
        var errorMsg = $"{command.CommandName}: unable to apply policy activation and assignment events";
        var updated = true switch
        {
            _ when financialPolicyChange.Revision != aggregate.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed policy revision is invalid"),
            _ when !financialPolicyChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{financialPolicyChange.RejectionCode};{financialPolicyChange.RejectionReason}"),
            _ when financialPolicyChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed originating command identity is invalid"),
            _ => aggregate.Update(policyEvent, command)
        };
        if (!updated) return command.UpdateFailed(errorMsg);
        TomasAI.IFM.Domain.Portfolio.Projection.PortfolioAcceptedProjection.Capture(policyEvent, aggregate);
        var portfolioEvent = await events.CommitPolicyAssignmentAsync(command, candidate, now, principal,
            policyEvent, aggregate, cancellationToken).ConfigureAwait(false);
        await Projection.PortfolioProjectionSubmission.SubmitAsync(projector, policyEvent, logger).ConfigureAwait(false);
        await Projection.PortfolioProjectionSubmission.SubmitAsync(portfolioProjector, portfolioEvent, logger).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
    /// <summary>Completes an older accepted activation whose assignment was not part of an atomic batch.</summary>
    static async ValueTask<ServiceResult<GuidResult>> CompleteAcceptedAssignmentAsync(
        ActivateAndAssignPortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyAggregate aggregate,
        PortfolioId portfolioId,
        PortfolioFinancialPolicyActivatedEvent activated, IPortfolioEventStore events, IEventProjector<PortfolioCommandActor> portfolioProjector,
        IEventProjector<PortfolioFinancialPolicyCommandActor> projector, ILogger logger, string principal, CancellationToken cancellationToken)
    {
        var assignmentEvent = await events.FindCommittedPortfolioCommandAsync(portfolioId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (assignmentEvent is null)
        {
            var replayCandidate = aggregate.Versions.Single(x => x.PolicyVersion == activated.PolicyVersion);
            assignmentEvent = await events.RepairPolicyAssignmentAsync(command, replayCandidate,
                DateTime.UtcNow, principal, cancellationToken).ConfigureAwait(false);
        }
        // A prior attempt may have committed the policy event and failed before
        // projection/assignment. Both operations are idempotent, so replay heals
        // every derived surface as well as the authoritative Portfolio reference.
        await Projection.PortfolioProjectionSubmission.SubmitAsync(projector, activated, logger).ConfigureAwait(false);
        if (assignmentEvent is PortfolioFinancialPolicyAssignedEvent assignmentSource)
            await Projection.PortfolioProjectionSubmission.SubmitAsync(portfolioProjector, assignmentSource, logger).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }


    /// <param name="command">
    /// The concrete business command and originating identity.
    /// </param>
    extension(ActivateAndAssignPortfolioFinancialPolicyCommand command)
    {
        /// <summary>Computes the accepted Activate business values without mutating state.</summary>
        ///
        /// <param name="state">The owning authoritative command state.</param>
        /// <param name="now">The authoritative UTC decision time.</param>
        /// <param name="principal">The authenticated and authorized operator.</param>
        /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
        /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
        internal bool Compute(PortfolioFinancialPolicyAggregate state,
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
        ///
        /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
        /// <returns>The computed business values or created event; no transport or storage effects.</returns>
        internal PortfolioFinancialPolicyActivatedEvent CreatePortfolioFinancialPolicyActivatedEvent(PortfolioFinancialPolicyActivatedCompute financialPolicyChange) => new()
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
        ///
        /// <param name="state">The owning authoritative command state.</param>
        /// <param name="financialPolicy">The financial policy business input.</param>
        /// <param name="now">The authoritative UTC decision time.</param>
        /// <param name="principal">The authenticated and authorized operator.</param>
        /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
        /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
        internal bool ComputeAssignment(PortfolioAggregate state, PortfolioFinancialPolicyReadModel financialPolicy,
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
        ///
        /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
        /// <returns>The computed business values or created event; no transport or storage effects.</returns>
        internal PortfolioFinancialPolicyAssignedEvent CreatePortfolioFinancialPolicyAssignedEvent(PortfolioFinancialPolicyAssignedCompute portfolioChange) => new()
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

}
