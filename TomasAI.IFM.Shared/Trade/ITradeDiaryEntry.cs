using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade
{
    public interface ITradeDiaryEntry: IEvent
    {
        OptionTradeEntityId EntityId { get; }
        new TradeDiaryId Id { get; }
        DateTime EntryDate { get; }
        int OrderId { get; }
        int TradeId { get; }
        DateOnly valueDate { get; }
        TradeStatus TradeStatus { get; }
        ActionSource ActionSource { get; }
        ActionType ActionType { get; }
        ActionSubType ActionSubType { get; }
        ActionState ActionState { get; }
        string ActionReason { get; }
        string ActionDataType { get; }
        string ActionData { get; }
    }
}
