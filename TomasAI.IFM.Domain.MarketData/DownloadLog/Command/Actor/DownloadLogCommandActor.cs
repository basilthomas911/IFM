using System.Collections.Frozen;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Validation;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;

/// <summary>Serializes durable download outcomes through exact command maps and event-sourced state.</summary>
public sealed class DownloadLogCommandActor(ICommandActorContext<DownloadLogCommandActor> actorContext,
    IEventProjector<DownloadLogCommandActor> eventProjector)
    : BaseEventSourceCommandActor<DownloadLogCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>The published DownloadLog command mailbox name.</summary>
    public const string ActorName = "DownloadLogCommand";
    IEventSourceActorStateRepository<DownloadLogCommandState> _stateRepository = default!;
    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<DownloadLogCommandActor> context)
        => OnStartup(context, CancellationToken.None);
    /// <inheritdoc />
    protected override async ValueTask OnStartup(ICommandActorContext<DownloadLogCommandActor> context, CancellationToken cancellationToken)
    {
        _stateRepository = context.Container.Resolve<IEventSourceActorStateRepository<DownloadLogCommandState>>();
        try { await eventProjector.StartAsync(context, cancellationToken).ConfigureAwait(false); }
        catch { await eventProjector.StopAsync().ConfigureAwait(false); throw; }
    }
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<DownloadLogCommandActor> context)
        => eventProjector.StopAsync();
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>
        { [InsertMarketDataDownloadLogCommand.Verb] = static message => message.AsCommand<InsertMarketDataDownloadLogCommand>()! }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(InsertMarketDataDownloadLogCommand)] = static command =>
            {
                var typed = (InsertMarketDataDownloadLogCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateDownloadLogId(typed.EntityId, typed.CommandName)
                    .ValidateDownloadOutcome(typed.Outcome)
                    .ValidateDownloadLogEnvelope(typed);
            }
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, DownloadLogCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, DownloadLogCommandState, ServiceResult<GuidResult>>>
        { [typeof(InsertMarketDataDownloadLogCommand)] = static (command, state) => ((InsertMarketDataDownloadLogCommand)command).Execute(state) }.ToFrozenDictionary();

    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<DownloadLogCommandActor> context, IActorMessage message)
    {
        var command = ParseMappedCommand(context, message, _parseMap);
        ValidateMappedCommand(command, _validationMap); // Invalid envelopes never reserve the stable attempt ID.
        return command;
    }
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command)
    { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DownloadLogCommandActor> context, IActorState state, ICommand command)
        => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (DownloadLogCommandState)state));
    /// <inheritdoc />
    protected override async ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<DownloadLogCommandActor> context, ICommand command, CancellationToken cancellationToken)
        => await ((InsertMarketDataDownloadLogCommand)command).ShouldProcessDuplicateAsync(_stateRepository, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _stateRepository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _stateRepository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _stateRepository.SaveStateAsync(context, (DownloadLogCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _stateRepository.SaveStateAsync(context, (DownloadLogCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, ex.Message));
}
