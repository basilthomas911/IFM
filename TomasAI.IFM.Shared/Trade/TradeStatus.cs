using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// 
/// </summary>
public enum TradeStatus
{
    Open,
    IntraDay,
    EndOfDay,
    Close
}

public static class TradeStatusExtensions
{
    public static string ToStringFast(this TradeStatus value) => value switch
    {
        TradeStatus.Open => nameof(TradeStatus.Open),
        TradeStatus.IntraDay => nameof(TradeStatus.IntraDay),
        TradeStatus.EndOfDay => nameof(TradeStatus.EndOfDay),
        TradeStatus.Close => nameof(TradeStatus.Close),
        _ => value.ToString()
    };
}
