namespace TomasAI.IFM.Shared.Time;

/// <summary>Bounds one operation by logical time and an independent physical execution budget.</summary>
/// <remarks>Ordinary live System time uses one timer. A frozen or paused business clock cannot disable IO cancellation.</remarks>
public sealed class ClockDeadline : IDisposable
{
    readonly CancellationTokenSource logical;
    readonly CancellationTokenSource? physical;
    readonly CancellationTokenSource? joined;

    /// <summary>Creates budgets from an already calculated remaining duration; retries must retain their original deadline.</summary>
    public ClockDeadline(TimeSpan remaining, TimeProvider businessClock, TimeProvider physicalClock)
    {
        ArgumentNullException.ThrowIfNull(businessClock);
        ArgumentNullException.ThrowIfNull(physicalClock);
        logical = new(remaining, businessClock);
        if (!ReferenceEquals(businessClock, physicalClock))
        {
            physical = new(remaining, physicalClock);
            joined = CancellationTokenSource.CreateLinkedTokenSource(logical.Token, physical.Token);
        }
    }

    public CancellationToken Token => joined?.Token ?? logical.Token;
    public bool IsCancellationRequested => Token.IsCancellationRequested;
    public void Dispose()
    {
        joined?.Dispose();
        physical?.Dispose();
        logical.Dispose();
    }
}
