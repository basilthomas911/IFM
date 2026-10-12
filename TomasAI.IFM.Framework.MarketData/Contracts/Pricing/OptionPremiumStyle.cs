using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
public enum OptionPremiumStyle { Unknown = 0, PaidUpfront = 1, FuturesStyle = 2 }
