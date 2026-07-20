using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade
{
    public class TradeDiaryId
    {
        private readonly int _orderId;
        private readonly int _tradeId;
        private DateOnly _valueDate;
        private readonly TradeStatus _tradeStatus;
        private readonly ActionSource _actionSource;
        private readonly ActionType _actionType;
        private readonly ActionSubType _actionSubType;
        private readonly ActionState _actionState;
        private readonly string _actionReason;

        public int OrderId => _orderId;
        public int TradeId => _tradeId;
        public DateOnly ValueDate => _valueDate;
        public TradeStatus TradeStatus => _tradeStatus;
        public ActionSource ActionSource => _actionSource;
        public ActionType ActionType => _actionType;
        public ActionSubType ActionSubType => _actionSubType;
        public ActionState ActionState => _actionState;
        public string ActionReason => _actionReason;

        public static TradeDiaryId Create(int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, ActionSource actionSource, ActionType actionType, ActionSubType actionSubType, ActionState actionState, string actionReason)
            => new TradeDiaryId(orderId, tradeId,valueDate, tradeStatus, actionSource, actionType, actionSubType, actionState, actionReason);

        [JsonConstructor]
        private TradeDiaryId(
            int orderId, 
            int tradeId, 
            DateOnly valueDate, 
            TradeStatus tradeStatus, 
            ActionSource actionSource, 
            ActionType actionType, 
            ActionSubType actionSubType, 
            ActionState actionState, 
            string actionReason)
        {
            _orderId = orderId;
            _tradeId = tradeId;
            _valueDate = valueDate;
            _tradeStatus = tradeStatus;
            _actionSource = actionSource;
            _actionType = actionType;
            _actionSubType = actionSubType;
            _actionState = actionState;
            _actionReason = actionReason ?? string.Empty;
        }

        public override string ToString() => JsonConvert.SerializeObject(new TradeDiaryId(OrderId, TradeId, ValueDate, TradeStatus, ActionSource, ActionType, ActionSubType, ActionState, ActionReason), Formatting.None);

        public override bool Equals(object obj) => (obj != null) && (obj is TradeDiaryId id)
            && id.OrderId == _orderId
            && id.TradeId == _tradeId
            && id.ValueDate == _valueDate
            && id.TradeStatus == _tradeStatus
            && id.ActionSource == _actionSource
            && id.ActionType == _actionType
            && id.ActionSubType == _actionSubType
            && id.ActionState == _actionState
            && id.ActionReason == _actionReason;

        public override int GetHashCode() => $"{this}".GetHashCode();
    }
}
