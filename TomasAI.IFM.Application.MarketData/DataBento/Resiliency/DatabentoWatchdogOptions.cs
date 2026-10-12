using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoWatchdogOptions
{
    public bool Enabled { get; init; } = true;
    public string NativeBackend { get; init; } = "Cpp";
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan AttemptTwoDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan AttemptThreeDelay { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan PersistenceRetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan YellowFreshnessAge { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RedFreshnessAge { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan HardStallTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan DatasetTeardownTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan DatasetQualificationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public DatabentoWatchdogOptions Validate()
    {
        if (NativeBackend is not ("Cpp" or "Rust"))
            throw new InvalidOperationException("Databento native backend must be explicitly configured as Cpp or Rust.");
        if (PollInterval <= TimeSpan.Zero || ProbeTimeout <= TimeSpan.Zero
            || AttemptTwoDelay < TimeSpan.Zero || AttemptThreeDelay < TimeSpan.Zero
            || PersistenceRetryDelay < TimeSpan.Zero
            || YellowFreshnessAge <= TimeSpan.Zero || RedFreshnessAge <= YellowFreshnessAge
            || HardStallTimeout <= TimeSpan.Zero || DatasetTeardownTimeout <= TimeSpan.Zero
            || DatasetQualificationTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Databento watchdog intervals and freshness boundaries are invalid.");
        return this;
    }
}
