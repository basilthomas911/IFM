namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>One provider-neutral top-of-book quote supplied to simulated market execution.</summary>
public readonly record struct BrokerMarketQuote(string ContractId, decimal Bid, decimal Ask, int BidSize,
    int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);
