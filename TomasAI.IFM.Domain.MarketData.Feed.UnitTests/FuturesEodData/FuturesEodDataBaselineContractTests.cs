using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

/// <summary>
/// Freezes the serialization surface that the Market Signal Interface migration
/// must preserve or replace explicitly after MDSI-0.
/// </summary>
public sealed class FuturesEodDataBaselineContractTests
{
    /// <summary>Verifies the MDSI-4 cutover copies only raw OHLCV facts from the compatibility model.</summary>
    [Fact]
    public void RawObservationFactory_DoesNotCopyOrRecalculateLegacyDerivedFields()
    {
        var valueDate = new DateOnly(2026, 8, 25);
        var source = new FuturesEodDataV2ReadModel(
            "ESZ26", valueDate, "ES", 6400m, 6425m, 6375m, 6410m, 1234567,
            0.99, 0.88, 777, 7000, 6500, 6000,
            MarketDirectionType.Down, MarketVolatilityType.High,
            PriceDirectionType.Falling, PriceVolatilityType.Rising,
            0.73, 20, 6250m, 5980m);

        var raw = FuturesEodRawObservationFactory.Create(
            source, 42, new CmeFuturesMarketSessionCalendar());

        raw.ContractId.Should().Be("ESZ26");
        raw.ValueDate.Should().Be(valueDate);
        raw.Open.Should().Be(6400m);
        raw.High.Should().Be(6425m);
        raw.Low.Should().Be(6375m);
        raw.Close.Should().Be(6410m);
        raw.Volume.Should().Be(1234567m);
        raw.GetType().GetProperties().Select(x => x.Name).Should().NotContain([
            nameof(FuturesEodDataV2ReadModel.UpperBand),
            nameof(FuturesEodDataV2ReadModel.Mean),
            nameof(FuturesEodDataV2ReadModel.LowerBand),
            nameof(FuturesEodDataV2ReadModel.FiftyDMA),
            nameof(FuturesEodDataV2ReadModel.TwoHundredDMA)]);
    }

    /// <summary>
    /// Verifies the established raw and derived EOD fields retain their exact
    /// MessagePack keys throughout the incremental migration.
    /// </summary>
    [Fact]
    public void FuturesEodData_MessagePackKeys_AreFrozenAtTheMdsi0Baseline()
    {
        MessagePackKeys<FuturesEodDataV2ReadModel>().Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                [nameof(FuturesEodDataV2ReadModel.ContractId)] = 0,
                [nameof(FuturesEodDataV2ReadModel.ValueDate)] = 1,
                [nameof(FuturesEodDataV2ReadModel.Symbol)] = 2,
                [nameof(FuturesEodDataV2ReadModel.OpenPrice)] = 3,
                [nameof(FuturesEodDataV2ReadModel.HighPrice)] = 4,
                [nameof(FuturesEodDataV2ReadModel.LowPrice)] = 5,
                [nameof(FuturesEodDataV2ReadModel.ClosePrice)] = 6,
                [nameof(FuturesEodDataV2ReadModel.Volume)] = 7,
                [nameof(FuturesEodDataV2ReadModel.DailyPercentChange)] = 8,
                [nameof(FuturesEodDataV2ReadModel.DailyStdDev)] = 9,
                [nameof(FuturesEodDataV2ReadModel.DailyStdDevAmount)] = 10,
                [nameof(FuturesEodDataV2ReadModel.UpperBand)] = 11,
                [nameof(FuturesEodDataV2ReadModel.Mean)] = 12,
                [nameof(FuturesEodDataV2ReadModel.LowerBand)] = 13,
                [nameof(FuturesEodDataV2ReadModel.MarketDirection)] = 14,
                [nameof(FuturesEodDataV2ReadModel.MarketVolatility)] = 15,
                [nameof(FuturesEodDataV2ReadModel.PriceDirection)] = 16,
                [nameof(FuturesEodDataV2ReadModel.PriceVolatility)] = 17,
                [nameof(FuturesEodDataV2ReadModel.MarketDirectionIndicator)] = 18,
                [nameof(FuturesEodDataV2ReadModel.WindowSize)] = 19,
                [nameof(FuturesEodDataV2ReadModel.FiftyDMA)] = 20,
                [nameof(FuturesEodDataV2ReadModel.TwoHundredDMA)] = 21
            });
    }

    /// <summary>
    /// Verifies both raw OHLCV and legacy derived values survive the current
    /// MessagePack round trip before those responsibilities are separated.
    /// </summary>
    [Fact]
    public void FuturesEodData_CurrentRawAndDerivedValues_RoundTripTogether()
    {
        var source = new FuturesEodDataV2ReadModel(
            "ESZ26",
            new DateOnly(2026, 8, 25),
            "ES",
            6_400.25m,
            6_425.50m,
            6_375.00m,
            6_410.75m,
            1_234_567,
            0.0125,
            0.018,
            115.42,
            6_520.25,
            6_400.00,
            6_279.75,
            MarketDirectionType.Up,
            MarketVolatilityType.High,
            PriceDirectionType.Rising,
            PriceVolatilityType.Rising,
            0.73,
            20,
            6_250.50m,
            5_980.25m);

        var roundTrip = MessagePackSerializer.Deserialize<FuturesEodDataV2ReadModel>(
            MessagePackSerializer.Serialize(source));

        roundTrip.Should().Be(source);
    }

    /// <summary>
    /// Verifies MDSI-1 can only append normalized trade-lineage properties
    /// after the five existing trade snapshot keys.
    /// </summary>
    [Fact]
    public void FuturesMarketTradeSnapshot_MessagePackKeys_PreserveBaselineAndAppendMdsi1Lineage()
    {
        MessagePackKeys<FuturesMarketTradeSnapshot>().Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                [nameof(FuturesMarketTradeSnapshot.LastPrice)] = 0,
                [nameof(FuturesMarketTradeSnapshot.LastSize)] = 1,
                [nameof(FuturesMarketTradeSnapshot.SourceSequence)] = 2,
                [nameof(FuturesMarketTradeSnapshot.EventTimestamp)] = 3,
                [nameof(FuturesMarketTradeSnapshot.ReceiveTimestamp)] = 4,
                [nameof(FuturesMarketTradeSnapshot.NormalizedTradeAction)] = 5,
                [nameof(FuturesMarketTradeSnapshot.NormalizedTradeSide)] = 6,
                [nameof(FuturesMarketTradeSnapshot.NormalizedTradeConditionFlags)] = 7,
                [nameof(FuturesMarketTradeSnapshot.StreamEpochId)] = 8,
                [nameof(FuturesMarketTradeSnapshot.TradeOrdinal)] = 9
            });
    }

    /// <summary>
    /// Verifies a payload produced before MDSI-1 remains readable and receives
    /// safe unknown lineage defaults rather than fabricated exact lineage.
    /// </summary>
    [Fact]
    public void FuturesMarketTradeSnapshot_LegacyPayload_DeserializesWithUnknownLineage()
    {
        var eventTimestamp = new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);
        var receiveTimestamp = eventTimestamp.AddMilliseconds(2);
        var legacy = new LegacyFuturesMarketTradeSnapshot(
            6_401.25m,
            12,
            42,
            eventTimestamp,
            receiveTimestamp);

        var current = MessagePackSerializer.Deserialize<FuturesMarketTradeSnapshot>(
            MessagePackSerializer.Serialize(legacy));

        current.LastPrice.Should().Be(legacy.LastPrice);
        current.LastSize.Should().Be(legacy.LastSize);
        current.SourceSequence.Should().Be(legacy.SourceSequence);
        current.EventTimestamp.Should().Be(eventTimestamp);
        current.ReceiveTimestamp.Should().Be(receiveTimestamp);
        current.NormalizedTradeAction.Should().Be(NormalizedTradeAction.Unknown);
        current.NormalizedTradeSide.Should().Be(NormalizedTradeSide.Unknown);
        current.NormalizedTradeConditionFlags.Should().Be(NormalizedTradeConditionFlags.None);
        current.StreamEpochId.Should().BeEmpty();
        current.TradeOrdinal.Should().Be(0);
    }

    /// <summary>Verifies every MDSI-1 trade-lineage value survives serialization.</summary>
    [Fact]
    public void FuturesMarketTradeSnapshot_CurrentPayload_RoundTripsAppendedLineage()
    {
        var source = new FuturesMarketTradeSnapshot(
            6_401.25m,
            12,
            42,
            new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 14, 0, 0, 2, TimeSpan.Zero),
            NormalizedTradeAction.New,
            NormalizedTradeSide.Buy,
            NormalizedTradeConditionFlags.LastInEvent | NormalizedTradeConditionFlags.TopOfBook,
            Guid.NewGuid(),
            7);

        var roundTrip = MessagePackSerializer.Deserialize<FuturesMarketTradeSnapshot>(
            MessagePackSerializer.Serialize(source));

        roundTrip.Should().Be(source);
    }

    /// <summary>
    /// Verifies the current realtime price event envelope remains append-only
    /// while its nested trade snapshot evolves.
    /// </summary>
    [Fact]
    public void FuturesMarketPriceUpdatedEvent_MessagePackKeys_AreFrozenAtZeroThroughTen()
    {
        MessagePackKeys<FuturesMarketPriceUpdatedRealtimeEvent>().Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.Subject)] = 0,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.Id)] = 1,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.EntityId)] = 2,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.EventId)] = 3,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.CommandId)] = 4,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.AggregateId)] = 5,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.EventSource)] = 6,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.ReceivedOn)] = 7,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.SchemaVersion)] = 8,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.Price)] = 9,
                [nameof(FuturesMarketPriceUpdatedRealtimeEvent.UpdateSource)] = 10
            });
    }

    static Dictionary<string, int> MessagePackKeys<T>() => typeof(T)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()))
        .Where(item => item.Key is not null)
        .ToDictionary(item => item.Property.Name, item => item.Key!.IntKey!.Value);

    [MessagePackObject]
    public readonly record struct LegacyFuturesMarketTradeSnapshot(
        [property: Key(0)] decimal LastPrice,
        [property: Key(1)] uint LastSize,
        [property: Key(2)] long SourceSequence,
        [property: Key(3)] DateTimeOffset EventTimestamp,
        [property: Key(4)] DateTimeOffset ReceiveTimestamp);
}
