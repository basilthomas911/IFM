using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record struct TradePositionStateReadModel(
    int OrderId,
    int TradeId,
    TradePositionState TradePositionState,
    DateTime OpenedOn,
    string OpenedBy)
{
    [JsonIgnore]
    public TradePositionStateEntityId EntityId => new(OrderId, TradeId);

    [JsonIgnore]
    public bool IsValid => OrderId > 0 && TradeId > 0;
}
