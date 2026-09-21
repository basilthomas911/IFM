using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Application.TradeBroker.Mapping;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Application.TradeBroker;

/// <summary>Application facade over an order port and account port from the same synthetic ledger.</summary>
public sealed class InteractiveBrokersEmulatorTradeBroker : ITradeBroker
{
    private readonly IFrameworkOrderExecutionBroker _orders;
    private readonly IFrameworkBrokerAccount _account;

    public InteractiveBrokersEmulatorTradeBroker(IFrameworkOrderExecutionBroker orders, IFrameworkBrokerAccount account)
    {
        if (orders.AccountAlias != account.AccountAlias || orders.Generation != account.Generation)
            throw new ArgumentException("Emulator order and account ports are not aligned.");
        _orders = orders;
        _account = account;
    }

    public BrokerEnvironment Environment => BrokerEnvironment.Emulator;
    public string AccountAlias => _orders.AccountAlias;
    public long Generation => _account.Generation;
    public BrokerCapabilities Capabilities => BrokerCapabilities.Emulator(AccountAlias);

    public async ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Environment != Environment || !string.Equals(request.AccountAlias, AccountAlias, StringComparison.Ordinal))
            return new(BrokerDispatchOutcome.RejectedLocally, request.OperationId, request.BrokerOrderId,
                "TB.ACCOUNT.MISMATCH", "Broker environment or account does not match the loaded adapter/account.", DateTime.UtcNow);
        if (Capabilities.Validate(request) is { } capabilityFailure)
            return new(BrokerDispatchOutcome.RejectedLocally, request.OperationId, request.BrokerOrderId,
                "TB.CAPABILITY.UNSUPPORTED", capabilityFailure, DateTime.UtcNow);
        return TradeBrokerMapper.ToApplication(await _orders.PlaceAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
    }

    public async ValueTask<BrokerDispatchReceipt> ModifyLimitAsync(BrokerLimitUpdate request, CancellationToken cancellationToken = default) =>
        TradeBrokerMapper.ToApplication(await _orders.ModifyLimitAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
    public async ValueTask<BrokerDispatchReceipt> CancelAsync(BrokerCancelRequest request, CancellationToken cancellationToken = default) =>
        TradeBrokerMapper.ToApplication(await _orders.CancelAsync(TradeBrokerMapper.ToFramework(request), cancellationToken));
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
