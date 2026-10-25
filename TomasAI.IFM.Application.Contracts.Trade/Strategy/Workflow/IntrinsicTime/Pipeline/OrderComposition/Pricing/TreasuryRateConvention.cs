using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Verified quotation convention; zero remains unqualified for legacy observations.</summary>
public enum TreasuryRateConvention { Unknown = 0, UsTreasuryCmtNominalSemiannual = 1 }
