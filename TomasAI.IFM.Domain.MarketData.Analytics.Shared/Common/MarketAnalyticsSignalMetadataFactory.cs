using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Creates consistent signal lineage from an immutable futures session bar.</summary>
public static class MarketAnalyticsSignalMetadataFactory
{
    /// <summary>Creates metadata for a signal calculated from the supplied observation.</summary>
    public static MarketAnalyticsSignalMetadata Create(
        FuturesTradeSessionBarReadModel observation,
        MarketAnalyticsSignalKind signalKind,
        string configurationId,
        string calculationVersion,
        ushort schemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new()
        {
            SignalKey = new(observation.MarketSeriesIdentity, signalKind, observation.TimeFrame, configurationId),
            ContractId = observation.ContractId,
            ValueDate = observation.ValueDate,
            ObservationId = observation.ObservationId,
            MarketDataAsOfUtc = observation.LastMarketEventUtc,
            CalculatedAtUtc = observation.CalculatedAtUtc,
            SourceSequence = observation.LastSourceSequence,
            StreamEpochId = observation.StreamEpochId,
            SchemaVersion = schemaVersion,
            CalculationVersion = calculationVersion,
            CalculationMethod = observation.CalculationMethod,
            IsValid = observation.IsValid,
            ValidationIssues = observation.ValidationIssues
        };
    }
}
