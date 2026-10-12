namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record RecoveryProbeResult(string Name, RecoveryInfrastructureKind Kind,
    bool Qualified, string Detail);
