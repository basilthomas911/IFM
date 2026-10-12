using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts;

/// <summary>Verified quotation convention; zero remains unqualified for legacy observations.</summary>
public enum TreasuryRateConvention { Unknown = 0, UsTreasuryCmtNominalSemiannual = 1 }
