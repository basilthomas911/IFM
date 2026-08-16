namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Effective duplicate behavior persisted on import commands and events.
/// Overwrite is zero so historical serialized messages retain existing upsert
/// behavior when the new field is absent.
/// </summary>
public enum ImportDuplicatePolicy
{
    Overwrite = 0,
    Reject = 1
}
