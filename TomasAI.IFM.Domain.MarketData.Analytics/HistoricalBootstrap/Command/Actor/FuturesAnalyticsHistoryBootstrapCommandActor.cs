using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Actor;

/// <summary>Durably accepts parameter-only Analytics history-bootstrap commands.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapCommandActor(
    ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesAnalyticsHistoryBootstrapCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string ActorName = BootstrapFuturesAnalyticsHistoryCommand.Actor;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context)
        => await context.BootstrapProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context)
        => context.BootstrapProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName,
                Verb: BootstrapFuturesAnalyticsHistoryCommand.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return message.AsCommand<BootstrapFuturesAnalyticsHistoryCommand>()
            ?? throw new InvalidOperationException("Unable to deserialize the bootstrap command.");
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        var value = command as BootstrapFuturesAnalyticsHistoryCommand
            ?? throw new InvalidOperationException("Unsupported bootstrap command.");
        if (value.CommandId == Guid.Empty || value.EntityId.Value == Guid.Empty
            || value.CommandId != value.EntityId.Value)
            throw new ArgumentException("CommandId and BootstrapAttemptId must be the same non-empty identity.");
        if (value.Parameters.StartDate == default || value.Parameters.EndDate < value.Parameters.StartDate)
            throw new ArgumentException("A valid inclusive bootstrap date range is required.");
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
                throw new ArgumentException("Every bootstrap series identity must be valid.");
            if (!Enum.IsDefined(series.Schema))
                throw new ArgumentException("Every bootstrap historical schema must be supported.");
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        IActorState state,
        ICommand command) => ValueTask.FromResult(
            ((BootstrapFuturesAnalyticsHistoryCommand)command)
                .Execute((FuturesAnalyticsHistoryBootstrapCommandState)state));

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        ICommand command) => await context.BootstrapRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await context.BootstrapRepository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) => context.BootstrapRepository.SaveStateAsync(
            context, (FuturesAnalyticsHistoryBootstrapCommandState)state, command);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) => context.BootstrapRepository.SaveStateAsync(
            context, (FuturesAnalyticsHistoryBootstrapCommandState)state, command, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? 26020, exception.Message));
}
