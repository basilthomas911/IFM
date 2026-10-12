using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

public enum VolatilityPublicationStage
{
    None = 0,
    BeforeSources = 1,
    BeforeSnapshotSeal = 2,
    BeforeMetricHistory = 3,
    BeforeLatestAdvertisement = 4
}
