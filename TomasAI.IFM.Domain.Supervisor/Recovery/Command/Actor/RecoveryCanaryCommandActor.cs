using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Validation;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;

/// <summary>Acknowledges one isolated canary command after publishing its projected event.</summary>
public sealed class RecoveryCanaryCommandActor(ICommandActorContext<RecoveryCanaryCommandActor> context,
    ILogger<RecoveryCanaryCommandActor> logger)
    : BaseEventSourceCommandActor<RecoveryCanaryCommandActor>(context, logger)
{
    public const string ActorName = RecoveryCanaryCommand.Actor;

    protected override ValueTask OnStartup(ICommandActorContext<RecoveryCanaryCommandActor> context)
        => Typed(context).EventProjector.StartAsync(context);

    protected override ValueTask OnShutdown(ICommandActorContext<RecoveryCanaryCommandActor> context)
        => Typed(context).EventProjector.StopAsync();

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ActorThreadId threadId,
        ICommand command, CancellationToken cancellationToken)
        => await Typed(context).StateRepository.LoadStateAsync(command, cancellationToken)
            .ConfigureAwait(false);

    protected override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ActorThreadId threadId,
        ICommand command)
        => LoadStateAsync(context, command, CancellationToken.None);

    static async ValueTask<IActorState> LoadStateAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ICommand command,
        CancellationToken cancellationToken)
        => await Typed(context).StateRepository.LoadStateAsync(command, cancellationToken)
            .ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command, CancellationToken cancellationToken)
        => Typed(context).StateRepository.SaveStateAsync(
            context, (RecoveryCanaryCommandState)state, command, cancellationToken);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command)
        => Typed(context).StateRepository.SaveStateAsync(
            context, (RecoveryCanaryCommandState)state, command);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [RecoveryCanaryCommand.Verb] = static message => message.AsCommand<RecoveryCanaryCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RecoveryCanaryCommand)] = static command =>
            {
                var typed = (RecoveryCanaryCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateCanaryCorrelation(typed.CorrelationId)
                    .ValidateCanaryGeneration(typed.GenerationId)
                    .ValidateCanaryValueDate(typed.ValueDate)
                    .ValidateCanaryDataset(typed.Dataset)
                    .ValidateCanaryIssuedUtc(typed.IssuedUtc)
                    .ValidateCanaryRoute(typed);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<ICommand, RecoveryCanaryCommandState, CancellationToken,
            ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, RecoveryCanaryCommandState,
            CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(RecoveryCanaryCommand)] = static (command, state, token) =>
                ((RecoveryCanaryCommand)command).ExecuteAsync(state, token)
        }.ToFrozenDictionary();

    protected override ICommand ParseMessage(ICommandActorContext<RecoveryCanaryCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<RecoveryCanaryCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, IActorState state, ICommand command) =>
        ResolveMappedCommandHandler(command, _receiveMap)(
            command, (RecoveryCanaryCommandState)state, CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, IActorState state, ICommand command,
        CancellationToken cancellationToken) =>
        ResolveMappedCommandHandler(command, _receiveMap)(
            command, (RecoveryCanaryCommandState)state, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<RecoveryCanaryCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception)
    {
        logger.LogError(exception, "Supervisor recovery canary command failed for {ActorThreadId}.", threadId);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? RecoveryCanaryCommand.ErrorId,
                "Supervisor recovery canary command failed."));
    }

    static IRecoveryCanaryCommandContext Typed(
        ICommandActorContext<RecoveryCanaryCommandActor> context) =>
        context as IRecoveryCanaryCommandContext
        ?? throw new ArgumentException("A recovery canary command context is required.", nameof(context));
}
