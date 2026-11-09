using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;

public sealed class MarketOutlookSnapshotCommandActor(
    ICommandActorContext<MarketOutlookSnapshotCommandActor> actorContext, ResidentActorPilotOptions? residency = null)
    : BaseInMemoryEventSourceCommandActor<MarketOutlookSnapshotCommandActor, MarketOutlookSnapshotCommandState>(
        actorContext,
        ((IMarketOutlookSnapshotCommandContext)actorContext).Logger, committedStatePolicy: (residency ?? new()).MarketOutlook)
{
    public const string ActorName = InsertMarketOutlookSnapshotCommand.Actor;

    IMarketOutlookSnapshotCommandContext DomainContext =>
        (IMarketOutlookSnapshotCommandContext)Context;

    protected override ValueTask OnInMemoryStartupAsync(ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => DomainContext.EventProjector.StartAsync(context);

    protected override ValueTask OnInMemoryShutdownAsync(ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
        => DomainContext.EventProjector.StopAsync();

    protected override ICommand ParseMessage(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
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

    protected override async ValueTask<MarketOutlookSnapshotCommandState> LoadStateFromStoreAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken) => await DomainContext.Repository
            .LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask SaveStateToStoreAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        MarketOutlookSnapshotCommandState state,
        ICommand command,
        CancellationToken cancellationToken) => DomainContext.Repository.SaveStateAsync(
            context, (MarketOutlookSnapshotCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> HandleCommandExceptionAsync(
        ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write24(DomainContext.Logger, ex, threadId, command?.CommandId);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? InsertMarketOutlookSnapshotCommand.ErrorId,
                ex.Message));
    }

    protected override bool IsResidentCommand(ICommand command) => command is InsertMarketOutlookSnapshotCommand;
    protected override bool IsResidentMessage(ActorSubject subject) => subject.Verb == InsertMarketOutlookSnapshotCommand.Verb;
    protected override ValueTask ValidateResidentNoChangeAsync(ICommandActorContext<MarketOutlookSnapshotCommandActor> context,
        MarketOutlookSnapshotCommandState state, ICommand command, CancellationToken cancellationToken)
        => ((IResidentCommandStateRepository<MarketOutlookSnapshotCommandState>)DomainContext.Repository)
            .ValidateNoChangeAsync(state, command, cancellationToken);
}
