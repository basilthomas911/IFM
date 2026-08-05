using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Fund.Command.Model;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Fund.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class FundCollectionBenchmarks
{
    FundOrderCollection _orders = null!;
    FundOrderTradeCollection _trades = null!;
    FundTransactionCollection _transactions = null!;
    FundTransactionEntityId _transactionKey = null!;

    [Params(32, 256)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _orders = new FundOrderCollection();
        _trades = new FundOrderTradeCollection();
        _transactions = new FundTransactionCollection();

        for (var id = 1; id <= Count; id++)
        {
            _orders.Add(new BenchmarkFundOrder(id));
            _trades.Add(new BenchmarkFundOrderTrade(id));
            _transactions.Add(new BenchmarkFundTransaction(id));
        }

        _transactionKey = new FundTransactionEntityId(1, Count);
    }

    [Benchmark]
    public IFundOrder OrderLookupLast() => _orders[Count];

    [Benchmark]
    public bool OrderExistsLast() => _orders.Exists(Count);

    [Benchmark]
    public IFundOrderTrade? TradeLookupLast() => _trades[Count];

    [Benchmark]
    public bool TradeExistsLast() => _trades.Exists(Count);

    [Benchmark]
    public bool TransactionExistsLast() => _transactions.Exists(1, Count);

    [Benchmark]
    public IFundTransaction? TransactionLookupLast()
        => _transactions.Get(_transactionKey, TradeStatus.Open);

    sealed class BenchmarkFundOrder(int id) : IFundOrder
    {
        public int OrderId => id;
        public int FundId => 1;
        public string Reference => string.Empty;
        public TomasAI.IFM.Domain.Fund.Shared.OrderStatus OrderStatus
            => TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open;
        public DateTime CreatedOn => default;
        public string CreatedBy => string.Empty;
        public IFundOrderTradeCollection Trades { get; } = new FundOrderTradeCollection();
        public FundOrderReadModel ToViewModel() => throw new NotSupportedException();
        public void SetClosed() { }
    }

    sealed class BenchmarkFundOrderTrade(int id) : IFundOrderTrade
    {
        public int OrderId => 1;
        public int TradeId => id;
        public TradeState TradeState => default;
        public DateTime CreatedOn => default;
        public string CreatedBy => string.Empty;
        public FundOrderTradeReadModel ToViewModel() => throw new NotSupportedException();
        public void SetTradeState(TradeState tradeState) { }
    }

    sealed class BenchmarkFundTransaction(int id) : IFundTransaction
    {
        public FundTransactionId TransactionId { get; } = new(1, default, id, id, default, default, default, id);
        public DateTime TransactionDate => default;
        public FundTransactionType TransactionType => default;
        public int FundId => 1;
        public int OrderId => id;
        public int TradeId => id;
        public TradeType TradeType => default;
        public DateOnly ValueDate => default;
        public TradeStatus TradeStatus => TradeStatus.Open;
        public string Description => string.Empty;
        public decimal Amount => 0;
        public decimal Balance => 0;
    }
}
