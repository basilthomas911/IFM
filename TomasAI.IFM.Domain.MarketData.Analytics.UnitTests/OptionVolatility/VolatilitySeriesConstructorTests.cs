using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class VolatilitySeriesConstructorTests
{
    [Fact]
    public void Construct_SelectsAtmDeterministicallyWithLowerStrikeTieBreakAndContractIdTieBreak()
    {
        var inputs = new[]
        {
            OptionVolatilityTestData.Input("z-call", 30, 4990m, true, 0.50m),
            OptionVolatilityTestData.Input("a-call", 30, 4990m, true, 0.20m),
            OptionVolatilityTestData.Input("put", 30, 4990m, false, 0.30m),
            OptionVolatilityTestData.Input("upper-call", 30, 5010m, true, 0.80m),
            OptionVolatilityTestData.Input("upper-put", 30, 5010m, false, 0.90m)
        };

        var result = Construct(inputs);

        result.Failure.Should().Be(VolatilitySeriesConstructionFailure.None);
        result.Observation.ImpliedVolatility.Should().Be(0.25m);
        result.Observation.Provenance.Contributors.Select(x => x.OptionContractId)
            .Should().Equal("a-call", "put");
    }

    [Fact]
    public void Construct_CombinesCallAndPutWithConfiguredArithmeticMean()
    {
        var result = Construct([
            OptionVolatilityTestData.Input("call", 30, 5000m, true, 0.20m),
            OptionVolatilityTestData.Input("put", 30, 5000m, false, 0.30m)]);

        result.Observation.ImpliedVolatility.Should().Be(0.25m);
    }

    [Fact]
    public void Construct_InterpolatesConstantTenorUsingTotalVariance()
    {
        var result = Construct([
            OptionVolatilityTestData.Input("20-call", 20, 5000m, true, 0.20m),
            OptionVolatilityTestData.Input("20-put", 20, 5000m, false, 0.20m),
            OptionVolatilityTestData.Input("40-call", 40, 5000m, true, 0.40m),
            OptionVolatilityTestData.Input("40-put", 40, 5000m, false, 0.40m)]);

        var expected = (decimal)Math.Sqrt((20d * 0.2d * 0.2d + 0.5d *
            (40d * 0.4d * 0.4d - 20d * 0.2d * 0.2d)) / 30d);
        result.Observation.ImpliedVolatility.Should().BeApproximately(expected, 0.000000000000001m);
    }

    [Fact]
    public void Construct_MissingBracketingExpiryIsUnavailableWithoutFallback()
    {
        var result = Construct([
            OptionVolatilityTestData.Input("20-call", 20, 5000m, true, 0.20m),
            OptionVolatilityTestData.Input("20-put", 20, 5000m, false, 0.20m)]);

        result.Failure.Should().Be(VolatilitySeriesConstructionFailure.MissingTenorBracket);
        result.Observation.Status.Should().Be(VolatilityObservationStatus.Unavailable);
        result.Observation.ImpliedVolatility.Should().BeNull();
    }

    [Fact]
    public void Construct_MissingConfiguredSideIsPartial()
    {
        var result = Construct([OptionVolatilityTestData.Input("call", 30, 5000m, true, 0.20m)]);

        result.Failure.Should().Be(VolatilitySeriesConstructionFailure.MissingOptionSide);
        result.Observation.Status.Should().Be(VolatilityObservationStatus.Partial);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("environment")]
    [InlineData("convention")]
    public void Construct_RejectsIncompatibleUnderlyingEnvironmentOrConvention(string incompatibility)
    {
        var input = OptionVolatilityTestData.Input("call", 30, 5000m, true, 0.20m);
        input = incompatibility switch
        {
            "root" => input with { UnderlyingRoot = "NQ" },
            "environment" => input with { Environment = "production" },
            _ => input with { ExerciseConvention = "American" }
        };

        var result = Construct([input]);

        result.Observation.Status.Should().Be(VolatilityObservationStatus.Unavailable);
        result.Failure.Should().Be(incompatibility == "convention"
            ? VolatilitySeriesConstructionFailure.IncompatibleConvention
            : VolatilitySeriesConstructionFailure.IncompatibleSource);
    }

    [Fact]
    public void Construct_RejectsMixedUnderlyingContractsWithinSelectedExpiry()
    {
        var result = Construct([
            OptionVolatilityTestData.Input("call", 30, 5000m, true, 0.20m, "ESZ6"),
            OptionVolatilityTestData.Input("put", 30, 5000m, false, 0.30m, "ESH7")]);

        result.Failure.Should().Be(VolatilitySeriesConstructionFailure.ExactUnderlyingMismatch);
    }

    [Fact]
    public void Construct_PreservesExactContributorProvenanceInCanonicalOrder()
    {
        var put = OptionVolatilityTestData.Input("z-put", 30, 5000m, false, 0.30m) with
        { QuoteSequence = 42, UnderlyingSequence = 84 };
        var call = OptionVolatilityTestData.Input("a-call", 30, 5000m, true, 0.20m) with
        { QuoteSequence = 21, UnderlyingSequence = 63 };

        var result = Construct([put, call]);

        result.Observation.Provenance.Contributors.Should().Equal(
            new VolatilityContributor(call.OptionContractId, call.UnderlyingFuturesContractId,
                call.QuoteObservedAtUtc, call.UnderlyingObservedAtUtc, 21, 63, call.SourceGenerationId),
            new VolatilityContributor(put.OptionContractId, put.UnderlyingFuturesContractId,
                put.QuoteObservedAtUtc, put.UnderlyingObservedAtUtc, 42, 84, put.SourceGenerationId));
        result.Observation.Provenance.InputDigest.Should().HaveLength(64);
    }

    [Fact]
    public void Construct_FuturesRollKeepsStableSeriesIdentityWhileRecordingActualUnderlying()
    {
        var before = Construct([
            OptionVolatilityTestData.Input("z-call", 30, 5000m, true, 0.20m, "ESZ6"),
            OptionVolatilityTestData.Input("z-put", 30, 5000m, false, 0.30m, "ESZ6")]);
        var after = Construct([
            OptionVolatilityTestData.Input("h-call", 30, 5000m, true, 0.21m, "ESH7"),
            OptionVolatilityTestData.Input("h-put", 30, 5000m, false, 0.31m, "ESH7")],
            OptionVolatilityTestData.ValueDate.AddDays(1));

        before.Observation.Series.Should().Be(after.Observation.Series).And.Be(OptionVolatilityTestData.Series);
        before.Observation.Provenance.Contributors.Should().OnlyContain(x => x.UnderlyingFuturesContractId == "ESZ6");
        after.Observation.Provenance.Contributors.Should().OnlyContain(x => x.UnderlyingFuturesContractId == "ESH7");
    }

    [Fact]
    public void Construct_CorrectionIdentityIncludesRevisionAndSupersededIdentity()
    {
        var inputs = new[]
        {
            OptionVolatilityTestData.Input("call", 30, 5000m, true, 0.20m),
            OptionVolatilityTestData.Input("put", 30, 5000m, false, 0.30m)
        };
        var original = Construct(inputs);
        var corrected = Construct(inputs, revision: 2, supersedes: original.Observation.ObservationId);

        corrected.Observation.ObservationId.Should().NotBe(original.Observation.ObservationId);
        corrected.Observation.Revision.Should().Be(2);
        corrected.Observation.SupersedesObservationId.Should().Be(original.Observation.ObservationId);
    }

    static VolatilitySeriesConstructionResult Construct(IEnumerable<QualifiedOptionIvInput> inputs,
        DateOnly? date = null, int revision = 1, string? supersedes = null) =>
        VolatilitySeriesConstructor.Construct(new(OptionVolatilityTestData.Definition(),
            date ?? OptionVolatilityTestData.ValueDate, "daily-close", OptionVolatilityTestData.Now,
            revision, supersedes, inputs.ToImmutableArray()));
}
