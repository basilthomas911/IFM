using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Validation;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Actor;

/// <summary>Provides the DatabaseBackupRetentionCommandActor implementation.</summary>
public class DatabaseBackupRetentionCommandActor(
    ICommandActorContext<DatabaseBackupRetentionCommandActor> actorContext)
    : BaseEventSourceCommandActor<DatabaseBackupRetentionCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupRetentionCommandContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupRetentionCommandContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupRetentionCommand";
    IEventSourceActorStateRepository<DatabaseBackupRetentionCommandState> _repository = default!;
    readonly IEventProjector<DatabaseBackupRetentionCommandActor> _eventProjector = actorContext.EventProjector;

    /// <summary>Gets the SupportedCommandTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            ["EvaluateRetention"] = message => message.AsCommand<RequestBackupRetentionEvaluationCommand>()!,
            ["ExecuteRetention"] = message => message.AsCommand<ExecuteBackupRetentionPlanCommand>()!,
            ["RecordAdmission"] = message => message.AsCommand<RecordDatabaseOperationAdmissionCommand>()!,
            ["RecordStarted"] = message => message.AsCommand<RecordDatabaseOperationStartedCommand>()!,
            ["RecordProgress"] = message => message.AsCommand<RecordDatabaseOperationProgressCommand>()!,
            ["RecordVerification"] = message => message.AsCommand<RecordDatabaseOperationVerificationCommand>()!,
            ["RecordError"] = message => message.AsCommand<RecordDatabaseOperationErrorCommand>()!,
            ["CompleteOperation"] = message => message.AsCommand<CompleteDatabaseOperationCommand>()!,
            ["FailOperation"] = message => message.AsCommand<FailDatabaseOperationCommand>()!,
            ["RecordCancelled"] = message => message.AsCommand<RecordDatabaseOperationCancelledCommand>()!,
            ["RecordRetentionResult"] = message => message.AsCommand<RecordDatabaseRetentionResultCommand>()!,
            ["ReconcileServiceState"] = message => message.AsCommand<ReconcileDatabaseBackupServiceStateCommand>()!,
            ["RecordRunStatistics"] = message => message.AsCommand<RecordDatabaseRecoveryRunStatisticsCommand>()!
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RequestBackupRetentionEvaluationCommand)] = command =>
            {
                var typed = (RequestBackupRetentionEvaluationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateRecoveryOperationId(typed.EntityId, typed.CommandName)
                    .ValidateBackupCommand(typed);
            },
            [typeof(ExecuteBackupRetentionPlanCommand)] = command =>
            {
                var typed = (ExecuteBackupRetentionPlanCommand)command;
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
            [typeof(RecordDatabaseRetentionResultCommand)] = command =>
            {
                var typed = (RecordDatabaseRetentionResultCommand)command;
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
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<DatabaseBackupRetentionCommandActor>, DatabaseBackupRetentionCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, ICommandActorContext<DatabaseBackupRetentionCommandActor>, DatabaseBackupRetentionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(RequestBackupRetentionEvaluationCommand)] = (command, _, state) => ((RequestBackupRetentionEvaluationCommand)command).Execute(state),
            [typeof(ExecuteBackupRetentionPlanCommand)] = (command, _, state) => ((ExecuteBackupRetentionPlanCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationAdmissionCommand)] = (command, _, state) => ((RecordDatabaseOperationAdmissionCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationStartedCommand)] = (command, _, state) => ((RecordDatabaseOperationStartedCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationProgressCommand)] = (command, _, state) => ((RecordDatabaseOperationProgressCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationVerificationCommand)] = (command, _, state) => ((RecordDatabaseOperationVerificationCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationErrorCommand)] = (command, _, state) => ((RecordDatabaseOperationErrorCommand)command).Execute(state),
            [typeof(CompleteDatabaseOperationCommand)] = (command, _, state) => ((CompleteDatabaseOperationCommand)command).Execute(state),
            [typeof(FailDatabaseOperationCommand)] = (command, _, state) => ((FailDatabaseOperationCommand)command).Execute(state),
            [typeof(RecordDatabaseOperationCancelledCommand)] = (command, _, state) => ((RecordDatabaseOperationCancelledCommand)command).Execute(state),
            [typeof(RecordDatabaseRetentionResultCommand)] = (command, _, state) => ((RecordDatabaseRetentionResultCommand)command).Execute(state),
            [typeof(ReconcileDatabaseBackupServiceStateCommand)] = (command, _, state) => ((ReconcileDatabaseBackupServiceStateCommand)command).Execute(state),
            [typeof(RecordDatabaseRecoveryRunStatisticsCommand)] = (command, _, state) => ((RecordDatabaseRecoveryRunStatisticsCommand)command).Execute(state)
        };

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupRetentionCommandActor> context)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, CancellationToken.None);
    }

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, CancellationToken cancellationToken)

    {
        ArgumentNullException.ThrowIfNull(context);
        return StartAsync(context, cancellationToken);
    }

    async ValueTask StartAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, CancellationToken cancellationToken)
    {
        _repository = context.Container.Resolve<IEventSourceActorStateRepository<DatabaseBackupRetentionCommandState>>();
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

    protected override ValueTask OnShutdown(ICommandActorContext<DatabaseBackupRetentionCommandActor> context)
        => _eventProjector.StopAsync(CancellationToken.None);

    protected override ICommand ParseMessage(
        ICommandActorContext<DatabaseBackupRetentionCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, IActorState state, ICommand command)
    {
        var aggregate = (DatabaseBackupRetentionCommandState)state;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receive(command, context, aggregate));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repository.SaveStateAsync(context, (DatabaseBackupRetentionCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repository.SaveStateAsync(context, (DatabaseBackupRetentionCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DatabaseBackupRetentionCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)

    {
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9199, ex.Message));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => _receiveMap.Keys as IReadOnlyCollection<Type> ?? _receiveMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => _parseMap.Keys as IReadOnlyCollection<string> ?? _parseMap.Keys.ToArray(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
