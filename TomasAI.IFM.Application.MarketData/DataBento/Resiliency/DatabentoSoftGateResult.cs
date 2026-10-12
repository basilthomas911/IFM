namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoSoftGateResult(bool Qualified, int Rounds,
    IReadOnlyList<RecoveryProbeResult> Probes);
