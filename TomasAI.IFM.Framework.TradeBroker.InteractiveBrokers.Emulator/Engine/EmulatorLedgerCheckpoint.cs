using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "This published actor message uses arrays as its existing serialized payload contract.")]
public sealed record EmulatorLedgerCheckpoint(decimal Cash, long Sequence, long Generation,
    EmulatorOrderCheckpoint[] Orders, EmulatorOperationCheckpoint[] Operations,
    FrameworkAccountPosition[] Positions, EmulatorJournalEntry[] Journal,
    FrameworkBrokerObservation[] Observations);
