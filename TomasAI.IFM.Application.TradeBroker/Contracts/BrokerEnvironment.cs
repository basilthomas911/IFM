namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>Account mode; Unknown cannot authorize a broker dispatch.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The enum storage width is part of the existing persisted and messaging contract.")]
public enum BrokerEnvironment : byte { Unknown = 0, Emulator = 1, Paper = 2, Live = 3 }
