namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Represents a stable count and commutative fingerprint for a projection.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct ProjectionIdentity(long Count, ulong Xor, ulong Sum)
{
    /// <summary>
    /// Gets the hexadecimal projection fingerprint.
    /// </summary>
    public string Fingerprint => $"{Xor:X16}{Sum:X16}";
}
