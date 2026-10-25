using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

/// <summary>Strict rules reader and semantic hash. Existing selection/Portfolio hash formats remain unchanged.</summary>
public static class CompositionHash
{
    public static string Compute<T>(T value) => CompositionSemanticHash.Compute(value);
    public static string Candidate(CompositionCandidate value) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return Compute(value with { CandidateHash = "", DecisionEvidence = null });
    }
    public static string Binding(CompositionBinding value) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return Compute(value with { BindingSha256 = "" });
    }
}
