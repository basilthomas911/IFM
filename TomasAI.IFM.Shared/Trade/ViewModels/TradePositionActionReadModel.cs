using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels
{
    [MessagePackObject(true)]
    public partial class TradePositionActionReadModel
    {
 
        [JsonConstructor]
        public TradePositionActionReadModel(
            ActionSource actionSource,
            ActionType actionType,
            ActionSubType actionSubType,
            ActionState actionState,
            string actionReason)
        {
            ActionSource = actionSource;
            ActionType = actionType;
            ActionSubType = actionSubType;
            ActionState = actionState;
            ActionReason = actionReason;
        }

        public ActionSource ActionSource { get; }
        public ActionType ActionType { get; private set; }
        public ActionSubType ActionSubType { get; }
        public ActionState ActionState { get; }
        public string ActionReason { get; }

        public TradePositionActionReadModel SetActionType(ActionType actionType)
        {
            ActionType = actionType;
            return this;
        }
    }
}
