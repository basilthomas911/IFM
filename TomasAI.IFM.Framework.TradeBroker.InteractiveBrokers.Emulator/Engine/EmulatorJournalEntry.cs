using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>An immutable account and order journal entry.</summary>
public sealed record EmulatorJournalEntry(long Sequence, string Kind, string BrokerOrderId, Guid OperationId, decimal CashAfter, DateTime AtUtc);
