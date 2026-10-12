namespace TomasAI.IFM.Application.TradeBroker.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The enum storage width is part of the existing persisted and messaging contract.")]
public enum BrokerOrderType : byte { Unknown = 0, Market = 1, Limit = 2 }
