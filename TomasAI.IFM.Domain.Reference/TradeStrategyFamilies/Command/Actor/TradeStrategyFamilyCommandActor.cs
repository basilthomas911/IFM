using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;

/// <summary>Catalog command transport; ConfigurationDb is the active authority. Legacy verbs return a migration error.</summary>
public sealed class TradeStrategyFamilyCommandActor(ICommandActorContext<TradeStrategyFamilyCommandActor> context,
    TradeStrategyFamilyCreationService service, ILogger<TradeStrategyFamilyCommandActor> logger,
    TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService? catalog = null) : BaseEventSourceCommandActor<TradeStrategyFamilyCommandActor>(context, logger)
{
    public const string ActorName = CreateTradeStrategyFamilyCommand.Actor;
    protected override ICommand ParseMessage(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorMessage message) =>
        ParseMappedCommand(context, message, new Dictionary<string, Func<IActorMessage, ICommand>>
        {
            [TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogCommand.Verb] = msg => msg.AsCommand<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogCommand>()!,
            [CreateTradeStrategyFamilyCommand.Verb] = msg => msg.AsCommand<CreateTradeStrategyFamilyCommand>()!,
            [ChangeTradeStrategyFamilyCommand.Verb] = msg => msg.AsCommand<ChangeTradeStrategyFamilyCommand>()!,
            [RemoveTradeStrategyFamilyCommand.Verb] = msg => msg.AsCommand<RemoveTradeStrategyFamilyCommand>()!
        });
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command) =>
        ReceiveAsync(context, state, command, CancellationToken.None);
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogCommand changeCatalog:
                if (catalog is null) throw new InvalidOperationException("ConfigurationDb catalog service is unavailable.");
                var request = TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogJson.Read<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogCommandRequest>(changeCatalog.RequestJson);
                if (request.OperationId != command.CommandId) throw new ArgumentException("Catalog OperationId must match CommandId.");
                await catalog.ExecuteAsync(request, changeCatalog.OriginatedBy, cancellationToken).ConfigureAwait(false);
                break;
            case CreateTradeStrategyFamilyCommand:
            case ChangeTradeStrategyFamilyCommand:
            case RemoveTradeStrategyFamilyCommand:
                throw new InvalidOperationException("Legacy trade strategy families are read-only. Use the ConfigurationDb strategy catalog in Reference Data Manager.");
            default: throw new ArgumentException("CommandId must equal the nonempty OperationId.");
        }
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, ex.Message));
}

public sealed class TradeStrategyFamilyCommandContext(IActorSupervisor supervisor, ILogger<TradeStrategyFamilyCommandActor> logger)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, TradeStrategyFamilyCommandActor.ActorName)), ICommandActorContext<TradeStrategyFamilyCommandActor>
{
    public ILogger<TradeStrategyFamilyCommandActor> Logger { get; } = logger;
}
