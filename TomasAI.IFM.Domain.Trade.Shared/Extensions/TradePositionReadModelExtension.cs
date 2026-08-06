using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Extensions
{
    public static class TradePositionReadModelExtension
    {
      
        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType tradeType, TradeStatus tradeStatus)
           => GetLatest(tradePosition, tradeType, tradeStatus);

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, OptionType optionType, TradeStatus tradeStatus)
        {
            var tradeType = GetTradePositionTradeType(baseTradeType, optionType);
            return GetLatest(tradePosition, tradeType, tradeStatus);
        }


        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate)
             => GetLast(tradePosition, tradeType, tradeStatus, valueDate);

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, OptionType optionType, TradeStatus tradeStatus, DateOnly valueDate)
        {
            var tradeType = GetTradePositionTradeType(baseTradeType, optionType);
            return GetLast(tradePosition, tradeType, tradeStatus, valueDate);
        }

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradePositionEntityId key)
        {
            for (var index = tradePosition.Length - 1; index >= 0; index--)
                if (tradePosition[index].EntityId.Equals(key))
                    return tradePosition[index];
            return null;
        }

        public static decimal GetTradePnl(this TradePositionReadModel[] tradePosition)
            => tradePosition.Sum(e => e.TradePnl);

        public static decimal GetEodTradePnl(this TradePositionReadModel[] tradePosition)
        {
            var result = 0m;
            foreach (var position in tradePosition)
                if (position.TradeStatus is TradeStatus.Open or TradeStatus.EndOfDay)
                    result += position.TradePnl;
            return result;
        }

        public static decimal GetNetSpread(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, TradeStatus tradeStatus)
        {
            var putSpreadTradeType = GetTradePositionTradeType(baseTradeType, OptionType.Put);
            var putTradePosition = tradePosition.Get(putSpreadTradeType, tradeStatus);
            var callSpreadTradeType = GetTradePositionTradeType(baseTradeType, OptionType.Call);
            var callTradePosition = tradePosition.Get(callSpreadTradeType, tradeStatus);
            return putTradePosition is not null && callTradePosition is not null
                ? Math.Abs(putTradePosition.NetSpread + callTradePosition.NetSpread)
                : 0.0m;
        }

        public static decimal GetForwardPrice(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, TradeStatus tradeStatus)
        {
            var putSpreadTradeType = GetTradePositionTradeType(baseTradeType, OptionType.Put);
            var putTradePosition = tradePosition.Get(putSpreadTradeType, tradeStatus);
            var callSpreadTradeType = GetTradePositionTradeType(baseTradeType, OptionType.Call);
            var callTradePosition = tradePosition.Get(callSpreadTradeType, tradeStatus);
            return putTradePosition is not null && callTradePosition is not null
                ? Math.Abs(putTradePosition.ForwardPrice) + Math.Abs(callTradePosition.ForwardPrice)
                : 0.0m;

            /*
            decimal GetForwardPriceByTradeType()
              => baseTradeType switch
              {
                  TradeType.ShortIronCondor => Math.Abs(putTradePosition.ForwardPrice) - (1 * Math.Abs(callTradePosition.ForwardPrice)),
                  TradeType.LongIronCondor => Math.Abs(putTradePosition.ForwardPrice) - (1 * Math.Abs(callTradePosition.ForwardPrice)),
                  _ => throw new NotImplementedException()
              };
            */
        }


        public static double GetFowardLossRatio(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, TradeStatus tradeStatus, decimal limitPrice)
        {
            var forwardPrice = GetForwardPrice(tradePosition, baseTradeType, tradeStatus);
            return (double) (forwardPrice == 0.0m ? 0.0m : forwardPrice / limitPrice);
        }

        public static void Set(this TradePositionReadModel[] tradePosition, TradePositionReadModel? newTradePosition)
        {
            if (tradePosition is null || newTradePosition is null) return;
            for (var index = tradePosition.Length-1; index >= 0; index--)
            {
                var e = tradePosition[index];
                if (e.EntityId.Equals(newTradePosition.EntityId))
                {
                    tradePosition[index] = newTradePosition;
                    break;
                }
            }
        }

        public static void Set(this TradePositionReadModel[] tradePosition, TradePositionReadModel oldTradePosition, TradePositionReadModel? newTradePosition)
        {
            if (tradePosition is null || newTradePosition is null) return;
            for (var index = tradePosition.Length - 1; index >= 0; index--)
            {
                var e = tradePosition[index];
                if (e.EntityId.Equals(oldTradePosition.EntityId))
                {
                    tradePosition[index] = newTradePosition;
                    break;
                }
            }
        }
        private static TradeType GetTradePositionTradeType(TradeType tradeType, OptionType optionType)
            => tradeType switch {
                TradeType.ShortIronCondor => optionType == OptionType.Put ? TradeType.PutCreditSpread : TradeType.CallCreditSpread,
                TradeType.LongIronCondor => optionType == OptionType.Put ? TradeType.PutDebitSpread : TradeType.CallDebitSpread,
                _ => throw new NotImplementedException()
            };

        static TradePositionReadModel? GetLatest(TradePositionReadModel[] positions, TradeType tradeType, TradeStatus tradeStatus)
        {
            TradePositionReadModel? latest = null;
            foreach (var position in positions)
            {
                if (position.TradeType == tradeType
                    && position.TradeStatus == tradeStatus
                    && (latest is null || position.ValueDate >= latest.ValueDate))
                    latest = position;
            }
            return latest;
        }

        static TradePositionReadModel? GetLast(TradePositionReadModel[] positions, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate)
        {
            for (var index = positions.Length - 1; index >= 0; index--)
            {
                var position = positions[index];
                if (position.TradeType == tradeType && position.TradeStatus == tradeStatus && position.ValueDate == valueDate)
                    return position;
            }
            return null;
        }

    }
}
