using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to start futures option tick data streaming.
/// </summary>
/// <param name="FeedId">The feed identifier for the streaming session.</param>
/// <param name="OptionContract">The option contract to stream tick data for.</param>
/// <param name="BaseContract">The underlying futures contract.</param>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="MaturityDate">The option maturity date.</param>
/// <param name="RiskFreeRate">The risk-free interest rate used for option calculations.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesOptionTickDataStreamingParameter(
    FuturesOptionTickEntityId EntityId,
    FuturesOptionContractReadModel OptionContract,
    FuturesContractV2ReadModel BaseContract,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    double RiskFreeRate,
    int ErrorCode)
    : ICommandParameter;
