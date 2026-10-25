using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
public enum OptionSettlementStyle { Unknown = 0, DeliveryOfFuture = 1, Cash = 2 }
