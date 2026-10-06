using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Application.TradeBroker.Mapping;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Application.TradeBroker;

/// <summary>Application facade over an order port and account port from the same synthetic ledger.</summary>
public sealed class InteractiveBrokersEmulatorTradeBroker : ITradeBroker
{
    private readonly IFrameworkOrderExecutionBroker _orders;
    private readonly IFrameworkBrokerAccount _account;
    readonly ILogger logger;

    public InteractiveBrokersEmulatorTradeBroker(IFrameworkOrderExecutionBroker orders, IFrameworkBrokerAccount account, ILogger? logger = null)
    {
        if (orders.AccountAlias != account.AccountAlias || orders.Generation != account.Generation)
            throw new ArgumentException("Emulator order and account ports are not aligned.");
        _orders = orders;
        _account = account;
        this.logger = logger ?? NullLogger.Instance;
    }

    public BrokerEnvironment Environment => BrokerEnvironment.Emulator;
    public string AccountAlias => _orders.AccountAlias;
    public long Generation => _account.Generation;
    public BrokerCapabilities Capabilities => BrokerCapabilities.Emulator(AccountAlias);

    async ValueTask<BrokerDispatchReceipt> PlaceCoreAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Environment != Environment || !string.Equals(request.AccountAlias, AccountAlias, StringComparison.Ordinal))
            return new(BrokerDispatchOutcome.RejectedLocally, request.OperationId, request.BrokerOrderId,
                "TB.ACCOUNT.MISMATCH", "Broker environment or account does not match the loaded adapter/account.", DateTime.UtcNow);
        if (Capabilities.Validate(request) is { } capabilityFailure)
            return new(BrokerDispatchOutcome.RejectedLocally, request.OperationId, request.BrokerOrderId,
                "TB.CAPABILITY.UNSUPPORTED", capabilityFailure, DateTime.UtcNow);
        return TradeBrokerMapper.ToApplication(await _orders.PlaceAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
    }

    public async ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var receipt = await PlaceCoreAsync(request, cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
                BrokerDispatchLogging.Completed(logger, nameof(PlaceAsync), request.OperationId, request.BrokerOrderId,
                request.AccountAlias, request.Legs.Length, request.SignedNetDebitLimit, 0, receipt.Outcome.ToString(),
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return receipt;
        }
        catch (Exception exception)
        {
            BrokerDispatchLogging.Failed(logger, nameof(PlaceAsync), request.OperationId, request.BrokerOrderId, exception);
            throw;
        }
    }

    public async ValueTask<BrokerDispatchReceipt> ModifyLimitAsync(BrokerLimitUpdate request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var receipt = TradeBrokerMapper.ToApplication(await _orders.ModifyLimitAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
            if (logger.IsEnabled(LogLevel.Information))
                BrokerDispatchLogging.Completed(logger, nameof(ModifyLimitAsync), request.OperationId, request.BrokerOrderId,
                request.AccountAlias, 0, request.NewSignedNetDebitLimit, request.ExpectedRevision, receipt.Outcome.ToString(),
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return receipt;
        }
        catch (Exception exception)
        {
            BrokerDispatchLogging.Failed(logger, nameof(ModifyLimitAsync), request.OperationId, request.BrokerOrderId, exception);
            throw;
        }
    }
    public async ValueTask<BrokerDispatchReceipt> CancelAsync(BrokerCancelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var receipt = TradeBrokerMapper.ToApplication(await _orders.CancelAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
            if (logger.IsEnabled(LogLevel.Information))
                BrokerDispatchLogging.Completed(logger, nameof(CancelAsync), request.OperationId, request.BrokerOrderId,
                request.AccountAlias, 0, 0m, request.ExpectedRevision, receipt.Outcome.ToString(),
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return receipt;
        }
        catch (Exception exception)
        {
            BrokerDispatchLogging.Failed(logger, nameof(CancelAsync), request.OperationId, request.BrokerOrderId, exception);
            throw;
        }
    }

    public async ValueTask<BrokerObservation[]> ReconcileOrderAsync(string brokerOrderId, CancellationToken cancellationToken = default) =>
        [.. (await _orders.ReconcileAsync(brokerOrderId, cancellationToken)).Select(TradeBrokerMapper.ToApplication)];
    public async ValueTask<BrokerAccountSnapshot> GetAccountSnapshotAsync(CancellationToken cancellationToken = default) =>
        TradeBrokerMapper.ToApplication(await _account.GetSnapshotAsync(cancellationToken));
    public async ValueTask<BrokerAccountSnapshot> ResynchronizeAccountAsync(CancellationToken cancellationToken = default) =>
        TradeBrokerMapper.ToApplication(await _account.ResynchronizeAsync(cancellationToken));
    public ValueTask<int> PublishMarketQuoteAsync(BrokerMarketQuote quote, CancellationToken cancellationToken = default) =>
        _orders.PublishMarketQuoteAsync(TradeBrokerMapper.ToFramework(quote), cancellationToken);
    public async IAsyncEnumerable<BrokerObservation> ObserveOrdersAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var fact in _orders.ObserveAsync(cancellationToken)) yield return TradeBrokerMapper.ToApplication(fact);
    }
    public async IAsyncEnumerable<BrokerObservation> ObserveAccountAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var fact in _account.ObserveAsync(cancellationToken)) yield return TradeBrokerMapper.ToApplication(fact);
    }
}
