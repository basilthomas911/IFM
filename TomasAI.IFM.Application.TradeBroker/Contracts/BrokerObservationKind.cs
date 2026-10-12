namespace TomasAI.IFM.Application.TradeBroker.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The enum storage width is part of the existing persisted and messaging contract.")]
public enum BrokerObservationKind : byte { Unknown = 0, Acknowledged = 1, Execution = 2, Commission = 3, Rejected = 4, Cancelled = 5, AccountSnapshot = 6, GateChanged = 7, ConnectionChanged = 8, OrderCompleted = 9 }
