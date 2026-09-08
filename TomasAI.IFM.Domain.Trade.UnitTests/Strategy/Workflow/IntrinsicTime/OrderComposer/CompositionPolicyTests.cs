using System.Collections.Immutable;
using System.Reflection;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CompositionPolicyTests
{
    [Theory]
    [InlineData(CompositionOperation.Set, 3)] [InlineData(CompositionOperation.Add, 5)]
    [InlineData(CompositionOperation.Subtract, -1)] [InlineData(CompositionOperation.Multiply, 6)]
    [InlineData(CompositionOperation.Minimum, 2)] [InlineData(CompositionOperation.Maximum, 3)]
    public void Every_numeric_operation_uses_explicit_ordered_bounds(CompositionOperation operation, decimal expected)
    {
        var rules = Rule(operation);
        // Fee can never be negative; use target delta as the bounded signed test parameter.
        rules = rules with { BaseParameters = rules.BaseParameters with { TargetNetDelta = .2m },
            HardBounds = [new() { Parameter = CompositionParameter.TargetNetDelta, Minimum = -1, Maximum = 1, Grid = .1m }],
            AdjustmentRules = [rules.AdjustmentRules[0] with { Parameter = CompositionParameter.TargetNetDelta, Operand = operation == CompositionOperation.Multiply ? 3 : .3m }] };
        var value = CompositionParameterResolver.Resolve(rules, new Dictionary<CompositionFeature, decimal> { [CompositionFeature.SelectionConfidence] = .9m }, default);
        Assert.Equal(expected / 10, value.Values.TargetNetDelta);
        Assert.Single(value.Evidence); Assert.NotEmpty(value.Hash);
    }
    [Fact]
    public void Missing_optional_feature_is_not_zero_and_mandatory_missing_feature_fails()
    {
        var rule = Rule(CompositionOperation.Set);
        var result = CompositionParameterResolver.Resolve(rule, new Dictionary<CompositionFeature, decimal>(), default);
        Assert.Equal("InputUnavailable", result.Evidence[0].Status);
        rule = rule with { AdjustmentRules = [rule.AdjustmentRules[0] with { Predicate = rule.AdjustmentRules[0].Predicate with { Required = true } }] };
        Assert.Throws<CompositionException>(() => CompositionParameterResolver.Resolve(rule, new Dictionary<CompositionFeature, decimal>(), default));
    }
    [Fact]
    public async Task Function_has_all_five_frozen_exact_maps_and_no_domain_deadline_overrides()
    {
        var fixture = new CompositionFunctionFixture(await CompositionFixture.Command());
        var actor = typeof(OrderCompositionFunctionActor);
        Assert.StartsWith("BaseEventSourceFunctionActor", actor.BaseType!.Name);
        foreach (var name in new[] { "_parseMap", "_validationMap", "_receiveMap", "_executionPolicyMap", "_eventMap" })
        {
            var field = actor.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Contains("Frozen", field.GetValue(field.IsStatic ? null : fixture.actor)!.GetType().FullName);
        }
        Assert.DoesNotContain(actor.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance), x => x.Name is "GetFunctionDeadline" or "Typed");
    }
    [Theory]
    [InlineData("schema")] [InlineData("hash")] [InlineData("route")] [InlineData("revision")]
    [InlineData("time")] [InlineData("reservation")]
    public async Task Invalid_contract_is_rejected_before_repository_load(string change)
    {
        var c = await CompositionFixture.Command();
        c = change switch
        {
            "schema" => c with { SchemaVersion = 0 }, "hash" => c with { InputSha256 = new('a', 64) },
            "route" => c with { PostEvents = true }, "revision" => c with { InputWorkflowRevision = 0 },
            "time" => c with { EvaluatedAtUtc = DateTime.SpecifyKind(c.EvaluatedAtUtc, DateTimeKind.Unspecified) },
            _ => c with { Reservation = null! }
        };
        var f = new CompositionFunctionFixture(c);
        var result = await f.Execute(); Assert.True(result.IsFailed); Assert.Empty(f.Order);
    }
    [Theory]
    [InlineData("portfolio")] [InlineData("fund")] [InlineData("date")] [InlineData("size")] [InlineData("malformed")]
    public void History_cursor_cannot_cross_the_original_scope(string change)
    {
        var date = new DateOnly(2026, 9, 8); var encoded = OrderCompositionPaging.Encode(1, 2, date, 10, [1, 2]);
        Assert.ThrowsAny<ArgumentException>(() => OrderCompositionPaging.Decode(change == "portfolio" ? 3 : 1,
            change == "fund" ? 3 : 2, change == "date" ? date.AddDays(1) : date, change == "size" ? 11 : 10,
            change == "malformed" ? "invalid" : encoded));
    }
    static CompositionVariantRules Rule(CompositionOperation operation) => new()
    {
        BaseParameters = CompositionDefaultProfiles.Parameters(TimeFrameType.Daily),
        HardBounds = [new() { Parameter = CompositionParameter.FeePerContract, Minimum = 0, Maximum = 10, Grid = .1m }],
        AdjustmentRules = [new() { Code = "Fee", Parameter = CompositionParameter.FeePerContract, Operation = operation, Operand = 3,
            Predicate = new() { Comparison = CompositionComparison.Greater, Feature = CompositionFeature.SelectionConfidence, Values = [.5m] } }]
    };
}
