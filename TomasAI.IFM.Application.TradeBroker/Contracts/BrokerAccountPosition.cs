namespace TomasAI.IFM.Application.TradeBroker.Contracts;

public sealed record BrokerAccountPosition(string ContractId, int SignedQuantity, decimal AveragePrice);
