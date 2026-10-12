using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum TradeBlotterStrategy : byte { IronCondor = 1, VerticalSpread = 2, FuturesOutright = 3 }
