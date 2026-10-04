using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Validation;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;
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

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Actor;

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
    IPortfolioEventStore GetEventStore() => events;
    IPortfolioDbWriteContext GetProjections() => projections;
    IEventProjector<PortfolioFinancialPolicyCommandActor> GetProjector() => projector;

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
            [typeof(CreatePortfolioFinancialPolicyCommand)] = static command =>
            {
                var typed = (CreatePortfolioFinancialPolicyCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFinancialPolicyCommand(typed);
            },
            [typeof(AddPortfolioFinancialPolicyVersionCommand)] = static command =>
            {
                var typed = (AddPortfolioFinancialPolicyVersionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFinancialPolicyCommand(typed);
            },
            [typeof(ActivateAndAssignPortfolioFinancialPolicyCommand)] = static command =>
            {
                var typed = (ActivateAndAssignPortfolioFinancialPolicyCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFinancialPolicyCommand(typed);
            },
            [typeof(RetirePortfolioFinancialPolicyCommand)] = static command =>
            {
                var typed = (RetirePortfolioFinancialPolicyCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFinancialPolicyCommand(typed);
            },
            [typeof(DeleteDraftPortfolioFinancialPolicyCommand)] = static command =>
            {
                var typed = (DeleteDraftPortfolioFinancialPolicyCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateFinancialPolicyCommand(typed);
            },
        };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioFinancialPolicyCommandActor, ICommand,
        PolicyActorState, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioFinancialPolicyCommandActor, ICommand,
            PolicyActorState, string, CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(CreatePortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                ((CreatePortfolioFinancialPolicyCommand)command).ExecuteAsync(state.Aggregate, principal,
                    (createEvent, conflict) => actor.CommitPolicyMutationAsync(state, (CreatePortfolioFinancialPolicyCommand)command, principal, createEvent, conflict, cancellationToken)),
            [typeof(AddPortfolioFinancialPolicyVersionCommand)] = static (actor, command, state, principal, cancellationToken) =>
                ((AddPortfolioFinancialPolicyVersionCommand)command).ExecuteAsync(state.Aggregate, principal,
                    (createEvent, conflict) => actor.CommitPolicyMutationAsync(state, (AddPortfolioFinancialPolicyVersionCommand)command, principal, createEvent, conflict, cancellationToken)),
            [typeof(ActivateAndAssignPortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                ((ActivateAndAssignPortfolioFinancialPolicyCommand)command).ExecuteAsync(state.Aggregate, state.PolicyId,
                    actor.GetEventStore(), actor.GetProjections(), actor.GetProjector(), principal, cancellationToken),
            [typeof(RetirePortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                ((RetirePortfolioFinancialPolicyCommand)command).ExecuteAsync(state.Aggregate, principal,
                    (createEvent, conflict) => actor.CommitPolicyMutationAsync(state, (RetirePortfolioFinancialPolicyCommand)command, principal, createEvent, conflict, cancellationToken)),
            [typeof(DeleteDraftPortfolioFinancialPolicyCommand)] = static (actor, command, state, principal, cancellationToken) =>
                ((DeleteDraftPortfolioFinancialPolicyCommand)command).ExecuteAsync(state.Aggregate, principal,
                    (createEvent, conflict) => actor.CommitPolicyMutationAsync(state, (DeleteDraftPortfolioFinancialPolicyCommand)command, principal, createEvent, conflict, cancellationToken)),
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



    sealed class PolicyActorState(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyAggregate aggregate) : IActorState<PolicyActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioFinancialPolicyId PolicyId { get; } = id;
        public PortfolioFinancialPolicyAggregate Aggregate { get; } = aggregate;
    }
}
