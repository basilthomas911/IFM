using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class OptionVolatilityParameterSetTests
{
    [Fact]
    public void Descriptors_share_the_option_volatility_area_and_have_distinct_details()
    {
        var summaries = new[]
        {
            ParameterComponentModelRegistry.Get(ParameterSchemaRegistry.OptionVolatilitySeriesComponent).Summary,
            ParameterComponentModelRegistry.Get(ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent).Summary,
            ParameterComponentModelRegistry.Get(ParameterSchemaRegistry.OptionVolatilityRetentionComponent).Summary
        };

        summaries.Should().OnlyContain(summary =>
            summary.AreaCode == "option-volatility" && summary.AreaName == "Option Volatility");
        summaries.Select(summary => summary.Name).Should().Equal("Series", "Consumer Rules", "Retention");
        summaries.Select(summary => summary.ComponentCode).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Series_draft_has_the_review_baseline_exactly()
    {
        var id = Guid.NewGuid();
        var value = JsonSerializer.Deserialize<OptionVolatilitySeriesParameterSet>(
            new OptionVolatilitySeriesParameterModel().CreateDraftPayload(id))!;

        value.Should().BeEquivalentTo(new
        {
            SchemaVersion = 1, ParameterSetId = id, Version = 1,
            SeriesId = "ES-ATM-30D", MethodologyVersion = "methodology-v1",
            UnderlyingRoot = "ES", Venue = "XCME", Currency = "USD",
            Environment = OptionVolatilityEnvironment.Production,
            MarketDataProvider = "Databento", Dataset = "GLBX.MDP3",
            TargetMaturityCalendarDays = 30,
            AtmConvention = OptionVolatilityAtmConvention.Forward,
            OptionCombination = OptionVolatilityOptionCombination.CallPutArithmeticMean,
            ExpirySelection = OptionVolatilityExpirySelection.QualifiedBracketingExpiries,
            Interpolation = OptionVolatilityInterpolation.LinearTotalVariance,
            UnderlyingMatch = OptionVolatilityUnderlyingMatch.Exact,
            QuoteMark = OptionVolatilityQuoteMark.ExecutableMidpoint,
            MinimumDisplayedSize = 1,
            CrossedMarketPolicy = OptionVolatilityCrossedMarketPolicy.Reject,
            LockedMarketPolicy = OptionVolatilityLockedMarketPolicy.Allow,
            MaximumQuoteAgeSeconds = 5, MaximumIvAgeSeconds = 5, MaximumQuoteSkewMilliseconds = 250,
            RollPolicy = OptionVolatilityRollPolicy.StableSeries,
            ExchangeCalendar = "CME", TimeZone = "America/Chicago",
            DailySamplingPolicy = OptionVolatilitySamplingPolicy.ExchangeDefinedDailySettlement,
            IntradayCoalescingMinutes = 5, MaximumIntradayCheckpoints = 84,
            HistoricalLookbackSessions = 252, MinimumValidObservations = 220, MinimumCoverage = .85m,
            GapPolicy = OptionVolatilityGapPolicy.Preserve,
            RankBounds = OptionVolatilityRankBounds.CurrentAndPrior,
            PercentileConvention = OptionVolatilityPercentileConvention.StrictlyBelowPrior,
            ImpliedVolatilityUnit = ImpliedVolatilityUnit.AnnualDecimal,
            RankAndPercentileUnit = VolatilityMetricUnit.ZeroToOneHundred
        });
        value.EligibleProductFamilies.Should().Equal("ES", "EW", "EOM");
        value.Pricers.Should().HaveCount(2);
        value.Pricers.Single(x => x.ExerciseStyle == OptionVolatilityExerciseStyle.European)
            .PricerVersions.Should().Equal("Black76.Managed/v1", "Black76.Rust/v1");
        value.Pricers.Single(x => x.ExerciseStyle == OptionVolatilityExerciseStyle.American)
            .PricerVersions.Should().Equal("AmericanFutures.CRR.Managed/v1");
    }

    [Fact]
    public void Consumer_draft_has_null_numerical_rules_and_protective_safeguards()
    {
        var id = Guid.NewGuid();
        var value = JsonSerializer.Deserialize<OptionVolatilityConsumerRulesParameterSet>(
            new OptionVolatilityConsumerRulesParameterModel().CreateDraftPayload(id))!;

        value.Should().BeEquivalentTo(new
        {
            SchemaVersion = 1, ParameterSetId = id, Version = 1,
            SeriesId = "ES-ATM-30D", MethodologyVersion = "methodology-v1", MetricPolicyVersion = "metric-policy-v1",
            OpenMarketEvidence = VolatilityEvidenceRequirement.Optional, LiveMaximumAgeMinutes = 15,
            ClosedMarketEvidence = ClosedMarketEvidencePolicy.LatestQualifiedSession, MaximumSessionLag = 1,
            UnavailablePolicy = UnavailableVolatilityPolicy.NeverTreatAsZero,
            ExactDecisionSnapshotRequired = true, AllowDownstreamSilentReplacement = false,
            IronCondor = VolatilityStrategyPolicy.Optional, VerticalSpread = VolatilityStrategyPolicy.Optional,
            FuturesOutright = VolatilityStrategyPolicy.Disabled,
            HeldOptions = HeldOptionVolatilityPolicy.ObserveOnly,
            ProtectiveClose = ProtectiveActionPolicy.Independent,
            ProtectiveCancel = ProtectiveActionPolicy.Independent
        });
        value.NumericalRules.GetType().GetProperties()
            .Select(property => property.GetValue(value.NumericalRules)).Should().OnlyContain(item => item == null);
        value.ExactDecisionSnapshotRequired.Should().BeTrue();
        value.AllowDownstreamSilentReplacement.Should().BeFalse();
        value.ProtectiveClose.Should().Be(ProtectiveActionPolicy.Independent);
        value.ProtectiveCancel.Should().Be(ProtectiveActionPolicy.Independent);
    }

    [Fact]
    public void Retention_draft_has_permanent_authorities_and_isolated_environments()
    {
        var id = Guid.NewGuid();
        var value = JsonSerializer.Deserialize<OptionVolatilityRetentionParameterSet>(
            new OptionVolatilityRetentionParameterModel().CreateDraftPayload(id))!;

        value.Should().BeEquivalentTo(new
        {
            SchemaVersion = 1, ParameterSetId = id, Version = 1,
            SourceObservationYears = 2, FinalizedDailyMetricYears = 7, IntradayCheckpointDays = 90,
            DecisionSnapshotYearsAfterCompletion = 7,
            ReferencedEvidencePolicy = RetentionDependencyPolicy.FollowSnapshotCannotExpireIndependently,
            LatestPointerLifetime = RetentionLifetime.Permanent,
            DefinitionLifetime = RetentionLifetime.Permanent,
            PublishedParameterLifetime = RetentionLifetime.Permanent,
            CorrectionPolicy = CorrectionRetentionPolicy.Retain,
            BackfillStagingDays = 30, FailedPublicationDays = 30,
            PartitionBucket = RetentionBucket.Monthly, MaximumPageSize = 256,
            TradeIvBackfill = TradeIvBackfillPolicy.Disallowed,
            AllowUnqualifiedBackfillPublication = false
        });
        value.HistoricalSources.Should().Equal(
            HistoricalVolatilitySource.DatabentoQuoteHistory,
            HistoricalVolatilitySource.IfmReferenceMetadata);
        value.IsolatedEnvironments.Should().Equal(
            OptionVolatilityEnvironment.Production,
            OptionVolatilityEnvironment.Paper,
            OptionVolatilityEnvironment.Simulation);
        value.AllowUnqualifiedBackfillPublication.Should().BeFalse();
    }

    [Fact]
    public void All_three_strict_schemas_are_registered_and_defaults_are_losslessly_editable()
    {
        var registrations = new (string Code, IParameterComponentDescriptor Descriptor)[]
        {
            (ParameterSchemaRegistry.OptionVolatilitySeriesComponent, new OptionVolatilitySeriesParameterModel()),
            (ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent, new OptionVolatilityConsumerRulesParameterModel()),
            (ParameterSchemaRegistry.OptionVolatilityRetentionComponent, new OptionVolatilityRetentionParameterModel())
        };

        foreach (var registration in registrations)
        {
            var payload = registration.Descriptor.CreateDraftPayload(Guid.NewGuid());
            ParameterSchemaRegistry.Default.Get(registration.Code, 1).Version.Should().Be(1);
            ParameterSchemaRegistry.Default.ValidateStructure(registration.Code, 1, payload).Should().BeEmpty();
            ParameterSchemaRegistry.Default.CanEditLosslessly(registration.Code, 1, payload).Should().BeTrue();
            registration.Descriptor.Validate(payload, 1).Should().BeEmpty();

            using var document = JsonDocument.Parse(payload);
            var extension = document.RootElement.GetRawText().TrimEnd('}') + ",\"FutureField\":true}";
            ParameterSchemaRegistry.Default.CanEditLosslessly(registration.Code, 1, extension).Should().BeFalse();
        }
    }

    [Fact]
    public void Invalid_series_boundaries_are_rejected()
    {
        var model = new OptionVolatilitySeriesParameterModel();
        var value = OptionVolatilitySeriesParameterModel.CreateDefault(Guid.NewGuid()) with
        {
            MinimumDisplayedSize = 0,
            MaximumIntradayCheckpoints = 85,
            HistoricalLookbackSessions = 219,
            MinimumValidObservations = 220,
            MinimumCoverage = 1.01m,
            EligibleProductFamilies = ["ES", "ES"]
        };

        model.Validate(JsonSerializer.Serialize(value), 1).Select(issue => issue.Path).Should().Contain(
            "MinimumDisplayedSize", "MaximumIntradayCheckpoints", "MinimumValidObservations",
            "MinimumCoverage", "EligibleProductFamilies");
    }

    [Fact]
    public void Invalid_consumer_and_retention_safety_boundaries_are_rejected()
    {
        var consumerModel = new OptionVolatilityConsumerRulesParameterModel();
        var consumer = OptionVolatilityConsumerRulesParameterModel.CreateDefault(Guid.NewGuid()) with
        {
            ExactDecisionSnapshotRequired = false,
            AllowDownstreamSilentReplacement = true,
            NumericalRules = new() { MinimumIvRank = 101, DeltaTargetAdjustment = -1.01m }
        };
        consumerModel.Validate(JsonSerializer.Serialize(consumer), 1).Select(issue => issue.Path).Should().Contain(
            "ExactDecisionSnapshotRequired", "AllowDownstreamSilentReplacement",
            "NumericalRules/MinimumIvRank", "NumericalRules/DeltaTargetAdjustment");

        var retentionModel = new OptionVolatilityRetentionParameterModel();
        var retention = OptionVolatilityRetentionParameterModel.CreateDefault(Guid.NewGuid()) with
        {
            MaximumPageSize = 257,
            AllowUnqualifiedBackfillPublication = true,
            IsolatedEnvironments = [OptionVolatilityEnvironment.Production]
        };
        retentionModel.Validate(JsonSerializer.Serialize(retention), 1).Select(issue => issue.Path).Should().Contain(
            "MaximumPageSize", "AllowUnqualifiedBackfillPublication", "IsolatedEnvironments");
    }
}
