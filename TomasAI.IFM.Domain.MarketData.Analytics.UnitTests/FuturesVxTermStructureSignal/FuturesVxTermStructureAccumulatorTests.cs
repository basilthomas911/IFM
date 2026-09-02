using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesVxTermStructureSignal;

/// <summary>Qualifies deterministic and replay-safe VX front/back calculations.</summary>
public sealed class FuturesVxTermStructureAccumulatorTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 26);
    static readonly Guid Epoch = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly FuturesVxTermStructureConfiguration Configuration = new()
    {
        ConfigurationId = "test-v1",
        FlatEpsilon = 0.001m,
        MaximumSourceSkew = TimeSpan.FromSeconds(5)
    };
    static readonly FuturesVxTermStructureSignalEntityId EntityId = new(
        ValueDate, "VX20260916", "VX20261021", Configuration.ConfigurationId);

    [Theory]
    [InlineData(20, 21, FuturesVxTermStructureState.Contango)]
    [InlineData(21, 20, FuturesVxTermStructureState.Backwardation)]
    [InlineData(20, 20.01, FuturesVxTermStructureState.Flat)]
    public void PairClassifiesExpectedCurve(
        double frontPrice,
        double backPrice,
        FuturesVxTermStructureState expected)
    {
        var first = FuturesVxTermStructureAccumulator.Apply(
            EntityId, null, Front((decimal)frontPrice, 1), Configuration);
        var second = FuturesVxTermStructureAccumulator.Apply(
            EntityId, first.Checkpoint, Back((decimal)backPrice, 1), Configuration);

        Assert.NotNull(second.Signal);
        Assert.Equal(expected, second.Signal.TermStructureState);
        Assert.Equal((decimal)backPrice - (decimal)frontPrice, second.Signal.FrontBackSpread);
        Assert.Equal((decimal)frontPrice / (decimal)backPrice, second.Signal.FrontBackRatio);
        Assert.True(second.Signal.IsValid);
    }

    [Fact]
    public void SourceSkewPreservesLegsWithoutPublishingMisalignedCurve()
    {
        var first = FuturesVxTermStructureAccumulator.Apply(
            EntityId, null, Front(20m, 1), Configuration);
        var delayed = Back(21m, 1) with
        {
            SourceTimestampUtc = FrontTimestamp.AddSeconds(6)
        };
        var result = FuturesVxTermStructureAccumulator.Apply(
            EntityId, first.Checkpoint, delayed, Configuration);

        Assert.Null(result.Signal);
        Assert.NotNull(result.Checkpoint.Front);
        Assert.NotNull(result.Checkpoint.Back);
    }

    [Fact]
    public void DuplicateOrOlderSequenceInSameEpochIsRejected()
    {
        var first = FuturesVxTermStructureAccumulator.Apply(
            EntityId, null, Front(20m, 10), Configuration);

        Assert.Throws<InvalidOperationException>(() =>
            FuturesVxTermStructureAccumulator.Apply(
                EntityId, first.Checkpoint, Front(20.1m, 10), Configuration));
        Assert.Throws<InvalidOperationException>(() =>
            FuturesVxTermStructureAccumulator.Apply(
                EntityId, first.Checkpoint, Front(20.1m, 9), Configuration));
    }

    [Fact]
    public void NewStreamEpochMayRestartSourceSequence()
    {
        var first = FuturesVxTermStructureAccumulator.Apply(
            EntityId, null, Front(20m, 10), Configuration);
        var nextEpoch = Front(20.1m, 1) with { StreamEpochId = Guid.NewGuid() };

        var result = FuturesVxTermStructureAccumulator.Apply(
            EntityId, first.Checkpoint, nextEpoch, Configuration);

        Assert.Equal(1, result.Checkpoint.Front!.SourceSequence);
    }

    [Fact]
    public void EntityIdentityRoundTripsAndSeparatesRolloverPairs()
    {
        Assert.True(FuturesVxTermStructureSignalEntityId.TryParse(EntityId.Format(), out var parsed));
        Assert.Equal(EntityId, parsed);
        Assert.NotEqual(EntityId, EntityId with { BackContractId = "VX20261118" });
    }

    [Fact]
    public void RealtimeActorContainsNoCalculationCheckpointOrSignalState()
    {
        var fields = typeof(FuturesVxTermStructureSignalRealtimeActor).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.DoesNotContain(fields, field =>
            field.FieldType == typeof(FuturesVxTermStructureCheckpoint)
            || field.FieldType == typeof(FuturesVxTermStructureSignalReadModel));
    }

    [Fact]
    public async Task StreamOwnership_BackAcquisitionFailure_ReleasesNewFrontLease()
    {
        var api = TermStructureApi();
        var frontOwner = new TickerStreamOwner("FuturesVxTermStructureSignal", "CurrentCurve", "Front");
        var backOwner = new TickerStreamOwner("FuturesVxTermStructureSignal", "CurrentCurve", "Back");
        api.StartStreamingFuturesTickDataAsync(EntityId.FrontContractId, frontOwner)
            .Returns(Task.FromResult(true));
        api.StartStreamingFuturesTickDataAsync(EntityId.BackContractId, backOwner)
            .Returns(Task.FromException<bool>(new InvalidOperationException("Back route failed")));
        var ownership = new FuturesVxTermStructureStreamOwnership();

        var action = () => ownership.EnsureAsync(api).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Back route failed");
        await api.Received(1).StopStreamingFuturesTickDataAsync(
            EntityId.FrontContractId, frontOwner);
    }

    static readonly DateTimeOffset FrontTimestamp = new(2026, 8, 26, 14, 30, 0, TimeSpan.Zero);

    static FuturesVxTermStructureLegObservation Front(decimal price, long sequence) => new()
    {
        Leg = FuturesVxTermStructureLeg.Front,
        ContractId = EntityId.FrontContractId,
        Expiry = new(2026, 9, 16),
        Price = price,
        SourceSequence = sequence,
        SourceTimestampUtc = FrontTimestamp,
        StreamEpochId = Epoch
    };

    static FuturesVxTermStructureLegObservation Back(decimal price, long sequence) => new()
    {
        Leg = FuturesVxTermStructureLeg.Back,
        ContractId = EntityId.BackContractId,
        Expiry = new(2026, 10, 21),
        Price = price,
        SourceSequence = sequence,
        SourceTimestampUtc = FrontTimestamp.AddSeconds(1),
        StreamEpochId = Epoch
    };

    static IMarketDataApi TermStructureApi()
    {
        var api = Substitute.For<IMarketDataApi>();
        var pair = new FuturesTermStructureContracts(
            Contract(EntityId.FrontContractId, "VX/U6", new(2026, 9, 16)),
            Contract(EntityId.BackContractId, "VX/V6", new(2026, 10, 21)));
        api.TryGetFuturesTermStructureContracts("VX", out Arg.Any<FuturesTermStructureContracts>())
            .Returns(call =>
            {
                call[1] = pair;
                return true;
            });
        return api;
    }

    static FuturesContractV3ReadModel Contract(
        string contractId,
        string localSymbol,
        DateOnly maturity) => new(
            contractId, $"{localSymbol} future", "VX", localSymbol, "FUT", "USD", "CFE",
            "1000", maturity, true);
}
