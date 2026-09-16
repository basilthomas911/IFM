using TomasAI.IFM.Domain.Trade.Shared;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Command;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Actor;

public sealed class TradeOrderCommandActor(ICommandActorContext<TradeOrderCommandActor> context)
    : BaseEventSourceCommandActor<TradeOrderCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = TradeOrderActorNames.Command;
    readonly ITradeOrderCommandContext services = Typed(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [CreateTradeOrderCommand.Verb] = m => m.AsCommand<CreateTradeOrderCommand>()!,
            [AmendTradeOrderCommand.Verb] = m => m.AsCommand<AmendTradeOrderCommand>()!,
            [ApproveTradeOrderCommand.Verb] = m => m.AsCommand<ApproveTradeOrderCommand>()!,
            [ReadyTradeOrderCommand.Verb] = m => m.AsCommand<ReadyTradeOrderCommand>()!,
            [BindTradeOrderExecutionCommand.Verb] = m => m.AsCommand<BindTradeOrderExecutionCommand>()!,
            [ReleaseTradeOrderExecutionCommand.Verb] = m => m.AsCommand<ReleaseTradeOrderExecutionCommand>()!,
            [CompleteTradeOrderCommand.Verb] = m => m.AsCommand<CompleteTradeOrderCommand>()!,
            [CancelTradeOrderCommand.Verb] = m => m.AsCommand<CancelTradeOrderCommand>()!,
            [ExpireTradeOrderCommand.Verb] = m => m.AsCommand<ExpireTradeOrderCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
            {
                [typeof(CreateTradeOrderCommand)] = Validate,
                [typeof(AmendTradeOrderCommand)] = Validate,
                [typeof(ApproveTradeOrderCommand)] = Validate,
                [typeof(ReadyTradeOrderCommand)] = Validate,
                [typeof(BindTradeOrderExecutionCommand)] = Validate,
                [typeof(ReleaseTradeOrderExecutionCommand)] = Validate,
                [typeof(CompleteTradeOrderCommand)] = Validate,
                [typeof(CancelTradeOrderCommand)] = Validate,
                [typeof(ExpireTradeOrderCommand)] = Validate
            }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, TradeOrderCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, TradeOrderCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(CreateTradeOrderCommand)] = static (c,s) => ((CreateTradeOrderCommand)c).Execute(s),
            [typeof(AmendTradeOrderCommand)] = static (c,s) => ((AmendTradeOrderCommand)c).Execute(s),
            [typeof(ApproveTradeOrderCommand)] = static (c,s) => ((ApproveTradeOrderCommand)c).Execute(s),
            [typeof(ReadyTradeOrderCommand)] = static (c,s) => ((ReadyTradeOrderCommand)c).Execute(s),
            [typeof(BindTradeOrderExecutionCommand)] = static (c,s) => ((BindTradeOrderExecutionCommand)c).Execute(s),
            [typeof(ReleaseTradeOrderExecutionCommand)] = static (c,s) => ((ReleaseTradeOrderExecutionCommand)c).Execute(s),
            [typeof(CompleteTradeOrderCommand)] = static (c,s) => ((CompleteTradeOrderCommand)c).Execute(s),
            [typeof(CancelTradeOrderCommand)] = static (c,s) => ((CancelTradeOrderCommand)c).Execute(s),
            [typeof(ExpireTradeOrderCommand)] = static (c,s) => ((ExpireTradeOrderCommand)c).Execute(s)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(ICommandActorContext<TradeOrderCommandActor> c) => services.EventProjector.StartAsync(c);
    protected override ValueTask OnShutdown(ICommandActorContext<TradeOrderCommandActor> c) => services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<TradeOrderCommandActor> c, IActorMessage m) => ParseMappedCommand(c,m,_parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<TradeOrderCommandActor> c, ActorThreadId id, ICommand command)
    { ValidateMappedCommand(command,_validationMap); return ValueTask.CompletedTask; }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<TradeOrderCommandActor> c, ActorThreadId id, ICommand command) => await services.StateRepository.LoadStateAsync(command);
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<TradeOrderCommandActor> c, ActorThreadId id, IActorState state, ICommand command) => await services.StateRepository.SaveStateAsync(c,(TradeOrderCommandState)state,command);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeOrderCommandActor> c, IActorState state, ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command,_receiveMap)(command,(TradeOrderCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TradeOrderCommandActor> c, ActorThreadId id, ICommand command, Exception ex) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode,ex.Message));

    static List<ValidationError> Validate(ICommand command) => new List<ValidationError>()
        .ValidateCommandId(command.CommandId,command.CommandName)
        .CaptureCommandValidation(() =>
        {
            if (command is not ICommand<TomasAI.IFM.Domain.Trade.Shared.TradeOrderId> typed || !typed.EntityId.IsValid ||
                !string.Equals(command.Subject.EntityId,typed.EntityId.Format(),StringComparison.Ordinal))
                throw new ArgumentException("Valid Trade Order identity and matching subject are required.");
        });
    static ITradeOrderCommandContext Typed(ICommandActorContext<TradeOrderCommandActor> c) => c as ITradeOrderCommandContext ?? throw new ArgumentException("Typed Trade Order command context required.");
}
