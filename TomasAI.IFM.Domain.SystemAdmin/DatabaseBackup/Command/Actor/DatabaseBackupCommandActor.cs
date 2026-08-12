using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;

public class DatabaseBackupCommandActor(
    IEventProjector<DatabaseBackupCommandActor> eventProjector,
    ILogger<DatabaseBackupCommandActor> logger)
    : BaseEventSourceCommandActor<DatabaseBackupCommandActor>(logger, new ActorMailboxId(ActorType.Command, Actor))
{
    public const string Actor = DatabaseBackupCommand.Actor;
    IEventSourceActorStateRepository<DatabaseBackupCommandState> _repository = default!;
    readonly IEventProjector<DatabaseBackupCommandActor> _eventProjector = eventProjector;

    public static IReadOnlyCollection<Type> SupportedCommandTypes => CommandTypes;
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

    protected override ValueTask OnStartup(ICommandActorContext context)
        => StartAsync(context, CancellationToken.None);

    protected override ValueTask OnStartup(ICommandActorContext context, CancellationToken cancellationToken)
        => StartAsync(context, cancellationToken);

    async ValueTask StartAsync(ICommandActorContext context, CancellationToken cancellationToken)
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

    protected override ValueTask OnShutdown(ICommandActorContext context)
        => _eventProjector.StopAsync(CancellationToken.None);

    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: Actor }
            || !ParseMap.TryGetValue(message.Subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {Actor} command from message: {message.Subject}");
        return parser(message);
    }

    static ICommand ParseTyped<TCommand>(IActorMessage message) where TCommand : class, ICommand
        => message.AsCommand<TCommand>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TCommand).Name}.");

    protected override ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command)
    {
        if (command is not IDatabaseBackupValidatable validatable)
            throw new InvalidOperationException($"Unsupported DatabaseBackup contract '{command.GetType().Name}'.");
        validatable.Validate();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command)
    {
        var aggregate = (DatabaseBackupCommandState)state;
        var operationId = command switch
        {
            DatabaseBackupCommand value => aggregate.Execute(value),
            DatabaseBackupInternalCommand value => aggregate.Execute(value),
            _ => throw new InvalidOperationException($"Unsupported DatabaseBackup command '{command.GetType().Name}'.")
        };
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(operationId.Value)));
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken)
        => await _repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => _repository.SaveStateAsync(context, (DatabaseBackupCommandState)state, command);

    protected override ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken)
        => _repository.SaveStateAsync(context, (DatabaseBackupCommandState)state, command, cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9199, exception.Message));
}
