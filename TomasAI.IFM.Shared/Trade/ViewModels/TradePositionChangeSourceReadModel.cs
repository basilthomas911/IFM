using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels
{
    public class TradePositionChangeSourceReadModel
    {
  
        public TradePositionChangeSourceReadModel(
            TradePositionReadModel putTradePosition,
            TradePositionReadModel callTradePosition,
            TradePositionChangeSourceType tradePositionChangeSource,
            string optionLegId)
        {
            PutTradePosition = putTradePosition;
            CallTradePosition = callTradePosition;
            TradePositionChangeSource = tradePositionChangeSource;
            OptionLegId = optionLegId;
        }

        public TradePositionReadModel PutTradePosition { get; }
        public TradePositionReadModel CallTradePosition { get; }
        public TradePositionChangeSourceType TradePositionChangeSource { get; }
        public string OptionLegId { get; }
    }
}
