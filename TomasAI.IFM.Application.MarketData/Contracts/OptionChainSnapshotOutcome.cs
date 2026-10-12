using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Explicit outcomes; unavailability is not permission to use stale quotes.</summary>
public enum OptionChainSnapshotOutcome { Ready, Deferred, NoTrade, Rejected }
