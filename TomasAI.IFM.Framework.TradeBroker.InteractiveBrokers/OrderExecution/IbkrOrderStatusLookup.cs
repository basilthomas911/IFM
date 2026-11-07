using IBApi;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.OrderExecution;

/// <summary>Exact provider identity retained when the owning IBKR session places an order.</summary>
public sealed record IbkrOrderIdentity(string Account, int ClientId, int OrderId, int PermanentId, string OrderReference);

/// <summary>One matched status from the open or completed order download.</summary>
public sealed record IbkrOrderStatus(string Status, bool Completed);

/// <summary>One matched execution. The provider execution identity is retained for deduplication.</summary>
public sealed record IbkrOrderExecution(string ExecutionId, int ContractId, string Side, double Quantity, double Price, string Time);

/// <summary>Evidence collected after all three IBKR response terminators arrive.</summary>
public sealed record IbkrOrderStatusEvidence(IbkrOrderStatus[] Statuses, IbkrOrderExecution[] Executions);

/// <summary>Read-only lookup callback wrapper for a dedicated, connected TWS or IB Gateway session.</summary>
public sealed class IbkrOrderStatusLookup : DefaultEWrapper
{
    readonly object gate = new();
    Batch? active;
    bool reconnectRequired;

    /// <summary>
    /// Requests open orders, completed orders, and executions on a dedicated session using this wrapper.
    /// Empty evidence leaves the order outcome unknown. The caller supplies an unused execution request ID.
    /// </summary>
    public Task<IbkrOrderStatusEvidence> LookupAsync(EClientSocket client, IbkrOrderIdentity identity,
        int executionRequestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!client.IsConnected())
            throw new InvalidOperationException("IBKR status session is disconnected.");
        return CollectAsync(identity, executionRequestId, () =>
        {
            client.reqAllOpenOrders();
            client.reqCompletedOrders(false);
            client.reqExecutions(executionRequestId, new ExecutionFilter { AcctCode = identity.Account });
        }, cancellationToken);
    }

    internal async Task<IbkrOrderStatusEvidence> CollectAsync(IbkrOrderIdentity identity, int executionRequestId,
        Action request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.Account) || executionRequestId <= 0 ||
            identity.PermanentId <= 0 && (identity.OrderId <= 0 || identity.ClientId < 0 || string.IsNullOrWhiteSpace(identity.OrderReference)))
            throw new ArgumentException("An exact IBKR account and order identity are required.", nameof(identity));

        var batch = new Batch(identity, executionRequestId);
        lock (gate)
        {
            if (reconnectRequired || active is not null)
                throw new InvalidOperationException("IBKR status downloads require a fresh idle session.");
            active = batch;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            request();
            return await batch.Result.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (gate)
            {
                // The open and completed callbacks carry no request ID. Late callbacks from
                // an incomplete batch must never be attributed to a subsequent lookup.
                reconnectRequired = !batch.Result.Task.IsCompletedSuccessfully;
                active = null;
            }
        }
    }

    public override void openOrder(int orderId, Contract contract, Order order, OrderState orderState) =>
        RecordStatus(orderId, order, orderState.Status, completed: false);

    public override void completedOrder(Contract contract, Order order, OrderState orderState) =>
        RecordStatus(order.OrderId, order, string.IsNullOrWhiteSpace(orderState.CompletedStatus)
            ? orderState.Status : orderState.CompletedStatus, completed: true);

    public override void openOrderEnd() => Complete(1);
    public override void completedOrdersEnd() => Complete(2);

    public override void execDetailsEnd(int reqId)
    {
        lock (gate)
            if (active?.ExecutionRequestId == reqId)
                Complete(4);
    }

    public override void execDetails(int reqId, Contract contract, Execution execution)
    {
        lock (gate)
        {
            if (active is not { } batch || batch.ExecutionRequestId != reqId ||
                !Matches(batch.Identity, execution.AcctNumber, execution.ClientId, execution.OrderId, execution.PermId, execution.OrderRef))
                return;
            var fact = new IbkrOrderExecution(execution.ExecId, contract.ConId, execution.Side, execution.Shares, execution.Price, execution.Time);
            if (string.IsNullOrWhiteSpace(fact.ExecutionId) ||
                batch.Executions.TryGetValue(fact.ExecutionId, out var prior) && prior != fact)
                batch.Result.TrySetException(new InvalidOperationException("IBKR returned conflicting execution evidence."));
            else
                batch.Executions[fact.ExecutionId] = fact;
        }
    }

    public override void connectionClosed() => Fail();
    public override void error(Exception e) => Fail();
    public override void error(string str) => Fail();

    public override void error(int id, int errorCode, string errorMsg)
    {
        lock (gate)
            if (active is { } batch && (id == batch.ExecutionRequestId || id == -1 && errorCode is not (2104 or 2106 or 2158)))
                Fail();
    }

    void RecordStatus(int orderId, Order order, string status, bool completed)
    {
        lock (gate)
            if (active is { } batch && Matches(batch.Identity, order.Account, order.ClientId, orderId, order.PermId, order.OrderRef))
                batch.Statuses.Add(new(status, completed));
    }

    static bool Matches(IbkrOrderIdentity identity, string account, int clientId, int orderId, int permanentId, string orderReference) =>
        string.Equals(account, identity.Account, StringComparison.Ordinal) &&
        (identity.PermanentId > 0 ? permanentId == identity.PermanentId :
            clientId == identity.ClientId && orderId == identity.OrderId &&
            string.Equals(orderReference, identity.OrderReference, StringComparison.Ordinal));

    void Complete(int download)
    {
        lock (gate)
        {
            if (active is not { } batch)
                return;
            batch.Completed |= download;
            if (batch.Completed == 7)
                batch.Result.TrySetResult(new([.. batch.Statuses], [.. batch.Executions.Values]));
        }
    }

    void Fail()
    {
        lock (gate)
            active?.Result.TrySetException(new InvalidOperationException("IBKR status evidence download was interrupted."));
    }

    sealed class Batch(IbkrOrderIdentity identity, int executionRequestId)
    {
        public IbkrOrderIdentity Identity { get; } = identity;
        public int ExecutionRequestId { get; } = executionRequestId;
        public int Completed { get; set; }
        public HashSet<IbkrOrderStatus> Statuses { get; } = [];
        public Dictionary<string, IbkrOrderExecution> Executions { get; } = new(StringComparer.Ordinal);
        public TaskCompletionSource<IbkrOrderStatusEvidence> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
