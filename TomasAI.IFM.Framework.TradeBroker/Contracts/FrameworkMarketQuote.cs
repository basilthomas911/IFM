namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>One top-of-book quote used by simulated market execution.</summary>
public readonly record struct FrameworkMarketQuote(string ContractId, decimal Bid, decimal Ask, int BidSize,
    int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);
