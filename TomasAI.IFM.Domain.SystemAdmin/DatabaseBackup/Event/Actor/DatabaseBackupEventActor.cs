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
    : BaseEventActor<DatabaseBackupEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupEventContext ActorContext =>
        IsArgumentNull.Set(Context as IDatabaseBackupEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupEvent";

    /// <summary>Gets the SupportedServiceEventTypes value.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes => ServiceEventTypes;
    /// <summary>Gets the SupportedVerbs value.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>
        _parseMap.Keys as IReadOnlyCollection<string> ?? _parseMap.Keys.ToArray();

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
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = ServiceEventTypes.ToDictionary(
        type => ((DatabaseBackupServiceEventContract)Activator.CreateInstance(type)!).Verb,
        type => (Func<IActorMessage, IEvent>)ParseMethod.MakeGenericMethod(type).CreateDelegate(typeof(Func<IActorMessage, IEvent>)),
        StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<
        IEvent,
        IEventActorContext<DatabaseBackupEventActor>,
        ValueTask>> _receiveMap = ServiceEventTypes.ToDictionary(
            type => type,
            _ => (Func<IEvent, IEventActorContext<DatabaseBackupEventActor>, ValueTask>)ReceiveServiceEventAsync);

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    static IEvent ParseTyped<TEvent>(IActorMessage message) where TEvent : class, IEvent
        => message.AsEvent<TEvent>() ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TEvent).Name}.");

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupEventActor> context, IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context);
    }

    static async ValueTask ReceiveServiceEventAsync(
        IEvent @event,
        IEventActorContext<DatabaseBackupEventActor> context)
    {
        var serviceEvent = (DatabaseBackupServiceEventContract)@event;
        var command = DatabaseBackupEventTranslator.Translate(serviceEvent);
        var result = await RequestAsync(context, command).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"DatabaseBackup command rejected: {result.ErrorMessage}");
    }

    static ValueTask<ServiceResult<GuidResult>> RequestAsync(IEventActorContext<DatabaseBackupEventActor> context, DatabaseBackupInternalCommand command)
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

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupEventActor> context, ActorThreadId threadId, IEvent @event, Exception exception)
    {
        Context.Logger.LogError(exception, "DatabaseBackup service event {EventName} failed.", @event?.EventName);
        return ValueTask.CompletedTask;
    }
}
