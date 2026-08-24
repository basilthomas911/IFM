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
    public static IReadOnlyCollection<string> SupportedVerbs => ParseMap.Keys;

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
    static readonly Dictionary<string, Func<IActorMessage, ICommand>> ParseMap = CommandTypes.ToDictionary(
        type => (string)type.GetProperty(nameof(DatabaseBackupCommand.Verb))!.GetValue(Activator.CreateInstance(type))!,
        type => (Func<IActorMessage, ICommand>)ParseMethod.MakeGenericMethod(type).CreateDelegate(typeof(Func<IActorMessage, ICommand>)),
        StringComparer.Ordinal);

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

    protected override ICommand ParseMessage(ICommandActorContext<DatabaseBackupCommandActor> context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: Actor }
            || !ParseMap.TryGetValue(message.Subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {Actor} command from message: {message.Subject}");
        return parser(message);
    }

    static ICommand ParseTyped<TCommand>(IActorMessage message) where TCommand : class, ICommand
        => message.AsCommand<TCommand>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TCommand).Name}.");

    protected override ValueTask OnValidateAsync(ICommandActorContext<DatabaseBackupCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        if (command is not IDatabaseBackupValidatable validatable)
            throw new InvalidOperationException($"Unsupported DatabaseBackup contract '{command.GetType().Name}'.");
        validatable.Validate();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<DatabaseBackupCommandActor> context, IActorState state, ICommand command)
    {
        var dispatchContext = context;
        var aggregate = (DatabaseBackupCommandState)state;
        var operationId = command switch
        {
            DatabaseBackupCommand value => aggregate.Execute(value),
            DatabaseBackupInternalCommand value => aggregate.Execute(value),
            _ => throw new InvalidOperationException($"Unsupported DatabaseBackup command '{command.GetType().Name}'.")
        };
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(operationId.Value)));
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
