using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.ServerManager;

/// <summary>Publishes from outside the API, including when the replacement API never starts.</summary>
public static class ApiRecoveryUiNotifier
{
    public static async Task SendAsync(string url, ApiRecoveryRestartResult result, CancellationToken token, Action<string>? audit = null)
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        await using var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url=url }, NullLogger<NatsJetStreamActorProducer>.Instance);
        var id = new MarketDataFeedId(result.ValueDate);
        var failure = new MarketDataDatasetRecoveryFailedEvent {
            Id=Guid.NewGuid(), CommandId=result.OperationId, EntityId=id,
            Subject=new(ActorType.Event,MarketDataDatasetRecoveryFailedEvent.Actor,MarketDataDatasetRecoveryFailedEvent.Verb,id.Format()),
            EventSource=nameof(ApiRecoveryUiNotifier), Role="Options", Dataset="GLBX.MDP3", ErrorCode=503,
            ErrorType=ErrorType.System, ErrorMessage=result.Error!, ErrorDate=DateTime.UtcNow, ReceivedOn=DateTime.UtcNow };
        // Completion means producer publication, not user acknowledgement.
        void Log(string stage, string? error = null) => audit?.Invoke(JsonSerializer.Serialize(new {
            TimestampUtc=DateTimeOffset.UtcNow, Event=stage, Subject=failure.Subject.ToString(),
            OperationId=failure.CommandId, EventId=failure.Id, Detail=failure.ErrorMessage, Error=error }));
        Log("backend.failure_notification_publishing");
        try {
            await producer.StartAsync(new(ActorType.Event, nameof(ApiRecoveryUiNotifier)), token);
            await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent,MarketDataFeedId>(failure.Subject,failure,token);
            Log("backend.failure_notification_published");
        }
        catch(Exception error) { Log("backend.failure_notification_publish_failed", error.ToString()); throw; }
    }
}
