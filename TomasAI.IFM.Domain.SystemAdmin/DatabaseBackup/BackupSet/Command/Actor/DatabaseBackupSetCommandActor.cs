using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Validation;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.Actor;

/// <summary>Provides the DatabaseBackupSetCommandActor implementation.</summary>
public class DatabaseBackupSetCommandActor(
    ICommandActorContext<DatabaseBackupSetCommandActor> actorContext)
    : BaseEventSourceCommandActor<DatabaseBackupSetCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupSetCommandContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupSetCommandContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupSetCommand";
    IEventSourceActorStateRepository<DatabaseBackupSetCommandState> _repository = default!;
    readonly IEventProjector<DatabaseBackupSetCommandActor> _eventProjector = actorContext.EventProjector;

    /// <summary>Gets the SupportedCommandTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            ["PlaceLegalHold"] = message => message.AsCommand<PlaceBackupLegalHoldCommand>()!,
            ["ReleaseLegalHold"] = message => message.AsCommand<ReleaseBackupLegalHoldCommand>()!
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PlaceBackupLegalHoldCommand)] = command =>
            {
                var typed = (PlaceBackupLegalHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(ReleaseBackupLegalHoldCommand)] = command =>
            {
                var typed = (ReleaseBackupLegalHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            }
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<DatabaseBackupSetCommandActor>, DatabaseBackupSetCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, ICommandActorContext<DatabaseBackupSetCommandActor>, DatabaseBackupSetCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(PlaceBackupLegalHoldCommand)] = (command, _, state) => ((PlaceBackupLegalHoldCommand)command).Execute(state),
            [typeof(ReleaseBackupLegalHoldCommand)] = (command, _, state) => ((ReleaseBackupLegalHoldCommand)command).Execute(state)
        };

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupSetCommandActor> context)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, CancellationToken.None);
    }

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupSetCommandActor> context, CancellationToken cancellationToken)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, cancellationToken);
    }

    async ValueTask StartAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, CancellationToken cancellationToken)
    {
        _repository = context.Container.Resolve<IEventSourceActorStateRepository<DatabaseBackupSetCommandState>>();
        try
        {
            await _eventProjector.StartAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _eventProjector.StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    protected override ValueTask OnShutdown(ICommandActorContext<DatabaseBackupSetCommandActor> context)
        => _eventProjector.StopAsync(CancellationToken.None);

    protected override ICommand ParseMessage(
        ICommandActorContext<DatabaseBackupSetCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, IActorState state, ICommand command)
    {
        var aggregate = (DatabaseBackupSetCommandState)state;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receive(command, context, aggregate));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repository.SaveStateAsync(context, (DatabaseBackupSetCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repository.SaveStateAsync(context, (DatabaseBackupSetCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DatabaseBackupSetCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)

    {
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9199, ex.Message));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => _receiveMap.Keys as IReadOnlyCollection<Type> ?? _receiveMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => _parseMap.Keys as IReadOnlyCollection<string> ?? _parseMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
