namespace TomasAI.IFM.Application.TradeBroker.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The enum storage width is part of the existing persisted and messaging contract.")]
public enum BrokerOrderShape : byte { Unknown = 0, FuturesOutright = 1, VerticalSpread = 2, IronCondor = 3 }
