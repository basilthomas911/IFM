using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesRiskPositionType
{
    internal static async ValueTask GetFuturesRiskPositionTypeAsync(
        this GetFuturesRiskPositionTypeQuery q,
        IQueryActorContext context,
        IDbContextFactory dbFactory)
    {
        var riskPositionType = RiskPositionType.Unknown;

        var futuresEodData = await dbFactory.MarketDataDb.GetCurrentFuturesEodDataAsync(q.ValueDate);
        if (futuresEodData is not null)
        {
            var riskPositionValue = q.TradeType switch
            {
                TradeType.ShortIronCondor => GetShortIronCondorRiskPosition(futuresEodData),
                TradeType.LongIronCondor => GetLongIronCondorRiskPosition(futuresEodData),
                _ => throw new NotImplementedException($"Risk Position Type not implemented for: {q.TradeType}")
            };
            riskPositionType = riskPositionValue switch
            {
                _ when riskPositionValue < 3 => RiskPositionType.Low,
                _ when riskPositionValue == 3 => RiskPositionType.Medium,
                _ when riskPositionValue > 3 => RiskPositionType.High,
                _ => RiskPositionType.Unknown
            };
        }

        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesRiskPositionTypeQuery.Verb, new ServiceResult<RiskPositionTypeReadModel>(new RiskPositionTypeReadModel(riskPositionType)));

        static int GetShortIronCondorRiskPosition(FuturesEodDataV2ReadModel futuresEodData)
        {
            var riskPositionValue = 0;
            if (futuresEodData.MarketDirection == MarketDirectionType.NeutralUp || futuresEodData.MarketDirection == MarketDirectionType.Up)
                riskPositionValue++;
            if (futuresEodData.MarketVolatility == MarketVolatilityType.Normal || futuresEodData.MarketVolatility == MarketVolatilityType.Falling)
                riskPositionValue++;
            if (futuresEodData.PriceDirection == PriceDirectionType.Rising)
                riskPositionValue++;
            if (futuresEodData.PriceVolatility == PriceVolatilityType.Falling)
                riskPositionValue++;
            return riskPositionValue;
        }

        static int GetLongIronCondorRiskPosition(FuturesEodDataV2ReadModel futuresEodData)
        {
            var riskPositionValue = 0;
            if (futuresEodData.MarketDirection == MarketDirectionType.NeutralDown || futuresEodData.MarketDirection == MarketDirectionType.Down)
                riskPositionValue++;
            if (futuresEodData.MarketVolatility == MarketVolatilityType.Rising || futuresEodData.MarketVolatility == MarketVolatilityType.High)
                riskPositionValue++;
            if (futuresEodData.PriceDirection == PriceDirectionType.Falling)
                riskPositionValue++;
            if (futuresEodData.PriceVolatility == PriceVolatilityType.Rising)
                riskPositionValue++;
            return riskPositionValue;
        }
    }
}
