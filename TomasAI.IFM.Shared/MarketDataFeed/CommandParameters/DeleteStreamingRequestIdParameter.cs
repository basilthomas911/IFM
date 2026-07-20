using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to delete a streaming request identifier.
/// </summary>
/// <param name="FeedId">The feed identifier whose streaming request mapping should be deleted.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record DeleteStreamingRequestIdParameter(FeedId FeedId, int ErrorCode)
    : ICommandParameter;
