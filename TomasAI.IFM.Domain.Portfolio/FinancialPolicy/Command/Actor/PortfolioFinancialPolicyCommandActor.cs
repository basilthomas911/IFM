using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioFinancialPolicyCommandActor(
    ICommandActorContext<PortfolioFinancialPolicyCommandActor> context,
    IPortfolioEventStore events,
    IPortfolioDbWriteContext projections,
    IEventProjector<PortfolioFinancialPolicyCommandActor> projector,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFinancialPolicyCommandActor> logger,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi? referenceQueries = null)
    : BaseEventSourceCommandActor<PortfolioFinancialPolicyCommandActor>(context, logger)
{
    public const string ActorName = CreatePortfolioFinancialPolicyCommand.Actor;
    static readonly ConcurrentDictionary<int, SemaphoreSlim> PortfolioAssignmentLocks = new();

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioFinancialPolicyCommandActor> actorContext, CancellationToken cancellationToken) =>
        projector.StartAsync(actorContext, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioFinancialPolicyCommandActor> actorContext) => projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [CreatePortfolioFinancialPolicyCommand.Verb] = static message => message.AsCommand<CreatePortfolioFinancialPolicyCommand>()!,
        [AddPortfolioFinancialPolicyVersionCommand.Verb] = static message => message.AsCommand<AddPortfolioFinancialPolicyVersionCommand>()!,
        [ActivateAndAssignPortfolioFinancialPolicyCommand.Verb] = static message => message.AsCommand<ActivateAndAssignPortfolioFinancialPolicyCommand>()!,
        [RetirePortfolioFinancialPolicyCommand.Verb] = static message => message.AsCommand<RetirePortfolioFinancialPolicyCommand>()!,
        [DeleteDraftPortfolioFinancialPolicyCommand.Verb] = static message => message.AsCommand<DeleteDraftPortfolioFinancialPolicyCommand>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreatePortfolioFinancialPolicyCommand)] = command =>
            {
                var typed = (CreatePortfolioFinancialPolicyCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateCreate(errors, typed);
                return errors;
            },
            [typeof(AddPortfolioFinancialPolicyVersionCommand)] = command =>
            {
                var typed = (AddPortfolioFinancialPolicyVersionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateVersion(errors, typed);
                return errors;
            },
            [typeof(ActivateAndAssignPortfolioFinancialPolicyCommand)] = command =>
            {
                var typed = (ActivateAndAssignPortfolioFinancialPolicyCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateActivation(errors, typed);
                return errors;
            },
            [typeof(RetirePortfolioFinancialPolicyCommand)] = command =>
            {
                var typed = (RetirePortfolioFinancialPolicyCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateRetire(errors, typed);
                return errors;
            },
            [typeof(DeleteDraftPortfolioFinancialPolicyCommand)] = command =>
            {
                var typed = (DeleteDraftPortfolioFinancialPolicyCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateDelete(errors, typed);
                return errors;
            },
        };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioFinancialPolicyCommandActor, ICommand,
        PolicyActorState, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioFinancialPolicyCommandActor, ICommand,
            PolicyActorState, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(CreatePortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                actor.CreateAsync(state, (CreatePortfolioFinancialPolicyCommand)command, principal, cancellationToken),
            [typeof(AddPortfolioFinancialPolicyVersionCommand)] = static (actor, command, state, principal, cancellationToken) =>
                actor.AddVersionAsync(state, (AddPortfolioFinancialPolicyVersionCommand)command, principal, cancellationToken),
            [typeof(ActivateAndAssignPortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                actor.ActivateWithLockAsync(state, (ActivateAndAssignPortfolioFinancialPolicyCommand)command, principal, cancellationToken),
            [typeof(RetirePortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                actor.RetireAsync(state, (RetirePortfolioFinancialPolicyCommand)command, principal, cancellationToken),
            [typeof(DeleteDraftPortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                actor.DeleteDraftAsync(state, (DeleteDraftPortfolioFinancialPolicyCommand)command, principal, cancellationToken),
        };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioFinancialPolicyCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ValidateMappedCommand(command, _validationMap);
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
        dynamic request = command;
        using var activity = PortfolioTelemetry.StartRequest("command", command.Subject.Verb, request.CorrelationId, command);
        var principal = operationalGuard.Demand(PortfolioOperation.AdministerPortfolio, request.Access, mutation: true).Principal;
        var policy = command switch
        {
            CreatePortfolioFinancialPolicyCommand create => create.Policy,
            AddPortfolioFinancialPolicyVersionCommand change => change.Policy,
            ActivateAndAssignPortfolioFinancialPolicyCommand activate => state.Aggregate.Versions.SingleOrDefault(x => x.PolicyVersion == activate.PolicyVersion),
            _ => null
        };
        if (policy is not null && referenceQueries is not null && await events.FindCommittedPolicyCommandAsync(state.PolicyId, command.CommandId, cancellationToken).ConfigureAwait(false) is null)
            foreach (var limit in policy.TradeFamilyLimits)
            {
                var key = limit.CatalogDeployment ?? throw new ArgumentException("Legacy risk limits are read-only. Create a policy version with explicit ConfigurationDb deployment limits.");
                await TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogPermissionValidation.ValidateDeploymentAsync(referenceQueries, key,
                    command is ActivateAndAssignPortfolioFinancialPolicyCommand && limit.Enabled, cancellationToken);
            }
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return await receive(this, command, state, principal, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask<ServiceResult<GuidResult>> CommitPolicyMutationAsync(
        PolicyActorState state,
        ICommand<PortfolioFinancialPolicyId> command,
        string principal,
        Func<bool, DateTime, IPortfolioFinancialPolicyDomainEvent> createEvent,
        Func<IPortfolioFinancialPolicyDomainEvent, bool>? isIdempotencyConflict,
        CancellationToken cancellationToken)
    {
        var committed = await events.FindCommittedPolicyCommandAsync(state.PolicyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (isIdempotencyConflict?.Invoke(committed) == true)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the command was committed for a different policy payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        var now = DateTime.UtcNow;
        var currentPortfolio = await events.LoadPortfolioAsync(new PortfolioId(state.PolicyId.PortfolioId), cancellationToken).ConfigureAwait(false);
        var referenced = currentPortfolio.Current?.ActivePolicyId == state.PolicyId.PolicyId;
        var domainEvent = createEvent(referenced, now);
        await events.AppendPolicyAsync(state.PolicyId, domainEvent, domainEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent])).ConfigureAwait(false);
        if (domainEvent is DraftPortfolioFinancialPolicyDeletedEvent)
            await projections.DeleteDraftPolicyAsync(new(state.PolicyId.PortfolioId, state.PolicyId.PolicyId, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId)), cancellationToken).ConfigureAwait(false);
        else
            await ProjectAllAsync(state.Aggregate, domainEvent, cancellationToken).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    ValueTask<ServiceResult<GuidResult>> CreateAsync(
        PolicyActorState state,
        CreatePortfolioFinancialPolicyCommand command,
        string principal,
        CancellationToken cancellationToken) =>
        CommitPolicyMutationAsync(
            state,
            command,
            principal,
            (_, now) => state.Aggregate.Create(command.CommandId, command.IdempotencyKey, command.Policy, now, principal),
            committed => committed is PortfolioFinancialPolicyCreatedEvent prior &&
                !string.Equals(command.Policy.CanonicalSha256(), prior.Policy.CanonicalSha256(), StringComparison.Ordinal),
            cancellationToken);

    ValueTask<ServiceResult<GuidResult>> AddVersionAsync(
        PolicyActorState state,
        AddPortfolioFinancialPolicyVersionCommand command,
        string principal,
        CancellationToken cancellationToken) =>
        CommitPolicyMutationAsync(
            state,
            command,
            principal,
            (_, now) => state.Aggregate.AddVersion(command.CommandId, command.ExpectedVersion, command.Policy, now, principal),
            null,
            cancellationToken);

    ValueTask<ServiceResult<GuidResult>> RetireAsync(
        PolicyActorState state,
        RetirePortfolioFinancialPolicyCommand command,
        string principal,
        CancellationToken cancellationToken) =>
        CommitPolicyMutationAsync(
            state,
            command,
            principal,
            (referenced, now) => state.Aggregate.Retire(command.CommandId, command.ExpectedRevision,
                command.PolicyVersion, command.Reason, referenced, now, principal),
            null,
            cancellationToken);

    ValueTask<ServiceResult<GuidResult>> DeleteDraftAsync(
        PolicyActorState state,
        DeleteDraftPortfolioFinancialPolicyCommand command,
        string principal,
        CancellationToken cancellationToken) =>
        CommitPolicyMutationAsync(
            state,
            command,
            principal,
            (referenced, now) => state.Aggregate.DeleteDraft(command.CommandId, command.ExpectedRevision,
                command.Reason, referenced, now, principal),
            null,
            cancellationToken);

    async ValueTask<ServiceResult<GuidResult>> ActivateWithLockAsync(
        PolicyActorState state,
        ActivateAndAssignPortfolioFinancialPolicyCommand command,
        string principal,
        CancellationToken cancellationToken)
    {
        var assignmentLock = PortfolioAssignmentLocks.GetOrAdd(state.PolicyId.PortfolioId, static _ => new SemaphoreSlim(1, 1));
        await assignmentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ActivateAndAssignAsync(state, command, principal, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    async ValueTask<ServiceResult<GuidResult>> ActivateAndAssignAsync(
        PolicyActorState state,
        ActivateAndAssignPortfolioFinancialPolicyCommand command,
        string principal,
        CancellationToken cancellationToken)
    {
        var committed = await events.FindCommittedPolicyCommandAsync(state.PolicyId, command.CommandId, cancellationToken).ConfigureAwait(false);
        var portfolioId = new PortfolioId(state.PolicyId.PortfolioId);
        var portfolio = await events.LoadPortfolioAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        if (committed is PortfolioFinancialPolicyActivatedEvent activated)
        {
            if (portfolio.Current?.ActivePolicyId != state.PolicyId.PolicyId || portfolio.Current.ActivePolicyVersion != activated.PolicyVersion)
            {
                var replayCandidate = state.Aggregate.Versions.Single(x => x.PolicyVersion == activated.PolicyVersion);
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
            await ProjectAllAsync(state.Aggregate, activated, cancellationToken).ConfigureAwait(false);
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
        var policyEvent = state.Aggregate.Activate(command.CommandId, command.ExpectedPolicyRevision, command.PolicyVersion, now, principal);
        var candidate = state.Aggregate.Current!;
        var portfolioEvent = portfolio.AssignFinancialPolicy(command.CommandId, command.ExpectedPortfolioRevision, candidate, now, principal);
        await events.AppendPolicyAsync(state.PolicyId, policyEvent, policyEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await events.AppendPortfolioAsync(portfolioId, portfolioEvent, portfolioEvent.Revision - 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([policyEvent])).ConfigureAwait(false);
        await ProjectAllAsync(state.Aggregate, policyEvent, cancellationToken).ConfigureAwait(false);
        await projections.UpsertPortfolioAsync(
            PortfolioProjection<PortfolioReadModel>.Create(portfolio.Current!, portfolio.Revision, Math.Max(1, portfolioEvent.EventId == 0 ? portfolioEvent.Revision : portfolioEvent.EventId), portfolioEvent.OccurredOnUtc),
            TomasAI.IFM.Domain.Portfolio.Projection.PortfolioProjectionHandler.StateBucket(portfolioId.Id), cancellationToken).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async Task ProjectAllAsync(PortfolioFinancialPolicyAggregate aggregate, IPortfolioFinancialPolicyDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        foreach (var policy in aggregate.Versions)
            await projections.UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel>.Create(
                policy.DefensiveCopy() with { AggregateRevision = aggregate.Revision }, domainEvent.Revision, Math.Max(1, domainEvent.EventId == 0 ? domainEvent.Revision : domainEvent.EventId), domainEvent.OccurredOnUtc), cancellationToken).ConfigureAwait(false);
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioFinancialPolicyCommandActor> _, ActorThreadId __, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(ex switch
        {
            PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
            PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
            _ => PortfolioErrorCodes.ValidationFailed,
        }, ex.Message));

    static PortfolioFinancialPolicyId ParseId(ICommand command)
    {
        var parts = command.Subject.EntityId.Split('.');
        return parts.Length == 2 && int.TryParse(parts[0], out var portfolioId) && int.TryParse(parts[1], out var policyId)
            ? new(portfolioId, policyId) : new();
    }

    static void ValidateIdentity(
        List<ValidationError> errors,
        ICommand<PortfolioFinancialPolicyId> command)
    {
        if (command.EntityId is null)
        {
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
    }

    static void ValidateCreate(List<ValidationError> errors, CreatePortfolioFinancialPolicyCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        ValidatePolicy(errors, command, command.Policy);
    }

    static void ValidateVersion(List<ValidationError> errors, AddPortfolioFinancialPolicyVersionCommand command)
    {
        ValidateExpectedRevision(errors, command.ExpectedVersion, command.CommandName);
        ValidatePolicy(errors, command, command.Policy);
    }

    static void ValidateActivation(List<ValidationError> errors, ActivateAndAssignPortfolioFinancialPolicyCommand command)
    {
        if (command.PolicyVersion <= 0)
            errors.Add(new($"{command.CommandName}.PolicyVersion must be positive"));
        ValidateExpectedRevision(errors, command.ExpectedPolicyRevision, command.CommandName);
        ValidateExpectedRevision(errors, command.ExpectedPortfolioRevision, command.CommandName);
    }

    static void ValidateRetire(List<ValidationError> errors, RetirePortfolioFinancialPolicyCommand command)
    {
        if (command.PolicyVersion <= 0)
            errors.Add(new($"{command.CommandName}.PolicyVersion must be positive"));
        ValidateExpectedRevision(errors, command.ExpectedRevision, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateDelete(List<ValidationError> errors, DeleteDraftPortfolioFinancialPolicyCommand command)
    {
        ValidateExpectedRevision(errors, command.ExpectedRevision, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidatePolicy(
        List<ValidationError> errors,
        ICommand<PortfolioFinancialPolicyId> command,
        PortfolioFinancialPolicyReadModel? policy)
    {
        if (policy is null)
        {
            errors.Add(new($"{command.CommandName}.Policy is null"));
            return;
        }
        if (policy.TradeFamilyLimits is null || policy.TradeFamilyLimits.Any(static family => family is null))
            errors.Add(new($"{command.CommandName}.Policy.TradeFamilyLimits contains null values"));
        else
            AddErrors(errors, policy.Validate(), command.CommandName);
        if (command.EntityId is null)
            return;
        if (policy.PortfolioId != command.EntityId.PortfolioId || policy.PolicyId != command.EntityId.PolicyId)
            errors.Add(new($"{command.CommandName}.Policy identity does not match EntityId"));
    }

    static void ValidateExpectedRevision(List<ValidationError> errors, long expectedRevision, string commandName)
    {
        if (expectedRevision < 0)
            errors.Add(new($"{commandName}.expected revision cannot be negative"));
    }

    static void ValidateReason(List<ValidationError> errors, string? reason, string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Reason is required"));
    }

    static void AddErrors(List<ValidationError> errors, IEnumerable<string> messages, string commandName)
    {
        foreach (var message in messages)
            errors.Add(new($"{commandName}.{message}"));
    }

    sealed class PolicyActorState(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyAggregate aggregate) : IActorState<PolicyActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFinancialPolicyId PolicyId { get; } = id;
        public PortfolioFinancialPolicyAggregate Aggregate { get; } = aggregate;
    }
}
