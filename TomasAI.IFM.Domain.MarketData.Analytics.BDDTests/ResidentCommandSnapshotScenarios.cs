using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests;

/// <summary>Eviction/restart uses full committed facts and preserves the same next-transition decisions.</summary>
public sealed class ResidentCommandSnapshotScenarios
{
    [Fact]
    public void GivenCommittedVwap_WhenRestoredFromLatestFact_ThenNextUpdateMatchesResidentState()
    {
        var warm = new FuturesVwapSignalCommandState();
        Assert.True(Vwap(1).Execute(warm).Success);
        Assert.True(Vwap(2).Execute(warm).Success);
        var source = Assert.IsType<FuturesVwapSignalUpdatedEvent>(warm.Events.Last());
        var restored = new FuturesVwapSignalCommandState();
        restored.ReplayEvents(new IEvent[] { RoundTrip(source) });
        warm.AcceptChanges();
        Assert.Equal(warm.FuturesVwapCheckpoint, restored.FuturesVwapCheckpoint);
        Assert.True(Vwap(2).Execute(restored).Success);
        Assert.Empty(restored.Events);
        Assert.True(Vwap(3).Execute(warm).Success);
        Assert.True(Vwap(3).Execute(restored).Success);
        Assert.Equal(warm.FuturesVwapCheckpoint, restored.FuturesVwapCheckpoint);
        Assert.Equal(warm.FuturesVwapSignal, restored.FuturesVwapSignal);
    }

    [Fact]
    public void GivenCommittedOutlook_WhenRestoredFromLatestFact_ThenDuplicateAndChangedPayloadDecisionsMatch()
    {
        var warm = new MarketOutlookSnapshotCommandState();
        var first = Outlook(5000);
        var latest = Outlook(5001);
        Assert.True(first.Execute(warm).Success);
        Assert.True(latest.Execute(warm).Success);
        var source = Assert.IsType<MarketOutlookSnapshotInsertedEvent>(warm.Events.Last());
        var restored = new MarketOutlookSnapshotCommandState();
        restored.ReplayEvents(new IEvent[] { RoundTrip(source) });
        warm.AcceptChanges();
        Assert.True(latest.Execute(warm).Success);
        Assert.True(latest.Execute(restored).Success);
        Assert.Empty(warm.Events);
        Assert.Empty(restored.Events);
        var next = Outlook(5002);
        Assert.True(next.Execute(warm).Success);
        Assert.True(next.Execute(restored).Success);
        Assert.Equal(Assert.IsType<MarketOutlookSnapshotInsertedEvent>(warm.Events.Single()).MarketOutlook,
            Assert.IsType<MarketOutlookSnapshotInsertedEvent>(restored.Events.Single()).MarketOutlook);
    }

    static T RoundTrip<T>(T fact) where T : class, IEvent
        => EventLogMessagePackCodec.Shared.Deserialize<T>(fact.EventId, EventLogMessagePackCodec.Shared.Serialize(fact));

    static InsertMarketOutlookSnapshotCommand Outlook(decimal price) => new(new MarketOutlookReadModel
    {
        ContractId = "ES20261218", ValueDate = new(2026, 11, 6),
        FuturesEodData = new FuturesEodDataV2ReadModel { ContractId = "ES20261218", ClosePrice = price }
    });

    static UpdateFuturesVwapSignalCommand Vwap(long revision)
    {
        var config = FuturesVwapConfiguration.Standard;
        return new()
        {
            CommandId = Guid.NewGuid(), EntityId = new("ES20261218", new(2026, 11, 6), config.ConfigurationId),
            Configuration = config, SessionStartUtc = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero),
            SessionEndUtc = new(2026, 11, 6, 23, 0, 0, TimeSpan.Zero),
            SourceCheckpoint = new FuturesVwapSourceCheckpoint
            {
                StreamEpochId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                RecoveryGenerationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                LastTradeOrdinal = revision, LastTradeSourceSequence = revision,
                AsOfUtc = new(2026, 11, 6, 15, 0, (int)revision, TimeSpan.Zero),
                CumulativePriceVolume = 5000 * revision, CumulativeVolume = revision,
                EligibleTradeCount = revision, LastPrice = 5000, IsValid = true, IsReplayComplete = true
            }
        };
    }
}
