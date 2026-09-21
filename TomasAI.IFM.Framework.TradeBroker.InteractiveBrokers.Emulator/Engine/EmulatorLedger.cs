using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>One synchronized synthetic ledger shared by order execution and account ports.</summary>
public sealed class EmulatorLedger
{
    private static readonly EmulatorFaultProfile NoFaults = new();
    private readonly object _sync = new();
    private readonly EmulatorScenario _scenario;
    private readonly IEmulatorClock _clock;
    private readonly IEmulatorLedgerStore _store;
    private readonly Dictionary<string, OrderState> _orders = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (string Hash, FrameworkDispatchReceipt Receipt)> _operations = [];
    private readonly Dictionary<string, FrameworkAccountPosition> _positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EmulatorQuote> _latestQuotes = new(StringComparer.Ordinal);
    private readonly List<EmulatorJournalEntry> _journal = [];
    private readonly List<FrameworkBrokerObservation> _observations = [];
    private readonly Channel<FrameworkBrokerObservation> _orderEvents = CreateCriticalObservationChannel();
    private readonly Channel<FrameworkBrokerObservation> _accountEvents = CreateCriticalObservationChannel();
    private decimal _cash;
    private long _sequence;
    private long _generation = 1;
    private EmulatorFaultProfile Faults => _scenario.Faults ?? NoFaults;

    public EmulatorLedger(EmulatorScenario scenario, IEmulatorClock clock)
        : this(scenario, clock, new InMemoryEmulatorLedgerStore()) { }

    public EmulatorLedger(EmulatorScenario scenario, IEmulatorClock clock, IEmulatorLedgerStore store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.AccountAlias);
        if (scenario.StartingCash < 0 || scenario.PerLegCommission < 0 ||
            scenario.MaximumQuoteAge <= TimeSpan.Zero || scenario.MaximumStrategyUnitsPerFill <= 0)
            throw new ArgumentOutOfRangeException(nameof(scenario));
        _scenario = scenario;
        _clock = clock;
        _store = store;
        var checkpoint = store.Load();
        if (checkpoint is null)
        {
            _cash = scenario.StartingCash;
            return;
        }
        _cash = checkpoint.Cash;
        _sequence = checkpoint.Sequence;
        _generation = checkpoint.Generation;
        foreach (var item in checkpoint.Orders)
        {
            var totalUnits = Math.Abs(item.Request.Legs[0].SignedQuantity);
            _orders.Add(item.Request.BrokerOrderId, new OrderState(item.Request)
            {
                Limit = item.Limit,
                Revision = item.Revision,
                Filled = item.Filled,
                Cancelled = item.Cancelled,
                FilledStrategyUnits = item.FilledStrategyUnits > 0
                    ? item.FilledStrategyUnits
                    : item.Filled ? totalUnits : 0
            });
        }
        foreach (var item in checkpoint.Operations) _operations.Add(item.OperationId, (item.Hash, item.Receipt));
        foreach (var item in checkpoint.Positions) _positions.Add(item.ContractId, item);
        _journal.AddRange(checkpoint.Journal);
        _observations.AddRange(checkpoint.Observations);
    }

    public string AccountAlias => _scenario.AccountAlias;
    public long Generation { get { lock (_sync) return _generation; } }
    public ImmutableArray<EmulatorJournalEntry> Journal { get { lock (_sync) return [.. _journal]; } }
    public string LedgerHash
    {
        get
        {
            lock (_sync)
                return Convert.ToHexString(SHA256.HashData(
                    JsonSerializer.SerializeToUtf8Bytes(CreateCheckpoint())));
        }
    }

    public FrameworkDispatchReceipt Place(FrameworkOrderRequest request)
    {
        lock (_sync)
        {
            var hash = Fingerprint(request);
            if (_operations.TryGetValue(request.OperationId, out var prior))
                return prior.Hash == hash ? prior.Receipt : Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.OPERATION.CONFLICT", "Operation ID was reused with different content.");
            var failure = Validate(request);
            if (failure is not null)
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, failure.Value.Code, failure.Value.Detail));
            if (_orders.TryGetValue(request.BrokerOrderId, out var existing))
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.ORDER.EXISTS", $"Order already exists at revision {existing.Revision}."));
            var reserve = ReserveFor(request);
            if (reserve > _cash - ActiveReserve())
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.CASH.INSUFFICIENT", "Conservative synthetic capital reservation exceeds available cash."));
            _orders.Add(request.BrokerOrderId, new OrderState(request));
            Append("Place", request.BrokerOrderId, request.OperationId);
            var receipt = Faults.PlaceDispatchOutcomeUnknown
                ? Store(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId,
                    FrameworkDispatchOutcome.OutcomeUnknown, "EM.DISPATCH.UNKNOWN",
                    "Synthetic order was accepted, but the local dispatch outcome is deliberately ambiguous."))
                : Store(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId,
                    FrameworkDispatchOutcome.AcceptedForDispatch, "EM.DISPATCHED",
                    "Synthetic order accepted for matching; no fill implied."));
            if (Faults.SuppressAcknowledgement)
                Persist();
            else
                CommitAndPublish(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.Acknowledged, ObservationId = ObservationId("ack", request.BrokerOrderId, _sequence), AccountAlias = AccountAlias, BrokerOrderId = request.BrokerOrderId, OperationId = request.OperationId, ComponentId = request.ComponentId, OrderRevision = 1, SourceEpoch = _generation, SourceSequence = _sequence, OccurredAtUtc = _clock.UtcNow });
            return receipt;
        }
    }

    public FrameworkDispatchReceipt Modify(FrameworkLimitUpdate request)
    {
        lock (_sync)
        {
            var hash = Fingerprint(request);
            if (_operations.TryGetValue(request.OperationId, out var prior))
                return prior.Hash == hash ? prior.Receipt : Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.OPERATION.CONFLICT", "Operation ID was reused with different content.");
            if (request.AccountAlias != AccountAlias || !_orders.TryGetValue(request.BrokerOrderId, out var order) || order.Cancelled || order.Filled)
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.ORDER.UNAVAILABLE", "Working order/account was not found."));
            if (request.ExpectedRevision != order.Revision || !ValidLimit(request.NewSignedNetDebitLimit, order.Request))
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.LIMIT.INVALID", "Expected revision or approved price envelope is invalid."));
            order.Revision++;
            order.Limit = request.NewSignedNetDebitLimit;
            // Later authoritative fills belong to the most recently accepted mutation. Keeping
            // this operation identity current lets the durable BrokerOrder stream correlate a
            // fill that follows a price change without weakening its operation guard.
            order.Request = order.Request with
            {
                OperationId = request.OperationId,
                SignedNetDebitLimit = request.NewSignedNetDebitLimit
            };
            Append("Modify", request.BrokerOrderId, request.OperationId);
            var receipt = Store(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.AcceptedForDispatch, "EM.MODIFIED", "Price-only change accepted."));
            CommitAndPublish(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.Acknowledged,
                ObservationId = ObservationId("modified", request.BrokerOrderId, _sequence),
                AccountAlias = AccountAlias, BrokerOrderId = request.BrokerOrderId,
                OperationId = request.OperationId, ComponentId = order.Request.ComponentId,
                OrderRevision = order.Revision, SourceEpoch = _generation,
                SourceSequence = _sequence, OccurredAtUtc = _clock.UtcNow });
            return receipt;
        }
    }

    public FrameworkDispatchReceipt Cancel(FrameworkCancelRequest request)
    {
        lock (_sync)
        {
            var hash = Fingerprint(request);
            if (_operations.TryGetValue(request.OperationId, out var prior))
                return prior.Hash == hash ? prior.Receipt : Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.OPERATION.CONFLICT", "Operation ID was reused with different content.");
            if (request.AccountAlias != AccountAlias || !_orders.TryGetValue(request.BrokerOrderId, out var order) || request.ExpectedRevision != order.Revision)
                return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.RejectedLocally, "EM.CANCEL.INVALID", "Order/account or revision is invalid."));
            if (!order.Filled && !order.Cancelled)
            {
                order.Cancelled = true;
                Append("Cancel", request.BrokerOrderId, request.OperationId);
                CommitAndPublish(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.Cancelled, ObservationId = ObservationId("cancel", request.BrokerOrderId, _sequence), AccountAlias = AccountAlias, BrokerOrderId = request.BrokerOrderId, OperationId = request.OperationId, ComponentId = order.Request.ComponentId, OrderRevision = order.Revision, SourceEpoch = _generation, SourceSequence = _sequence, OccurredAtUtc = _clock.UtcNow });
            }
            return StoreAndPersist(request.OperationId, hash, Receipt(request.OperationId, request.BrokerOrderId, FrameworkDispatchOutcome.AcceptedForDispatch, "EM.CANCELLED", "Synthetic order cancelled or already final."));
        }
    }

    /// <summary>Updates one transient top-of-book quote and evaluates only working orders that use its contract.</summary>
    public int PublishQuote(EmulatorQuote quote)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(quote.ContractId) || quote.MarketTimeUtc.Kind != DateTimeKind.Utc ||
                quote.SourceEpoch <= 0 || quote.SourceSequence <= 0 || quote.Bid <= 0 || quote.Ask < quote.Bid)
                return 0;
            _latestQuotes[quote.ContractId] = quote;
            var candidates = _orders.Values
                .Where(order => (!order.Cancelled || Faults.AllowFillsAfterCancel) && !order.Filled &&
                    order.Request.Legs.Any(leg => leg.ContractId == quote.ContractId))
                .Select(order => order.Request.BrokerOrderId)
                .ToArray();
            var matched = 0;
            foreach (var brokerOrderId in candidates)
            {
                var order = _orders[brokerOrderId];
                var quotes = new EmulatorQuote[order.Request.Legs.Length];
                var complete = true;
                for (var index = 0; index < order.Request.Legs.Length; index++)
                {
                    if (!_latestQuotes.TryGetValue(order.Request.Legs[index].ContractId, out var found))
                    {
                        complete = false;
                        break;
                    }
                    quotes[index] = found;
                }
                if (complete && TryMatch(brokerOrderId, quotes)) matched++;
            }
            return matched;
        }
    }

    /// <summary>Match a complete quote set once; a missing, stale or mixed-source quote keeps the order working.</summary>
    public bool TryMatch(string brokerOrderId, IReadOnlyList<EmulatorQuote> quotes)
    {
        lock (_sync)
        {
            if (Faults.DisconnectMarketMatching ||
                !_orders.TryGetValue(brokerOrderId, out var order) ||
                order.Cancelled && !Faults.AllowFillsAfterCancel || order.Filled ||
                _clock.UtcNow > order.Request.ValidUntilUtc) return false;
            var legs = order.Request.Legs;
            if (quotes.Count != legs.Length || quotes.Count == 0) return false;
            var epoch = quotes[0].SourceEpoch;
            var earliestMarketTime = quotes.Min(quote => quote.MarketTimeUtc);
            var latestMarketTime = quotes.Max(quote => quote.MarketTimeUtc);
            var sourceSequence = quotes.Max(quote => quote.SourceSequence);
            if (latestMarketTime - earliestMarketTime > _scenario.MaximumQuoteAge) return false;
            decimal netDebit = 0;
            var availableStrategyUnits = int.MaxValue;
            for (var i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                var quote = quotes[i];
                if (leg.ContractId != quote.ContractId || quote.SourceEpoch != epoch ||
                    quote.MarketTimeUtc.Kind != DateTimeKind.Utc || quote.MarketTimeUtc > _clock.UtcNow ||
                    _clock.UtcNow - quote.MarketTimeUtc > _scenario.MaximumQuoteAge || quote.Bid <= 0 ||
                    quote.Ask < quote.Bid) return false;
                var executableSize = leg.SignedQuantity > 0 ? quote.AskSize : quote.BidSize;
                if (executableSize <= 0) return false;
                availableStrategyUnits = Math.Min(availableStrategyUnits, executableSize);
                netDebit += (leg.SignedQuantity > 0 ? quote.Ask : -quote.Bid) * Math.Abs(leg.SignedQuantity);
            }
            // Combo limits are quoted per balanced strategy unit, not per total order quantity.
            var totalUnits = Math.Abs(legs[0].SignedQuantity);
            var remainingUnits = totalUnits - order.FilledStrategyUnits;
            var fillUnits = Math.Min(remainingUnits,
                Math.Min(availableStrategyUnits, _scenario.MaximumStrategyUnitsPerFill));
            if (fillUnits <= 0) return false;
            var netDebitPerUnit = netDebit / totalUnits;
            if (order.Request.OrderType == FrameworkOrderType.Limit && netDebitPerUnit > order.Limit) return false;
            var fee = _scenario.PerLegCommission * legs.Length * fillUnits;
            var cashMovement = netDebitPerUnit * fillUnits * legs[0].CashMultiplier;
            if (cashMovement + fee > _cash - ActiveReserve(except: brokerOrderId)) return false;
            order.FilledStrategyUnits += fillUnits;
            order.Filled = order.FilledStrategyUnits == totalUnits;
            _cash -= cashMovement + fee;
            var executionFacts = new List<FrameworkBrokerObservation>(legs.Length);
            var commissionFacts = new List<FrameworkBrokerObservation>(legs.Length);
            var pendingFacts = new List<FrameworkBrokerObservation>(legs.Length * 2 + 2);
            foreach (var leg in legs)
            {
                var quote = quotes[Array.FindIndex(legs, x => x.LegId == leg.LegId)];
                var price = leg.SignedQuantity > 0 ? quote.Ask : quote.Bid;
                var previous = _positions.GetValueOrDefault(leg.ContractId);
                var filledQuantity = Math.Sign(leg.SignedQuantity) * fillUnits;
                var quantity = (previous?.SignedQuantity ?? 0) + filledQuantity;
                if (quantity == 0) _positions.Remove(leg.ContractId);
                else _positions[leg.ContractId] = new FrameworkAccountPosition(leg.ContractId, quantity, price);
                Append("Execution", brokerOrderId, order.Request.OperationId);
                var execId = $"EM-{brokerOrderId}-{leg.LegId:N}-{_sequence}";
                executionFacts.Add(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.Execution, ObservationId = ObservationId("exec", brokerOrderId, _sequence), AccountAlias = AccountAlias, BrokerOrderId = brokerOrderId, OperationId = order.Request.OperationId, ComponentId = order.Request.ComponentId, LegId = leg.LegId, ContractId = leg.ContractId, ExternalExecutionId = execId, SignedQuantity = filledQuantity, Price = price, OrderRevision = order.Revision, SourceEpoch = epoch, SourceSequence = quote.SourceSequence, OccurredAtUtc = _clock.UtcNow });
                Append("Commission", brokerOrderId, order.Request.OperationId);
                commissionFacts.Add(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.Commission, ObservationId = ObservationId("fee", brokerOrderId, _sequence), AccountAlias = AccountAlias, BrokerOrderId = brokerOrderId, OperationId = order.Request.OperationId, ComponentId = order.Request.ComponentId, LegId = leg.LegId, ContractId = leg.ContractId, ExternalExecutionId = execId, Commission = _scenario.PerLegCommission * fillUnits, OrderRevision = order.Revision, SourceEpoch = epoch, SourceSequence = quote.SourceSequence, OccurredAtUtc = _clock.UtcNow });
            }
            if (Faults.PublishCommissionBeforeExecution)
            {
                pendingFacts.AddRange(commissionFacts);
                pendingFacts.AddRange(executionFacts);
            }
            else
            {
                pendingFacts.AddRange(executionFacts);
                pendingFacts.AddRange(commissionFacts);
            }
            if (order.Filled)
            {
                Append("OrderCompleted", brokerOrderId, order.Request.OperationId);
                pendingFacts.Add(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.OrderCompleted,
                    ObservationId = ObservationId("completed", brokerOrderId, _sequence), AccountAlias = AccountAlias,
                    BrokerOrderId = brokerOrderId, OperationId = order.Request.OperationId,
                    ComponentId = order.Request.ComponentId, OrderRevision = order.Revision,
                    SourceEpoch = epoch, SourceSequence = _sequence, OccurredAtUtc = _clock.UtcNow });
            }
            _generation++;
            Append("Account", brokerOrderId, order.Request.OperationId);
            pendingFacts.Add(new FrameworkBrokerObservation { Kind = FrameworkObservationKind.AccountSnapshot, ObservationId = ObservationId("account", brokerOrderId, _sequence), AccountAlias = AccountAlias, BrokerOrderId = brokerOrderId, OperationId = order.Request.OperationId, SourceEpoch = _generation, SourceSequence = _sequence, OccurredAtUtc = _clock.UtcNow });
            CommitAndPublish([.. pendingFacts]);
            return true;
        }
    }

    public FrameworkAccountSnapshot Snapshot()
    {
        lock (_sync)
        {
            var available = _cash - ActiveReserve();
            return new FrameworkAccountSnapshot { AccountAlias = AccountAlias, Currency = _scenario.Currency, CashBalance = _cash,
                AvailableFunds = available, Complete = true, NewRiskAllowed = available >= 0,
                Generation = _generation, AsOfUtc = _clock.UtcNow, Positions = [.. _positions.Values] };
        }
    }

    public FrameworkBrokerObservation[] Reconcile(string brokerOrderId)
    {
        lock (_sync) return [.. _observations.Where(x => x.BrokerOrderId == brokerOrderId)];
    }

    public IAsyncEnumerable<FrameworkBrokerObservation> ObserveOrders(CancellationToken token) => _orderEvents.Reader.ReadAllAsync(token);
    public IAsyncEnumerable<FrameworkBrokerObservation> ObserveAccount(CancellationToken token) => _accountEvents.Reader.ReadAllAsync(token);

    private (string Code, string Detail)? Validate(FrameworkOrderRequest r)
    {
        if (r.AccountAlias != AccountAlias || string.IsNullOrWhiteSpace(r.BrokerOrderId) || r.OperationId == Guid.Empty || r.ComponentId == Guid.Empty || string.IsNullOrWhiteSpace(r.ApprovalHash)) return ("EM.IDENTITY.INVALID", "Account/order/operation/approval is missing or mismatched.");
        if (r.OrderType is not (FrameworkOrderType.Market or FrameworkOrderType.Limit)
            || r.Algorithm is not (FrameworkOrderAlgorithm.None or FrameworkOrderAlgorithm.Adaptive))
            return ("EM.CAPABILITY.UNSUPPORTED", "Only None/Adaptive on Market/Limit orders are emulated.");
        if (r.ValidUntilUtc.Kind != DateTimeKind.Utc || r.ValidUntilUtc <= _clock.UtcNow) return ("EM.EXPIRED", "Approved order is expired or time is not UTC.");
        var expected = r.Shape switch { FrameworkOrderShape.FuturesOutright => 1, FrameworkOrderShape.VerticalSpread => 2, FrameworkOrderShape.IronCondor => 4, _ => 0 };
        if (expected == 0 || r.Legs.Length != expected || r.Legs.Any(x => x.LegId == Guid.Empty || string.IsNullOrWhiteSpace(x.ContractId) || x.SignedQuantity == 0 || x.CashMultiplier <= 0) || r.Legs.Select(x => x.LegId).Distinct().Count() != expected) return ("EM.SHAPE.UNSUPPORTED", "Approved component does not have the expected distinct contract legs and cash multipliers.");
        if (r.Shape == FrameworkOrderShape.FuturesOutright && r.Legs[0].Strike is not null) return ("EM.SHAPE.INVALID", "Futures outright cannot contain option strike.");
        if (r.Shape != FrameworkOrderShape.FuturesOutright && r.Legs.Any(x => Math.Abs(x.SignedQuantity) != Math.Abs(r.Legs[0].SignedQuantity))) return ("EM.SHAPE.UNBALANCED", "Combo legs must have one balanced strategy unit ratio.");
        if (r.Legs.Any(x => x.CashMultiplier != r.Legs[0].CashMultiplier)) return ("EM.MULTIPLIER.MISMATCH", "Combo leg cash multipliers must be coherent.");
        if (r.OrderType == FrameworkOrderType.Limit && !ValidLimit(r.SignedNetDebitLimit, r)) return ("EM.LIMIT.INVALID", "Signed net debit limit is outside approved tick-aligned bounds.");
        if (r.OrderType == FrameworkOrderType.Market && r.TickIncrement <= 0)
            return ("EM.MARKET.INVALID", "Market orders still require a positive contract tick for execution evidence.");
        if (r.RequiredCapital < 0 || r.MaximumLoss < 0 || r.RequiredCapital == 0 && r.MaximumLoss == 0)
            return ("EM.CAPITAL.UNKNOWN", "An approved nonzero synthetic capital or loss bound is required.");
        return null;
    }

    private decimal ActiveReserve(string? except = null) => _orders.Values
        .Where(x => !x.Filled && !x.Cancelled && x.Request.BrokerOrderId != except)
        .Sum(ReserveFor);

    private decimal ReserveFor(OrderState order)
    {
        var totalUnits = Math.Abs(order.Request.Legs[0].SignedQuantity);
        var remainingUnits = totalUnits - order.FilledStrategyUnits;
        return totalUnits == 0 ? 0 : ReserveFor(order.Request) * remainingUnits / totalUnits;
    }

    private decimal ReserveFor(FrameworkOrderRequest request)
    {
        var units = Math.Abs(request.Legs[0].SignedQuantity);
        var positiveDebit = Math.Max(0, request.MaximumLimit * units * request.Legs[0].CashMultiplier);
        var fee = _scenario.PerLegCommission * request.Legs.Sum(x => Math.Abs(x.SignedQuantity));
        return Math.Max(Math.Max(request.RequiredCapital, request.MaximumLoss), positiveDebit + fee);
    }

    private static bool ValidLimit(decimal limit, FrameworkOrderRequest r) => r.TickIncrement > 0 && r.MinimumLimit <= r.MaximumLimit && limit >= r.MinimumLimit && limit <= r.MaximumLimit && decimal.Remainder(limit, r.TickIncrement) == 0;
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value))));
    private FrameworkDispatchReceipt Receipt(Guid op, string id, FrameworkDispatchOutcome outcome, string category, string detail) => new(outcome, op, id, category, detail, _clock.UtcNow);
    private FrameworkDispatchReceipt Store(Guid op, string hash, FrameworkDispatchReceipt receipt) { if (op != Guid.Empty) _operations.Add(op, (hash, receipt)); return receipt; }
    private FrameworkDispatchReceipt StoreAndPersist(Guid op, string hash, FrameworkDispatchReceipt receipt)
    {
        Store(op, hash, receipt);
        Persist();
        return receipt;
    }
    private void Append(string kind, string id, Guid op) { _sequence++; _journal.Add(new EmulatorJournalEntry(_sequence, kind, id, op, _cash, _clock.UtcNow)); }
    private void CommitAndPublish(params FrameworkBrokerObservation[] facts)
    {
        _observations.AddRange(facts);
        Persist();
        foreach (var fact in facts)
        {
            var channel = fact.Kind == FrameworkObservationKind.AccountSnapshot ? _accountEvents : _orderEvents;
            channel.Writer.WriteAsync(fact).AsTask().GetAwaiter().GetResult();
            if (Faults.DuplicateExecutionCallbacks &&
                fact.Kind is FrameworkObservationKind.Execution or FrameworkObservationKind.Commission)
                channel.Writer.WriteAsync(fact).AsTask().GetAwaiter().GetResult();
        }
    }

    private static Channel<FrameworkBrokerObservation> CreateCriticalObservationChannel() =>
        Channel.CreateBounded<FrameworkBrokerObservation>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    private void Persist() => _store.Save(CreateCheckpoint());

    private EmulatorLedgerCheckpoint CreateCheckpoint() => new(_cash, _sequence, _generation,
        [.. _orders.Values.Select(x => new EmulatorOrderCheckpoint(
            x.Request, x.Limit, x.Revision, x.Filled, x.Cancelled, x.FilledStrategyUnits))],
        [.. _operations.Select(x => new EmulatorOperationCheckpoint(x.Key, x.Value.Hash, x.Value.Receipt))],
        [.. _positions.Values], [.. _journal], [.. _observations]);
    private static Guid ObservationId(string kind, string id, long seq) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{id}|{seq}"))[..16]);

    private sealed class OrderState(FrameworkOrderRequest request)
    {
        public FrameworkOrderRequest Request { get; set; } = request;
        public decimal Limit { get; set; } = request.SignedNetDebitLimit;
        public int Revision { get; set; } = 1;
        public bool Filled { get; set; }
        public bool Cancelled { get; set; }
        public int FilledStrategyUnits { get; set; }
    }
}
