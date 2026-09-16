using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>A coherent market snapshot; every quote must have the same source epoch and sequence.</summary>
public sealed record EmulatorQuote(string ContractId, decimal Bid, decimal Ask, int BidSize, int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);

/// <summary>Conservative synthetic execution settings; no broker-equivalent margin is implied.</summary>
public sealed record EmulatorScenario(
    string AccountAlias,
    string Currency,
    decimal StartingCash,
    decimal PerLegCommission,
    TimeSpan MaximumQuoteAge,
    int MaximumStrategyUnitsPerFill = int.MaxValue,
    EmulatorFaultProfile? Faults = null)
{
    public static EmulatorScenario Development(string accountAlias) => new(accountAlias, "USD", 1_000_000m, 0.65m, TimeSpan.FromSeconds(2));
}

/// <summary>Deterministic fault switches used only by isolated emulator qualification scenarios.</summary>
public sealed record EmulatorFaultProfile(
    bool PlaceDispatchOutcomeUnknown = false,
    bool SuppressAcknowledgement = false,
    bool PublishCommissionBeforeExecution = false,
    bool DuplicateExecutionCallbacks = false,
    bool AllowFillsAfterCancel = false,
    bool DisconnectMarketMatching = false);

/// <summary>A testable UTC clock supplied to the deterministic matching engine.</summary>
public interface IEmulatorClock { DateTime UtcNow { get; } }

/// <summary>The runtime UTC clock; tests can supply a manually advanced clock.</summary>
public sealed class SystemEmulatorClock : IEmulatorClock { public DateTime UtcNow => DateTime.UtcNow; }

/// <summary>An immutable account and order journal entry.</summary>
public sealed record EmulatorJournalEntry(long Sequence, string Kind, string BrokerOrderId, Guid OperationId, decimal CashAfter, DateTime AtUtc);
