namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Accumulates a stable identity for an unordered projection result set.
/// </summary>
public sealed class ProjectionIdentityBuilder
{
    long _count;
    ulong _xor;
    ulong _sum;

    /// <summary>
    /// Adds one stable row hash to the projection identity.
    /// </summary>
    public void Add(ulong rowHash)
    {
        _count++;
        _xor ^= rowHash;
        _sum = unchecked(_sum + rowHash * 1099511628211UL);
    }

    /// <summary>
    /// Creates the accumulated projection identity.
    /// </summary>
    public ProjectionIdentity Build() => new(_count, _xor, _sum);
}
