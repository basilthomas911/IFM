using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Extensions;

using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;

/// <summary>Provides the DatabaseBackupEventActor implementation.</summary>
public class DatabaseBackupEventActor(
    IEventActorContext<DatabaseBackupEventActor> actorContext)
    : BaseEventActor<DatabaseBackupEventActor>(actorContext.Supervisor, actorContext.Logger, actorContext.ActorId)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupEventContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IDatabaseBackupEventContext, nameof(actorContext))!;

    public const string Actor = "DatabaseBackupEvent";

    /// <summary>Gets the SupportedServiceEventTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes => ServiceEventTypes;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs => ParseMap.Keys;

    static readonly Type[] ServiceEventTypes =
    [
        typeof(DatabaseBackupServiceAcceptedEvent), typeof(DatabaseBackupServiceRejectedEvent), typeof(DatabaseBackupServiceStartedEvent),
        typeof(DatabaseBackupServiceProgressEvent), typeof(DatabaseBackupBoundaryEstablishedEvent), typeof(DatabaseBackupArtifactReplicaUpdatedEvent),
        typeof(DatabaseBackupVerificationCompletedEvent), typeof(DatabaseBackupServiceErrorEvent), typeof(DatabaseBackupServiceCompletedEvent),
        typeof(DatabaseBackupServiceFailedEvent), typeof(DatabaseBackupServiceCancelledEvent), typeof(DatabaseRestoreServiceAcceptedEvent),
        typeof(DatabaseRestoreServiceRejectedEvent), typeof(DatabaseRestoreServiceStartedEvent), typeof(DatabaseRestoreServiceProgressEvent),
        typeof(DatabaseRestoreValidationCompletedEvent), typeof(DatabaseRestoreReadyForCutoverEvent), typeof(DatabaseRestoreDrillCompletedEvent),
        typeof(DatabaseRestoreServiceErrorEvent), typeof(DatabaseRestoreServiceCompletedEvent), typeof(DatabaseRestoreServiceFailedEvent),
        typeof(DatabaseRestoreServiceCancelledEvent), typeof(DatabaseRecoveryRunStatisticsCapturedEvent), typeof(DatabaseBackupPolicyAppliedEvent),
        typeof(DatabaseBackupPolicyRejectedEvent), typeof(DatabaseRetentionPlanCreatedEvent), typeof(DatabaseRetentionExecutionCompletedEvent),
        typeof(DatabaseRetentionExecutionFailedEvent), typeof(DatabaseBackupServiceReconciliationEvent), typeof(DatabaseBackupServiceCapabilityChangedEvent)
    ];
    static readonly MethodInfo ParseMethod = typeof(DatabaseBackupEventActor).GetMethod(nameof(ParseTyped), BindingFlags.Static | BindingFlags.NonPublic)!;
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> ParseMap = ServiceEventTypes.ToDictionary(
        type => ((DatabaseBackupServiceEventContract)Activator.CreateInstance(type)!).Verb,
        type => (Func<IActorMessage, IEvent>)ParseMethod.MakeGenericMethod(type).CreateDelegate(typeof(Func<IActorMessage, IEvent>)),
        StringComparer.Ordinal);

    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Event, Name: Actor }
            || !ParseMap.TryGetValue(message.Subject.Verb, out var parser))
            return default!;
        return parser(message);
    }

    static IEvent ParseTyped<TEvent>(IActorMessage message) where TEvent : class, IEvent
        => message.AsEvent<TEvent>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TEvent).Name}.");

    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        var dispatchContext = actorContext.RouteTo(context);
        if (@event is not DatabaseBackupServiceEventContract serviceEvent)
            throw new InvalidOperationException($"Unsupported DatabaseBackup event '{@event.GetType().Name}'.");
        var command = DatabaseBackupEventTranslator.Translate(serviceEvent);
        var result = await RequestAsync(context, command).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"DatabaseBackup command rejected: {result.ErrorMessage}");
    }

    static ValueTask<ServiceResult<GuidResult>> RequestAsync(IEventActorContext context, DatabaseBackupInternalCommand command)
        => command switch
        {
            RecordDatabaseOperationAdmissionCommand value => context.RequestAsync<RecordDatabaseOperationAdmissionCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationStartedCommand value => context.RequestAsync<RecordDatabaseOperationStartedCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationProgressCommand value => context.RequestAsync<RecordDatabaseOperationProgressCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupBoundaryCommand value => context.RequestAsync<RecordDatabaseBackupBoundaryCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseArtifactReplicaCommand value => context.RequestAsync<RecordDatabaseArtifactReplicaCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationVerificationCommand value => context.RequestAsync<RecordDatabaseOperationVerificationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationErrorCommand value => context.RequestAsync<RecordDatabaseOperationErrorCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRestoreReadyForCutoverCommand value => context.RequestAsync<RecordDatabaseRestoreReadyForCutoverCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            CompleteDatabaseOperationCommand value => context.RequestAsync<CompleteDatabaseOperationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            FailDatabaseOperationCommand value => context.RequestAsync<FailDatabaseOperationCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseOperationCancelledCommand value => context.RequestAsync<RecordDatabaseOperationCancelledCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupPolicyStatusCommand value => context.RequestAsync<RecordDatabaseBackupPolicyStatusCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRetentionResultCommand value => context.RequestAsync<RecordDatabaseRetentionResultCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            ReconcileDatabaseBackupServiceStateCommand value => context.RequestAsync<ReconcileDatabaseBackupServiceStateCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseBackupServiceCapabilityCommand value => context.RequestAsync<RecordDatabaseBackupServiceCapabilityCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            RecordDatabaseRecoveryRunStatisticsCommand value => context.RequestAsync<RecordDatabaseRecoveryRunStatisticsCommand, Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId>(value),
            _ => throw new InvalidOperationException($"Unsupported translated command '{command.GetType().Name}'.")
        };

    protected override ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception exception)
    {
        actorContext.Logger.LogError(exception, "DatabaseBackup service event {EventName} failed.", @event?.EventName);
        return ValueTask.CompletedTask;
    }
}
