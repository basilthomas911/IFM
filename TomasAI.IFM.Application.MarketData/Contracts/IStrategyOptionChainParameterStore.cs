using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Combined application capability for option chain parameter projections.</summary>
public interface IStrategyOptionChainParameterStore : IStrategyOptionChainParameterReadStore, IStrategyOptionChainParameterWriteStore;
