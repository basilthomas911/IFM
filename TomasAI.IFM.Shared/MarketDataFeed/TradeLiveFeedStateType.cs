using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Specifies the operational states of a trade live feed.
/// </summary>
/// <remarks>Use this enumeration to represent and monitor the current status of a trade live feed. The values
/// indicate whether the feed is active, halted, turned off, or in an unknown state. This can be useful for controlling
/// feed behavior and responding to changes in feed availability.</remarks>
public enum TradeLiveFeedStateType
{
    Unknown,
    Off,
    Halted,
    On
}

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
