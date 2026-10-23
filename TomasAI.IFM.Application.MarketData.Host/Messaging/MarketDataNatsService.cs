using System.Diagnostics;
using TomasAI.IFM.Shared.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using System.Collections.Concurrent;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Runs independent bounded command and emergency-stop subscribers.</summary>
public sealed class MarketDataNatsService(MarketDataWorkerHost host, MarketDataHostConnection connection,
    IConfiguration configuration, ILogger<MarketDataNatsService> logger, IHostApplicationLifetime? lifetime = null,
    MarketDataApiBackend? backend = null, IHostEnvironment? environment = null) : BackgroundService
{
    static readonly ActivitySource TraceSource = new(OptionFeedTrace.SourceName);
    readonly ConcurrentDictionary<Guid, Lazy<Task<MarketDataHostReply>>> operations = new();
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        ListenAsync(connection.Scope + ".control", 8, stoppingToken), ListenAsync(connection.Scope + ".stop", 2, stoppingToken),
        ListenAsync(connection.Scope + ".observe", 2, stoppingToken));
    async Task ListenAsync(string subject, int concurrency, CancellationToken token)
    {
        var client = await connection.GetClientAsync(token).ConfigureAwait(false);
        await using var sub = await client.Connection.SubscribeCoreAsync<MarketDataHostRequest>(subject,
            serializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default, cancellationToken: token).ConfigureAwait(false);
        await Parallel.ForEachAsync(sub.Msgs.ReadAllAsync(token), new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = token },
            async (message, ct) =>
            {
                try
                {
                    var request = message.Data ?? throw new InvalidDataException("Missing market-data request.");
                    using var remote = TraceSource.StartActivity("marketdata.host.receive", ActivityKind.Server, ActorTrace.Extract(message.Headers));
                    remote?.SetTag("rpc.method", request.Path);
                    remote?.SetTag("rpc.operation_id", request.OperationId);
                    using var requestTrace = request.Path.Contains("Option", StringComparison.Ordinal) || request.Path.Contains("TradeFeedRecovery", StringComparison.Ordinal)
                        ? new OptionFeedTrace("option.host.request." + request.Path, logger, request.OperationId.ToString()) : null;
                    MarketDataHostReply reply;
                    if (request.Path.EndsWith("/stop", StringComparison.Ordinal))
                    {
                        // A full ordinary-command deduplication table must never block containment.
                        reply = await DispatchAsync(request, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        if (operations.Count > 1024)
                            foreach (var pair in operations.Where(pair => pair.Value.IsValueCreated && pair.Value.Value.IsCompleted).Take(256)) operations.TryRemove(pair.Key, out _);
                        if (operations.Count > 2048) throw new InvalidOperationException("Market-data control capacity exhausted.");
                        reply = await operations.GetOrAdd(request.OperationId, _ => new Lazy<Task<MarketDataHostReply>>(() => DispatchAsync(request, ct))).Value.ConfigureAwait(false);
                    }
                    if (reply.Error is null) requestTrace?.Complete();
                    else requestTrace?.Fail(new InvalidOperationException(reply.Error));
                    await message.ReplyAsync(reply, serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: ct).ConfigureAwait(false);
                    if (request.Path == "qualification/shutdown" && reply.Error is null
                        && configuration["MarketDataHost:DataSource"] == "Synthetic")
                    {
                        await client.PingAsync(ct).ConfigureAwait(false);
                        lifetime?.StopApplication();
                    }
                }
                catch (Exception error) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning(error, "MarketData Host command failed");
                    await message.ReplyAsync(new MarketDataHostReply([], error.Message),
                        serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: ct).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }
    async Task<MarketDataHostReply> DispatchAsync(MarketDataHostRequest request, CancellationToken token)
    {
        var recovery = MarketDataHostConnection.IsRecoveryRequest(request.Path);
        var remaining = request.DeadlineUtc - DateTime.UtcNow;
        if (!recovery && remaining <= TimeSpan.Zero) return new([], "Market-data request deadline expired.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        var maximum = request.Path == "api/RefreshInstrumentDefinitionsAsync" ? TimeSpan.FromMinutes(30)
            : request.Path == "api/LoadHistoricalAsync" ? TimeSpan.FromMinutes(25)
            : request.Path.EndsWith("/start", StringComparison.Ordinal) || request.Path is "api/StartAsync" or "api/ReadFiveMinuteSeedBarsAsync" or "api/RefreshOptionCalendarAsync"
            ? TimeSpan.FromSeconds(180) : request.Path is "api/AcquireOptionScopeAsync" or "api/PrepareCompositionAsync" ? TimeSpan.FromSeconds(90)
            : TimeSpan.FromSeconds(30);
        if (!recovery) deadline.CancelAfter(remaining < maximum ? remaining : maximum);
        try
        {
            if (request.Path == "ready") return Reply(host.IsReady);
            if (request.Path == "qualification/shutdown" && configuration["MarketDataHost:DataSource"] == "Synthetic")
            {
                await host.StopAsync(deadline.Token).ConfigureAwait(false);
                return Reply(true);
            }
            if (request.Path.StartsWith("api/", StringComparison.Ordinal) && backend is not null)
            {
                var call = MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Remote.MarketDataApiCall>(request.Payload);
                return new(await new TomasAI.IFM.Application.MarketData.Remote.MarketDataApiDispatcher(backend)
                    .InvokeAsync(call, deadline.Token).ConfigureAwait(false), null);
            }
            if (request.Path == "api/GetBackendStatusAsync") return Reply(host.GetBackendStatus());
            if (request.Path == "api/InjectDevelopmentOptionWorkerFaultAsync")
            {
                var call = MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Remote.MarketDataApiCall>(request.Payload);
                return Reply(await host.InjectDevelopmentFaultAsync(MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Contracts.MarketDataDevelopmentFaultRequest>(call.Arguments[0]),
                    environment ?? throw new InvalidOperationException("Missing Host environment."), deadline.Token).ConfigureAwait(false));
            }
            if (request.Path == "api/RecoverMarketDataAsync")
            {
                var call = MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Remote.MarketDataApiCall>(request.Payload);
                return Reply(await host.RecoverAsync(MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryRequest>(call.Arguments[0]), deadline.Token).ConfigureAwait(false));
            }
            var path = request.Path.Split('/');
            if (path.Length < 3 || path[0] != "workers") throw new InvalidDataException("Unknown market-data operation.");
            var role = path[1];
            if (path.Length == 3 && path[2] == "start")
                return Reply(await host.StartWorkerAsync(role, MarketDataHostConnection.Decode<MarketDataWorkerStart>(request.Payload), deadline.Token).ConfigureAwait(false));
            if (path.Length != 4 || !Guid.TryParse(path[2], out var id)) throw new InvalidDataException("Missing worker identity.");
            if (path[3] == "stop") { await host.StopWorkerAsync(role, id).ConfigureAwait(false); return Reply(true); }
            var worker = host.RequireWorker(role, id);
            object result = path[3] switch
            {
                "api" => await worker.InvokeMarketDataApiAsync(MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Remote.MarketDataApiCall>(request.Payload), deadline.Token).ConfigureAwait(false),
                "probe" => (await worker.ProbeHealthAsync(deadline.Token).ConfigureAwait(false)) with { BootstrapToken = "" },
                "health" => (await worker.GetHealthAsync(deadline.Token).ConfigureAwait(false)) with { BootstrapToken = "" },
                "reset" => await host.ResetOwnedWorkerAsync(role, id, MarketDataHostConnection.Decode<DatasetSubscriptionManifest>(request.Payload), deadline.Token).ConfigureAwait(false),
                "manifest" => (await worker.ApplyManifestAsync(MarketDataHostConnection.Decode<DatasetSubscriptionManifest>(request.Payload), deadline.Token).ConfigureAwait(false)) with { BootstrapToken = "" },
                "acquire" => await host.AcquireOptionLeaseAsync(role, id, MarketDataHostConnection.Decode<WorkerOptionChainRequest>(request.Payload), deadline.Token).ConfigureAwait(false),
                "release" => await host.ReleaseOptionLeaseAsync(role, id, MarketDataHostConnection.Decode<WorkerOptionChainRelease>(request.Payload), deadline.Token).ConfigureAwait(false),
                "ownership" => await worker.ApplyOptionChainOwnershipAsync(MarketDataHostConnection.Decode<WorkerOptionChainRelease>(request.Payload), deadline.Token).ConfigureAwait(false),
                "capture" => await worker.CaptureCompositionSnapshotAsync(MarketDataHostConnection.Decode<CompositionSnapshotRequest>(request.Payload), deadline.Token).ConfigureAwait(false),
                "hang" when configuration["MarketDataHost:DataSource"] == "Synthetic" => await HangAsync(worker, deadline.Token).ConfigureAwait(false),
                _ => throw new InvalidDataException("Unsupported market-data operation.")
            };
            return Reply(result);
        }
        catch (Exception error) when (!token.IsCancellationRequested) { return new([], error.Message); }
    }
    static MarketDataHostReply Reply<T>(T result) => new(MarketDataHostConnection.Encode(result), null);
    static async Task<bool> HangAsync(DatasetWorkerProcessSupervisor worker, CancellationToken token)
    { await worker.HangAsync(token).ConfigureAwait(false); return true; }
}
