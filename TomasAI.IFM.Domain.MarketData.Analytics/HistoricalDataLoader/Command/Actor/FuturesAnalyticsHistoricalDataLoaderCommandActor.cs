using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;

/// <summary>Durably accepts parameter-only Analytics historical data-load commands.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderCommandActor(
    ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesAnalyticsHistoricalDataLoaderCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string ActorName = LoadFuturesAnalyticsHistoricalDataCommand.Actor;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context)
        => await context.DataLoaderProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context)
        => context.DataLoaderProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [LoadFuturesAnalyticsHistoricalDataCommand.Verb] = message =>
            message.AsCommand<LoadFuturesAnalyticsHistoricalDataCommand>()
            ?? throw new InvalidOperationException("Unable to deserialize the data load command.")
    };

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(LoadFuturesAnalyticsHistoricalDataCommand)] = static command =>
            {
                var load = (LoadFuturesAnalyticsHistoricalDataCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(load.CommandId, load.CommandName)
                    .ValidateDataLoadAttemptId(load.EntityId)
                    .ValidateHistoricalLoad(load);
            }
        };

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var receiveCommand = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receiveCommand.Invoke(
            command,
            context,
            (FuturesAnalyticsHistoricalDataLoaderCommandState)state));
    }

    static readonly IReadOnlyDictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor>,
        FuturesAnalyticsHistoricalDataLoaderCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand,
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor>,
        FuturesAnalyticsHistoricalDataLoaderCommandState, ServiceResult<GuidResult>>>()
        {
            [typeof(LoadFuturesAnalyticsHistoricalDataCommand)] = static (command, _, state) =>
                ((LoadFuturesAnalyticsHistoricalDataCommand)command).Execute(state)
        };

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        ICommand command) => await context.DataLoaderRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await context.DataLoaderRepository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) => context.DataLoaderRepository.SaveStateAsync(
            context, (FuturesAnalyticsHistoricalDataLoaderCommandState)state, command);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) => context.DataLoaderRepository.SaveStateAsync(
            context, (FuturesAnalyticsHistoricalDataLoaderCommandState)state, command, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? 26020, exception.Message));
}
