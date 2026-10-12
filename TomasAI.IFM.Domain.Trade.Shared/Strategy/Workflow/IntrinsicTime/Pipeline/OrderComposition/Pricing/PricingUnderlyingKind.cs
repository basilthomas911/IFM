using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
public enum PricingUnderlyingKind { Unknown = 0, Futures = 1, Equity = 2 }
