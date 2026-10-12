using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedQualificationResult
{
    public required bool Passed { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
}
