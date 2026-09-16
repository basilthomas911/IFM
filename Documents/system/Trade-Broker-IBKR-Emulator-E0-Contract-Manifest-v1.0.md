# IBKR emulator E0 contract and economic manifest

**Status:** Implemented engineering baseline, 2026-09-16. The settlement rule was approved by the product owner. E10 verification and E11 human account acceptance remain separate gates; this document does not open the operational account.

## Frozen runtime boundary

- Actors use the application-level `ITradeBroker`. It exposes provider-neutral place, price-only modify, cancel, reconciliation, account snapshot/resynchronization, market-quote input, and separate order/account observation streams.
- The framework boundary remains split into `IFrameworkOrderExecutionBroker` and `IFrameworkBrokerAccount`. Both emulator ports resolve to one singleton `EmulatorLedger` and therefore one account generation, cash balance, order ledger, position ledger, journal, and checkpoint.
- The API host registers only the emulator implementation for this release. The emulator project has no IBKR API package, credentials, or TWS socket path.
- Trade domain messages retain broker-neutral string `ContractId` values. A future live IBKR adapter owns conversion to IBKR contract identifiers.
- `BrokerOrderId` is `TradeOrderId + ExecutionAttemptId + ComponentId`. Portfolio, Fund, Order, and reserved Trade identities originate from Portfolio acceptance.
- BrokerOrder, OrderExecution, and BrokerAccount are durable event-sourced actor aggregates. PortfolioDb remains the PostgreSQL financial book of record.

## Frozen V1 economic rules

| Decision | Emulator rule |
|---|---|
| Supported order shapes | One-leg futures outright, two-leg futures-option vertical spread, and four-leg futures-option iron condor. Each executable component supports Opening or Closing. Unsupported/custom geometry fails locally before a fill. |
| Combo price sign | The component limit is signed net debit per balanced strategy unit. Buy-leg prices add and sell-leg prices subtract. Positive is cash paid; negative is cash received. The emulator never flips this sign. |
| Leg identity | Every leg has a stable `TradeLegId`, broker-neutral `ContractId`, nonzero signed integer quantity, and positive cash multiplier. Option combinations require balanced absolute quantities and coherent multipliers. |
| Limit envelope | Place and modify require explicit minimum, maximum, tick increment, and current signed limit. A modify is price-only and requires the current broker revision. |
| Quote evidence | Every exact leg needs a valid UTC top-of-book quote from one source epoch. Individual instrument sequences may differ. The quote set must fit the configured coherence/freshness window; missing, stale, mixed-epoch, crossed, or undersized evidence leaves the order working. |
| Fill rule | A futures fill uses its exact quote. A combination computes the signed executable net debit from every exact leg and fills only when it satisfies the approved component limit. No favorable price is invented. |
| Synthetic capital | Placement reserves the greatest of approved required capital, approved maximum loss, or positive maximum debit plus declared commission. Insufficient available synthetic cash rejects without creating an order or execution. |
| Commission | The scenario charges an explicit per-contract fee and emits a separate commission observation keyed to the external execution ID. No implicit zero commission is created. |
| Settlement | Confirmed signed settlement debits Fund Asset and credits Cash for a net debit; a net credit reverses those sides. Commission posts separately to Expense/Cash. Closing clears basis and posts the calculated realized P&L under the Portfolio rule. An order receipt, acknowledgement, status, or unconfirmed fill cannot post cash. |
| Persistence | Production development mode uses an atomic file checkpoint containing cash, operations, orders, positions, journal, and normalized observations. Tests use the same contract with an in-memory store. Restart restores the same order/account evidence and idempotent operation receipts. |
| Callback delivery | Separate bounded 4,096-item critical channels carry order and account observations with wait-on-full behavior. Actor observation bridges are the only readers and route normalized events through EventActor mailboxes. |

## Account and approval gate

The configured development account is `IFM-EMULATOR-PAPER` in `BrokerEnvironment.Emulator`. A complete synthetic account snapshot does not authorize opening risk. The durable state must contain:

1. a submitted manifest hash and evidence reference;
2. `ReviewPending` qualification;
3. an explicit human `AcceptAccountQualificationCommand` containing a nonempty approval ID, the exact submitted manifest hash, reviewer identity, and UTC review time;
4. no revocation or manual hold; and
5. a complete current account snapshot that allows new risk.

The desktop qualification dialog exposes submit, accept, revoke, hold, release, and resynchronize actions through `IBrokerAccountCommandApi`. Acceptance uses an explicit warning prompt and is never performed by startup, configuration, unit tests, or the emulator engine. A changed manifest, revocation, hold, incomplete account, or environment/account mismatch closes opening-risk dispatch. Closing orders remain eligible to reduce known risk under their own exact position identity.

## Manual trading route

Futures, Vertical Spread, and Iron Condor manual orders first enter Portfolio order-composition evaluation. Only Portfolio-accepted orders receive full IDs and reach `ITradeOrderLifecycleApi` with `ExecutionChannel.Broker`. The resulting committed OrderExecution creates BrokerOrder component streams. Committed BrokerOrder mutation events call the broker facade; normalized acknowledgements, executions, commissions, completion, cancellation, and rejection return through actor mailboxes.

The desktop views show the exact account gate, qualification, current BrokerOrder state, dispatch detail, execution attempt, per-leg contract/quantity/price, external execution ID, and commission. Futures and Vertical Spread use their dedicated manual editors and blotters. The Iron Condor blotter retains its strategy monitor and adds the same durable broker-evidence panel.

## Version and compatibility rules

- MessagePack additions are append-only. Immutable historic TradeOrders remain readable.
- A historic or incomplete TradeOrder without exact broker account, environment, approval reference, component envelope, tick, capital, loss, contract, quantity, and multiplier cannot enter broker dispatch.
- Operation IDs are idempotent: exact content returns the original receipt; changed content for the same ID is a conflict.
- Observation and external execution IDs are idempotent: exact redelivery is ignored; changed content is a classified failure.
- A price update becomes the current accepted operation identity, so later fills and fees correlate to the latest durable mutation.

## E0 exit evidence

The application/framework interfaces, three approved shapes, signed-price convention, quote requirements, capital rule, commission rule, Portfolio settlement rule, account gate, runtime DI topology, checkpoint boundary, and manual UI route are no longer ambiguous. Test and benchmark results are recorded in the E10 verification manifest. E11 remains closed until the product owner reviews that package and performs the explicit durable acceptance action.
