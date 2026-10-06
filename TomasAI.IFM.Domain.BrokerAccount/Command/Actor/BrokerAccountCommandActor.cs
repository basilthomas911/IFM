using System.Collections.Frozen;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Command.Validation;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Actor;

/// <summary>Serializes durable account evidence, holds, and qualification decisions.</summary>
public sealed class BrokerAccountCommandActor(ICommandActorContext<BrokerAccountCommandActor> context)
    : BaseEventSourceCommandActor<BrokerAccountCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerAccountActorNames.Command;
    private readonly IBrokerAccountCommandContext _services = Typed(context);

    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [RecordBrokerAccountSnapshotCommand.Verb] = message => message.AsCommand<RecordBrokerAccountSnapshotCommand>()!,
            [SubmitAccountQualificationEvidenceCommand.Verb] = message => message.AsCommand<SubmitAccountQualificationEvidenceCommand>()!,
            [AcceptAccountQualificationCommand.Verb] = message => message.AsCommand<AcceptAccountQualificationCommand>()!,
            [RevokeAccountQualificationCommand.Verb] = message => message.AsCommand<RevokeAccountQualificationCommand>()!,
            [SetManualTradingHoldCommand.Verb] = message => message.AsCommand<SetManualTradingHoldCommand>()!,
            [ReleaseManualTradingHoldCommand.Verb] = message => message.AsCommand<ReleaseManualTradingHoldCommand>()!,
            [RequestBrokerAccountResynchronizationCommand.Verb] = message => message.AsCommand<RequestBrokerAccountResynchronizationCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RecordBrokerAccountSnapshotCommand)] = static command =>
            {
                var typed = (RecordBrokerAccountSnapshotCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(SubmitAccountQualificationEvidenceCommand)] = static command =>
            {
                var typed = (SubmitAccountQualificationEvidenceCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(AcceptAccountQualificationCommand)] = static command =>
            {
                var typed = (AcceptAccountQualificationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(RevokeAccountQualificationCommand)] = static command =>
            {
                var typed = (RevokeAccountQualificationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(SetManualTradingHoldCommand)] = static command =>
            {
                var typed = (SetManualTradingHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(ReleaseManualTradingHoldCommand)] = static command =>
            {
                var typed = (ReleaseManualTradingHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            },
            [typeof(RequestBrokerAccountResynchronizationCommand)] = static command =>
            {
                var typed = (RequestBrokerAccountResynchronizationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateBrokerAccountId(typed.EntityId, typed.CommandName)
                    .ValidateBrokerAccountCommand(typed);
            }
        }.ToFrozenDictionary();

    private static readonly IReadOnlyDictionary<Type, Func<ICommand, IBrokerAccountCommandContext,
        BrokerAccountCommandState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, IBrokerAccountCommandContext, BrokerAccountCommandState,
            ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(RecordBrokerAccountSnapshotCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((RecordBrokerAccountSnapshotCommand)command).Execute(state)),
            [typeof(SubmitAccountQualificationEvidenceCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((SubmitAccountQualificationEvidenceCommand)command).Execute(state)),
            [typeof(AcceptAccountQualificationCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((AcceptAccountQualificationCommand)command).Execute(state)),
            [typeof(RevokeAccountQualificationCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((RevokeAccountQualificationCommand)command).Execute(state)),
            [typeof(SetManualTradingHoldCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((SetManualTradingHoldCommand)command).Execute(state)),
            [typeof(ReleaseManualTradingHoldCommand)] = static (command, _, state) =>
                ValueTask.FromResult(((ReleaseManualTradingHoldCommand)command).Execute(state)),
            [typeof(RequestBrokerAccountResynchronizationCommand)] = static (command, owner, state) =>
                ((RequestBrokerAccountResynchronizationCommand)command).ExecuteAsync(owner, state)
        }.ToFrozenDictionary();

    protected override async ValueTask OnStartup(ICommandActorContext<BrokerAccountCommandActor> context)
    {
        await _services.EventProjector.StartAsync(context).ConfigureAwait(false);
        await _services.ObservationBridge.StartAsync().ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(ICommandActorContext<BrokerAccountCommandActor> context)
    {
        await _services.ObservationBridge.StopAsync().ConfigureAwait(false);
        await _services.EventProjector.StopAsync().ConfigureAwait(false);
    }

    protected override ICommand ParseMessage(ICommandActorContext<BrokerAccountCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<BrokerAccountCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<BrokerAccountCommandActor> context, ActorThreadId threadId, ICommand command) =>
        await _services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<BrokerAccountCommandActor> context,
        ActorThreadId threadId, IActorState state, ICommand command) =>
        await _services.StateRepository.SaveStateAsync(context, (BrokerAccountCommandState)state, command)
            .ConfigureAwait(false);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<BrokerAccountCommandActor> context, IActorState state, ICommand command) =>
        ResolveMappedCommandHandler(command, _receiveMap)(
            command, _services, (BrokerAccountCommandState)state);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<BrokerAccountCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode,
                $"BA.EXCEPTION;ExceptionType={exception.GetType().Name};{exception.Message}"));

    private static IBrokerAccountCommandContext Typed(ICommandActorContext<BrokerAccountCommandActor> context) =>
        context as IBrokerAccountCommandContext ??
        throw new ArgumentException("Typed BrokerAccount command context required.");
}
