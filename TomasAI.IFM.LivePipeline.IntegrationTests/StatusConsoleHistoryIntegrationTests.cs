using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using NATS.Client.JetStream.Models;
using NATS.Client.JetStream;
using NATS.Net;
using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Application.EventServices;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class StatusConsoleHistoryIntegrationTests
{
    [IsolatedRecoveryBrokerTheory(Timeout = 60000)]
    [InlineData(true)]
    public async Task Single_console_batch_is_removed_only_after_ack_and_unacknowledged_deliveries_survive_restart(bool isolated)
    {
        Assert.True(isolated);
        var url = Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")!;
        Assert.DoesNotContain(":4222", url);
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        var options = new NatsProducerOptions { Url = url };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await using var connection = new NatsConnectionManager();
        var js = await connection.GetJetStreamContextAsync(url, deadline.Token);
        // Exercise migration from the actual unlimited history policy.
        await js.CreateOrUpdateStreamAsync(new StreamConfig("IFM_STATUS_CONSOLE_HISTORY", ["Notify.StatusConsoleEvent.Logged.>"])
        { Storage = StreamConfigStorage.File, MaxMsgs = -1, MaxBytes = -1, MaxAge = TimeSpan.Zero }, deadline.Token);
        var stream = await StatusConsoleHistory.EnsureAsync(js, deadline.Token);
        Assert.Equal(StreamConfigRetention.Workqueue, stream.Info.Config.Retention);
        Assert.Equal(StreamConfigDiscard.Old, stream.Info.Config.Discard);
        Assert.Equal(10_000, stream.Info.Config.MaxMsgs);
        Assert.Equal(10 * 1024 * 1024, stream.Info.Config.MaxBytes);
        Assert.Equal(TimeSpan.FromMinutes(5), stream.Info.Config.MaxAge);
        var legacy = await js.GetStreamAsync("IFM_STATUS_CONSOLE_HISTORY", cancellationToken: deadline.Token);
        Assert.Equal(TimeSpan.FromMinutes(5), legacy.Info.Config.MaxAge);
        Assert.DoesNotContain("Notify.StatusConsoleEvent.Logged.>", legacy.Info.Config.Subjects);

        await using var producer = new StatusConsoleEventProducer(options, NullLogger.Instance, connection);
        var writer = new StatusConsoleWriter(producer);
        await writer.WriteConsoleAsync(LogSourceType.System, "one before UI");
        await writer.WriteConsoleAsync(LogSourceType.System, "two before UI");
        Assert.Equal(2L, (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.State.Messages);
        await using var liveClient = new NatsClient(url);
        await liveClient.ConnectAsync();
        var subscription = liveClient.SubscribeAsync<byte[]>(StatusConsoleHistory.DeliverySubject,
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: deadline.Token).GetAsyncEnumerator(deadline.Token);
        var first = new List<(StatusConsoleLoggedEvent Message, string Receipt)>();
        for (var index = 0; index < 2; index++)
        {
            Assert.True(await subscription.MoveNextAsync());
            first.Add((ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(subscription.Current.Data!)!, subscription.Current.ReplyTo!));
        }
        Assert.Equal(["one before UI", "two before UI"], first.Select(row => row.Message.StatusConsoleLog.Message));
        Assert.Equal(2L, (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.State.Messages);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(connection);
        builder.Services.AddSingleton<INatsProducerOptions>(options);
        await using var api = builder.Build();
        api.Urls.Add("http://127.0.0.1:0"); api.MapStatusConsole();
        await api.StartAsync(deadline.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(api.Urls.Single()) };
            var receipt = new StatusConsoleHistory.DisplayedBatch(first.Select(row => row.Receipt).ToArray());
            Assert.True((await http.PostAsync("/api/status-console/ack", JsonContent.Create(receipt), deadline.Token)).IsSuccessStatusCode);
            Assert.Equal(0L, (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.State.Messages);
            Assert.True((await http.PostAsync("/api/status-console/ack", JsonContent.Create(receipt), deadline.Token)).IsSuccessStatusCode);
            var invalid = await http.PostAsync("/api/status-console/ack",
                JsonContent.Create(new StatusConsoleHistory.DisplayedBatch(["Event.Trade.Delete"])), deadline.Token);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.True((await http.PostAsync("/api/status-console/connect", null, deadline.Token)).IsSuccessStatusCode);
            Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, (await http.GetAsync("/api/status-console/connect", deadline.Token)).StatusCode);

            await writer.WriteConsoleAsync(LogSourceType.System, "live push delivery");
            Assert.True(await subscription.MoveNextAsync());
            Assert.Equal("live push delivery", ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(subscription.Current.Data!)!.StatusConsoleLog.Message);
            await StatusConsoleHistory.AcknowledgeAsync(connection, options, new([subscription.Current.ReplyTo!]), deadline.Token);

            await writer.WriteConsoleAsync(LogSourceType.System, "UI crashed before display");
            Assert.True(await subscription.MoveNextAsync());
            var unacknowledgedId = ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(subscription.Current.Data!)!.Id;
            await subscription.DisposeAsync();
            await using var restartedApi = new NatsConnectionManager();
            await using var restartedClient = new NatsClient(url);
            await restartedClient.ConnectAsync();
            await using var restored = restartedClient.SubscribeAsync<byte[]>(StatusConsoleHistory.DeliverySubject,
                serializer: new NatsByteArrayMessageSerializer(), cancellationToken: deadline.Token).GetAsyncEnumerator(deadline.Token);
            // JetStream redelivers after AckWait without any client polling or fetch request.
            Assert.True(await restored.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15), deadline.Token));
            Assert.Equal(unacknowledgedId, ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(restored.Current.Data!)!.Id);
            await StatusConsoleHistory.AcknowledgeAsync(restartedApi, options, new([restored.Current.ReplyTo!]), deadline.Token);
            Assert.Equal(0L, (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.State.Messages);
        }
        finally { await api.StopAsync(deadline.Token); }

        // Accelerate only the isolated broker's limits to test eviction and expiry without a five-minute test sleep.
        // Production limits were asserted above and EnsureAsync restores them on the next delivery.
        var bounded = (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.Config;
        bounded.MaxMsgs = 3;
        await js.UpdateStreamAsync(bounded, deadline.Token);
        for (var index = 0; index < 6; index++) await PublishAsync("capacity " + index);
        var capped = await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token);
        Assert.Equal(3L, capped.Info.State.Messages);
        await using var capacityClient = new NatsClient(url);
        await capacityClient.ConnectAsync();
        var capacitySubscription = capacityClient.SubscribeAsync<byte[]>(StatusConsoleHistory.DeliverySubject,
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: deadline.Token).GetAsyncEnumerator(deadline.Token);
        var survivorMessages = new List<string>(); var survivorReceipts = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            Assert.True(await capacitySubscription.MoveNextAsync());
            survivorMessages.Add(ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(capacitySubscription.Current.Data!)!.StatusConsoleLog.Message);
            survivorReceipts.Add(capacitySubscription.Current.ReplyTo!);
        }
        Assert.Equal(["capacity 3", "capacity 4", "capacity 5"], survivorMessages);
        await StatusConsoleHistory.AcknowledgeAsync(connection, options, new(survivorReceipts.ToArray()), deadline.Token);
        await capacitySubscription.DisposeAsync();

        bounded = (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.Config;
        bounded.MaxAge = TimeSpan.FromMilliseconds(200);
        bounded.DuplicateWindow = TimeSpan.FromMilliseconds(100);
        await js.UpdateStreamAsync(bounded, deadline.Token);
        await PublishAsync("expires without a UI");
        await Task.Delay(TimeSpan.FromMilliseconds(600), deadline.Token);
        Assert.Equal(0L, (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.State.Messages);
        await StatusConsoleHistory.EnsureAsync(js, deadline.Token);

        bounded = (await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token)).Info.Config;
        bounded.MaxBytes = 1500;
        bounded.MaxMsgSize = 1024;
        await js.UpdateStreamAsync(bounded, deadline.Token);
        for (var index = 0; index < 10; index++) await PublishAsync(new string('x', 300) + index);
        var bytesCapped = await js.GetStreamAsync(StatusConsoleHistory.StreamName, cancellationToken: deadline.Token);
        Assert.InRange(bytesCapped.Info.State.Bytes, 1L, 1500L);
        Assert.InRange(bytesCapped.Info.State.Messages, 1L, 9L);

        async Task PublishAsync(string text)
        {
            var value = new StatusConsoleLoggedEvent { Id = Guid.NewGuid(), StatusConsoleLog = new StatusConsoleLogReadModel(DateTime.UtcNow, 0, LogSourceType.System, text) };
            (await js.PublishAsync(StatusConsoleHistory.Subject, value, serializer: NatsMessagePackSerializer<StatusConsoleLoggedEvent>.Default,
                cancellationToken: deadline.Token)).EnsureSuccess();
        }
    }
}
