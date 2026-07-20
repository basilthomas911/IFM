using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Extensions
{
    public static class TradePositionReadModelExtension
    {
      
        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType tradeType, TradeStatus tradeStatus)
           => tradePosition
                .OrderBy(e => e.EntityId.ValueDate)
                .Where(e => e.EntityId.TradeType == tradeType && e.EntityId.TradeStatus == tradeStatus)
                .LastOrDefault();

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, OptionType optionType, TradeStatus tradeStatus)
        {
            var tradeType = GetTradePositionTradeType(baseTradeType, optionType);
            return tradePosition
                 .OrderBy(e => e.EntityId.ValueDate)
                 .Where(e => e.EntityId.TradeType == tradeType && e.EntityId.TradeStatus == tradeStatus)
                 .LastOrDefault();
        }


        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate)
             => tradePosition
                  .Where(e => e.EntityId.TradeType == tradeType && e.EntityId.TradeStatus == tradeStatus && $"{e.EntityId.ValueDate:yyyyMMdd}" == $"{valueDate:yyyyMMdd}")
                  .LastOrDefault();

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradeType baseTradeType, OptionType optionType, TradeStatus tradeStatus, DateOnly valueDate)
        {
            var tradeType = GetTradePositionTradeType(baseTradeType, optionType);
            return tradePosition
                 .Where(e => e.EntityId.TradeType == tradeType && e.EntityId.TradeStatus == tradeStatus && $"{e.EntityId.ValueDate:yyyyMMdd}" == $"{valueDate:yyyyMMdd}")
                 .LastOrDefault();
        }

        public static TradePositionReadModel? Get(this TradePositionReadModel[] tradePosition, TradePositionEntityId key)
            => tradePosition
                .Where(e => e.EntityId.Equals(key))
                .LastOrDefault();

        public static decimal GetTradePnl(this TradePositionReadModel[] tradePosition)
            => tradePosition.Sum(e => e.TradePnl);

        public static decimal GetEodTradePnl(this TradePositionReadModel[] tradePosition) 
            => tradePosition.Where(e => e.TradeStatus == TradeStatus.Open && e.TradeStatus == TradeStatus.EndOfDay).Sum(e => e.TradePnl);

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

    }
}
