namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying trade plan endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// trade plan API. These endpoints are used to retrieve loss probabilities, trade plan actions, forward loss ratios, and related data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class TradePlanQueryUriPath
{
    public const string GetLossProbability = "/api/tradeplan/lossprobability";
    public const string GetLossProbabilityDistribution = "/api/tradeplan/lossprobabilitydistribution";
    public const string GetStopLossLimit = "/api/tradeplan/stoplosslimit";
    public const string GetTradePlanForwardLossRatios = "/api/tradeplan/forwardlossratios";
    public const string GetTradePlanForwardLossRatio = "/api/tradeplan/forwardlossratio";
    public const string GetTradePlanAction = "/api/tradeplan/action";
    public const string GetTradePlans = "/api/tradeplan/tradeplans";
    public const string GetIronCondorForwardDelta = "/api/tradeplan/ironcondorforwarddelta";
    public const string GetTradePlanForwardLossLimit = "/api/tradeplan/forwardlosslimit";
    public const string GetTradePlanForwardLossLimitType = "/api/tradeplan/forwardlosslimit/type";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
