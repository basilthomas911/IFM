using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;

public sealed class DownloadLogCommandActor(ICommandActorContext<DownloadLogCommandActor> actorContext,
    IEventProjector<DownloadLogCommandActor> eventProjector)
    : BaseEventSourceCommandActor<DownloadLogCommandActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "DownloadLogCommand";
    IEventSourceActorStateRepository<DownloadLogCommandState> _repo = default!;
    protected override ValueTask OnStartup(ICommandActorContext<DownloadLogCommandActor> context)
        => OnStartup(context, CancellationToken.None);
    protected override async ValueTask OnStartup(ICommandActorContext<DownloadLogCommandActor> context, CancellationToken cancellationToken)
    {
        _repo = context.Container.Resolve<IEventSourceActorStateRepository<DownloadLogCommandState>>();
        try { await eventProjector.StartAsync(context, cancellationToken).ConfigureAwait(false); }
        catch { await eventProjector.StopAsync().ConfigureAwait(false); throw; }
    }
    protected override ValueTask OnShutdown(ICommandActorContext<DownloadLogCommandActor> context)
        => eventProjector.StopAsync();
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>
        { [InsertMarketDataDownloadLogCommand.Verb] = m => m.AsCommand<InsertMarketDataDownloadLogCommand>()! };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        { [typeof(InsertMarketDataDownloadLogCommand)] = c => { ((InsertMarketDataDownloadLogCommand)c).Validate(); return []; } };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, DownloadLogCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, DownloadLogCommandState, ServiceResult<GuidResult>>>
        { [typeof(InsertMarketDataDownloadLogCommand)] = (c, s) => ((InsertMarketDataDownloadLogCommand)c).Execute(s) };

    protected override ICommand ParseMessage(ICommandActorContext<DownloadLogCommandActor> context, IActorMessage message)
    {
        var command = ParseMappedCommand(context, message, _parseMap);
        ValidateMappedCommand(command, _validationMap); // Invalid envelopes never reserve the stable attempt ID.
        return command;
    }
    protected override ValueTask OnValidateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command)
    { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DownloadLogCommandActor> context, IActorState state, ICommand command)
        => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (DownloadLogCommandState)state));
    protected override async ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<DownloadLogCommandActor> context, ICommand command, CancellationToken cancellationToken)
        => !(await _repo.LoadStateAsync(command, cancellationToken)).VerifyDuplicate((InsertMarketDataDownloadLogCommand)command);
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _repo.LoadStateAsync(command);
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(command, cancellationToken);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repo.SaveStateAsync(context, (DownloadLogCommandState)state, command);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repo.SaveStateAsync(context, (DownloadLogCommandState)state, command, cancellationToken);
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DownloadLogCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(6050, ex.Message));
}
