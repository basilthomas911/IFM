using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.Model;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class StatusConsoleHistoryIntegrationTests
{
    [IsolatedRecoveryBrokerTheory(Timeout = 45000)]
    [InlineData(true)]
    public async Task Console_written_before_UI_is_retained_and_paged_through_live_overlap_and_API_reconnect(bool isolated)
    {
        Assert.True(isolated);
        var url = Environment.GetEnvironmentVariable("IFM_RECOVERY_TEST_NATS_URL")!;
        Assert.DoesNotContain(":4222", url);
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        var options = new NatsProducerOptions { Url = url };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await using var connection = new NatsConnectionManager();
        await using var producer = new StatusConsoleEventProducer(options, NullLogger.Instance, connection);
        var writer = new StatusConsoleWriter(producer);
        var js = await connection.GetJetStreamContextAsync(url, deadline.Token);
        var stream = await StatusConsoleHistory.EnsureAsync(js, deadline.Token);
        Assert.Equal(NATS.Client.JetStream.Models.StreamConfigStorage.File, stream.Info.Config.Storage);
        Assert.Equal(-1, stream.Info.Config.MaxMsgs);
        var start = stream.Info.State.LastSeq;
        var prefix = Guid.NewGuid().ToString("N");
        // No UI subscription exists while these messages are written.
        await writer.WriteConsoleAsync(LogSourceType.System, prefix + "-one");
        await writer.WriteConsoleAsync(LogSourceType.System, 123, prefix + "-two");
        var first = await StatusConsoleHistory.ReadAsync(connection, options, start, null, 1, deadline.Token);
        Assert.True(first.HasMore);
        Assert.Single(first.Messages);
        Assert.Equal(prefix + "-one", first.Messages[0].Message);

        await using var liveClient = new NatsClient(url);
        await liveClient.ConnectAsync();
        var live = liveClient.SubscribeAsync<byte[]>("Notify.StatusConsoleEvent.Logged.>",
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: deadline.Token).GetAsyncEnumerator(deadline.Token);
        await using var ownedSubscription = live;
        var pendingLive = live.MoveNextAsync().AsTask();
        await liveClient.PingAsync(deadline.Token);
        await writer.WriteConsoleAsync(LogSourceType.System, prefix + "-live");
        Assert.True(await pendingLive.WaitAsync(deadline.Token));
        var liveMessage = ActorExtensions.DataSerializer.Deserialize<StatusConsoleLoggedEvent>(live.Current.Data!)!;
        Assert.Equal(prefix + "-live", liveMessage.StatusConsoleLog.Message);

        // A fixed watermark excludes messages that arrived while the UI replayed.
        var second = await StatusConsoleHistory.ReadAsync(connection, options, first.NextSequence, first.ThroughSequence, 1, deadline.Token);
        Assert.False(second.HasMore);
        Assert.Contains(prefix + "-two", second.Messages.Single().Message);
        Assert.NotEqual(first.Messages[0].Id, second.Messages[0].Id);
        Assert.NotEqual(Guid.Empty, first.Messages[0].Id);

        // A fresh API connection resumes the cursor; persisted and live identities match.
        await using var restartedApi = new NatsConnectionManager();
        var tail = await StatusConsoleHistory.ReadAsync(restartedApi, options, second.NextSequence, null, 100, deadline.Token);
        Assert.False(tail.HasMore);
        Assert.Equal(liveMessage.Id, tail.Messages.Single().Id);
        Assert.Equal(prefix + "-live", tail.Messages[0].Message);
        var empty = await StatusConsoleHistory.ReadAsync(restartedApi, options, tail.NextSequence, null, 100, deadline.Token);
        Assert.Empty(empty.Messages);
        await Assert.ThrowsAsync<ArgumentException>(() => StatusConsoleHistory.ReadAsync(restartedApi, options, tail.NextSequence + 1, tail.NextSequence, 100, deadline.Token));

        // Exercise the actual HTTP endpoint without starting any trading actors.
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(restartedApi);
        builder.Services.AddSingleton<INatsProducerOptions>(options);
        await using var api = builder.Build();
        api.Urls.Add("http://127.0.0.1:0");
        api.MapStatusConsole();
        await api.StartAsync(deadline.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(api.Urls.Single()) };
            var response = await http.GetFromJsonAsync<StatusConsoleHistory.Page>($"/api/status-console?after={start}&limit=1", deadline.Token);
            Assert.Equal(first.Messages[0].Id, response!.Messages[0].Id);
            Assert.True(response.HasMore);
            var invalid = await http.GetAsync("/api/status-console?limit=0", deadline.Token);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally { await api.StopAsync(deadline.Token); }
    }
}
