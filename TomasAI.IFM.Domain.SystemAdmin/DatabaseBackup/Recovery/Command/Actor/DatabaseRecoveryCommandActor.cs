using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Validation;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;

/// <summary>Provides the DatabaseRecoveryCommandActor implementation.</summary>
public class DatabaseRecoveryCommandActor(
    ICommandActorContext<DatabaseRecoveryCommandActor> actorContext)
    : BaseEventSourceCommandActor<DatabaseRecoveryCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseRecoveryCommandContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseRecoveryCommandContext, nameof(Context))!;

    public const string Actor = "DatabaseRecoveryCommand";
    IEventSourceActorStateRepository<DatabaseRecoveryCommandState> _repository = default!;
    readonly IEventProjector<DatabaseRecoveryCommandActor> _eventProjector = actorContext.EventProjector;

    /// <summary>Gets the SupportedCommandTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            ["RequestRestore"] = message => message.AsCommand<RequestDatabaseRestoreCommand>()!,
            ["ApproveRestore"] = message => message.AsCommand<ApproveDatabaseRestoreCommand>()!,
            ["CancelRestore"] = message => message.AsCommand<CancelDatabaseRestoreCommand>()!,
            ["ApproveCutover"] = message => message.AsCommand<ApproveDatabaseCutoverCommand>()!,
            ["RequestRestoreDrill"] = message => message.AsCommand<RequestDatabaseRestoreDrillCommand>()!,
            ["RecordAdmission"] = message => message.AsCommand<RecordDatabaseOperationAdmissionCommand>()!,
            ["RecordStarted"] = message => message.AsCommand<RecordDatabaseOperationStartedCommand>()!,
            ["RecordProgress"] = message => message.AsCommand<RecordDatabaseOperationProgressCommand>()!,
            ["RecordVerification"] = message => message.AsCommand<RecordDatabaseOperationVerificationCommand>()!,
            ["RecordError"] = message => message.AsCommand<RecordDatabaseOperationErrorCommand>()!,
            ["RecordReadyForCutover"] = message => message.AsCommand<RecordDatabaseRestoreReadyForCutoverCommand>()!,
            ["CompleteOperation"] = message => message.AsCommand<CompleteDatabaseOperationCommand>()!,
            ["FailOperation"] = message => message.AsCommand<FailDatabaseOperationCommand>()!,
            ["RecordCancelled"] = message => message.AsCommand<RecordDatabaseOperationCancelledCommand>()!,
            ["ReconcileServiceState"] = message => message.AsCommand<ReconcileDatabaseBackupServiceStateCommand>()!,
            ["RecordRunStatistics"] = message => message.AsCommand<RecordDatabaseRecoveryRunStatisticsCommand>()!
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RequestDatabaseRestoreCommand)] = command =>
            {
                var typed = (RequestDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(ApproveDatabaseRestoreCommand)] = command =>
            {
                var typed = (ApproveDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(CancelDatabaseRestoreCommand)] = command =>
            {
                var typed = (CancelDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(ApproveDatabaseCutoverCommand)] = command =>
            {
                var typed = (ApproveDatabaseCutoverCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RequestDatabaseRestoreDrillCommand)] = command =>
            {
                var typed = (RequestDatabaseRestoreDrillCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationAdmissionCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationAdmissionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationStartedCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationStartedCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationProgressCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationProgressCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationVerificationCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationVerificationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationErrorCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationErrorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseRestoreReadyForCutoverCommand)] = command =>
            {
                var typed = (RecordDatabaseRestoreReadyForCutoverCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(CompleteDatabaseOperationCommand)] = command =>
            {
                var typed = (CompleteDatabaseOperationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(FailDatabaseOperationCommand)] = command =>
            {
                var typed = (FailDatabaseOperationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseOperationCancelledCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationCancelledCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(ReconcileDatabaseBackupServiceStateCommand)] = command =>
            {
                var typed = (ReconcileDatabaseBackupServiceStateCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(RecordDatabaseRecoveryRunStatisticsCommand)] = command =>
            {
                var typed = (RecordDatabaseRecoveryRunStatisticsCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            }
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<DatabaseRecoveryCommandActor>, DatabaseRecoveryCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, ICommandActorContext<DatabaseRecoveryCommandActor>, DatabaseRecoveryCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(RequestDatabaseRestoreCommand)] = (command, _, state) => ((RequestDatabaseRestoreCommand)command).Execute(state),
            [typeof(ApproveDatabaseRestoreCommand)] = (command, _, state) => ((ApproveDatabaseRestoreCommand)command).Execute(state),
            [typeof(CancelDatabaseRestoreCommand)] = (command, _, state) => ((CancelDatabaseRestoreCommand)command).Execute(state),
            [typeof(ApproveDatabaseCutoverCommand)] = (command, _, state) => ((ApproveDatabaseCutoverCommand)command).Execute(state),
            [typeof(RequestDatabaseRestoreDrillCommand)] = (command, _, state) => ((RequestDatabaseRestoreDrillCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationAdmissionCommand)] = (command, _, state) => ((RecordDatabaseOperationAdmissionCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationStartedCommand)] = (command, _, state) => ((RecordDatabaseOperationStartedCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationProgressCommand)] = (command, _, state) => ((RecordDatabaseOperationProgressCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationVerificationCommand)] = (command, _, state) => ((RecordDatabaseOperationVerificationCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationErrorCommand)] = (command, _, state) => ((RecordDatabaseOperationErrorCommand)command).Execute(state),
            [typeof(RecordDatabaseRestoreReadyForCutoverCommand)] = (command, _, state) => ((RecordDatabaseRestoreReadyForCutoverCommand)command).Execute(state),
            [typeof(CompleteDatabaseOperationCommand)] = (command, _, state) => ((CompleteDatabaseOperationCommand)command).Execute(state),
            [typeof(FailDatabaseOperationCommand)] = (command, _, state) => ((FailDatabaseOperationCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationCancelledCommand)] = (command, _, state) => ((RecordDatabaseOperationCancelledCommand)command).Execute(state),
            [typeof(ReconcileDatabaseBackupServiceStateCommand)] = (command, _, state) => ((ReconcileDatabaseBackupServiceStateCommand)command).Execute(state),
            [typeof(RecordDatabaseRecoveryRunStatisticsCommand)] = (command, _, state) => ((RecordDatabaseRecoveryRunStatisticsCommand)command).Execute(state)
        };

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseRecoveryCommandActor> context)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, CancellationToken.None);
    }

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseRecoveryCommandActor> context, CancellationToken cancellationToken)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, cancellationToken);
    }

    async ValueTask StartAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, CancellationToken cancellationToken)
    {
        _repository = context.Container.Resolve<IEventSourceActorStateRepository<DatabaseRecoveryCommandState>>();
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

    protected override ValueTask OnShutdown(ICommandActorContext<DatabaseRecoveryCommandActor> context)
        => _eventProjector.StopAsync(CancellationToken.None);

    protected override ICommand ParseMessage(
        ICommandActorContext<DatabaseRecoveryCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, IActorState state, ICommand command)
    {
        var aggregate = (DatabaseRecoveryCommandState)state;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receive(command, context, aggregate));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repository.SaveStateAsync(context, (DatabaseRecoveryCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repository.SaveStateAsync(context, (DatabaseRecoveryCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DatabaseRecoveryCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)

    {
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9199, ex.Message));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => _receiveMap.Keys as IReadOnlyCollection<Type> ?? _receiveMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => _parseMap.Keys as IReadOnlyCollection<string> ?? _parseMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
