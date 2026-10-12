using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
public enum OptionPremiumTickRule { Unspecified = 0, Fixed = 1, CmeEsGlobex358A = 2 }
