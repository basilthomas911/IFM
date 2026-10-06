using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
internal static partial class OfflineSimulationLogging
{
    [LoggerMessage(2510500, LogLevel.Information, "{Component}.{Method} started; BrokerOrderId={BrokerOrderId}; RandomSeed={RandomSeed}; StrategyUnits={StrategyUnits}; FillBatchCount={FillBatchCount}; CompletionMilliseconds={CompletionMilliseconds}")]
    internal static partial void Started(ILogger logger, string brokerOrderId, int randomSeed, int strategyUnits, int fillBatchCount, double completionMilliseconds, string component = "OfflineFillSimulation", string method = "RunAsync");
    [LoggerMessage(2510501, LogLevel.Information, "{Component}.{Method} atomic strategy batch; BrokerOrderId={BrokerOrderId}; FilledStrategyUnits={FilledStrategyUnits}; RemainingStrategyUnits={RemainingStrategyUnits}")]
    internal static partial void FillBatch(ILogger logger, string brokerOrderId, int filledStrategyUnits, int remainingStrategyUnits, string component = "OfflineFillSimulation", string method = "RunAsync");
    [LoggerMessage(2510599, LogLevel.Error, "{Component}.{Method} failed; BrokerOrderId={BrokerOrderId}")]
    internal static partial void Failed(ILogger logger, string brokerOrderId, Exception exception, string component = "OfflineFillSimulation", string method = "RunAsync");
}
