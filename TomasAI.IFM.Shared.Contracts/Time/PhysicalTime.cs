namespace TomasAI.IFM.Shared.Time;

/// <summary>Identifies the clock for infrastructure waits, independently of business time.</summary>
public sealed class PhysicalTime
{
    /// <summary>Creates a physical clock dependency. Production uses the system monotonic clock.</summary>
    public PhysicalTime() : this(TimeProvider.System) { }

    PhysicalTime(TimeProvider provider) => Provider = provider;

    /// <summary>Supplies an independent physical test clock without binding the business TimeProvider in DI.</summary>
    public static PhysicalTime FromProvider(TimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new PhysicalTime(provider);
    }

    /// <summary>Clock used for transport, resource and cancellation budgets.</summary>
    public TimeProvider Provider { get; }
}
