using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Identifies the observed market price basis; a quote is never an executed source trade.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero represents no valid persisted business choice; adding it would introduce an unsupported value.")]
public enum OptionMarketPriceBasis { Trade = 1, QuoteMidpoint = 2 }
