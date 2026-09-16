using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;

/// <summary>Serializes durable mutations for one logical broker order.</summary>
public sealed class BrokerOrderCommandActor(ICommandActorContext<BrokerOrderCommandActor> context)
    : BaseEventSourceCommandActor<BrokerOrderCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerOrderActorNames.Command;
    private readonly IBrokerOrderCommandContext _services = Typed(context);
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [CreateBrokerOrderCommand.Verb] = message => message.AsCommand<CreateBrokerOrderCommand>()!,
            [RecordBrokerDispatchCommand.Verb] = message => message.AsCommand<RecordBrokerDispatchCommand>()!,
            [RecordBrokerOrderObservationCommand.Verb] = message => message.AsCommand<RecordBrokerOrderObservationCommand>()!,
            [RequestBrokerOrderLimitUpdateCommand.Verb] = message => message.AsCommand<RequestBrokerOrderLimitUpdateCommand>()!,
            [RequestBrokerOrderCancelCommand.Verb] = message => message.AsCommand<RequestBrokerOrderCancelCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreateBrokerOrderCommand)] = BrokerOrderCommandValidation.Validate,
            [typeof(RecordBrokerDispatchCommand)] = BrokerOrderCommandValidation.Validate,
            [typeof(RecordBrokerOrderObservationCommand)] = BrokerOrderCommandValidation.Validate,
            [typeof(RequestBrokerOrderLimitUpdateCommand)] = BrokerOrderCommandValidation.Validate,
            [typeof(RequestBrokerOrderCancelCommand)] = BrokerOrderCommandValidation.Validate
        }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, IBrokerOrderCommandContext,
        BrokerOrderCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, IBrokerOrderCommandContext,
            BrokerOrderCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(CreateBrokerOrderCommand)] = static (command, owner, state) =>
                ((CreateBrokerOrderCommand)command).Execute(state, owner.BrokerAccountStore),
            [typeof(RecordBrokerDispatchCommand)] = static (command, _, state) => ((RecordBrokerDispatchCommand)command).Execute(state),
            [typeof(RecordBrokerOrderObservationCommand)] = static (command, _, state) => ((RecordBrokerOrderObservationCommand)command).Execute(state),
            [typeof(RequestBrokerOrderLimitUpdateCommand)] = static (command, _, state) => ((RequestBrokerOrderLimitUpdateCommand)command).Execute(state),
            [typeof(RequestBrokerOrderCancelCommand)] = static (command, _, state) => ((RequestBrokerOrderCancelCommand)command).Execute(state)
        }.ToFrozenDictionary();

    protected override async ValueTask OnStartup(ICommandActorContext<BrokerOrderCommandActor> actorContext)
    {
        await _services.EventProjector.StartAsync(actorContext).ConfigureAwait(false);
        await _services.ObservationBridge.StartAsync().ConfigureAwait(false);
    }
    protected override async ValueTask OnShutdown(ICommandActorContext<BrokerOrderCommandActor> actorContext)
    {
        await _services.ObservationBridge.StopAsync().ConfigureAwait(false);
        await _services.EventProjector.StopAsync().ConfigureAwait(false);
    }
    protected override ICommand ParseMessage(ICommandActorContext<BrokerOrderCommandActor> actorContext, IActorMessage message) => ParseMappedCommand(actorContext, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<BrokerOrderCommandActor> actorContext, ActorThreadId id, ICommand command)
    { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<BrokerOrderCommandActor> actorContext, ActorThreadId id, ICommand command) => await _services.StateRepository.LoadStateAsync(command);
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<BrokerOrderCommandActor> actorContext, ActorThreadId id, IActorState state, ICommand command) => await _services.StateRepository.SaveStateAsync(actorContext, (BrokerOrderCommandState)state, command);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<BrokerOrderCommandActor> actorContext, IActorState state, ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, _services, (BrokerOrderCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<BrokerOrderCommandActor> actorContext, ActorThreadId id, ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));
    private static IBrokerOrderCommandContext Typed(ICommandActorContext<BrokerOrderCommandActor> context) => context as IBrokerOrderCommandContext ?? throw new ArgumentException("Typed BrokerOrder command context required.");
}
