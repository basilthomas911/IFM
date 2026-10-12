using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new FeedQualificationBaseline instance.</summary>
/// <param name="RecordsPerSecond">The records per second.</param>
/// <param name="P99Latency">The p99 latency.</param>
public sealed record FeedQualificationBaseline(
    double RecordsPerSecond,
    TimeSpan P99Latency);
