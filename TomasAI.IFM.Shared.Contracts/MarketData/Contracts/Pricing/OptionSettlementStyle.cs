using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
public enum OptionSettlementStyle { Unknown = 0, DeliveryOfFuture = 1, Cash = 2 }
