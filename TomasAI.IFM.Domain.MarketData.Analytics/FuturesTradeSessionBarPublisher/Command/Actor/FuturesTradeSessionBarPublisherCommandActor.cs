using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Actor;

/// <summary>Owns the durable, event-sourced publication state for completed futures trade-session bars.</summary>
public sealed class FuturesTradeSessionBarPublisherCommandActor(
    ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> actorContext)
    : BaseEventSourceCommandActor<FuturesTradeSessionBarPublisherCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Command actor mailbox name.</summary>
    public const string ActorName = PublishFuturesTradeSessionBarCommand.Actor;

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context)
        => await context.BarPublisherProjector.StartAsync(context).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context)
        => context.BarPublisherProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName,
                Verb: PublishFuturesTradeSessionBarCommand.Verb })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from {message.Subject}.");
        return message.AsCommand<PublishFuturesTradeSessionBarCommand>()
            ?? throw new InvalidOperationException("Unable to deserialize the Publish bar command.");
    }

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        var value = command as PublishFuturesTradeSessionBarCommand
            ?? throw new InvalidOperationException("Unsupported publisher command.");
        if (value.CommandId == Guid.Empty || value.CommandId != value.Bar.ObservationId.Value)
            throw new ArgumentException("CommandId must equal the deterministic bar identity.");
        if (new FuturesTradeSessionBarEntityIdValidationRules().Execute(value.EntityId).Length != 0)
            throw new ArgumentException("A valid bar publisher entity identity is required.");
        if (new FuturesTradeSessionBarReadModelValidationRules().Execute(value.Bar).Length != 0)
            throw new ArgumentException("A valid completed futures trade-session bar is required.");
        if (value.EntityId.MarketSeriesIdentity != value.Bar.MarketSeriesIdentity
            || value.EntityId.TimeFrame != value.Bar.TimeFrame
            || value.Subject.EntityId != value.EntityId.Format())
            throw new ArgumentException("Command routing identity must match the completed bar.");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        IActorState state,
        ICommand command) => ValueTask.FromResult(
            ((PublishFuturesTradeSessionBarCommand)command)
                .Execute((FuturesTradeSessionBarPublisherCommandState)state));

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        ICommand command) => await context.BarPublisherRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await context.BarPublisherRepository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command) => context.BarPublisherRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarPublisherCommandState)state, command);

    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken) => context.BarPublisherRepository.SaveStateAsync(
            context, (FuturesTradeSessionBarPublisherCommandState)state, command, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? PublishFuturesTradeSessionBarCommand.ErrorId,
                exception.Message));
}
