using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using MessagePack;
using MessagePack.Resolvers;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[MessagePackObject]
public sealed record MarketDataHostRequest([property: Key(0)] string Path, [property: Key(1)] byte[] Payload,
    [property: Key(2)] Guid OperationId, [property: Key(3)] DateTime DeadlineUtc);
[MessagePackObject]
public sealed record MarketDataHostReply([property: Key(0)] byte[] Payload, [property: Key(1)] string? Error);

/// <summary>Versioned NATS request/reply and binary event transport for one deployment scope.</summary>
public sealed class MarketDataHostConnection : IAsyncDisposable
{
    readonly NatsConnectionManager connections;
    readonly string url;
    public string Scope { get; }
    public static MessagePackSerializerOptions Serialization { get; } = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance).WithSecurity(MessagePackSecurity.UntrustedData);
    public MarketDataHostConnection(NatsConnectionManager connections, string url, string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("A single deployment scope token is required.", nameof(scope));
        this.connections = connections;
        this.url = url;
        Scope = $"ifm.{scope}.marketdata.v1";
    }
    public ValueTask<NatsClient> GetClientAsync(CancellationToken token) => connections.GetClientAsync(url, token);
    public string ControlSubject(string path) => path is "ready" or "api/GetBackendStatusAsync" or "api/GetBackendReadSnapshotAsync"
        ? Scope + ".observe" : Scope + (path.EndsWith("/stop", StringComparison.Ordinal)
        || path is "api/RecoverMarketDataAsync" or "api/StopAsync" ? ".stop" : ".control");
    public string Publications(string role, Guid worker) => $"{Scope}.publications.{role}.{worker:N}";
    public static byte[] Encode<T>(T value) => MessagePackSerializer.Serialize(value, Serialization);
    public static T Decode<T>(byte[] value) => MessagePackSerializer.Deserialize<T>(value, Serialization);
    /// <summary>Recovery owns its deadline in Host; RPC must not introduce a competing timer.</summary>
    public static bool IsRecoveryRequest(string path) => path == "api/RecoverMarketDataAsync" || path.EndsWith("/reset", StringComparison.Ordinal);
    public async Task<T> PostAsync<T>(string path, object request, CancellationToken token, Guid? operationId = null)
    {
        var recovery = IsRecoveryRequest(path);
        var budget = recovery ? Timeout.InfiniteTimeSpan : path == "api/RefreshInstrumentDefinitionsAsync" ? TimeSpan.FromMinutes(30)
            : path == "api/LoadHistoricalAsync" ? TimeSpan.FromMinutes(25)
            : path.EndsWith("/start", StringComparison.Ordinal) || path is "api/StartAsync" or "api/ReadFiveMinuteSeedBarsAsync" or "api/RefreshOptionCalendarAsync"
            ? TimeSpan.FromSeconds(180) : path is "api/AcquireOptionScopeAsync" or "api/PrepareCompositionAsync" ? TimeSpan.FromSeconds(90)
            : TimeSpan.FromSeconds(30);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (!recovery) deadline.CancelAfter(budget);
        var client = await GetClientAsync(deadline.Token).ConfigureAwait(false);
        var body = MessagePackSerializer.Serialize(request.GetType(), request, Serialization, deadline.Token);
        var reply = await client.RequestAsync<MarketDataHostRequest, MarketDataHostReply>(ControlSubject(path),
            new(path, body, operationId ?? Guid.NewGuid(), recovery ? DateTime.MaxValue : DateTime.UtcNow.Add(budget)),
            requestSerializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default,
            replySerializer: NatsMessagePackSerializer<MarketDataHostReply>.Default,
            replyOpts: new NatsSubOpts { Timeout = budget }, cancellationToken: deadline.Token).ConfigureAwait(false);
        if (reply.Data is null) throw new InvalidDataException("MarketData Host returned no result.");
        if (reply.Data.Error is { } error) throw new InvalidOperationException(error);
        return Decode<T>(reply.Data.Payload);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
