using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Identifies the ordered activities in the application startup workflow.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum ApplicationStartupActivity
{
    ResolveAuthority = 1,
    ReconcileReferenceData = 2,
    ReconcileCurrentContracts = 3,
    StartMarketData = 4,
    WarmHistoricalAnalytics = 5,
    StartRealtimeAnalytics = 6,
    QualifyOperationalState = 7,
    ApplyParameterSets = 8
}
