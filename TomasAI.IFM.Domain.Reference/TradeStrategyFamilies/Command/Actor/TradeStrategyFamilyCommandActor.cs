using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;

/// <summary>Routes strategy catalog commands and rejects retired legacy family mutations.</summary>
public sealed class TradeStrategyFamilyCommandActor(
    ICommandActorContext<TradeStrategyFamilyCommandActor> context,
    TradeStrategyFamilyCreationService service,
    ILogger<TradeStrategyFamilyCommandActor> logger,
    StrategyCatalogService? catalog = null)
    : BaseEventSourceCommandActor<TradeStrategyFamilyCommandActor>(context, logger)
{
    public const string ActorName = CreateTradeStrategyFamilyCommand.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StrategyCatalogCommand.Verb] = message => message.AsCommand<StrategyCatalogCommand>()!,
            [CreateTradeStrategyFamilyCommand.Verb] = message => message.AsCommand<CreateTradeStrategyFamilyCommand>()!,
            [ChangeTradeStrategyFamilyCommand.Verb] = message => message.AsCommand<ChangeTradeStrategyFamilyCommand>()!,
            [RemoveTradeStrategyFamilyCommand.Verb] = message => message.AsCommand<RemoveTradeStrategyFamilyCommand>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(StrategyCatalogCommand)] = Validate,
            [typeof(CreateTradeStrategyFamilyCommand)] = Validate,
            [typeof(ChangeTradeStrategyFamilyCommand)] = Validate,
            [typeof(RemoveTradeStrategyFamilyCommand)] = Validate
        };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, StrategyCatalogService?, CancellationToken, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, StrategyCatalogService?, CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(StrategyCatalogCommand)] = static (command, service, token) => ((StrategyCatalogCommand)command).ExecuteAsync(service, token),
            [typeof(CreateTradeStrategyFamilyCommand)] = static (command, _, _) => ((CreateTradeStrategyFamilyCommand)command).ExecuteAsync(),
            [typeof(ChangeTradeStrategyFamilyCommand)] = static (command, _, _) => ((ChangeTradeStrategyFamilyCommand)command).ExecuteAsync(),
            [typeof(RemoveTradeStrategyFamilyCommand)] = static (command, _, _) => ((RemoveTradeStrategyFamilyCommand)command).ExecuteAsync()
        };

    protected override ICommand ParseMessage(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken)
        => ResolveMappedCommandHandler(command, _receiveMap)(command, catalog, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TradeStrategyFamilyCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static List<ValidationError> Validate(ICommand command)
        => new List<ValidationError>()
            .ValidateCommandId(command.CommandId, command.CommandName)
            .ValidateEntityId(command, command.CommandName);
}

/// <summary>Provides the Trade Strategy Family command mailbox context.</summary>
public sealed class TradeStrategyFamilyCommandContext(IActorSupervisor supervisor, ILogger<TradeStrategyFamilyCommandActor> logger)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, TradeStrategyFamilyCommandActor.ActorName)), ICommandActorContext<TradeStrategyFamilyCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    public ILogger<TradeStrategyFamilyCommandActor> Logger { get; } = logger;
}
