using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

internal readonly record struct DeleteFund(int fundId) : IBindValue
{
    public object Bind() => new { fundId };
}
internal readonly record struct DeleteFundOrder(int fundId, int orderId) : IBindValue
{
    public object Bind() => new { fundId, orderId };
}
internal readonly record struct DeleteFundOrderTrade(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId };
}
internal readonly record struct DeleteFundTransaction(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate) : IBindValue
{
    public object Bind() => new { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate };
}
internal readonly record struct GetFundByFundId(int fundId) : IBindValue
{
    public object Bind() => new { fundId };
}
internal readonly record struct GetFundOrder(int fundId, int orderId) : IBindValue
{
    public object Bind() => new { fundId, orderId };
}
internal readonly record struct GetFundOrderTrade(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId };
}
internal readonly record struct GetFundTransaction(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate) : IBindValue
{
    public object Bind() => new { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate };
}
internal readonly record struct GetFundTransactions(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundPnl(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundBalance(int fundId) : IBindValue
{
    public object Bind() => new { fundId };
}
internal readonly record struct GetFundTradeCommission(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundBalanceByTransactionId(int fundId, long transactionId) : IBindValue
{
    public object Bind() => new { fundId, transactionId };
}

internal readonly record struct GetFundMinTransactionId(int fundId, DateOnly startDate) : IBindValue
{
    public object Bind() => new { fundId, startDate };
}
internal readonly record struct GetFundMaxTransactionId(int fundId, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, endDate };
}
internal readonly record struct GetFundTransactionDateByTradeStatus(int fundId, DateOnly valueDate, string tradeStatus) : IBindValue
{
    public object Bind() => new { fundId, valueDate, tradeStatus };
}
internal readonly record struct GetFundBalanceByTransactionDate(int fundId, DateTime transactionDate) : IBindValue
{
    public object Bind() => new { fundId, transactionDate };
}
internal readonly record struct GetFundLossOrders(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundProfitOrders(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundDailyBalance(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { fundId, startDate, endDate };
}
internal readonly record struct GetFundIdFromOrderId(int orderId) : IBindValue
{
    public object Bind() => new { orderId };
}
internal readonly record struct InsertFund(int fundId, string name, string description, decimal balance, bool isProduction, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { fundId, name, description, balance, isProduction, createdOn, createdBy };
}
internal readonly record struct InsertFundOrder(int fundId, int orderId, DateTime orderDate, string orderStatus, string baseContractId, DateOnly tradeDate, DateOnly maturityDate, string reference, DateTime createdOn, string createdBy, DateTime? updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate, reference, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertFundOrderTrade(int fundId, int orderId, int tradeId, string tradeType, DateOnly tradeDate, DateOnly maturityDate, string tradeState, string tradeAction, string reference, bool primaryTrade, string baseContractSymbol, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId, tradeType, tradeDate, maturityDate, tradeState, tradeAction, reference, primaryTrade, baseContractSymbol, createdOn, createdBy };
}
internal readonly record struct InsertFundTransaction(long transactionId, DateTime transactionDate, string transactionType, int fundId, int orderId, int tradeId, string tradeType, DateOnly valueDate, string tradeStatus, string description, decimal amount, decimal balance) : IBindValue
{
    public object Bind() => new { transactionId, transactionDate, transactionType, fundId, orderId, tradeId, tradeType, valueDate, tradeStatus, description, amount, balance };
}
internal readonly record struct UpdateFundBalance(int fundId, decimal balance) : IBindValue
{
    public object Bind() => new { fundId, balance };
}
internal readonly record struct UpdateFundOrderTradeState(int fundId, int orderId, int tradeId, string tradeState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new { fundId, orderId, tradeId, tradeState, updatedOn, updatedBy };
}
internal readonly record struct UpdateFundOrderStatus(int fundId, int orderId, string orderStatus) : IBindValue
{
    public object Bind() => new { fundId, orderId, orderStatus };
}

