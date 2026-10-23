using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;

/// <summary>Observes real repeated API rejection logs for two minutes and delivers a notification without restarting live processes.</summary>
internal static class OptionNotificationVerification
{
    internal static async Task RunAsync(string logDirectory, string natsUrl, DateOnly session)
    {
        var observer = new OptionPipelineFailureObserver(TimeProvider.System);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        await using var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = natsUrl }, NullLogger<NatsJetStreamActorProducer>.Instance);
        await producer.StartAsync(new(ActorType.Event, nameof(OptionNotificationVerification)), deadline.Token);
        Console.WriteLine("Observing actual option mailbox Stopping rejections for 120 seconds; no process or lease changes.");
        try
        {
            while (true)
            {
                var file = new DirectoryInfo(logDirectory).GetFiles("ifm-apiserver-*.log").MaxBy(file => file.LastWriteTimeUtc)
                    ?? throw new InvalidOperationException("No API logs available.");
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                stream.Seek(Math.Max(0, stream.Length - 512 * 1024), SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var text = await reader.ReadToEndAsync(deadline.Token);
                var mailboxes = new Dictionary<ActorThreadId, ActorMailboxMetricsSnapshot>();
                const string prefix = "Realtime.IronCondorTradePositionRealtime.StrategyPositionChanged.";
                foreach (var line in text.Split('\n'))
                {
                    if (!line.Contains("Stopping", StringComparison.Ordinal) || !line.Contains(prefix, StringComparison.Ordinal)) continue;
                    JsonDocument document;
                    try { document = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                    using (document)
                    {
                        var root = document.RootElement;
                        var at = root.GetProperty("Timestamp").GetDateTimeOffset();
                        if (DateTimeOffset.UtcNow - at > TimeSpan.FromSeconds(15)) continue;
                        var properties = root.GetProperty("Properties");
                        if (properties.GetProperty("Reason").GetString() != "Stopping") continue;
                        var subject = properties.GetProperty("Subject").GetString()!;
                        if (!subject.StartsWith(prefix, StringComparison.Ordinal)) continue;
                        var id = new ActorThreadId(ActorType.Realtime, "IronCondorTradePositionRealtime", subject[prefix.Length..]);
                        mailboxes[id] = new(id, 0, 2048, 0, 0, 0, 0, 0, 0, 0, 1, false,
                            ActorMailboxLifecycleState.Paused, 1, false, "", null, null, null, null, "", "", 1,
                            LastRejectedUtc: at.UtcDateTime, LastRejectionReason: "Stopping");
                    }
                }
                foreach (var failure in observer.Observe(session, mailboxes.Values))
                {
                    await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, MarketDataFeedId>(failure.Subject, failure, deadline.Token);
                    observer.MarkPublished(failure.CommandId);
                    Console.WriteLine($"UI_FAILURE_PUBLISHED OperationId={failure.CommandId}; {failure.ErrorMessage}");
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
            }
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }
}
