namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new FeedRecoveryAttempt instance.</summary>
/// <param name="AttemptNumber">The attempt number.</param>
/// <param name="Timeout">The timeout.</param>
/// <param name="Fault">The feed fault that triggered recovery.</param>
/// <param name="Schema">The provider schema or recovery baseline required for the operation.</param>
public sealed record FeedRecoveryAttempt(
    int AttemptNumber,
    TimeSpan Timeout,
    FeedRecoveryFaultKind Fault,
    FeedRecoverySchema Schema);
