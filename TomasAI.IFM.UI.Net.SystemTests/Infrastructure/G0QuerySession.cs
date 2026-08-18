using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class G0QuerySession : IAsyncDisposable
{
    readonly NatsConnectionManager _connectionManager = new();
    readonly NatsActorProducer _producer;

    public G0QuerySession(Uri natsUri)
    {
        _producer = new NatsActorProducer(
            new NatsProducerOptions { Url = natsUri.ToString() },
            NullLogger.Instance,
            _connectionManager);
        MarketData = new MarketDataQueryApi(_producer);
        MarketDataCommands = new MarketDataCommandApi(_producer);
        MarketDataFeed = new MarketDataFeedQueryApi(_producer);
        MarketDataFeedCommands = new MarketDataFeedCommandApi(_producer);
        MarketDataAnalytics = new MarketDataAnalyticsQueryApi(_producer);
        Reference = new ReferenceQueryApi(_producer);
        ReferenceCommands = new ReferenceCommandApi(_producer);
        Fund = new FundQueryApi(_producer);
        FundCommands = new FundCommandApi(_producer);
    }

    public MarketDataQueryApi MarketData { get; }
    public MarketDataCommandApi MarketDataCommands { get; }
    public MarketDataFeedQueryApi MarketDataFeed { get; }
    public MarketDataFeedCommandApi MarketDataFeedCommands { get; }
    public MarketDataAnalyticsQueryApi MarketDataAnalytics { get; }
    public ReferenceQueryApi Reference { get; }
    public ReferenceCommandApi ReferenceCommands { get; }
    public FundQueryApi Fund { get; }
    public FundCommandApi FundCommands { get; }

    public ValueTask StartAsync(
        string runId,
        CancellationToken cancellationToken,
        string gate = "G0")
        => _producer.StartAsync(
            new ActorMailboxId(ActorType.Query, $"IFM.UI.{gate}.{runId}"),
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _producer.StopAsync().ConfigureAwait(false);
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }
}
