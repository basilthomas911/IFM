namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IRecoveryInfrastructureProbe
{
    string Name { get; }
    RecoveryInfrastructureKind Kind { get; }
    Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken);
}
