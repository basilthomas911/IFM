using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Event;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Event.Extensions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Event.Actor;

/// <summary>Provides the DatabaseBackupServiceEventActor implementation.</summary>
public class DatabaseBackupServiceEventActor(
    IEventActorContext<DatabaseBackupServiceEventActor> actorContext)
    : BaseEventActor<DatabaseBackupServiceEventActor>(actorContext, actorContext.Logger)
{
    internal const string HandlerErrorLoggedKey = "IFM.DatabaseBackup.EventHandlerErrorLogged";
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IDatabaseBackupServiceEventContext ActorContext =>
        IsArgumentNull.Named(Context as IDatabaseBackupServiceEventContext, nameof(Context))!;

    public const string Actor = "DatabaseBackupServiceEvent";

    /// <summary>Gets the supported service-event types.</summary>
    public static IReadOnlyCollection<Type> SupportedServiceEventTypes =>CachedSupportedServiceEventTypes.Value;
    /// <summary>Gets the supported service-event verbs.</summary>
    public static IReadOnlyCollection<string> SupportedVerbs =>CachedSupportedVerbs.Value;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            ["ServiceCapabilityChanged"] = static message => message.AsEvent<DatabaseBackupServiceCapabilityChangedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupServiceEventActor>, ILogger<DatabaseBackupServiceEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<DatabaseBackupServiceEventActor>, ILogger<DatabaseBackupServiceEventActor>, ValueTask>>
        {
            [typeof(DatabaseBackupServiceCapabilityChangedEvent)] = static (eventValue, context, logger) => ((DatabaseBackupServiceCapabilityChangedEvent)eventValue).ExecuteAsync(context, logger)
        };

    protected override IEvent ParseMessage(IEventActorContext<DatabaseBackupServiceEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IEventActorContext<DatabaseBackupServiceEventActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context, context.Logger);
    }

    protected override ValueTask OnExceptionAsync(IEventActorContext<DatabaseBackupServiceEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (!ex.Data.Contains(HandlerErrorLoggedKey))
            global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write9(Context.Logger, ex, nameof(DatabaseBackupServiceEventActor), nameof(OnExceptionAsync), @event?.EventName);
        return ValueTask.CompletedTask;
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedServiceEventTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
private static readonly System.Lazy<IReadOnlyCollection<string>> CachedSupportedVerbs = new(() => System.Array.AsReadOnly(_parseMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
