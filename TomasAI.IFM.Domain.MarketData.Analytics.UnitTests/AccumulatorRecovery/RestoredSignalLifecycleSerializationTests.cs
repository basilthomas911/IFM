using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.AccumulatorRecovery;

public sealed class RestoredSignalLifecycleSerializationTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 29);

    [Fact]
    public void Atr_started_event_preserves_restored_signal()
    {
        var source = new FuturesAtrSignalStartedEvent
        {
            EntityId = new("ES20261218", ValueDate, TimeFrameType.FiveMinutes, 14),
            RestoredSignal = new FuturesAtrSignalReadModel { ContractId = "ES20261218", IsWarm = true }
        };

        var restored = RoundTrip(source);

        Assert.True(restored.RestoredSignal!.IsWarm);
        Assert.Equal("ES20261218", restored.RestoredSignal.ContractId);
    }

    [Fact]
    public void Adx_started_event_preserves_restored_signal()
    {
        var source = new FuturesAdxSignalStartedEvent
        {
            EntityId = new("ES20261218", ValueDate, TimeFrameType.FiveMinutes, 14),
            RestoredSignal = new FuturesAdxSignalReadModel { ContractId = "ES20261218", IsWarm = true }
        };

        var restored = RoundTrip(source);

        Assert.True(restored.RestoredSignal!.IsWarm);
        Assert.Equal("ES20261218", restored.RestoredSignal.ContractId);
    }

    [Fact]
    public void Macd_started_event_preserves_restored_signal()
    {
        var source = new FuturesMacdSignalStartedEvent
        {
            EntityId = new("ES20261218", ValueDate, TimeFrameType.FiveMinutes, 9, 12, 26),
            RestoredSignal = new FuturesMacdSignalReadModel { ContractId = "ES20261218", IsWarm = true }
        };

        var restored = RoundTrip(source);

        Assert.True(restored.RestoredSignal!.IsWarm);
        Assert.Equal("ES20261218", restored.RestoredSignal.ContractId);
    }

    static T RoundTrip<T>(T value) => MessagePackSerializer.Deserialize<T>(
        MessagePackSerializer.Serialize(value));
}
