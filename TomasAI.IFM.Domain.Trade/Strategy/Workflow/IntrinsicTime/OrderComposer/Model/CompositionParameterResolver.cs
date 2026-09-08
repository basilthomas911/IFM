using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionRulesContract;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Resolves bounded, ordered adjustments against explicitly available frozen features.</summary>
public static partial class CompositionParameterResolver
{
    public static void ValidateBounds(CompositionVariantRules rules)
    {
        foreach (var bound in rules.HardBounds)
        {
            var baseline = Read(rules.BaseParameters, bound.Parameter);
            Require(baseline >= bound.Minimum && baseline <= bound.Maximum && bound.Grid > 0, "OC.CONFIG.RULE_INVALID");
            _ = Write(rules.BaseParameters, bound.Parameter, Quantize(baseline, bound));
        }
    }

    public static CompositionResolvedParameters Resolve(CompositionVariantRules rules,
        IReadOnlyDictionary<CompositionFeature, decimal> features, CancellationToken token)
    {
        var values = rules.BaseParameters;
        var evidence = ImmutableArray.CreateBuilder<CompositionRuleEvidence>();
        foreach (var bound in rules.HardBounds)
        {
            var before = Read(values, bound.Parameter);
            Require(before >= bound.Minimum && before <= bound.Maximum, "OC.CONFIG.RULE_INVALID");
            var after = Quantize(before, bound);
            values = Write(values, bound.Parameter, after);
            if (before != after) evidence.Add(new() { Code = "BaseGrid:" + bound.Parameter, Parameter = bound.Parameter,
                Before = before, Unclamped = before, After = after, Status = "Quantized" });
        }
        foreach (var rule in rules.AdjustmentRules.OrderBy(x => x.Priority).ThenBy(x => x.Code, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var before = Read(values, rule.Parameter);
            var matches = Evaluate(rule.Predicate, features);
            if (matches != true)
            {
                evidence.Add(new() { Code = rule.Code, Parameter = rule.Parameter, Before = before, Unclamped = before,
                    After = before, Status = matches is null ? "InputUnavailable" : "PredicateFalse" });
                continue;
            }
            var raw = rule.Operation switch
            {
                CompositionOperation.Set => rule.Operand,
                CompositionOperation.Add => checked(before + rule.Operand),
                CompositionOperation.Subtract => checked(before - rule.Operand),
                CompositionOperation.Multiply => checked(before * rule.Operand),
                CompositionOperation.Minimum => Math.Min(before, rule.Operand),
                CompositionOperation.Maximum => Math.Max(before, rule.Operand),
                _ => throw new CompositionException("OC.CONFIG.RULE_INVALID")
            };
            var bound = rules.HardBounds.Single(x => x.Parameter == rule.Parameter);
            var after = Quantize(raw, bound);
            values = Write(values, rule.Parameter, after);
            evidence.Add(new() { Code = rule.Code, Parameter = rule.Parameter, Before = before, Unclamped = raw,
                After = after, Status = after == raw ? "Applied" : "Clamped" });
        }
        Validate(values);
        var result = new CompositionResolvedParameters { Values = values, Evidence = evidence.ToImmutable() };
        return result with { Hash = CompositionHash.Compute(result) };
    }

static decimal Quantize(decimal value, CompositionParameterBound bound)
    {
        var first = decimal.Ceiling(bound.Minimum / bound.Grid);
        var last = decimal.Floor(bound.Maximum / bound.Grid);
        Require(first <= last, "OC.CONFIG.RULE_INVALID");
        return Math.Clamp(decimal.Round(Math.Clamp(value, bound.Minimum, bound.Maximum) / bound.Grid,
            0, MidpointRounding.ToEven), first, last) * bound.Grid;
    }

    static bool? Evaluate(CompositionPredicate p, IReadOnlyDictionary<CompositionFeature, decimal> features)
    {
        if (p.Comparison is CompositionComparison.All or CompositionComparison.Any)
        {
            var children = p.Children.Select(x => Evaluate(x, features)).ToArray();
            if (children.Any(x => x is null)) return null;
            return p.Comparison == CompositionComparison.All ? children.All(x => x == true) : children.Any(x => x == true);
        }
        if (!features.TryGetValue(p.Feature, out var actual))
        {
            Require(!p.Required, "OC.CONTRACT.REQUIRED_FIELD");
            return null;
        }
        return p.Comparison switch
        {
            CompositionComparison.Equal => actual == p.Values[0],
            CompositionComparison.Less => actual < p.Values[0],
            CompositionComparison.LessOrEqual => actual <= p.Values[0],
            CompositionComparison.Greater => actual > p.Values[0],
            CompositionComparison.GreaterOrEqual => actual >= p.Values[0],
            CompositionComparison.In => p.Values.Contains(actual),
            _ => throw new CompositionException("OC.CONFIG.RULE_INVALID")
        };
    }
}
