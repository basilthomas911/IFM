using System.Text.Json.Nodes;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
public sealed class TradeSelectionPolicyTests
{
    [Fact]
    public void Engineering_manifest_has_exactly_three_stable_fully_explicit_profiles()
    {
        var rows=TradeSelectionDefaultProfiles.EngineeringDefaults();rows.Select(x=>x.TargetHorizon).Should().Equal(TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly);
        rows.Select(x=>x.ParameterSetId).Distinct().Should().HaveCount(3);
        rows.Select(TradeSelectionPolicy.Hash).Should().Equal(TradeSelectionDefaultProfiles.EngineeringDefaults().Select(TradeSelectionPolicy.Hash));
    }

    [Theory]
    [InlineData("valid")] [InlineData("missing")] [InlineData("duplicate")] [InlineData("unknown")] [InlineData("override")]
    public void Specialized_rules_are_a_complete_bounded_replacement(string scenario)
    {
        var common=TradeSelectionDefaultProfiles.Create(Guid.NewGuid(),TimeFrameType.Daily);
        var rules=common.VariantRules;
        if(scenario=="missing")rules=rules.Skip(1).ToArray();
        if(scenario=="duplicate")rules=[..rules.Take(11),rules[0]];
        if(scenario=="unknown")rules=[rules[0] with {BuilderCapabilityCode="Unimplemented"},..rules.Skip(1)];
        var json=System.Text.Json.JsonSerializer.Serialize(new TradeSelectionSpecializedRules {SchemaVersion=1,Rules=rules});
        if(scenario=="override")json=json[..^1]+",\"MinimumRegimeConfidence\":0}";
        Action read=()=>TradeSelectionPolicy.ReadSpecialized(json,common);
        if(scenario=="valid") read.Should().NotThrow();
        else if(scenario=="override") read.Should().Throw<System.Text.Json.JsonException>();
        else read.Should().Throw<ArgumentException>();
    }

    [Theory,InlineData(TimeFrameType.Daily),InlineData(TimeFrameType.Weekly),InlineData(TimeFrameType.Monthly)]
    [Trait("Fixture","TS-C22")]
    public void Defaults_roundtrip_all_fields_and_twelve_variants(TimeFrameType horizon)
    {
        var p=TradeSelectionDefaultProfiles.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"),horizon);
        var json=TradeSelectionPolicy.Serialize(p);
        JsonNode.Parse(json)!.AsObject().Count.Should().Be(38);
        var copy=TradeSelectionPolicy.Read(json);
        TradeSelectionPolicy.Hash(copy).Should().Be(TradeSelectionPolicy.Hash(p));
        TradeSelectionPolicy.Hash(MessagePackSerializer.Deserialize<TradeSelectionParameterSet>(MessagePackSerializer.Serialize(p))).Should().Be(TradeSelectionPolicy.Hash(p));
        p.VariantRules.Select(TradeSelectionPolicy.Signature).Distinct().Count().Should().Be(12);
        foreach(var field in JsonNode.Parse(json)!.AsObject().Select(x=>x.Key))
        {
            var incomplete=JsonNode.Parse(json)!.AsObject(); incomplete.Remove(field);
            Action read=()=>TradeSelectionPolicy.Read(incomplete.ToJsonString());
            read.Should().Throw<Exception>("every policy field, including "+field+", is required");
        }
    }
    [Theory,InlineData("unknown"),InlineData("duplicate"),InlineData("range"),InlineData("variant"),InlineData("schema")]
    [Trait("Fixture","TS-C23")]
    public void Invalid_configuration_never_silently_defaults(string scenario)
    {
        var p=TradeSelectionDefaultProfiles.Create(Guid.NewGuid(),TimeFrameType.Daily);
        var json=TradeSelectionPolicy.Serialize(p);
        var node=JsonNode.Parse(json)!.AsObject();
        switch(scenario)
        {
            case "unknown":node["FutureField"]=true;json=node.ToJsonString();break;
            case "duplicate":json=json.Insert(1,"\"SchemaVersion\":1,");break;
            case "range":node["MinimumRegimeConfidence"]=1.001m;json=node.ToJsonString();break;
            case "variant":node["VariantRules"]!.AsArray().RemoveAt(0);json=node.ToJsonString();break;
            case "schema":node["SchemaVersion"]=2;json=node.ToJsonString();break;
        }
        Action act=()=>TradeSelectionPolicy.Read(json);act.Should().Throw<Exception>();
    }
    [Fact,Trait("Fixture","TS-C24")]
    public void Policy_arrays_cannot_be_mutated_by_caller()
    {
        var p=TradeSelectionDefaultProfiles.Create(Guid.NewGuid(),TimeFrameType.Daily);var hash=TradeSelectionPolicy.Hash(p);
        p.AllowedRegimeDirections[0]=(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDirection)254;
        p.VariantRules[0].AllowedTrendPhases[0]=(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.TrendRegimePhase)254;
        TradeSelectionPolicy.Hash(p).Should().Be(hash);
    }
    [Theory,InlineData("Up","Bullish"),InlineData("Long","Bullish"),InlineData("Bullish","Bullish"),InlineData("Down","Bearish"),InlineData("Short","Bearish"),InlineData("Bearish","Bearish"),InlineData("Neutral","Neutral")]
    [Trait("Fixture","TS-C03")]
    public void Direction_permission_mapping_is_explicit(string input,string expected)=>TradeSelectionEvaluator.NormalizeDirection(input).Should().Be(expected);
    [Fact]
    public void Unsupported_permission_is_configuration_failure()
    {
        Action act=()=>TradeSelectionEvaluator.NormalizeDirection("Balanced");act.Should().Throw<TradeSelectionValidationException>();
    }
}
