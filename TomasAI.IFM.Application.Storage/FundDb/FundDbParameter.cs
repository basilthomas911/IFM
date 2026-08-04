using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.FundDb;

internal readonly record struct DeleteFund(int fundId) : IBindValue
{
    public object Bind() => new object?[] { fundId };
}
internal readonly record struct DeleteFundOrder(int fundId, int orderId) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId };
}
internal readonly record struct ReleaseFundOrderWriteOwnershipV3(int orderId, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { orderId, operationId };
}
internal readonly record struct DeleteFundOrderByOrderIdV3ForOfflineRepair(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct DeleteFundOrderTrade(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, tradeId };
}
internal readonly record struct DeleteFundTransaction(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate };
}
internal readonly record struct DeleteFundTransactionTimelineV3(int fundId, DateOnly monthBucket, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate, long transactionId) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate, transactionId };
}
internal readonly record struct DeleteFundBalanceByStatusDayV3(int fundId, DateOnly monthBucket, DateOnly valueDate, string tradeStatus, DateTime transactionDate, long transactionId, int orderId, int tradeId, string tradeType, string transactionType) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, tradeStatus, transactionDate, transactionId, orderId, tradeId, tradeType, transactionType };
}
internal readonly record struct DeleteFundTransactionAmountV3(int fundId, DateOnly monthBucket, string transactionType, int amountSign, DateOnly valueDate, DateTime transactionDate, long transactionId, int orderId, int tradeId, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, transactionType, amountSign, valueDate, transactionDate, transactionId, orderId, tradeId, tradeType };
}
internal readonly record struct FundTransactionProjectionPartition(int fundId, DateOnly monthBucket) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket };
}
internal readonly record struct GetFundByFundId(int fundId) : IBindValue
{
    public object Bind() => new object?[] { fundId };
}
internal readonly record struct GetFundOrder(int fundId, int orderId) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId };
}
internal readonly record struct GetFundOrderTrade(int fundId, int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, tradeId };
}
internal readonly record struct GetFundTransaction(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate };
}
internal readonly record struct GetFundTransactionIdentityV4(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate };
}
internal readonly record struct ReserveFundTransactionIdentityV4(int fundId, DateOnly valueDate, int orderId, int tradeId, string tradeType, string transactionType, DateTime transactionDate, long transactionId) : IBindValue
{
    public object Bind() => new object?[] { fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate, transactionId };
}
internal readonly record struct GetFundTransactions(int fundId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, startDate, endDate };
}
internal readonly record struct GetFundTransactionTimelineV3(int fundId, DateOnly monthBucket, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, startDate, endDate };
}
internal readonly record struct GetFundTransactionAmountsV3(int fundId, DateOnly monthBucket, string transactionType, int amountSign, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, transactionType, amountSign, startDate, endDate };
}
internal readonly record struct GetOpeningFundBalanceV3(int fundId, DateOnly monthBucket, DateOnly valueDate, string tradeStatus) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, tradeStatus };
}
internal readonly record struct GetClosingFundBalanceV3(int fundId, DateOnly monthBucket, DateOnly valueDate, string tradeStatus) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, tradeStatus };
}
internal readonly record struct GetFundTransactionProjectionStateV3(int fundId, DateOnly monthBucket) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket };
}
internal readonly record struct MarkFundTransactionProjectionIncompleteV3(Guid generation, int fundId, DateOnly monthBucket) : IBindValue
{
    public object Bind() => new object?[] { generation, fundId, monthBucket };
}
internal readonly record struct MarkFundTransactionProjectionCompleteV3(long sourceCount, string sourceFingerprint, DateTime reconciledOn, int fundId, DateOnly monthBucket, Guid generation) : IBindValue
{
    public object Bind() => new object?[] { sourceCount, sourceFingerprint, reconciledOn, fundId, monthBucket, generation };
}
internal readonly record struct InsertFundTransactionProjectionMutationV3(int fundId, DateOnly monthBucket, Guid mutationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, mutationId, startedOn };
}
internal readonly record struct DeleteFundTransactionProjectionMutationV3(int fundId, DateOnly monthBucket, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, mutationId };
}
internal readonly record struct GetFundTransactionProjectionMutationsV3(int fundId, DateOnly monthBucket) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket };
}
internal readonly record struct InsertFundTransactionWriteMutationV3(int fundId, Guid mutationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { fundId, mutationId, startedOn };
}
internal readonly record struct GetFundTransactionWriteMutationsV3(int fundId) : IBindValue
{
    public object Bind() => new object?[] { fundId };
}
internal readonly record struct DeleteFundTransactionWriteMutationV3(int fundId, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { fundId, mutationId };
}
internal readonly record struct ClaimFundTransactionWriteOwnershipV3(int fundId, Guid mutationId, DateTime claimedOn) : IBindValue
{
    public object Bind() => new object?[] { fundId, mutationId, claimedOn };
}
internal readonly record struct FlagFundTransactionWriteOwnershipConflictV3(int fundId) : IBindValue
{
    public object Bind() => new object?[] { fundId };
}
internal readonly record struct ReleaseFundTransactionWriteOwnershipV3(int fundId, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { fundId, mutationId };
}
internal readonly record struct GetFirstFundTransactionValueDate(int fundId, DateOnly startDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, startDate };
}
internal readonly record struct GetLastFundTransactionValueDate(int fundId, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { fundId, endDate };
}
internal readonly record struct GetFundBalance(int fundId) : IBindValue
{
    public object Bind() => new object?[] { fundId };
}
internal readonly record struct GetFundIdFromOrderId(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct GetFundOrderReservationV3(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
internal readonly record struct ClaimFundOrderWriteOwnershipV3(
    int orderId,
    Guid operationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { orderId, operationId, startedOn };
}
internal readonly record struct InsertFund(int fundId, string name, string description, decimal balance, bool isProduction, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { fundId, name, description, balance, isProduction, createdOn, createdBy };
}
internal readonly record struct InsertFundOrder(int fundId, int orderId, DateTime orderDate, string orderStatus, string baseContractId, DateOnly tradeDate, DateOnly maturityDate, string reference, DateTime createdOn, string createdBy, DateTime? updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate, reference, createdOn, createdBy, updatedOn, updatedBy };
}
internal readonly record struct InsertFundOrderByOrderIdV3(
    int orderId,
    int fundId,
    Guid reservationToken) : IBindValue
{
    public object Bind() => new object?[] { orderId, fundId, reservationToken };
}
internal readonly record struct RotateFundOrderByOrderIdV3Reservation(
    Guid reservationToken,
    int orderId,
    int fundId,
    Guid expectedReservationToken) : IBindValue
{
    public object Bind() => new object?[]
    {
        reservationToken,
        orderId,
        fundId,
        expectedReservationToken
    };
}
internal readonly record struct InsertFundOrderTrade(int fundId, int orderId, int tradeId, string tradeType, DateOnly tradeDate, DateOnly maturityDate, string tradeState, string tradeAction, string reference, bool primaryTrade, string baseContractSymbol, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { fundId, orderId, tradeId, tradeType, tradeDate, maturityDate, tradeState, tradeAction, reference, primaryTrade, baseContractSymbol, createdOn, createdBy, null, null };
}
internal readonly record struct InsertFundTransaction(long transactionId, DateTime transactionDate, string transactionType, int fundId, int orderId, int tradeId, string tradeType, DateOnly valueDate, string tradeStatus, string description, decimal amount, decimal balance) : IBindValue
{
    public object Bind() => new object?[] { transactionId, transactionDate, transactionType, fundId, orderId, tradeId, tradeType, valueDate, tradeStatus, description, amount, balance };
}
internal readonly record struct InsertFundTransactionTimelineV3(int fundId, DateOnly monthBucket, DateOnly valueDate, DateTime transactionDate, long transactionId, string transactionType, int orderId, int tradeId, string tradeType, string tradeStatus, string description, decimal amount, decimal balance) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, transactionDate, transactionId, transactionType, orderId, tradeId, tradeType, tradeStatus, description, amount, balance };
}
internal readonly record struct InsertFundBalanceByStatusDayV3(int fundId, DateOnly monthBucket, DateOnly valueDate, string tradeStatus, DateTime transactionDate, long transactionId, string transactionType, int orderId, int tradeId, string tradeType, decimal balance) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, valueDate, tradeStatus, transactionDate, transactionId, transactionType, orderId, tradeId, tradeType, balance };
}
internal readonly record struct InsertFundTransactionAmountV3(int fundId, DateOnly monthBucket, string transactionType, int amountSign, DateOnly valueDate, DateTime transactionDate, long transactionId, int orderId, int tradeId, string tradeType, decimal amount) : IBindValue
{
    public object Bind() => new object?[] { fundId, monthBucket, transactionType, amountSign, valueDate, transactionDate, transactionId, orderId, tradeId, tradeType, amount };
}
internal readonly record struct UpdateFundBalance(int fundId, decimal balance) : IBindValue
{
    public object Bind() => new object?[] { balance, fundId };
}
internal readonly record struct UpdateFundOrderTradeState(int fundId, int orderId, int tradeId, string tradeState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeState, updatedOn, updatedBy, fundId, orderId, tradeId };
}
internal readonly record struct UpdateFundOrderStatus(int fundId, int orderId, string orderStatus) : IBindValue
{
    public object Bind() => new object?[] { orderStatus, fundId, orderId };
}

