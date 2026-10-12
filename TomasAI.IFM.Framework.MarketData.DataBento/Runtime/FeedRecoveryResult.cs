namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedRecoveryResult
{
    public bool ConnectionAuthenticated { get; init; }
    public bool SubscriptionsAcknowledged { get; init; }
    public bool ReplayComplete { get; init; }
    public bool ContinuityVerified { get; init; }
    public bool RequiredBaselinesReady { get; init; }
    public bool DefinitionsComplete { get; init; } = true;
    public string? Failure { get; init; }

    /// <summary>Determines whether connection, replay, continuity, and schema-specific baselines permit feed admission.</summary>
    /// <param name="schema">The provider schema or recovery baseline required for the operation.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool IsReady(FeedRecoverySchema schema) =>
        ConnectionAuthenticated
        && SubscriptionsAcknowledged
        && ReplayComplete
        && ContinuityVerified
        && RequiredBaselinesReady
        && (schema != FeedRecoverySchema.Definitions || DefinitionsComplete);
}
