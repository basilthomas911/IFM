namespace TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;

/// <summary>
/// Identifies the guarded lifecycle state of an immutable configuration version.
/// </summary>
public enum ConfigurationParameterSetStatus : byte
{
    /// <summary>The version is being authored and cannot be selected.</summary>
    Draft = 0,

    /// <summary>The version is published and eligible during its effective interval.</summary>
    Published = 1,

    /// <summary>The version is retired and no longer eligible for new workflows.</summary>
    Retired = 2
}
