using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public interface IDatabaseBackupServiceEventTransport
{
    ValueTask StartAsync(CancellationToken cancellationToken);
    ValueTask PublishAsync(DatabaseBackupServiceEventContract @event, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
}

public sealed class JetStreamDatabaseBackupServiceEventTransport(IJSActorProducer producer)
    : IDatabaseBackupServiceEventTransport
{
    static readonly MethodInfo SendTypedMethod = typeof(JetStreamDatabaseBackupServiceEventTransport)
        .GetMethod(nameof(SendTypedAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    public ValueTask StartAsync(CancellationToken cancellationToken)
        => producer.StartAsync(new ActorMailboxId(ActorType.Event, "DatabaseBackupEvent"), cancellationToken);

    public ValueTask PublishAsync(DatabaseBackupServiceEventContract @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var method = SendTypedMethod.MakeGenericMethod(@event.GetType());
        return (ValueTask)method.Invoke(null, [producer, @event, cancellationToken])!;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) => producer.StopAsync(cancellationToken);

    static ValueTask SendTypedAsync<TEvent>(
        IJSActorProducer producer,
        DatabaseBackupServiceEventContract @event,
        CancellationToken cancellationToken)
        where TEvent : class, Shared.EventSourcing.IEvent<DatabaseRecoveryOperationId>
        => producer.SendAsync<TEvent, DatabaseRecoveryOperationId>(
            @event.Subject, (TEvent)(object)@event, cancellationToken);
}
