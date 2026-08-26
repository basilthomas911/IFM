using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.SharedContracts;

/// <summary>Qualifies the shared identities, provenance, and observation contracts introduced by MDSI-1.</summary>
public sealed class Mdsi1SharedContractTests
{
    /// <summary>Verifies contract and continuation identities remain explicit and round-trip independently.</summary>
    [Fact]
    public void MarketSeriesIdentity_ExplicitVariants_FormatParseAndMessagePackRoundTrip()
    {
        var contract = MarketSeriesIdentity.ForContract("ESZ26");
        var continuation = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "VolumeCrossover", "BackAdjusted", 1));

        MarketSeriesIdentity.Parse(contract.Format()).Should().Be(contract);
        MarketSeriesIdentity.Parse(continuation.Format()).Should().Be(continuation);
        MessagePackRoundTrip(contract).Should().Be(contract);
        MessagePackRoundTrip(continuation).Should().Be(continuation);
        contract.Kind.Should().Be(MarketSeriesIdentityKind.Contract);
        continuation.Kind.Should().Be(MarketSeriesIdentityKind.FuturesContinuation);
    }

    /// <summary>Verifies ambiguous identity payloads and empty continuation definitions are rejected.</summary>
    [Fact]
    public void MarketSeriesIdentity_ValidationRejectsAmbiguousOrEmptyPayloads()
    {
        var ambiguous = new MarketSeriesIdentity(
            MarketSeriesIdentityKind.Contract,
            "ESZ26",
            new FuturesSeriesId("ES", "VolumeCrossover", "BackAdjusted", 1));

        new MarketSeriesIdentityValidationRules().Execute(ambiguous).Should().NotBeEmpty();
        new FuturesSeriesIdValidationRules().Execute(default).Should().NotBeEmpty();
        MarketSeriesIdentity.TryParse("ESZ26", out _).Should().BeFalse();
    }

    /// <summary>Verifies observation identity is deterministic and changes with immutable source lineage.</summary>
    [Fact]
    public void FuturesTradeSessionBarId_IsDeterministicFromSeriesIntervalAndSequence()
    {
        var series = MarketSeriesIdentity.ForContract("ESZ26");
        var intervalEnd = new DateTimeOffset(2026, 8, 25, 14, 15, 0, TimeSpan.Zero);

        var first = FuturesTradeSessionBarId.Create(
            series,
            TimeFrameType.FifteenMinutes,
            intervalEnd,
            42);
        var same = FuturesTradeSessionBarId.Create(
            series,
            TimeFrameType.FifteenMinutes,
            intervalEnd,
            42);
        var nextLineage = FuturesTradeSessionBarId.Create(
            series,
            TimeFrameType.FifteenMinutes,
            intervalEnd,
            43);

        first.Should().Be(same);
        nextLineage.Should().NotBe(first);
        FuturesTradeSessionBarId.Parse(first.ToString()).Should().Be(first);
    }

    /// <summary>Verifies the complete immutable OHLCV and provenance payload survives serialization.</summary>
    [Fact]
    public void FuturesTradeSessionBarReadModel_MessagePackRoundTripsAndValidates()
    {
        var observation = CreateObservation();

        var roundTrip = MessagePackRoundTrip(observation);

        roundTrip.Should().BeEquivalentTo(observation);
        new FuturesTradeSessionBarReadModelValidationRules().Execute(roundTrip)
            .Should().BeEmpty();
    }

    /// <summary>Verifies invalid OHLC relationships and incomplete-valid state are rejected.</summary>
    [Fact]
    public void FuturesTradeSessionBarReadModel_ValidationRejectsContradictoryState()
    {
        var invalid = CreateObservation() with
        {
            High = 6_390m,
            IsComplete = false,
            ValidationIssues = []
        };

        new FuturesTradeSessionBarReadModelValidationRules().Execute(invalid)
            .Should().NotBeEmpty();

        var mismatchedIdentity = CreateObservation() with
        {
            ObservationId = new FuturesTradeSessionBarId(Guid.NewGuid())
        };
        new FuturesTradeSessionBarReadModelValidationRules().Execute(mismatchedIdentity)
            .Should().NotBeEmpty();
    }

    /// <summary>Verifies signal metadata exposes one coherent key, source, configuration, and validity contract.</summary>
    [Fact]
    public void MarketAnalyticsSignalMetadata_RoundTripsAndExposesKeyIdentityWithoutDuplication()
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "VolumeCrossover", "BackAdjusted", 1));
        var marketDataAsOf = new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero);
        var source = new MarketAnalyticsSignalMetadata
        {
            SignalKey = new MarketAnalyticsSignalKey(
                series,
                MarketAnalyticsSignalKind.Ema,
                TimeFrameType.Daily,
                "EMA-200-v1"),
            ContractId = "ESZ26",
            ValueDate = new DateOnly(2026, 8, 25),
            ObservationId = FuturesTradeSessionBarId.Create(
                series,
                TimeFrameType.Daily,
                marketDataAsOf,
                42),
            MarketDataAsOfUtc = marketDataAsOf,
            CalculatedAtUtc = marketDataAsOf.AddMilliseconds(10),
            SourceSequence = 42,
            SchemaVersion = 1,
            CalculationVersion = "EMA-v1",
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
            IsValid = true,
            ValidationIssues = []
        };

        var roundTrip = MessagePackRoundTrip(source);

        new MarketAnalyticsSignalMetadataValidationRules().Execute(roundTrip).Should().BeEmpty();
        roundTrip.MarketSeriesIdentity.Should().Be(series);
        roundTrip.FuturesSeriesId.Should().Be(series.FuturesSeriesId);
        roundTrip.TimeFrame.Should().Be(TimeFrameType.Daily);
        roundTrip.CalculationConfigurationId.Should().Be("EMA-200-v1");
    }

    /// <summary>Verifies the realtime envelope uses an exact realtime subject and matching observation identity.</summary>
    [Fact]
    public void FuturesTradeSessionBarClosedRealtimeEvent_RoundTripsAndValidatesExactRoute()
    {
        var observation = CreateObservation();
        var entityId = new FuturesTradeSessionBarEntityId(
            observation.MarketSeriesIdentity,
            observation.TimeFrame);
        var source = new FuturesTradeSessionBarClosedRealtimeEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                FuturesTradeSessionBarClosedRealtimeEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventSource = "Mdsi1Fixture",
            ReceivedOn = DateTime.SpecifyKind(new DateTime(2026, 8, 25, 14, 15, 1), DateTimeKind.Utc),
            Observation = observation
        };

        var roundTrip = MessagePackRoundTrip(source);

        roundTrip.Should().BeEquivalentTo(source);
        FuturesTradeSessionBarEntityId.Parse(entityId.Format()).Should().Be(entityId);
        new FuturesTradeSessionBarClosedRealtimeEventValidationRules().Execute(roundTrip)
            .Should().BeEmpty();
    }

    /// <summary>Verifies new shared public contracts and properties emit XML documentation.</summary>
    [Fact]
    public void Mdsi1PublicContracts_EmitXmlDocumentationForTypesAndProperties()
    {
        var contractTypes = new[]
        {
            typeof(FuturesSeriesId),
            typeof(MarketSeriesIdentity),
            typeof(MarketAnalyticsSignalKey),
            typeof(MarketAnalyticsSignalMetadata),
            typeof(FuturesTradeSessionBarId),
            typeof(FuturesTradeSessionBarEntityId),
            typeof(FuturesTradeSessionBarReadModel),
            typeof(FuturesTradeSessionBarClosedRealtimeEvent)
        };
        var xmlPath = Path.ChangeExtension(typeof(MarketSeriesIdentity).Assembly.Location, ".xml");
        File.Exists(xmlPath).Should().BeTrue($"documentation should be emitted at {xmlPath}");
        var members = XDocument.Load(xmlPath)
            .Descendants("member")
            .Select(x => (string?)x.Attribute("name"))
            .Where(x => x is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var contractType in contractTypes)
        {
            members.Should().Contain($"T:{contractType.FullName}");
            foreach (var property in contractType.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                members.Should().Contain($"P:{contractType.FullName}.{property.Name}");
        }
    }

    static T MessagePackRoundTrip<T>(T value) =>
        MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));

    static FuturesTradeSessionBarReadModel CreateObservation()
    {
        var series = MarketSeriesIdentity.ForContract("ESZ26");
        var intervalStart = new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);
        var intervalEnd = intervalStart.AddMinutes(15);
        return new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(
                series,
                TimeFrameType.FifteenMinutes,
                intervalEnd,
                42),
            ContractId = "ESZ26",
            ValueDate = new DateOnly(2026, 8, 25),
            TimeFrame = TimeFrameType.FifteenMinutes,
            IntervalStartUtc = intervalStart,
            IntervalEndUtc = intervalEnd,
            Open = 6_400m,
            High = 6_410m,
            Low = 6_395m,
            Close = 6_405m,
            Volume = 30,
            TradeCount = 3,
            PriceVolumeSum = 192_150m,
            FirstSourceSequence = 40,
            LastSourceSequence = 42,
            FirstMarketEventUtc = intervalStart.AddSeconds(1),
            LastMarketEventUtc = intervalEnd.AddSeconds(-1),
            CalculatedAtUtc = intervalEnd.AddMilliseconds(10),
            SchemaVersion = 1,
            CalculationVersion = "Observation-v1",
            IsComplete = true,
            IsValid = true,
            ValidationIssues = [],
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
    }
}
