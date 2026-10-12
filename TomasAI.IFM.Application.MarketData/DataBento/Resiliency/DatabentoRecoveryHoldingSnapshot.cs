namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoRecoveryHoldingSnapshot(int Capacity, int Count,
    long Accepted, long Dropped);
