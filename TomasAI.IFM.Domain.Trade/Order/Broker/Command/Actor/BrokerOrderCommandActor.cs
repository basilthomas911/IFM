using TomasAI.IFM.Domain.Trade.Shared;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
/// <summary>Serializes durable mutations for one logical broker order.</summary>
public sealed class BrokerOrderCommandActor(ICommandActorContext<BrokerOrderCommandActor> context) : BaseEventSourceCommandActor<BrokerOrderCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerOrderActorNames.Command;
    private readonly IBrokerOrderCommandContext _services = Typed(context);
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [CreateBrokerOrderCommand.Verb] = message => message.AsCommand<CreateBrokerOrderCommand>()!,
        [RecordBrokerDispatchCommand.Verb] = message => message.AsCommand<RecordBrokerDispatchCommand>()!,
        [RecordBrokerOrderObservationCommand.Verb] = message => message.AsCommand<RecordBrokerOrderObservationCommand>()!,
        [RequestBrokerOrderLimitUpdateCommand.Verb] = message => message.AsCommand<RequestBrokerOrderLimitUpdateCommand>()!,
        [RequestBrokerOrderCancelCommand.Verb] = message => message.AsCommand<RequestBrokerOrderCancelCommand>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(CreateBrokerOrderCommand)] = static command =>
        {
            var typed = (CreateBrokerOrderCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateBrokerOrderId(typed.EntityId, typed.CommandName).ValidateBrokerOrderCommand(typed);
        },
        [typeof(RecordBrokerDispatchCommand)] = static command =>
        {
            var typed = (RecordBrokerDispatchCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateBrokerOrderId(typed.EntityId, typed.CommandName).ValidateBrokerOrderCommand(typed);
        },
        [typeof(RecordBrokerOrderObservationCommand)] = static command =>
        {
            var typed = (RecordBrokerOrderObservationCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateBrokerOrderId(typed.EntityId, typed.CommandName).ValidateBrokerOrderCommand(typed);
        },
        [typeof(RequestBrokerOrderLimitUpdateCommand)] = static command =>
        {
            var typed = (RequestBrokerOrderLimitUpdateCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateBrokerOrderId(typed.EntityId, typed.CommandName).ValidateBrokerOrderCommand(typed);
        },
        [typeof(RequestBrokerOrderCancelCommand)] = static command =>
        {
            var typed = (RequestBrokerOrderCancelCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateBrokerOrderId(typed.EntityId, typed.CommandName).ValidateBrokerOrderCommand(typed);
        }
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, IBrokerOrderCommandContext, BrokerOrderCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, IBrokerOrderCommandContext, BrokerOrderCommandState, ServiceResult<GuidResult>>>
    {
        [typeof(CreateBrokerOrderCommand)] = static (command, owner, state) => ((CreateBrokerOrderCommand)command).Execute(state),
        [typeof(RecordBrokerDispatchCommand)] = static (command, _, state) => ((RecordBrokerDispatchCommand)command).Execute(state),
        [typeof(RecordBrokerOrderObservationCommand)] = static (command, _, state) => ((RecordBrokerOrderObservationCommand)command).Execute(state),
        [typeof(RequestBrokerOrderLimitUpdateCommand)] = static (command, _, state) => ((RequestBrokerOrderLimitUpdateCommand)command).Execute(state),
        [typeof(RequestBrokerOrderCancelCommand)] = static (command, _, state) => ((RequestBrokerOrderCancelCommand)command).Execute(state)
    }.ToFrozenDictionary();
    protected override async ValueTask OnStartup(ICommandActorContext<BrokerOrderCommandActor> context)
    {
        await _services.EventProjector.StartAsync(context).ConfigureAwait(false);
        await _services.ObservationBridge.StartAsync().ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(ICommandActorContext<BrokerOrderCommandActor> context)
    {
        await _services.ObservationBridge.StopAsync().ConfigureAwait(false);
        await _services.EventProjector.StopAsync().ConfigureAwait(false);
    }

    protected override ICommand ParseMessage(ICommandActorContext<BrokerOrderCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<BrokerOrderCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<BrokerOrderCommandActor> context, ActorThreadId threadId, ICommand command) => await (_services.StateRepository.LoadStateAsync(command)).ConfigureAwait(false);
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<BrokerOrderCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => await (_services.StateRepository.SaveStateAsync(context, (BrokerOrderCommandState)state, command)).ConfigureAwait(false);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<BrokerOrderCommandActor> context, IActorState state, ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, _services, (BrokerOrderCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<BrokerOrderCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, ex.Message));
    }

    private static IBrokerOrderCommandContext Typed(ICommandActorContext<BrokerOrderCommandActor> context) => context as IBrokerOrderCommandContext ?? throw new ArgumentException("Typed BrokerOrder command context required.", nameof(context));
}
