using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Extensions;

using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;

/// <summary>Provides the DatabaseBackupCommandActor implementation.</summary>
public class DatabaseBackupCommandActor(
    ICommandActorContext<DatabaseBackupCommandActor> actorContext)
    : BaseEventSourceCommandActor<DatabaseBackupCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupCommandContext ActorContext =>
        IsArgumentNull.Set(Context as IDatabaseBackupCommandContext, nameof(Context))!;

    public const string Actor = DatabaseBackupCommand.Actor;
    IEventSourceActorStateRepository<DatabaseBackupCommandState> _repository = default!;
    readonly IEventProjector<DatabaseBackupCommandActor> _eventProjector = actorContext.EventProjector;

    /// <summary>Gets the SupportedCommandTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes => CommandTypes;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>
        _parseMap.Keys as IReadOnlyCollection<string> ?? _parseMap.Keys.ToArray();

    static readonly Type[] CommandTypes =
    [
        typeof(RequestDatabaseBackupCommand), typeof(CancelDatabaseBackupCommand), typeof(RequestDatabaseRestoreCommand),
        typeof(ApproveDatabaseRestoreCommand), typeof(CancelDatabaseRestoreCommand), typeof(ApproveDatabaseCutoverCommand),
        typeof(RequestDatabaseRestoreDrillCommand), typeof(UpdateDatabaseBackupPolicyCommand), typeof(PlaceBackupLegalHoldCommand),
        typeof(ReleaseBackupLegalHoldCommand), typeof(RequestBackupRetentionEvaluationCommand), typeof(ExecuteBackupRetentionPlanCommand),
        typeof(RecordDatabaseOperationAdmissionCommand), typeof(RecordDatabaseOperationStartedCommand), typeof(RecordDatabaseOperationProgressCommand),
        typeof(RecordDatabaseBackupBoundaryCommand), typeof(RecordDatabaseArtifactReplicaCommand), typeof(RecordDatabaseOperationVerificationCommand),
        typeof(RecordDatabaseOperationErrorCommand), typeof(RecordDatabaseRestoreReadyForCutoverCommand), typeof(CompleteDatabaseOperationCommand),
        typeof(FailDatabaseOperationCommand), typeof(RecordDatabaseOperationCancelledCommand), typeof(RecordDatabaseBackupPolicyStatusCommand),
        typeof(RecordDatabaseRetentionResultCommand), typeof(ReconcileDatabaseBackupServiceStateCommand),
        typeof(RecordDatabaseBackupServiceCapabilityCommand), typeof(RecordDatabaseRecoveryRunStatisticsCommand)
    ];
    static readonly MethodInfo ParseMethod = typeof(DatabaseBackupCommandActor).GetMethod(nameof(ParseTyped), BindingFlags.Static | BindingFlags.NonPublic)!;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = CommandTypes.ToDictionary(
        type => (string)type.GetProperty(nameof(DatabaseBackupCommand.Verb))!.GetValue(Activator.CreateInstance(type))!,
        type => (Func<IActorMessage, ICommand>)ParseMethod.MakeGenericMethod(type).CreateDelegate(typeof(Func<IActorMessage, ICommand>)),
        StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RequestDatabaseBackupCommand)] = command =>
            {
                var typed = (RequestDatabaseBackupCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(CancelDatabaseBackupCommand)] = command =>
            {
                var typed = (CancelDatabaseBackupCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RequestDatabaseRestoreCommand)] = command =>
            {
                var typed = (RequestDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(ApproveDatabaseRestoreCommand)] = command =>
            {
                var typed = (ApproveDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(CancelDatabaseRestoreCommand)] = command =>
            {
                var typed = (CancelDatabaseRestoreCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(ApproveDatabaseCutoverCommand)] = command =>
            {
                var typed = (ApproveDatabaseCutoverCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RequestDatabaseRestoreDrillCommand)] = command =>
            {
                var typed = (RequestDatabaseRestoreDrillCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(UpdateDatabaseBackupPolicyCommand)] = command =>
            {
                var typed = (UpdateDatabaseBackupPolicyCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(PlaceBackupLegalHoldCommand)] = command =>
            {
                var typed = (PlaceBackupLegalHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(ReleaseBackupLegalHoldCommand)] = command =>
            {
                var typed = (ReleaseBackupLegalHoldCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RequestBackupRetentionEvaluationCommand)] = command =>
            {
                var typed = (RequestBackupRetentionEvaluationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(ExecuteBackupRetentionPlanCommand)] = command =>
            {
                var typed = (ExecuteBackupRetentionPlanCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationAdmissionCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationAdmissionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationStartedCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationStartedCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationProgressCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationProgressCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseBackupBoundaryCommand)] = command =>
            {
                var typed = (RecordDatabaseBackupBoundaryCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseArtifactReplicaCommand)] = command =>
            {
                var typed = (RecordDatabaseArtifactReplicaCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationVerificationCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationVerificationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationErrorCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationErrorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseRestoreReadyForCutoverCommand)] = command =>
            {
                var typed = (RecordDatabaseRestoreReadyForCutoverCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(CompleteDatabaseOperationCommand)] = command =>
            {
                var typed = (CompleteDatabaseOperationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(FailDatabaseOperationCommand)] = command =>
            {
                var typed = (FailDatabaseOperationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseOperationCancelledCommand)] = command =>
            {
                var typed = (RecordDatabaseOperationCancelledCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseBackupPolicyStatusCommand)] = command =>
            {
                var typed = (RecordDatabaseBackupPolicyStatusCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseRetentionResultCommand)] = command =>
            {
                var typed = (RecordDatabaseRetentionResultCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(ReconcileDatabaseBackupServiceStateCommand)] = command =>
            {
                var typed = (ReconcileDatabaseBackupServiceStateCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseBackupServiceCapabilityCommand)] = command =>
            {
                var typed = (RecordDatabaseBackupServiceCapabilityCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            },
            [typeof(RecordDatabaseRecoveryRunStatisticsCommand)] = command =>
            {
                var typed = (RecordDatabaseRecoveryRunStatisticsCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed, typed.CommandName)
                    .CaptureCommandValidation(() => typed.Validate());
            }
        };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<DatabaseBackupCommandActor>, DatabaseBackupCommandState, ServiceResult<GuidResult>>> _receiveMap =
        CommandTypes.ToDictionary(
            type => type,
            type => typeof(DatabaseBackupCommand).IsAssignableFrom(type)
                ? (Func<ICommand, ICommandActorContext<DatabaseBackupCommandActor>, DatabaseBackupCommandState, ServiceResult<GuidResult>>)
                    ((command, _, state) => ((DatabaseBackupCommand)command).Execute(state))
                : ((command, _, state) => ((DatabaseBackupInternalCommand)command).Execute(state)));

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupCommandActor> context)
        => StartAsync(context, CancellationToken.None);

    protected override ValueTask OnStartup(ICommandActorContext<DatabaseBackupCommandActor> context, CancellationToken cancellationToken)
        => StartAsync(context, cancellationToken);

    async ValueTask StartAsync(ICommandActorContext<DatabaseBackupCommandActor> context, CancellationToken cancellationToken)
    {
        _repository = context.Container.Resolve<IEventSourceActorStateRepository<DatabaseBackupCommandState>>();
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

    protected override ValueTask OnShutdown(ICommandActorContext<DatabaseBackupCommandActor> context)
        => _eventProjector.StopAsync(CancellationToken.None);

    protected override ICommand ParseMessage(
        ICommandActorContext<DatabaseBackupCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    static ICommand ParseTyped<TCommand>(IActorMessage message) where TCommand : class, ICommand
        => message.AsCommand<TCommand>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TCommand).Name}.");

    protected override ValueTask OnValidateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DatabaseBackupCommandActor> context, IActorState state, ICommand command)
    {
        var aggregate = (DatabaseBackupCommandState)state;
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return ValueTask.FromResult(receive(command, context, aggregate));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, ICommand command)
        => await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repository.SaveStateAsync(context, (DatabaseBackupCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repository.SaveStateAsync(context, (DatabaseBackupCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9199, exception.Message));
}
