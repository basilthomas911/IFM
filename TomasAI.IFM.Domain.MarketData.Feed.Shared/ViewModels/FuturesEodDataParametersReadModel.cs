using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataParametersReadModel(
    [property: Key(0)] FuturesEodDataV2ReadModel FuturesEodDataToday,
    [property: Key(1)] FuturesEodDataV2ReadModel[] FuturesEodDataRange,
    [property: Key(2)] NormalCurveTableReadModel NormalCurveTable)
{
}
