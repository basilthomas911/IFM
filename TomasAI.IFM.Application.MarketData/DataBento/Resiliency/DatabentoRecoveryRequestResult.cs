namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Typed outcome of an accepted recovery or an ignored concurrent request.</summary>
public sealed record DatabentoRecoveryRequestResult(
    Guid CorrelationId,
    DatabentoRecoveryRequestOutcome Outcome,
    DatabentoHardRecoveryResult? HardResult,
    string Detail);
