namespace TomasAI.IFM.Domain.Reference.Services;

/// <summary>
/// Coordinates immediate lookup-cache invalidation inside one process.
/// Redis removal handles other processes when their bounded local snapshot expires.
/// </summary>
internal static class ReferenceLookupCacheGeneration
{
    static long _current;

    public static long Current => Volatile.Read(ref _current);

    public static void Invalidate() => Interlocked.Increment(ref _current);
}
