using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Registers and starts the actor runtime as one rollback-safe host operation.
/// </summary>
public static class ActorRuntimeStartup
{
    const string ServiceId = nameof(ActorRuntimeStartup);
    static readonly ActorType[] ConsumerTypes =
        [ActorType.Command, ActorType.Query];
    static readonly ActorType[] JetStreamConsumerTypes = [ActorType.Event];

    /// <summary>
    /// Registers actors, producers, and consumers, then starts the runtime.
    /// Any exception or cancellation shuts down everything registered or started by the attempt.
    /// </summary>
    public static async ValueTask StartAsync(
        IActorSupervisor supervisor,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        ArgumentNullException.ThrowIfNull(logger);
        var startedTimestamp = Stopwatch.GetTimestamp();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registry = supervisor.Container.Resolve<IActorRegistry>();
            var factory = supervisor.Container.Resolve<IActorFactory>();
            var actorTypes = registry.ActorTypes;
            var actors = new IActor[actorTypes.Length];

            for (var index = 0; index < actorTypes.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actor = factory.GetActor(actorTypes[index]);
                actors[index] = actor;
                supervisor.AddActor(actor);

                var producer = supervisor.Container.Resolve<IActorProducer>();
                if (producer is not null)
                    supervisor.AddProducer(actor.Id, producer);

                if (actor.Id.ActorType == ActorType.Event)
                {
                    var jetStreamProducer = supervisor.Container.Resolve<IJSActorProducer>();
                    if (jetStreamProducer is not null)
                        supervisor.AddJSProducer(actor.Id, jetStreamProducer);
                }
            }

            foreach (var consumerType in ConsumerTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var consumer = supervisor.Container.Resolve<IActorConsumer>();
                if (consumer is not null)
                    supervisor.AddConsumer(consumerType, consumer);
            }

            foreach (var consumerType in JetStreamConsumerTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var consumer = supervisor.Container.Resolve<IJSActorConsumer>();
                if (consumer is not null)
                    supervisor.AddConsumer(consumerType, consumer);
            }

            await supervisor.StartConsumersAsync(cancellationToken).ConfigureAwait(false);
            foreach (var actor in actors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await actor.StartAsync(supervisor, cancellationToken).ConfigureAwait(false);
                logger.LogInformationEvent(ServiceId, "Started {ActorType} actor.", actor.GetType().Name);
            }

            logger.LogInformationEvent(
                ServiceId,
                "Event model actor supervisor started with {ActorCount} actors.",
                actors.Length);
            ActorLifecycleMetrics.StartupCompleted.Add(1);
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                ActorLifecycleMetrics.RecordStartupCancellation();
            else
                ActorLifecycleMetrics.StartupFailures.Add(1);

            logger.LogError(exception, "Failed to start event model actor supervisor.");
            try
            {
                await supervisor.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollbackException)
            {
                ActorLifecycleMetrics.RecordCleanupFailure("startup_rollback");
                logger.LogError(rollbackException, "Failed to roll back event model actor supervisor startup.");
                throw;
            }
            throw;
        }
        finally
        {
            ActorLifecycleMetrics.StartupDuration.Record(
                Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds);
        }
    }
}
