namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>The original posted settlement for one confirmed opening fill.</summary>
public readonly record struct PostedOpeningSettlement(long JournalId, decimal SignedAmount);
