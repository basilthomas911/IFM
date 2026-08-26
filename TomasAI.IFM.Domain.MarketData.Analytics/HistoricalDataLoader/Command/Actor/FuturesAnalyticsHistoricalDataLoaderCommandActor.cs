using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

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
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName,
                Verb: LoadFuturesAnalyticsHistoricalDataCommand.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return message.AsCommand<LoadFuturesAnalyticsHistoricalDataCommand>()
            ?? throw new InvalidOperationException("Unable to deserialize the data load command.");
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        var value = command as LoadFuturesAnalyticsHistoricalDataCommand
            ?? throw new InvalidOperationException("Unsupported data load command.");
        if (value.CommandId == Guid.Empty || value.EntityId.Value == Guid.Empty
            || value.CommandId != value.EntityId.Value)
            throw new ArgumentException("CommandId and DataLoadAttemptId must be the same non-empty identity.");
        if (value.Parameters.StartDate == default || value.Parameters.EndDate < value.Parameters.StartDate)
            throw new ArgumentException("A valid inclusive data load date range is required.");
        if (value.Parameters.Series.Length == 0)
            throw new ArgumentException("At least one historical series is required.");
        if (value.Parameters.MaximumCostUsd <= 0 || value.Parameters.MaximumBytes <= 0)
            throw new ArgumentException("Positive cost and byte budgets are required.");
        if (string.IsNullOrWhiteSpace(value.Parameters.NormalizationVersion)
            || string.IsNullOrWhiteSpace(value.Parameters.CalculationConfigurationVersion)
            || string.IsNullOrWhiteSpace(value.Parameters.RequestedBy))
            throw new ArgumentException("Normalization, calculation configuration, and requester are required.");
        foreach (var series in value.Parameters.Series)
        {
            if (new MarketSeriesIdentityValidationRules().Execute(series.MarketSeriesIdentity).Length != 0)
                throw new ArgumentException("Every data load series identity must be valid.");
            if (!Enum.IsDefined(series.Schema))
                throw new ArgumentException("Every data load historical schema must be supported.");
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesAnalyticsHistoricalDataLoaderCommandActor> context,
        IActorState state,
        ICommand command) => ValueTask.FromResult(
            ((LoadFuturesAnalyticsHistoricalDataCommand)command)
                .Execute((FuturesAnalyticsHistoricalDataLoaderCommandState)state));

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
