using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
public enum OptionPremiumStyle { Unknown = 0, PaidUpfront = 1, FuturesStyle = 2 }
