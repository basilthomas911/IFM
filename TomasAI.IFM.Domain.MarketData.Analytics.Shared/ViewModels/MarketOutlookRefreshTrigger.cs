using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Identifies what caused the latest versionless Market Outlook cache replacement.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketOutlookRefreshTrigger : byte
{
    None = 0,
    Component = 1,
    EsTrade = 2,
    EodSession = 3,
    Warmup = 4,
    PersistedBaseline = 5
}
