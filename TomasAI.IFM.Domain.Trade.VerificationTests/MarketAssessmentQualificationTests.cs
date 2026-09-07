using System.Globalization;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests;

[Trait("Gate","MC-R08")]
public sealed class MarketAssessmentQualificationTests
{
    [Theory]
    [InlineData("1.49", "0.14",AssessmentStress.Normal)]
    [InlineData("1.50", "0.15",AssessmentStress.Normal)]
    [InlineData("1.51", "0.15",AssessmentStress.Elevated)]
    [InlineData("1.50", "0.16",AssessmentStress.Elevated)]
    public void Stress_thresholds_are_strict_and_independent_of_strategy_choice(string movement,string volatility,AssessmentStress expected)
    {
        var (c,s)=MarketConditionAssessmentReferenceGenerator.CreateScenario(TimeFrameType.Weekly,"directional");
        s=(s with {Observations=s.Observations.Select(x=>x.SourceId=="NormalizedMovement"?x with {Value=decimal.Parse(movement,CultureInfo.InvariantCulture)}:
            x.SourceId=="VolatilityChange"?x with {Value=decimal.Parse(volatility,CultureInfo.InvariantCulture)}:x).ToArray()}).Seal();
        var result=new MarketConditionAssessmentCalculator().Calculate(c,s,c.CommandId);
        result.Assessment.StressState.Should().Be(expected);result.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
    }
    [Fact]
    public void Representative_matrix_is_deterministic_under_concurrent_evaluation_and_serialization()
    {
        var expected=new MarketConditionAssessmentReferenceGenerator().Generate();
        Parallel.For(0,16,_=>MessagePackSerializer.Serialize(new MarketConditionAssessmentReferenceGenerator().Generate()).Should().Equal(MessagePackSerializer.Serialize(expected)));
        expected.Should().HaveCount(30);expected.Should().OnlyContain(x=>!x.IsAuthoritative&&x.Mode=="MarketAssessment");
        expected.Select(x=>x.Result.Assessment.ConditionType).Where(x=>x.HasValue).Distinct().Should().HaveCount(7);
    }
    [Fact]
    public void Published_parameter_hash_ignores_decimal_scale_and_preserves_explicit_calendar_binding()
    {
        var p=MarketConditionAssessmentReferenceGenerator.CreateScenario(TimeFrameType.Daily,"directional").Command.ParameterSet;
        MarketConditionAssessmentHash.Parameters(p with {MovementStressThreshold=1.500000m}).Should().Be(MarketConditionAssessmentHash.Parameters(p));
        var restored=System.Text.Json.JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(MarketConditionAssessmentHash.Serialize(p))!;
        MarketConditionAssessmentHash.Parameters(restored).Should().Be(MarketConditionAssessmentHash.Parameters(p));
        FluentActions.Invoking(()=>(p with {EconomicCalendarProvider="Other"}).Validate()).Should().Throw<ArgumentException>();
        FluentActions.Invoking(()=>(p with {EconomicCalendarScopes="CA"}).Validate()).Should().Throw<ArgumentException>();
    }
    [Fact]
    public void Every_new_messagepack_contract_has_unique_contiguous_keys_and_no_tradeability_fields()
    {
        var types=typeof(MarketConditionAssessmentResult).Assembly.GetTypes().Where(x=>x.Namespace==typeof(MarketConditionAssessmentResult).Namespace&&x.GetCustomAttribute<MessagePackObjectAttribute>() is not null);
        foreach(var type in types)
        {
            var keys=type.GetProperties().Select(x=>x.GetCustomAttribute<KeyAttribute>()).Where(x=>x is not null).Select(x=>x!.IntKey!.Value).Order().ToArray();
            keys.Should().Equal(Enumerable.Range(0,keys.Length),type.Name);
            type.GetProperties().Should().NotContain(x=>x.Name == "Tradeability" || x.Name == "OutputHints" || x.Name == "HintTradeType" || x.Name == "FundId",type.Name);
        }
    }
}
