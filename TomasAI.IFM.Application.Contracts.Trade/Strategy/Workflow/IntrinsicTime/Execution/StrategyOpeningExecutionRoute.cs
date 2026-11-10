using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Server-selected emulator route frozen when a new workflow starts.</summary>
[MessagePackObject]
public sealed record StrategyOpeningExecutionRoute
{
    /// <summary>Exact configured Development broker account alias.</summary>
    [Key(0)] public string AccountAlias { get; init; } = string.Empty;

    /// <summary>Explicit execution environment; automatic opening currently supports only Emulator.</summary>
    [Key(1)] public BrokerEnvironment Environment { get; init; }

    /// <summary>Immutable limit-only execution policy identity.</summary>
    [Key(2)] public string PolicyId { get; init; } = "StrategyOpeningLimit/v1";

    /// <summary>Canonical hash of the exact server-selected route and limit-only policy.</summary>
    [Key(3)] public string PolicyHash { get; init; } = string.Empty;
}
