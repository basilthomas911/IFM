using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;

/// <summary>Audited command transport; the reference CAS catalog is the durable authority and receipt.</summary>
public sealed class TradeStrategyFamilyCommandActor(ICommandActorContext<TradeStrategyFamilyCommandActor> context,
    TradeStrategyFamilyCreationService service, ILogger<TradeStrategyFamilyCommandActor> logger) : BaseEventSourceCommandActor<TradeStrategyFamilyCommandActor>(context, logger)
{
    public const string ActorName = CreateTradeStrategyFamilyCommand.Actor;
    protected override ICommand ParseMessage(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, new Dictionary<string, Func<IActorMessage, ICommand>>
        { [CreateTradeStrategyFamilyCommand.Verb] = msg => msg.AsCommand<CreateTradeStrategyFamilyCommand>()! });
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command) =>
        ReceiveAsync(context, state, command, CancellationToken.None);
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken)
    {
        var create = (CreateTradeStrategyFamilyCommand)command;
        if (create.CommandId == Guid.Empty || create.Request is null || create.CommandId != create.Request.OperationId)
            throw new ArgumentException("CommandId must equal the nonempty creation OperationId.");
        await service.CreateAsync(create.Request, create.OriginatedBy, cancellationToken).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(create.CommandId));
    }
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(CreateTradeStrategyFamilyCommand.ErrorId, ex.Message));
}

public sealed class TradeStrategyFamilyCommandContext(IActorSupervisor supervisor, ILogger<TradeStrategyFamilyCommandActor> logger)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, TradeStrategyFamilyCommandActor.ActorName)), ICommandActorContext<TradeStrategyFamilyCommandActor>
{
    public ILogger<TradeStrategyFamilyCommandActor> Logger { get; } = logger;
}
