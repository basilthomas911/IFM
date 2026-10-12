using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
public enum PricingDayCount { Unknown = 0, Actual365Fixed = 1, Actual360 = 2 }
