using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1720", Justification = "The message field name is part of the provider protocol contract.")]
public enum TradeBlotterPositionIntent : byte { Short = 1, Long = 2 }
