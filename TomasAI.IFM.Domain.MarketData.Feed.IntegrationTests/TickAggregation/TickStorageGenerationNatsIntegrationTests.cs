using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>Disposable broker qualification: worker generation headers survive actor intake through a completed DB call.</summary>
[Trait("Infrastructure", "SelfContained")]
public sealed class TickStorageGenerationNatsIntegrationTests
{
    [IsolatedNatsFact]
    public async Task Candidate_generation_reaches_tick_projector_after_real_Nats_delivery_and_database_write()
    {
        int port;
        using (var reservation = new TcpListener(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }
        var id = (await Docker("run", "-d", "--pull", "never", "--label", "ifm.test=dhr07-generation-proof",
            "-p", $"127.0.0.1:{port}:4222", "nats:2.12.0-alpine")).Trim();
        Assert.Matches("^[a-f0-9]{64}$", id);
        try
        {
            var address = (await Docker("port", id, "4222/tcp")).Trim();
            Assert.Matches("^127[.]0[.]0[.]1:[0-9]+$", address);
            var url = $"nats://{address}";
            var generation = Guid.NewGuid();
            var evidence = new TickStorageGenerationEvidence();
            evidence.Arm(new("GLBX.MDP3", generation));
            var db = Substitute.For<IMarketDataDbContext>();
            var factory = Substitute.For<IDbContextFactory>();
            factory.MarketDataDb.Returns(db);
            var written = new TaskCompletionSource<FuturesTickTradeDataInsertedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            db.InsertTickTradeDataAsync(Arg.Any<FuturesTickTradeDataInsertedEvent>())
                .Returns(call =>
                {
                    written.TrySetResult(call.Arg<FuturesTickTradeDataInsertedEvent>());
                    return Task.CompletedTask;
                });

            var supervisor = Substitute.For<IActorSupervisor>();
            var queues = Substitute.For<IActorThreadQueues>();
            var mailbox = Substitute.For<IActorMailbox>();
            mailbox.ThreadQueues.Returns(queues);
            supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(mailbox);
            supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
            var projector = new TickAggregationRealtimeProjector(factory,
                Substitute.For<ILogger<TickAggregationRealtimeProjector>>(),
                new LivePipelineEvidence(TimeProvider.System), evidence);
            var actor = new TickAggregationRealtimeActor(new TickAggregationRealtimeContext(
                supervisor, Substitute.For<ILogger<TickAggregationRealtimeActor>>(), projector));
            supervisor.ActorExists(actor.Id).Returns(true);
            supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [actor.Id] = actor });
            queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(),
                    Arg.Any<CancellationToken>())
                .Returns(call => AdmitAsync(actor, delivered, call));
            var consumer = new NatsActorConsumer(new NatsConsumerOptions
            {
                Url = url, DispatcherCount = 1, DispatcherCapacity = 16, SubscriptionCapacity = 16,
                FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
                { [ActorType.Realtime] = CoreNatsTrafficClass.Optional }
            }, Substitute.For<ILogger>());
            var producer = new NatsActorProducer(new NatsProducerOptions { Url = url },
                Substitute.For<ILogger>());
            var valueDate = new DateOnly(2026, 9, 30);
            var entity = new TickDataEntityId("ESZ26", valueDate, AssetTypeId.Futures);
            var source = new FuturesTickTradeDataChangedEvent
            {
                Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                    FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
                Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(),
                TickDataId = new("ESZ26", valueDate, 1, DateTime.UtcNow),
                AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3",
                SourceDataset = "GLBX.MDP3", SourceGenerationId = generation,
                EventSource = nameof(TickStorageGenerationNatsIntegrationTests),
                ReceivedOn = DateTime.UtcNow
            };
            try
            {
                await actor.StartAsync(supervisor);
                await consumer.StartAsync(supervisor, ActorType.Realtime,
                    $"tick-generation-{Guid.NewGuid():N}");
                await producer.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(
                    source.Subject, source);
                var stored = await written.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(generation, stored.SourceGenerationId);
                Assert.Equal(source.Dataset, stored.SourceDataset);
                Assert.Equal(1, evidence.Capture().CompletedWrites);
            }
            finally
            {
                await producer.StopAsync();
                await consumer.StopAsync();
                await actor.StopAsync();
            }
        }
        finally { await Docker("rm", "-f", id); }
    }

    static async ValueTask<ActorAdmissionResult> AdmitAsync(
        TickAggregationRealtimeActor actor, TaskCompletionSource delivered, NSubstitute.Core.CallInfo call)
    {
        var message = call.Arg<IActorMessage>();
        await actor.HandleMessageAsync(message, call.Arg<ActorSubject>().ThreadId);
        delivered.TrySetResult();
        return ActorAdmissionResult.AcceptedResult;
    }

    static async Task<string> Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        if (process.ExitCode != 0) throw new InvalidOperationException($"Isolated broker failed: {await error}");
        return await output;
    }
}
