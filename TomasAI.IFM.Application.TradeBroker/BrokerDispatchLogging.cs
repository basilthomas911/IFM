using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Application.TradeBroker;

/// <summary>Compiled diagnostics for low-frequency broker dispatch operations, never per market quote.</summary>
internal static partial class BrokerDispatchLogging
{
    [LoggerMessage(2510400, LogLevel.Information, "{Component}.{Method}; OperationId={OperationId}; BrokerOrderId={BrokerOrderId}; AccountAlias={AccountAlias}; LegCount={LegCount}; OrderPrice={OrderPrice}; ExpectedRevision={ExpectedRevision}; Outcome={Outcome}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    internal static partial void Completed(ILogger logger, string method, Guid operationId, string brokerOrderId, string accountAlias, int legCount, decimal orderPrice, int expectedRevision, string outcome, double elapsedMilliseconds, string component = "BrokerDispatch");
    [LoggerMessage(2510499, LogLevel.Error, "{Component}.{Method} failed; OperationId={OperationId}; BrokerOrderId={BrokerOrderId}")]
    internal static partial void Failed(ILogger logger, string method, Guid operationId, string brokerOrderId, Exception exception, string component = "BrokerDispatch");
}
