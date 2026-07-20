using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataParametersReadModel(
    [property: Key(0)] FuturesEodDataV2ReadModel FuturesEodDataToday,
    [property: Key(1)] FuturesEodDataV2ReadModel[] FuturesEodDataRange,
    [property: Key(2)] NormalCurveTableReadModel NormalCurveTable)
{
}
