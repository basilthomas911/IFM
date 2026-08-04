namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal readonly record struct MarketDataProjectionStateData(
    string ProjectionName,
    Guid Generation,
    bool IsReady);

internal readonly record struct MarketDataProjectionScopeStateData(
    string ProjectionName,
    string ScopeKey,
    Guid Generation,
    bool IsReady,
    bool Blocked,
    bool ActiveOperationsEmpty)
{
    public bool CanRead => IsReady && !Blocked && ActiveOperationsEmpty;
}

internal readonly record struct MarketDataProjectionScopeMutationData(
    string ProjectionName,
    string ScopeKey,
    Guid MutationId,
    DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}

internal readonly record struct MarketDataProjectionScopeGeneration(
    string ScopeKey,
    Guid Generation,
    bool IsMissing);

internal readonly record struct MarketDataProjectionScopeReadStamp(
    string ProjectionName,
    Guid GlobalGeneration,
    MarketDataProjectionScopeGeneration[] Scopes);

internal enum TickProjectionGuardFailureStage
{
    RegistrationResponseUnknown,
    RegisteredBeforeDataSubmission,
    DataBatchResponseUnknown,
    AfterDataAcknowledged
}

internal readonly record struct VixFuturesContractIndexData(int Bucket, string ContractId);

internal readonly record struct MarketDataProjectionMutationData(Guid MutationId, DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}

internal readonly record struct ProjectionIdentity(long Count, ulong Xor, ulong Sum)
{
    public string Fingerprint => $"{Xor:X16}{Sum:X16}";
}

internal sealed class ProjectionIdentityBuilder
{
    long _count;
    ulong _xor;
    ulong _sum;

    public void Add(ulong rowHash)
    {
        _count++;
        _xor ^= rowHash;
        _sum = unchecked(_sum + rowHash * 1099511628211UL);
    }

    public ProjectionIdentity Build() => new(_count, _xor, _sum);
}

internal static class MarketDataProjectionHash
{
    const ulong OffsetBasis = 14695981039346656037UL;
    const ulong Prime = 1099511628211UL;

    public static ulong Start() => OffsetBasis;

    public static ulong Add(ulong hash, string value)
    {
        foreach (var character in value)
        {
            hash = AddByte(hash, (byte)character);
            hash = AddByte(hash, (byte)(character >> 8));
        }
        return AddByte(hash, 0xFF);
    }

    public static ulong Add(ulong hash, DateOnly value) => Add(hash, value.DayNumber);
    public static ulong Add(ulong hash, TimeOnly value) => Add(hash, value.Ticks);
    public static ulong Add(ulong hash, int value) => Add(hash, unchecked((ulong)(uint)value));
    public static ulong Add(ulong hash, long value) => Add(hash, unchecked((ulong)value));
    public static ulong Add(ulong hash, double value) => Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

    public static ulong Add(ulong hash, decimal value)
        => Add(hash, value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));

    public static ulong Add(ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
            hash = AddByte(hash, (byte)(value >> shift));
        return hash;
    }

    static ulong AddByte(ulong hash, byte value)
        => unchecked((hash ^ value) * Prime);
}
