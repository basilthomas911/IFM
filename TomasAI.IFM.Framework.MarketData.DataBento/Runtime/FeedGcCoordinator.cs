using System.Runtime;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static class FeedGcCoordinator
{
    private static readonly System.Threading.Lock Gate = new();
    private static int _leases;
    private static GCLatencyMode _priorMode;
    private static bool _changedMode;

    internal static IDisposable Acquire(FeedGcOptions options)
    {
        lock (Gate)
        {
            if (_leases == 0)
            {
                if (options.RequireGcConfiguration && !GCSettings.IsServerGC)
                {
                    throw new InvalidOperationException(
                        "The strict feed profile requires Server GC.");
                }
                _priorMode = GCSettings.LatencyMode;
                _changedMode = false;
                if (options.EnableSustainedLowLatency
                    && _priorMode != GCLatencyMode.NoGCRegion)
                {
                    try
                    {
                        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                        _changedMode = GCSettings.LatencyMode == GCLatencyMode.SustainedLowLatency;
                    }
                    catch (InvalidOperationException) when (!options.RequireGcConfiguration)
                    {
                        // Optional latency configuration retains the existing GC mode.
                    }
                    if (options.RequireGcConfiguration && !_changedMode)
                    {
                        throw new InvalidOperationException(
                            "SustainedLowLatency GC mode could not be applied.");
                    }
                }
            }
            _leases++;
            return new Lease();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "Keep provider decoding and shared process configuration helpers with the component that owns their state.")]
    private static void Release()
    {
            lock (Gate)
            {
                _leases--;
                if (_leases == 0 && _changedMode
                    && GCSettings.LatencyMode == GCLatencyMode.SustainedLowLatency)
                {
                    GCSettings.LatencyMode = _priorMode;
                }
            }
    }

    private sealed class Lease : IDisposable
    {
        private int _disposed;

        /// <summary>Releases the resources owned by this instance.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            Release();
        }
    }
}
