using System.Buffers;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>
/// Event-driven native operation delivery. This foundation does not implement or
/// register IMarketDataApi yet; Treasury and futures-option numerical operations
/// are exposed. No production backend or service is changed by constructing it.
/// </summary>
public sealed partial class NativeMarketDataRuntime : IAsyncDisposable
{
    readonly NativeMarketDataLibrary library;
    readonly MarketDataRuntimeHandle handle;
    readonly object gate = new();
    readonly Dictionary<ulong, Pending> pending = [];
    readonly Task delivery;
    Task? disposal;
    Exception? deliveryFailure;
    ulong nextOperation;

    sealed class Pending
    {
        internal readonly TaskCompletionSource<double> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationTokenRegistration Cancellation;
        internal TaskCompletionSource<FuturesPricingResult>? Pricing;
        internal TaskCompletionSource? Control;
        internal TaskCompletionSource<NativeCatalogSnapshot>? CatalogQuery;
    }

    /// <summary>Creates a bounded runtime using the distinct DLL at an absolute path.</summary>
    public NativeMarketDataRuntime(string nativeLibraryPath, uint workerThreads = 2)
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("The Rust MarketData ABI requires a 64-bit process.");
        library = NativeMarketDataLibrary.Load(nativeLibraryPath);
        NativeMarketDataLibrary.Check(library.Create(1, 32, workerThreads, out var identity), "create");
        handle = new(library, identity);
        // A single dedicated managed delivery thread waits on a native signal.
        // It does not occupy a ThreadPool worker or poll with Task.Delay/timers.
        delivery = Task.Factory.StartNew(Deliver, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// Applies 2*ln(1+halfYield), preserving the current numerical core exactly.
    /// Domain validation and cancellation finish through the same event channel.
    /// The budget is capped natively at 120 seconds and is never renewed on wait.
    /// This does not acquire or validate a complete Treasury curve.
    /// </summary>
    public Task<double> ConvertTreasuryRateAsync(double halfYield, TimeSpan budget, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (budget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            if (deliveryFailure is not null) throw new InvalidOperationException("Native completion delivery failed.", deliveryFailure);
            var operation = checked(++nextOperation);
            var request = new Pending();
            pending.Add(operation, request);
            var milliseconds = (ulong)Math.Ceiling(Math.Min(budget.TotalMilliseconds, 120_000));
            var status = library.Submit(handle.Identity, operation, halfYield, milliseconds);
            if (status != 0)
            {
                pending.Remove(operation);
                NativeMarketDataLibrary.Check(status, "submit");
            }
            // The gate prevents completion from removing this registration while
            // it is being assigned. The native cancel call never invokes managed
            // code or blocks on operation cleanup, so immediate cancellation is safe.
            request.Cancellation = cancellationToken.UnsafeRegister(_ => library.Cancel(handle.Identity, operation), null);
            return request.Completion.Task;
        }
    }

    unsafe void Deliver()
    {
        var buffer = ArrayPool<NativeCompletion>.Shared.Rent(64);
        try
        {
            while (true)
            {
                NativeMarketDataLibrary.Check(library.Wait(handle.Identity, out var available), "wait");
                if (available == 0) break;
                uint count;
                fixed (NativeCompletion* records = buffer)
                    NativeMarketDataLibrary.Check(library.Drain(handle.Identity, records, 64, out count), "drain");
                if (count > 64) throw new InvalidDataException("Native completion batch exceeded caller capacity.");
                for (var index = 0; index < count; index++) Complete(buffer[index]);
                PumpLiveSignal();
            }
        }
        catch (Exception error)
        {
            Pending[] abandoned;
            lock (gate)
            {
                deliveryFailure = error;
                library.Stop(handle.Identity);
                abandoned = [.. pending.Values];
                pending.Clear();
            }
            foreach (var request in abandoned)
            {
                request.Cancellation.Dispose();
                request.Completion.TrySetException(error);
                request.Pricing?.TrySetException(error);
                request.Control?.TrySetException(error);
                request.CatalogQuery?.TrySetException(error);
            }
            throw;
        }
        finally { CompleteHistoricalReaders(deliveryFailure);livePulse.Writer.TryComplete(deliveryFailure); ArrayPool<NativeCompletion>.Shared.Return(buffer, clearArray: true); }
    }

    void Complete(NativeCompletion record)
    {
        if (record.Size != 32 || record.Reserved != 0 || record.Outcome is < 0 or > 4)
            throw new InvalidDataException("Malformed native completion record.");
        Pending request;
        lock (gate)
        {
            if (!pending.Remove(record.Operation, out request!)) throw new InvalidDataException("Unknown or repeated native operation completion.");
        }
        request.Cancellation.Dispose();
        if(request.CatalogQuery is not null){CompleteCatalogQuery(request,record);return;}
        if(request.Control is not null){CompleteControl(request,record);SignalHistoricalCompletion(record.Operation);return;}
        if(request.Pricing is {} pricing) {
            try {
                // The result reservation survives completion drain and is released
                // here before signalling callers or allowing runtime destruction.
                NativeMarketDataLibrary.Check(library.TakePricing(handle.Identity,record.Operation,64,out var result),"take pricing");
                if(result.Size!=64 || result.Failure>10) throw new InvalidDataException("Malformed native pricing result.");
                switch(record.Outcome) {
                    case 0: pricing.TrySetResult(new(result.Failure,result.Price,result.Volatility,result.Delta,result.Gamma,result.Vega,result.Theta,result.Rho));break;
                    case 1: pricing.TrySetCanceled();break;
                    case 2: pricing.TrySetException(new TimeoutException("Native market-data pricing deadline expired."));break;
                    default: pricing.TrySetException(new InvalidOperationException("Native pricing task failed internally."));break;
                }
            }catch(Exception error){pricing.TrySetException(error);throw;}
            return;
        }
        switch (record.Outcome)
        {
            case 0: request.Completion.TrySetResult(record.Value); break;
            case 1: request.Completion.TrySetCanceled(); break;
            case 2: request.Completion.TrySetException(new TimeoutException("Native market-data operation deadline expired.")); break;
            case 3: request.Completion.TrySetException(new ArgumentOutOfRangeException("halfYield", "Yield is outside the conversion domain.")); break;
            default: request.Completion.TrySetException(new InvalidOperationException("Native market-data task failed internally.")); break;
        }
    }

    /// <summary>
    /// Closes admission, cancels work, joins signalled delivery, then releases the
    /// native runtime. Repeated callers share the same disposal task. No finalizer
    /// performs an unbounded native wait or unloads the library.
    /// </summary>
    public ValueTask DisposeAsync()
        => StopAndDisposeAsync(TimeSpan.FromMinutes(2));

    /// <summary>
    /// Stops and disposes within the remaining caller budget, capped at two minutes.
    /// Recovery must pass its existing remaining budget here. Failure to complete
    /// reports timeout while retaining native ownership; it never frees live memory.
    /// Later callers observe the first disposal result without renewing its budget.
    /// </summary>
    public ValueTask StopAndDisposeAsync(TimeSpan remainingBudget)
    {
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        lock (gate)
        {
            // Cancellation must not depend on a free ThreadPool worker. Signal
            // the native root now; only awaited delivery/join/release is queued.
            if (disposal is null) NativeMarketDataLibrary.Check(library.Stop(handle.Identity), "stop");
            // Task.Run guarantees release never executes on a Tokio worker.
            // The independent wait covers stop, delivery AND native release. On
            // expiry the cleanup task retains the handle/library until safe; the
            // caller gets an error instead of waiting on an unresponsive owner.
            disposal ??= Task.Run(DisposeCoreAsync).WaitAsync(remainingBudget);
            return new(disposal);
        }
    }

    async Task DisposeCoreAsync()
    {
        await delivery.ConfigureAwait(false);
        await DiscardHistoricalReadersAsync().ConfigureAwait(false);
        DiscardReferences();
        NativeMarketDataLibrary.Check(library.Release(handle.Identity), "release");
        handle.SetHandleAsInvalid();
        handle.Dispose();
    }
}
