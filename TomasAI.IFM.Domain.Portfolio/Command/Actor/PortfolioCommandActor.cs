using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioCommandActor(
    ICommandActorContext<PortfolioCommandActor> context,
    IPortfolioEventStore eventStore,
    IEventProjector<PortfolioCommandActor> projector,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioCommandActor> logger)
    : BaseEventSourceCommandActor<PortfolioCommandActor>(context, logger)
{
    public const string ActorName = CreatePortfolioCommand.Actor;
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IEventProjector<PortfolioCommandActor> _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    readonly IPortfolioOperationalGuard _guard = operationalGuard ?? throw new ArgumentNullException(nameof(operationalGuard));

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioCommandActor> context, CancellationToken cancellationToken) =>
        _projector.StartAsync(context, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioCommandActor> context) => _projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [CreatePortfolioCommand.Verb] = static message => message.AsCommand<CreatePortfolioCommand>()!,
        [AddPortfolioVersionCommand.Verb] = static message => message.AsCommand<AddPortfolioVersionCommand>()!,
        [ChangePortfolioOperatingStateCommand.Verb] = static message => message.AsCommand<ChangePortfolioOperatingStateCommand>()!,
        [AddFundToPortfolioCommand.Verb] = static message => message.AsCommand<AddFundToPortfolioCommand>()!,
        [DelegateFundAllocationCommand.Verb] = static message => message.AsCommand<DelegateFundAllocationCommand>()!,
        [DelegateFundRiskEnvelopeCommand.Verb] = static message => message.AsCommand<DelegateFundRiskEnvelopeCommand>()!,
        [RetirePortfolioCommand.Verb] = static message => message.AsCommand<RetirePortfolioCommand>()!,
        [DeleteDraftPortfolioCommand.Verb] = static message => message.AsCommand<DeleteDraftPortfolioCommand>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreatePortfolioCommand)] = command =>
            {
                var typed = (CreatePortfolioCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateCreate(errors, typed);
                return errors;
            },
            [typeof(AddPortfolioVersionCommand)] = command =>
            {
                var typed = (AddPortfolioVersionCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateVersion(errors, typed);
                return errors;
            },
            [typeof(ChangePortfolioOperatingStateCommand)] = command =>
            {
                var typed = (ChangePortfolioOperatingStateCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateStateChange(errors, typed);
                return errors;
            },
            [typeof(AddFundToPortfolioCommand)] = command =>
            {
                var typed = (AddFundToPortfolioCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateFund(errors, typed);
                return errors;
            },
            [typeof(DelegateFundAllocationCommand)] = command =>
            {
                var typed = (DelegateFundAllocationCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateAllocation(errors, typed);
                return errors;
            },
            [typeof(DelegateFundRiskEnvelopeCommand)] = command =>
            {
                var typed = (DelegateFundRiskEnvelopeCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateRiskEnvelope(errors, typed);
                return errors;
            },
            [typeof(RetirePortfolioCommand)] = command =>
            {
                var typed = (RetirePortfolioCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateRetire(errors, typed);
                return errors;
            },
            [typeof(DeleteDraftPortfolioCommand)] = command =>
            {
                var typed = (DeleteDraftPortfolioCommand)command;
                var errors = new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName);
                ValidateIdentity(errors, typed);
                ValidateDelete(errors, typed);
                return errors;
            },
        };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioCommandActor, ICommand, PortfolioActorState,
        DateTime, string, CancellationToken, ValueTask<IPortfolioDomainEvent>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioCommandActor, ICommand, PortfolioActorState,
            DateTime, string, CancellationToken, ValueTask<IPortfolioDomainEvent>>>
        {
            [typeof(CreatePortfolioCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<IPortfolioDomainEvent>(((CreatePortfolioCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(AddPortfolioVersionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((AddPortfolioVersionCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(ChangePortfolioOperatingStateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((ChangePortfolioOperatingStateCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(AddFundToPortfolioCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((AddFundToPortfolioCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(DelegateFundAllocationCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((DelegateFundAllocationCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(DelegateFundRiskEnvelopeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((DelegateFundRiskEnvelopeCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(RetirePortfolioCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult(((RetirePortfolioCommand)command).Execute(state.Aggregate, now, principal)),
            [typeof(DeleteDraftPortfolioCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                ((DeleteDraftPortfolioCommand)command).ExecuteAsync(state.Aggregate, state.PortfolioId, actor._events, now, principal, cancellationToken),
        };

    protected override ICommand ParseMessage(ICommandActorContext<PortfolioCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command) =>
        new PortfolioActorState(ParseId(command), await _events.LoadPortfolioAsync(ParseId(command)).ConfigureAwait(false));

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioCommandActor> context, IActorState state, ICommand command) =>
        ReceiveCoreAsync((PortfolioActorState)state, command, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<PortfolioCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken) =>
        ReceiveCoreAsync((PortfolioActorState)state, command, cancellationToken);

    async ValueTask<ServiceResult<GuidResult>> ReceiveCoreAsync(PortfolioActorState state, ICommand command, CancellationToken cancellationToken)
    {
        dynamic request = command;
        using var activity = PortfolioTelemetry.StartRequest("command", command.Subject.Verb, request.CorrelationId, command);
        var principal = _guard.Demand(Operation(command.Subject.Verb), request.Access, mutation: true).Principal;
        var committed = await _events.FindCommittedPortfolioCommandAsync(state.PortfolioId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is CreatePortfolioCommand create && committed is PortfolioCreatedEvent prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Portfolio.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Portfolio.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Portfolio payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is CreatePortfolioCommand requestedCreate)
        {
            var priorCreate = await _events.FindPortfolioCreateByIdempotencyKeyAsync(state.PortfolioId, requestedCreate.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            if (priorCreate is not null)
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Portfolio payload.");
        }
        var now = DateTime.UtcNow;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        var domainEvent = await receive(this, command, state, now, principal, cancellationToken).ConfigureAwait(false);
        await _events.AppendPortfolioAsync(
            state.PortfolioId,
            domainEvent,
            domainEvent.Revision - 1,
            Metadata(command, now),
            cancellationToken).ConfigureAwait(false);
        await _projector.DomainEventsProjectionAsync(new DomainEventCollection(new IEvent[] { domainEvent })).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<PortfolioCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(ErrorCode(command, ex), ex.Message));

    static int ErrorCode(ICommand? command, Exception exception) => exception switch
    {
        PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
        PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
        _ => command?.ErrorCode ?? 34000,
    };

    static PortfolioOperation Operation(string verb) => verb switch
    {
        "DelegateFundAllocation" => PortfolioOperation.DelegateAllocation,
        "DelegateFundRiskEnvelope" => PortfolioOperation.DelegateRiskEnvelope,
        _ => PortfolioOperation.AdministerPortfolio,
    };

    static PortfolioId ParseId(ICommand command) =>
        int.TryParse(command.Subject.EntityId, out var id) && id > 0
            ? new PortfolioId(id)
            : throw new ArgumentException("Portfolio command subject identity is invalid.");

    static void ValidateIdentity(
        List<ValidationError> errors,
        ICommand<PortfolioId> command)
    {
        if (command.EntityId is null)
        {
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
    }

    static void ValidateCreate(List<ValidationError> errors, CreatePortfolioCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        if (command.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Portfolio is null"));
            return;
        }
        if (command.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Portfolio.Validate(requireActivePolicy: false), command.CommandName);
        if (command.Portfolio.PortfolioId != command.EntityId.Id)
            errors.Add(new($"{command.CommandName}.Portfolio.PortfolioId does not match EntityId"));
    }

    static void ValidateVersion(List<ValidationError> errors, AddPortfolioVersionCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Portfolio is null"));
            return;
        }
        if (command.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Portfolio.Validate(), command.CommandName);
        if (command.Portfolio.PortfolioId != command.EntityId.Id)
            errors.Add(new($"{command.CommandName}.Portfolio.PortfolioId does not match EntityId"));
    }

    static void ValidateStateChange(List<ValidationError> errors, ChangePortfolioOperatingStateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.State == PortfolioOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.State is required"));
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateFund(List<ValidationError> errors, AddFundToPortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.FundId is null)
            errors.Add(new($"{command.CommandName}.FundId is null"));
        else
        {
            AddErrors(errors, command.FundId.Validate(), command.CommandName);
            if (command.FundId.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.FundId.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateAllocation(List<ValidationError> errors, DelegateFundAllocationCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.Allocation is null)
            errors.Add(new($"{command.CommandName}.Allocation is null"));
        else
        {
            AddErrors(errors, command.Allocation.Validate(), command.CommandName);
            if (command.Allocation.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.Allocation.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateRiskEnvelope(List<ValidationError> errors, DelegateFundRiskEnvelopeCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.Envelope is null)
            errors.Add(new($"{command.CommandName}.Envelope is null"));
        else
        {
            AddErrors(errors, command.Envelope.Validate(), command.CommandName);
            if (command.Envelope.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.Envelope.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateRetire(List<ValidationError> errors, RetirePortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateDelete(List<ValidationError> errors, DeleteDraftPortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    static void ValidateExpectedVersion(List<ValidationError> errors, long expectedVersion, string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.ExpectedVersion cannot be negative"));
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

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        dynamic metadata = command;
        Guid correlationId = metadata.CorrelationId;
        DateTime requestedOnUtc = metadata.RequestedOnUtc;
        return new(correlationId != Guid.Empty ? correlationId : command.CommandId, command.CommandId,
            requestedOnUtc.Kind == DateTimeKind.Utc ? requestedOnUtc : nowUtc);
    }

    sealed class PortfolioActorState(PortfolioId portfolioId, PortfolioAggregate aggregate) : IActorState<PortfolioActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioId PortfolioId { get; } = portfolioId;
        public PortfolioAggregate Aggregate { get; } = aggregate;
    }
}
