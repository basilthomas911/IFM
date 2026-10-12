using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentBinding
{
    [Key(0)] public short ModeVersion { get; init; } = 1;
    [Key(1)] public MarketConditionAssessmentParameterSet Parameters { get; init; } = new();
    [Key(2)] public string PayloadSha256 { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless model validation reports malformed persisted instance content, not a caller argument.")]
    public void Validate()
    {
        if (ModeVersion != 1 || !string.Equals(PayloadSha256,MarketConditionAssessmentHash.Parameters(Parameters),StringComparison.Ordinal))
            throw new ArgumentException("Invalid frozen assessment mode or parameter hash.");
    }
}
