using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class G0QuerySession : IAsyncDisposable
{
    readonly NatsConnectionManager _connectionManager = new();
    readonly NatsActorProducer _producer;

    /// <summary>Creates a typed query and command session for an isolated NATS endpoint.</summary>
    /// <param name="natsUri">The NATS server used by the qualification run.</param>
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
        MarketDataAnalyticsCommands = new MarketDataAnalyticsCommandApi(_producer);
        Reference = new ReferenceQueryApi(_producer);
        ReferenceCommands = new ReferenceCommandApi(_producer);
        Fund = new FundQueryApi(_producer);
        FundCommands = new FundCommandApi(_producer);
        Trade = new OptionTradeQueryApi(_producer);
        StrategyPositions = new StrategyPositionQueryApi(_producer);
        StrategyPositionCommands = new StrategyPositionCommandApi(_producer);
        TradeOrders = new TradeOrderLifecycleApi(_producer);
        DatabaseBackup = new DatabaseBackupQueryApi(_producer);
        DatabaseBackupCommands = new DatabaseBackupCommandApi(_producer);
    }

    public MarketDataQueryApi MarketData { get; }
    public MarketDataCommandApi MarketDataCommands { get; }
    public MarketDataFeedQueryApi MarketDataFeed { get; }
    public MarketDataFeedCommandApi MarketDataFeedCommands { get; }
    public MarketDataAnalyticsQueryApi MarketDataAnalytics { get; }
    public MarketDataAnalyticsCommandApi MarketDataAnalyticsCommands { get; }
    public ReferenceQueryApi Reference { get; }
    public ReferenceCommandApi ReferenceCommands { get; }
    public FundQueryApi Fund { get; }
    public FundCommandApi FundCommands { get; }
    public OptionTradeQueryApi Trade { get; }
    /// <summary>Gets the canonical strategy-position query client.</summary>
    public StrategyPositionQueryApi StrategyPositions { get; }
    /// <summary>Gets the canonical strategy-position command client.</summary>
    public StrategyPositionCommandApi StrategyPositionCommands { get; }
    /// <summary>Gets the accepted Trade Order lifecycle client.</summary>
    public TradeOrderLifecycleApi TradeOrders { get; }
    public DatabaseBackupQueryApi DatabaseBackup { get; }
    public DatabaseBackupCommandApi DatabaseBackupCommands { get; }

    /// <summary>Starts the session with a run-specific query mailbox.</summary>
    /// <param name="runId">The unique qualification run identifier.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <param name="gate">The qualification gate name included in the mailbox.</param>
    /// <returns>A task that completes when the query producer is ready.</returns>
    public ValueTask StartAsync(
        string runId,
        CancellationToken cancellationToken,
        string gate = "G0")
        => _producer.StartAsync(
            new ActorMailboxId(ActorType.Query, $"IFM.UI.{gate}.{runId}"),
            cancellationToken);

    /// <summary>Stops the producer and releases the shared NATS connection manager.</summary>
    /// <returns>A task that completes after all session resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        await _producer.StopAsync().ConfigureAwait(false);
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }
}
