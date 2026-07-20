using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public class TradeDiaryEntryReadModel
{

    [JsonConstructor]
    public TradeDiaryEntryReadModel(
        DateTime entryDate,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        ActionSource actionSource,
        ActionType actionType,
        ActionSubType actionSubType,
        ActionState actionState,
        string actionReason,
        string actionDataType = null,
        string actionData = null)
    {
        Id = TradeDiaryId.Create(orderId, tradeId, valueDate, tradeStatus, actionSource, actionType, actionSubType, actionState, actionReason);
        OptionTradeId = new(Id.OrderId, Id.TradeId);
        EntryDate = entryDate;
        ActionDataType = actionDataType;
        ActionData = actionData;
    }

    public TradeDiaryEntryReadModel(TradePositionEntityId id, TradePositionActionReadModel tpa):this(
        entryDate: DateTime.Now,
        orderId: id.OrderId,
        tradeId: id.TradeId,
        valueDate: id.ValueDate,
        tradeStatus: id.TradeStatus,
        actionSource: tpa.ActionSource,
        actionType: tpa.ActionType,
        actionSubType: tpa.ActionSubType,
        actionState: tpa.ActionState,
        actionReason: tpa.ActionReason )
    {
    }

    public TradeDiaryEntryReadModel(ActionType actionType, TradeDiaryEntryReadModel e) :this(
        entryDate: e.EntryDate,
        orderId: e.OrderId,
        tradeId: e.TradeId,
        valueDate: e.ValueDate,
        tradeStatus: e.TradeStatus,
        actionSource: e.ActionSource,
        actionType: actionType,
        actionSubType: e.ActionSubType,
        actionState: e.ActionState,
        actionReason: e.ActionReason)
    {
    }

    [JsonIgnore]
    public TradeDiaryId Id { get; }
    [JsonIgnore]
    public OptionTradeEntityId OptionTradeId { get; }
    public DateTime EntryDate { get; }
    public int OrderId => Id.OrderId;
    public int TradeId => Id.TradeId;
    public DateOnly ValueDate => Id.ValueDate;
    public TradeStatus TradeStatus => Id.TradeStatus;
    public ActionSource ActionSource => Id.ActionSource;
    public ActionType ActionType => Id.ActionType;
    public ActionSubType ActionSubType => Id.ActionSubType;
    public ActionState ActionState => Id.ActionState;
    public string ActionReason => Id.ActionReason ?? string.Empty;
    public string ActionDataType { get; }
    public string ActionData { get; }
}
