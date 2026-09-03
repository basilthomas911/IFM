using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
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
using AddFundToPortfolioCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.AddFundPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using AddPortfolioVersionCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.AddPortfolioVersionPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using ChangePortfolioOperatingStateCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.ChangePortfolioStatePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using CreatePortfolioCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.CreatePortfolioPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using DelegateFundAllocationCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.DelegateAllocationPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using DelegateFundRiskEnvelopeCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.DelegateRiskEnvelopePayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using DeleteDraftPortfolioCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.DeleteDraftPortfolioPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;
using RetirePortfolioCommand = TomasAI.IFM.Domain.Portfolio.Shared.Commands.PortfolioCommand<TomasAI.IFM.Domain.Portfolio.Shared.Commands.RetirePortfolioPayload, TomasAI.IFM.Domain.Portfolio.Shared.Identities.PortfolioId>;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioCommandActor(
    ICommandActorContext<PortfolioCommandActor> context,
    IPortfolioEventStore eventStore,
    IEventProjector<PortfolioCommandActor> projector,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioCommandActor> logger)
    : BaseEventSourceCommandActor<PortfolioCommandActor>(context, logger)
{
    public const string ActorName = PortfolioCommandSubjects.PortfolioActor;
    readonly IPortfolioEventStore _events = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    readonly IEventProjector<PortfolioCommandActor> _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    readonly IPortfolioOperationalGuard _guard = operationalGuard ?? throw new ArgumentNullException(nameof(operationalGuard));

    protected override ValueTask OnStartup(ICommandActorContext<PortfolioCommandActor> context, CancellationToken cancellationToken) =>
        _projector.StartAsync(context, cancellationToken);

    protected override ValueTask OnShutdown(ICommandActorContext<PortfolioCommandActor> context) => _projector.StopAsync();

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [PortfolioCommandVerbs.CreatePortfolio] = static message => message.AsCommand<CreatePortfolioCommand>()!,
        [PortfolioCommandVerbs.AddPortfolioVersion] = static message => message.AsCommand<AddPortfolioVersionCommand>()!,
        [PortfolioCommandVerbs.ChangePortfolioOperatingState] = static message => message.AsCommand<ChangePortfolioOperatingStateCommand>()!,
        [PortfolioCommandVerbs.AddFundToPortfolio] = static message => message.AsCommand<AddFundToPortfolioCommand>()!,
        [PortfolioCommandVerbs.DelegateFundAllocation] = static message => message.AsCommand<DelegateFundAllocationCommand>()!,
        [PortfolioCommandVerbs.DelegateFundRiskEnvelope] = static message => message.AsCommand<DelegateFundRiskEnvelopeCommand>()!,
        [PortfolioCommandVerbs.RetirePortfolio] = static message => message.AsCommand<RetirePortfolioCommand>()!,
        [PortfolioCommandVerbs.DeleteDraftPortfolio] = static message => message.AsCommand<DeleteDraftPortfolioCommand>()!,
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
        DateTime, string, CancellationToken, ValueTask<PortfolioDomainEvent>>> _receiveMap =
        new Dictionary<Type, Func<PortfolioCommandActor, ICommand, PortfolioActorState,
            DateTime, string, CancellationToken, ValueTask<PortfolioDomainEvent>>>
        {
            [typeof(CreatePortfolioCommand)] = static (_, command, state, now, principal, _) =>
            {
                var typed = (CreatePortfolioCommand)command;
                return ValueTask.FromResult<PortfolioDomainEvent>(
                    ((PortfolioCreated)state.Aggregate.Create(typed.CommandId, typed.Payload.Portfolio, now, principal)) with
                    { IdempotencyKey = typed.Payload.IdempotencyKey });
            },
            [typeof(AddPortfolioVersionCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.AddVersion(
                    command.CommandId, ((AddPortfolioVersionCommand)command).Payload.ExpectedVersion,
                    ((AddPortfolioVersionCommand)command).Payload.Portfolio, now, principal)),
            [typeof(ChangePortfolioOperatingStateCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.ChangeState(
                    command.CommandId, ((ChangePortfolioOperatingStateCommand)command).Payload.ExpectedVersion,
                    ((ChangePortfolioOperatingStateCommand)command).Payload.State,
                    ((ChangePortfolioOperatingStateCommand)command).Payload.Reason, now, principal)),
            [typeof(AddFundToPortfolioCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.AddFund(
                    command.CommandId, ((AddFundToPortfolioCommand)command).Payload.ExpectedPortfolioVersion,
                    ((AddFundToPortfolioCommand)command).Payload.FundId, now, principal)),
            [typeof(DelegateFundAllocationCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.DelegateAllocation(
                    command.CommandId, ((DelegateFundAllocationCommand)command).Payload.ExpectedPortfolioVersion,
                    ((DelegateFundAllocationCommand)command).Payload.Allocation, now, principal)),
            [typeof(DelegateFundRiskEnvelopeCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.DelegateRiskEnvelope(
                    command.CommandId, ((DelegateFundRiskEnvelopeCommand)command).Payload.ExpectedPortfolioVersion,
                    ((DelegateFundRiskEnvelopeCommand)command).Payload.Envelope, now, principal)),
            [typeof(RetirePortfolioCommand)] = static (_, command, state, now, principal, _) =>
                ValueTask.FromResult<PortfolioDomainEvent>(state.Aggregate.Retire(
                    command.CommandId, ((RetirePortfolioCommand)command).Payload.ExpectedVersion,
                    ((RetirePortfolioCommand)command).Payload.Reason, now, principal)),
            [typeof(DeleteDraftPortfolioCommand)] = static (actor, command, state, now, principal, cancellationToken) =>
                actor.DeleteDraftAsync(state, (DeleteDraftPortfolioCommand)command, now, principal, cancellationToken),
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
        var request = (IPortfolioRequestMetadata)command;
        using var activity = PortfolioTelemetry.StartRequest("command", command.Subject.Verb, request);
        var principal = _guard.Demand(Operation(command.Subject.Verb), request, mutation: true).Principal;
        var committed = await _events.FindCommittedPortfolioCommandAsync(state.PortfolioId, command.CommandId, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            if (command is PortfolioCommand<CreatePortfolioPayload, PortfolioId> create && committed is PortfolioCreated prior &&
                !string.Equals(PortfolioCanonicalHash.Compute(create.Payload.Portfolio.DefensiveCopy()), PortfolioCanonicalHash.Compute(prior.Portfolio.DefensiveCopy()), StringComparison.Ordinal))
                return new ServiceFailed<GuidResult>(PortfolioErrorCodes.IdempotencyConflict, "IdempotencyKeyConflict: the key was already committed for a different Portfolio payload.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
        if (command is PortfolioCommand<CreatePortfolioPayload, PortfolioId> requestedCreate)
        {
            var priorCreate = await _events.FindPortfolioCreateByIdempotencyKeyAsync(state.PortfolioId, requestedCreate.Payload.IdempotencyKey, cancellationToken).ConfigureAwait(false);
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
        await _projector.DomainEventsProjectionAsync(new DomainEventCollection([domainEvent])).ConfigureAwait(false);
        PortfolioTelemetry.CommandOutcomes.Add(1,
            new KeyValuePair<string, object?>("portfolio.operation", command.Subject.Verb),
            new KeyValuePair<string, object?>("portfolio.outcome", "committed"));
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }

    async ValueTask<PortfolioDomainEvent> DeleteDraftAsync(
        PortfolioActorState state,
        DeleteDraftPortfolioCommand command,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {
        foreach (var fundId in state.Aggregate.FundIds)
        {
            var fund = await _events.LoadFundAsync(new PortfolioFundId(state.PortfolioId.Id, fundId), cancellationToken).ConfigureAwait(false);
            if (fund.Orders.Count != 0)
                throw new InvalidOperationException("A Draft Portfolio with composition history cannot be deleted.");
        }
        return state.Aggregate.DeleteDraft(command.CommandId, command.Payload.ExpectedVersion, command.Payload.Reason, now, principal);
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

    static void ValidateIdentity<TPayload>(
        List<ValidationError> errors,
        PortfolioCommand<TPayload, PortfolioId> command)
    {
        if (command.EntityId is null)
        {
            if (command.Payload is null)
                errors.Add(new($"{command.CommandName}.Payload is null"));
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
        if (command.Payload is null)
            errors.Add(new($"{command.CommandName}.Payload is null"));
    }

    static void ValidateCreate(List<ValidationError> errors, CreatePortfolioCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        if (command.Payload.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Payload.IdempotencyKey is empty"));
        if (command.Payload.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Portfolio is null"));
            return;
        }
        if (command.Payload.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Payload.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Payload.Portfolio.Validate(requireActivePolicy: false), command.CommandName);
        if (command.Payload.Portfolio.PortfolioId != command.EntityId.Id)
            errors.Add(new($"{command.CommandName}.Payload.Portfolio.PortfolioId does not match EntityId"));
    }

    static void ValidateVersion(List<ValidationError> errors, AddPortfolioVersionCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        if (command.Payload.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Payload.Portfolio is null"));
            return;
        }
        if (command.Payload.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Payload.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Payload.Portfolio.Validate(), command.CommandName);
        if (command.Payload.Portfolio.PortfolioId != command.EntityId.Id)
            errors.Add(new($"{command.CommandName}.Payload.Portfolio.PortfolioId does not match EntityId"));
    }

    static void ValidateStateChange(List<ValidationError> errors, ChangePortfolioOperatingStateCommand command)
    {
        if (command.Payload is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        if (command.Payload.State == PortfolioOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.Payload.State is required"));
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateFund(List<ValidationError> errors, AddFundToPortfolioCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedPortfolioVersion, command.CommandName);
        if (command.Payload.FundId is null)
            errors.Add(new($"{command.CommandName}.Payload.FundId is null"));
        else
        {
            AddErrors(errors, command.Payload.FundId.Validate(), command.CommandName);
            if (command.Payload.FundId.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.Payload.FundId.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateAllocation(List<ValidationError> errors, DelegateFundAllocationCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedPortfolioVersion, command.CommandName);
        if (command.Payload.Allocation is null)
            errors.Add(new($"{command.CommandName}.Payload.Allocation is null"));
        else
        {
            AddErrors(errors, command.Payload.Allocation.Validate(), command.CommandName);
            if (command.Payload.Allocation.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.Payload.Allocation.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateRiskEnvelope(List<ValidationError> errors, DelegateFundRiskEnvelopeCommand command)
    {
        if (command.Payload is null || command.EntityId is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedPortfolioVersion, command.CommandName);
        if (command.Payload.Envelope is null)
            errors.Add(new($"{command.CommandName}.Payload.Envelope is null"));
        else
        {
            AddErrors(errors, command.Payload.Envelope.Validate(), command.CommandName);
            if (command.Payload.Envelope.PortfolioId != command.EntityId.Id)
                errors.Add(new($"{command.CommandName}.Payload.Envelope.PortfolioId does not match EntityId"));
        }
    }

    static void ValidateRetire(List<ValidationError> errors, RetirePortfolioCommand command)
    {
        if (command.Payload is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateDelete(List<ValidationError> errors, DeleteDraftPortfolioCommand command)
    {
        if (command.Payload is null) return;
        ValidateExpectedVersion(errors, command.Payload.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Payload.Reason, command.CommandName);
    }

    static void ValidateExpectedVersion(List<ValidationError> errors, long expectedVersion, string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.Payload.ExpectedVersion cannot be negative"));
    }

    static void ValidateReason(List<ValidationError> errors, string? reason, string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Payload.Reason is required"));
    }

    static void AddErrors(List<ValidationError> errors, IEnumerable<string> messages, string commandName)
    {
        foreach (var message in messages)
            errors.Add(new($"{commandName}.{message}"));
    }

    static PortfolioEventMetadata Metadata(ICommand command, DateTime nowUtc)
    {
        var metadata = command as IPortfolioRequestMetadata;
        return new(
            metadata is not null && metadata.CorrelationId != Guid.Empty ? metadata.CorrelationId : command.CommandId,
            command.CommandId,
            metadata is { RequestedOnUtc.Kind: DateTimeKind.Utc } ? metadata.RequestedOnUtc : nowUtc);
    }

    sealed class PortfolioActorState(PortfolioId portfolioId, PortfolioAggregate aggregate) : IActorState<PortfolioActorState>
    {
        public ActorThreadId Id { get; set; }
        public PortfolioId PortfolioId { get; } = portfolioId;
        public PortfolioAggregate Aggregate { get; } = aggregate;
    }
}
