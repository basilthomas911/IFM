using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Provides extension methods for the TradeLiveFeedStateType enumeration.
/// </summary>
/// <remarks>This class contains methods that enhance the functionality of the TradeLiveFeedStateType enumeration,
/// allowing for easier string representation of its values.</remarks>
public static class TradeLiveFeedStateTypeExtensions
{
    public static string ToStringFast(this TradeLiveFeedStateType value) => value switch
    {
        TradeLiveFeedStateType.Unknown => nameof(TradeLiveFeedStateType.Unknown),
        TradeLiveFeedStateType.Off => nameof(TradeLiveFeedStateType.Off),
        TradeLiveFeedStateType.Halted => nameof(TradeLiveFeedStateType.Halted),
        TradeLiveFeedStateType.On => nameof(TradeLiveFeedStateType.On),
        _ => value.ToString()
    };
}
