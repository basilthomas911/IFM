using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

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
