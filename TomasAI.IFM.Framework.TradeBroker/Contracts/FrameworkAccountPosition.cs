namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>One contract-level position in an account snapshot.</summary>
public sealed record FrameworkAccountPosition(string ContractId, int SignedQuantity, decimal AveragePrice);
