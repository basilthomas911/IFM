using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Trade;

public record struct TradePositionStateEntityId(
    int OrderId, 
    int TradeId)
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
