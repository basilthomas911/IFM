using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Accepted direction of the option strategy. Catalog Balanced maps explicitly to Neutral.</summary>
public enum OptionStrategyMarketBias { Neutral, Bullish, Bearish }
