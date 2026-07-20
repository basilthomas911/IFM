using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Trade;

public enum TradeType
{
    Unknown,
    ShortPut,
    LongPut,
    ShortCall,
    LongCall,
    PutCreditSpread,
    PutDebitSpread,
    CallCreditSpread,
    CallDebitSpread,
    ShortIronCondor,
    LongIronCondor
}

public static class TradeTypeExtensions
{
    public static string ToStringFast(this TradeType value) => value switch
    {
        TradeType.Unknown => nameof(TradeType.Unknown),
        TradeType.ShortPut => nameof(TradeType.ShortPut),
        TradeType.LongPut => nameof(TradeType.LongPut),
        TradeType.ShortCall => nameof(TradeType.ShortCall),
        TradeType.LongCall => nameof(TradeType.LongCall),
        TradeType.PutCreditSpread => nameof(TradeType.PutCreditSpread),
        TradeType.PutDebitSpread => nameof(TradeType.PutDebitSpread),
        TradeType.CallCreditSpread => nameof(TradeType.CallCreditSpread),
        TradeType.CallDebitSpread => nameof(TradeType.CallDebitSpread),
        TradeType.ShortIronCondor => nameof(TradeType.ShortIronCondor),
        TradeType.LongIronCondor => nameof(TradeType.LongIronCondor),
        _ => value.ToString()
    };
}
