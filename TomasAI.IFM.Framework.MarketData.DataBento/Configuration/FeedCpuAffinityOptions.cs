namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedCpuAffinityOptions
{
    public bool PinFeedThreads { get; init; } = true;
    public CpuAffinityMode Mode { get; init; } = CpuAffinityMode.AutoPerformanceCores;
    public LogicalProcessorLocation? NativeProducer { get; init; }
    public LogicalProcessorLocation? ManagedDrain { get; init; }
    public bool RequirePerformanceCore { get; init; } = true;
    public bool AllowAffinityFallback { get; init; } = true;
}
