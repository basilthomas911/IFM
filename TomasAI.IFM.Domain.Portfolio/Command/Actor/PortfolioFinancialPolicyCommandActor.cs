using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioFinancialPolicyCommandActor(
    ICommandActorContext<PortfolioFinancialPolicyCommandActor> context,
    IPortfolioEventStore events,
    IPortfolioDbWriteContext projections,
    IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
    ILogger<PortfolioFinancialPolicyCommandActor> logger)
    : BaseEventSourceCommandActor<PortfolioFinancialPolicyCommandActor>(context, logger)
{
    public const string ActorName = PortfolioCommandSubjects.PolicyActor;
    const string Principal = "portfolio-policy-nats";
    static readonly ConcurrentDictionary<int, SemaphoreSlim> PortfolioAssignmentLocks = new();

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioFinancialPolicyCommandActor> actorContext, CancellationToken cancellationToken) =>
        projector.StartAsync(actorContext, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioFinancialPolicyCommandActor> actorContext) => projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> ParseMap = new Dictionary<string, Func<IActorMessage, ICommand>>
    {
        ["CreatePortfolioFinancialPolicy"] = x => x.AsCommand<PortfolioCommand<CreatePortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId>>()!,
        ["AddPortfolioFinancialPolicyVersion"] = x => x.AsCommand<PortfolioCommand<AddPortfolioFinancialPolicyVersionPayload, PortfolioFinancialPolicyId>>()!,
        ["ActivateAndAssignPortfolioFinancialPolicy"] = x => x.AsCommand<PortfolioCommand<ActivateAndAssignPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId>>()!,
        ["RetirePortfolioFinancialPolicy"] = x => x.AsCommand<PortfolioCommand<RetirePortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId>>()!,
        ["DeleteDraftPortfolioFinancialPolicy"] = x => x.AsCommand<PortfolioCommand<DeleteDraftPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId>>()!,
    };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioFinancialPolicyCommandActor> actorContext, IActorMessage message) =>
        ParseMappedCommand(actorContext, message, ParseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, ActorThreadId __, ICommand command)
    {
        if (command.CommandId == Guid.Empty || ParseId(command).Validate().Count != 0) throw new ArgumentException("A valid policy command identity is required.");
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, ActorThreadId __, ICommand command) =>
        new PolicyActorState(ParseId(command), await events.LoadPolicyAsync(ParseId(command)).ConfigureAwait(false));

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, IActorState state, ICommand command) =>
        ReceiveCoreAsync((PolicyActorState)state, command, CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        ReceiveCoreAsync((PolicyActorState)state, command, cancellationToken);

    async ValueTask<ServiceResult<GuidResult>> ReceiveCoreAsync(PolicyActorState state, ICommand command, CancellationToken cancellationToken)
    {
        if (command is PortfolioCommand<ActivateAndAssignPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> activation)
        {
            var assignmentLock = PortfolioAssignmentLocks.GetOrAdd(state.PolicyId.PortfolioId, static _ => new SemaphoreSlim(1, 1));
            await assignmentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ActivateAndAssignAsync(state, activation, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                assignmentLock.Release();
            }
        }

        var committed = await events.FindCommittedPolicyCommandAsync(state.PolicyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is PortfolioCommand<CreatePortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> create &&
                committed is PortfolioFinancialPolicyCreated prior &&
                !string.Equals(create.Payload.Policy.CanonicalSha256(), prior.Policy.CanonicalSha256(), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the command was committed for a different policy payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        var now = DateTime.UtcNow;
        var currentPortfolio = await events.LoadPortfolioAsync(new PortfolioId(state.PolicyId.PortfolioId), cancellationToken).ConfigureAwait(false);
        var referenced = currentPortfolio.Current?.ActivePolicyId == state.PolicyId.PolicyId;
        PortfolioFinancialPolicyDomainEvent domainEvent = command switch
        {
            PortfolioCommand<CreatePortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> x => state.Aggregate.Create(x.CommandId, x.Payload.IdempotencyKey, x.Payload.Policy, now, Principal),
            PortfolioCommand<AddPortfolioFinancialPolicyVersionPayload, PortfolioFinancialPolicyId> x => state.Aggregate.AddVersion(x.CommandId, x.Payload.ExpectedVersion, x.Payload.Policy, now, Principal),
            PortfolioCommand<RetirePortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> x => state.Aggregate.Retire(x.CommandId, x.Payload.ExpectedRevision, x.Payload.PolicyVersion, x.Payload.Reason, referenced, now, Principal),
            PortfolioCommand<DeleteDraftPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> x => state.Aggregate.DeleteDraft(x.CommandId, x.Payload.ExpectedRevision, x.Payload.Reason, referenced, now, Principal),
            _ => throw new InvalidOperationException($"Unsupported policy command {command.GetType().Name}."),
        };
        await events.AppendPolicyAsync(state.PolicyId, domainEvent, domainEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent])).ConfigureAwait(false);
        if (domainEvent is DraftPortfolioFinancialPolicyDeleted)
            await projections.DeleteDraftPolicyAsync(new(state.PolicyId.PortfolioId, state.PolicyId.PolicyId, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId)), cancellationToken).ConfigureAwait(false);
        else
            await ProjectAllAsync(state.Aggregate, domainEvent, cancellationToken).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async ValueTask<ServiceResult<GuidResult>> ActivateAndAssignAsync(
        PolicyActorState state,
        PortfolioCommand<ActivateAndAssignPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> command,
        CancellationToken cancellationToken)
    {
        var committed = await events.FindCommittedPolicyCommandAsync(state.PolicyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        var portfolioId = new PortfolioId(state.PolicyId.PortfolioId);
        var portfolio = await events.LoadPortfolioAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        if (committed is PortfolioFinancialPolicyActivated activated)
        {
            if (portfolio.Current?.ActivePolicyId != state.PolicyId.PolicyId || portfolio.Current.ActivePolicyVersion != activated.PolicyVersion)
            {
                var replayCandidate = state.Aggregate.Versions.Single(x => x.PolicyVersion == activated.PolicyVersion);
                var assignment = portfolio.AssignFinancialPolicy(command.CommandId, command.Payload.ExpectedPortfolioRevision, replayCandidate, DateTime.UtcNow, Principal);
                await events.AppendPortfolioAsync(portfolioId, assignment, assignment.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                await projections.UpsertPortfolioAsync(
                    PortfolioProjection<PortfolioReadModel>.Create(portfolio.Current!, portfolio.Revision, Math.Max(1, assignment.EventId == 0 ? assignment.Revision : assignment.EventId), assignment.OccurredOnUtc),
                    TomasAI.IFM.Domain.Portfolio.Projection.PortfolioProjectionHandler.StateBucket(portfolioId.Id), cancellationToken).ConfigureAwait(false);
            }
            // A prior attempt may have committed the policy event and failed before
            // projection/assignment. Both operations are idempotent, so replay heals
            // every derived surface as well as the authoritative Portfolio reference.
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([activated])).ConfigureAwait(false);
            await ProjectAllAsync(state.Aggregate, activated, cancellationToken).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (committed is not null)
            return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the command was committed for a different policy operation.");

        var now = DateTime.UtcNow;
        // Validate the Portfolio assignment before committing either stream. The
        // per-Portfolio coordinator prevents distinct policy actors in this host from
        // both passing this expected-revision check.
        if (portfolio.Revision != command.Payload.ExpectedPortfolioRevision)
            throw new InvalidOperationException($"Expected Portfolio revision {command.Payload.ExpectedPortfolioRevision}, actual {portfolio.Revision}.");
        var policyEvent = state.Aggregate.Activate(command.CommandId, command.Payload.ExpectedPolicyRevision, command.Payload.PolicyVersion, now, Principal);
        var candidate = state.Aggregate.Current!;
        var portfolioEvent = portfolio.AssignFinancialPolicy(command.CommandId, command.Payload.ExpectedPortfolioRevision, candidate, now, Principal);
        await events.AppendPolicyAsync(state.PolicyId, policyEvent, policyEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await events.AppendPortfolioAsync(portfolioId, portfolioEvent, portfolioEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([policyEvent])).ConfigureAwait(false);
        await ProjectAllAsync(state.Aggregate, policyEvent, cancellationToken).ConfigureAwait(false);
        await projections.UpsertPortfolioAsync(
            PortfolioProjection<PortfolioReadModel>.Create(portfolio.Current!, portfolio.Revision, Math.Max(1, portfolioEvent.EventId == 0 ? portfolioEvent.Revision : portfolioEvent.EventId), portfolioEvent.OccurredOnUtc),
            TomasAI.IFM.Domain.Portfolio.Projection.PortfolioProjectionHandler.StateBucket(portfolioId.Id), cancellationToken).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async Task ProjectAllAsync(PortfolioFinancialPolicyAggregate aggregate, PortfolioFinancialPolicyDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        foreach (var policy in aggregate.Versions)
            await projections.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(
                policy.DefensiveCopy() with { AggregateRevision = aggregate.Revision }, domainEvent.Revision, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId), domainEvent.OccurredOnUtc), cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, ActorThreadId __, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(PortfolioErrorCodes.ValidationFailed, ex.Message));

    static PortfolioFinancialPolicyId ParseId(ICommand command)
    {
        var parts = command.Subject.EntityId.Split('.');
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var policyId)
            ? new(portfolioId, policyId) : new();
    }

    sealed class PolicyActorState(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyAggregate aggregate) : IActorState<PolicyActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFinancialPolicyId PolicyId { get; } = id;
        public PortfolioFinancialPolicyAggregate Aggregate { get; } = aggregate;
    }
}
