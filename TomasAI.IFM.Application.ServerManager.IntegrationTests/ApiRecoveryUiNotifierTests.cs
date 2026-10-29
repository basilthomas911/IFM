using System.Diagnostics;
using NATS.Client.JetStream.Models;
using NATS.Net;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ApiRecoveryUiNotifierTests
{
    [Fact(Timeout=45000)]
    public async Task External_failure_reaches_durable_UI_event_channel_without_a_running_API()
    {
        var container="ifm-api-restart-test-"+Guid.NewGuid().ToString("N");
        async Task<string> Docker(params string[] arguments) {
            var start=new ProcessStartInfo("docker"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            foreach(var arg in arguments)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));await process.WaitForExitAsync(timeout.Token);
            var result=await stdout;var error=await stderr;if(process.ExitCode!=0)throw new InvalidOperationException(error);return result;
        }
        try
        {
            await Docker("run","--detach","--rm","--name",container,"-p","127.0.0.1::4222","nats:2.12.0-alpine","-js");
            var port=(await Docker("port",container,"4222/tcp")).Trim().Split(':').Last();var url="nats://127.0.0.1:"+port;
            await using var client=new NatsClient(url);await client.ConnectAsync();var js=client.CreateJetStreamContext();
            var date=DateOnly.FromDateTime(DateTime.UtcNow);var id=new MarketDataFeedId(date);
            var subject=new ActorSubject(ActorType.Event,MarketDataDatasetRecoveryFailedEvent.Actor,MarketDataDatasetRecoveryFailedEvent.Verb,id.Format()).ToString();
            var stream=await js.CreateStreamAsync(new StreamConfig("API_RESTART_UI_TEST",[subject]));
            var consumer=await stream.CreateOrUpdateConsumerAsync(new ConsumerConfig("ui"){AckPolicy=ConsumerConfigAckPolicy.Explicit,FilterSubject=subject});
            var result=new ApiRecoveryRestartResult(Guid.NewGuid(),date,"Failed",100,null,DateTime.UtcNow,"API recovery failed: test startup timeout");
            var audit=new List<string>();
            using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(10));await ApiRecoveryUiNotifier.SendAsync(url,result,budget.Token,audit.Add);
            Assert.Equal(2,audit.Count);
            var published=System.Text.Json.JsonDocument.Parse(audit[1]).RootElement;
            Assert.Equal("backend.failure_notification_published",published.GetProperty("Event").GetString());
            Assert.Equal(result.OperationId,published.GetProperty("OperationId").GetGuid());
            Assert.Equal(subject,published.GetProperty("Subject").GetString());
            Assert.Equal(result.Error,published.GetProperty("Detail").GetString());
            var received=false;
            await foreach(var message in consumer.ConsumeAsync<byte[]>(serializer:new NatsByteArrayMessageSerializer(),cancellationToken:budget.Token)) {
                var failure=ActorExtensions.DataSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(message.Data!)!;
                Assert.Equal(result.OperationId,failure.CommandId);Assert.Equal(result.Error,failure.ErrorMessage);Assert.Equal(nameof(ApiRecoveryUiNotifier),failure.EventSource);
                await message.AckAsync(cancellationToken:budget.Token);received=true;break;
            }
            Assert.True(received);
        }
        finally { await Docker("rm","--force",container); }
    }
}
