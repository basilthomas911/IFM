using System.Text.Json.Serialization;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "This published actor message uses arrays as its existing serialized payload contract.")]
public sealed record EmulatorLedgerCheckpoint(decimal Cash, long Sequence, long Generation,
    EmulatorOrderCheckpoint[] Orders, EmulatorOperationCheckpoint[] Operations,
    FrameworkAccountPosition[] Positions, EmulatorJournalEntry[] Journal,
    FrameworkBrokerObservation[] Observations,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] long CommitRevision = 0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] EmulatorQuote[]? LatestQuotes = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] EmulatorQuoteReceipt[]? QuoteReceipts = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] EmulatorMatchReceipt[]? MatchReceipts = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] int PublishedOrderObservationCount = 0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] EmulatorOwnerCheckpointReceipt[]? OwnerCheckpoints = null);
