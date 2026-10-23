using System.Text.Json;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;

/// <summary>Read-only independent observer. Never publishes an event or consumes/acknowledges the UI's durable queue.</summary>
internal static class OptionRecoveryFailureInspection
{
    internal static async Task RunAsync(string url, Guid operation, string output)
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        await using var client = new NatsClient(url);
        await client.ConnectAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var subject = $"Event.{MarketDataDatasetRecoveryFailedEvent.Actor}.{MarketDataDatasetRecoveryFailedEvent.Verb}.>";
        await using var subscription = await client.Connection.SubscribeCoreAsync<byte[]>(subject,
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token);
        await client.PingAsync(timeout.Token);
        Console.WriteLine($"Observing real published error for operation {operation}; no publications or UI consumer changes.");
        await foreach (var message in subscription.Msgs.ReadAllAsync(timeout.Token))
        {
            if (message.Data is null) continue;
            var failure = ActorExtensions.DataSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(message.Data);
            if (failure?.CommandId != operation) continue;
            var evidence = JsonSerializer.Serialize(new { ObservedUtc = DateTimeOffset.UtcNow, Subject = message.Subject,
                failure.CommandId, failure.Id, failure.ErrorDate, failure.ErrorMessage, failure.Role, failure.Dataset },
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(output, evidence, timeout.Token);
            Console.WriteLine(evidence);
            return;
        }
    }
}
