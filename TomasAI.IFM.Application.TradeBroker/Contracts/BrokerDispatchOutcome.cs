namespace TomasAI.IFM.Application.TradeBroker.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The enum storage width is part of the existing persisted and messaging contract.")]
public enum BrokerDispatchOutcome : byte { RejectedLocally = 0, AcceptedForDispatch = 1, OutcomeUnknown = 2 }
