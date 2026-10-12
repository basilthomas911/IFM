namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoHardRecoveryRequest(Guid CorrelationId, DateOnly ValueDate,
    Guid ExpectedGenerationId, string Source, string Reason);
