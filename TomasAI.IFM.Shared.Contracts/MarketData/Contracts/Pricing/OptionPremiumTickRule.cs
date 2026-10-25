using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
public enum OptionPremiumTickRule { Unspecified = 0, Fixed = 1, CmeEsGlobex358A = 2 }
