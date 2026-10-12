using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Identifies why a requested signal metric cannot be consumed.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeDiscoverySignalAvailability : byte
{
    /// <summary>The metric is available and compatible.</summary>
    Available = 0,
    /// <summary>The metric does not exist in the cache.</summary>
    Missing = 1,
    /// <summary>The metric exceeds its configured maximum age.</summary>
    Stale = 2,
    /// <summary>The metric has not completed warm-up.</summary>
    NotWarm = 3,
    /// <summary>The metric failed upstream validation.</summary>
    Invalid = 4,
    /// <summary>The metric timestamp exceeds permitted future clock skew.</summary>
    FutureTimestamp = 5,
    /// <summary>The metric schema is unsupported.</summary>
    SchemaUnsupported = 6,
    /// <summary>The metric calculation version is incompatible.</summary>
    CalculationVersionMismatch = 7,
    /// <summary>The metric configuration does not match the request.</summary>
    ConfigurationMismatch = 8,
    /// <summary>A stable cache revision could not be captured.</summary>
    SnapshotConsistencyFailure = 9
}
