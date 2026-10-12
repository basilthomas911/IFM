using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>A coherent market snapshot; every quote must have the same source epoch and sequence.</summary>
public sealed record EmulatorQuote(string ContractId, decimal Bid, decimal Ask, int BidSize, int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);
