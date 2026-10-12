using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoHardRecoveryPolicy
{
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan AttemptTwoDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan AttemptThreeDelay { get; init; } = TimeSpan.FromSeconds(15);

    public DatabentoHardRecoveryPolicy Validate()
    {
        if (OverallTimeout <= TimeSpan.Zero || OverallTimeout > TimeSpan.FromHours(1)
            || AttemptTwoDelay < TimeSpan.Zero || AttemptThreeDelay < TimeSpan.Zero
            || AttemptTwoDelay + AttemptThreeDelay >= OverallTimeout)
            throw new InvalidOperationException("Hard recovery policy has no finite attempt budget.");
        return this;
    }
}
