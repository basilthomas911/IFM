using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;

public sealed class MarketOutlookSnapshotCommandActor(
    ICommandActorContext<MarketOutlookSnapshotCommandActor> actorContext)
    : BaseEventSourceCommandActor<MarketOutlookSnapshotCommandActor>(
        actorContext,
        ((IMarketOutlookSnapshotCommandContext)actorContext).Logger)
{
    public const string ActorName = InsertMarketOutlookSnapshotCommand.Actor;

    IMarketOutlookSnapshotCommandContext DomainContext =>
        (IMarketOutlookSnapshotCommandContext)Context;

    protected override ValueTask OnStartup(ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => DomainContext.EventProjector.StartAsync(context);

    protected override ValueTask OnShutdown(ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => DomainContext.EventProjector.StopAsync();

    protected override ICommand ParseMessage(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>
        {
            [InsertMarketOutlookSnapshotCommand.Verb] = message =>
                message.AsCommand<InsertMarketOutlookSnapshotCommand>()
                ?? throw new InvalidOperationException("Unable to deserialize Market Outlook insert command.")
        };

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorState state,
        ICommand command)
        => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(
            command, context, (MarketOutlookSnapshotCommandState)state));

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<MarketOutlookSnapshotCommandActor>,
        MarketOutlookSnapshotCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, ICommandActorContext<MarketOutlookSnapshotCommandActor>,
            MarketOutlookSnapshotCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(InsertMarketOutlookSnapshotCommand)] = static (command, _, state) =>
                ((InsertMarketOutlookSnapshotCommand)command).Execute(state)
        };

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override IReadOnlyList<ValidationError>? GetCommandValidationErrors(ICommand command)
        => _validationMap.TryGetValue(command.GetType(), out var validate)
            ? validate(command)
            : throw new InvalidOperationException(
                $"Unable to validate {ActorName} commands from message: {command.Subject}");

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(InsertMarketOutlookSnapshotCommand)] = static command =>
            {
                var insert = (InsertMarketOutlookSnapshotCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(insert.CommandId, insert.CommandName)
                    .ValidateEntityId(insert.EntityId, insert.CommandName)
                    .ValidateSnapshot(insert);
            }
        };

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command) => await DomainContext.Repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await DomainContext.Repository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) => DomainContext.Repository.SaveStateAsync(
            context, (MarketOutlookSnapshotCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) => DomainContext.Repository.SaveStateAsync(
            context, (MarketOutlookSnapshotCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? InsertMarketOutlookSnapshotCommand.ErrorId,
                exception.Message));
}
