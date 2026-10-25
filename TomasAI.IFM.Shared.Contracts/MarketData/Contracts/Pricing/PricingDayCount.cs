using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
public enum PricingDayCount { Unknown = 0, Actual365Fixed = 1, Actual360 = 2 }
