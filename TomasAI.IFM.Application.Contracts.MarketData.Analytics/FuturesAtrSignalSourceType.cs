using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Specifies the source of Average True Range (ATR) signals used in futures trading analytics.
/// </summary>
/// <remarks>Use this enumeration to indicate whether ATR signals are derived from ITI signals, intraday data, or
/// are unspecified. Selecting the appropriate source type can help tailor trading strategies based on the origin of the
/// ATR calculation.</remarks>
public enum FuturesAtrSignalSourceType
{
    None,
    FuturesItiSignal,
    FuturesIntraDayData
}
