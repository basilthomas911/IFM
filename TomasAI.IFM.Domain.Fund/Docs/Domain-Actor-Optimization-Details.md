# Domain.Fund actor optimization details

Date: 2026-08-05

## Scope and invariants

The review covered the Fund command, event, query, projector, and nested FundTransaction actor paths from shared messages through state, repositories, calculations, and tests. The following established semantics remain unchanged:

- Fund event history remains immutable and unbounded.
- `FundTransactionEventActor` remains an intentionally empty same-domain event target.
- A successful command result and a state update remain distinct concepts. A command can succeed without producing a state change.
- State repository forwarding methods retain explicit `async`/`await`; no target-typed `ValueTask` forwarding micro-optimization was introduced.
- Actor state remains mailbox-owned. No locks or `Task.Run` calls were added.
- Cancellation propagation remains a solution-wide follow-up after the domain optimization passes.

## Top ten findings and disposition

1. **End-of-day event payload mutation - fixed.** The command now calculates the authoritative balance once and constructs the final event. State replay applies the immutable payload without reflection or recalculation.
2. **Full FundTransaction stream replay - deferred snapshot subphase.** Streams are partitioned by `FundId.OrderId`, but long-lived orders can still accumulate an unbounded replay tail.
3. **Fund replay tail after `FundCreatedEvent` - deferred snapshot subphase.** The current creation snapshot does not compact later order and trade history.
4. **Sequential independent financial reads - fixed.** P&L, win/loss, drawdown, and maximum-profit calculations start independent reads together and await them as a group.
5. **Allocation-heavy Sharpe calculation and swallowed failures - fixed.** Daily balances are read once, processed in one allocation-free pass, and storage exceptions flow to the actor/API error boundary. A zero denominator returns the documented zero result.
6. **Duplicated actor/API financial logic - fixed.** Both entry points now use `FundQueryCalculations`, preventing behavior and performance drift.
7. **Per-command validator graph and batch LINQ allocation - fixed.** Immutable FluentValidation graphs and rule objects are cached, batch consistency uses indexed loops, and transaction arrays are allocated at their exact size.
8. **Duplicate dictionary probes and LINQ state scans - fixed.** Order and trade collections expose `TryGet` and identifier-based removal, and state transitions use a single indexed lookup.
9. **Repeated command/event metadata generation - deferred.** This is a micro-optimization and should be revisited only with production/paper-trading telemetry. The current message contract was left unchanged.
10. **Typed transaction error event discarded from the returned result - fixed.** The transaction command actor now returns `ServiceFailed<GuidResult>` backed by the specialized error event it sent.

## Implementation details

### Query concurrency and calculation sharing

`FundQueryCalculations` owns P&L, win/loss, drawdown, maximum-profit, and Sharpe calculations. Each storage method creates its own operation context; characterization tests additionally verify that the Fund query boundary overlaps independent calls. The implementation uses asynchronous I/O directly and does not occupy worker threads while waiting.

P&L previously performed seven sequential storage calls because daily balances and Sharpe were requested twice. It now starts six independent calls and computes both currently equivalent Sharpe fields from the same result. Target and actual Sharpe semantics were deliberately not redefined by this optimization.

### Transaction immutability

Single and batch transactions now calculate balances before event construction. End-of-day state application no longer invokes `EventInitHelper.SetProperty` or derives a second balance from reconstructed transactions. This makes live processing and replay consume the same persisted payload.

### Validation and actor state

Fund validators contain immutable rules and are safe to reuse. Batch validation performs one consistency scan followed by validation, avoiding LINQ iterator allocation. Dictionary-backed order and trade collections now support one-probe retrieval and identifier removal; replay and command checks keep their prior state-change return behavior.

## Benchmark summary

BenchmarkDotNet 0.15.8, .NET 10.0.10, x64 RyuJIT, AMD Ryzen Threadripper 1950X, three warmups, eight measurement iterations.

| Hot path | Input | Before | After | Improvement | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|---:|
| Sharpe | 32 balances | 484.6 ns | 338.1 ns | 30.2% | 608 B | 0 B |
| Sharpe | 256 balances | 3,523.9 ns | 2,658.8 ns | 24.6% | 4,264 B | 0 B |
| Sharpe | 2,048 balances | 26,684.1 ns | 21,200.6 ns | 20.5% | 33,008 B | 0 B |
| Query fan-out | P&L-style reads | 108.38 ms | 15.53 ms | 85.7% | 2.04 KB | 2.03 KB |
| Batch materialization | 32 transactions | 622.5 ns | 221.0 ns | 64.5% | 632 B | 536 B |
| Batch materialization | 256 transactions | 4,653.6 ns | 1,584.0 ns | 66.0% | 4,216 B | 4,120 B |
| State probe | 32 items | 6.043 ns | 3.923 ns | 35.1% | 0 B | 0 B |
| State probe | 2,048 items | 5.982 ns | 3.934 ns | 34.2% | 0 B | 0 B |

The query benchmark uses controlled asynchronous delay and represents critical-path composition, not live database throughput. Full tables and methodology are in `TomasAI.IFM.Domain.Fund.Benchmarks/RESULTS.md`. No benchmark recorded monitor lock contention.

## Verification

- Domain.Fund unit tests, including concurrency, zero-denominator, and replay-immutability characterization.
- Domain.Fund BDD actor-pipeline tests.
- Release solution build to catch shared collection contract consumers.
- Benchmark project Release build and out-of-process BenchmarkDotNet measurements.

## TODO: compact snapshot replay subphase

Preserve unbounded immutable history while bounding reconstruction work:

1. Introduce explicit compact snapshot event types for Fund and FundTransaction state.
2. Store the complete current state needed by future commands, rather than an arbitrary last-N slice.
3. Load the newest compatible snapshot and replay only events after its stream position.
4. Fall back to the existing `FundCreatedEvent` or full transaction stream for historical streams with no compact snapshot.
5. Treat a missing snapshot or missing event type as an empty/best-effort result where the existing replay contract permits it; do not introduce command-processing exceptions solely for absence.
6. Preserve ascending replay order and immutable original events.
7. Benchmark 32, 256, and 2,048-event tails before implementing a snapshot cadence.

This subphase is intentionally separate because snapshot schema, cadence, compatibility, and deployment require their own design review.

## TODO: solution-wide cancellation semantics

After all domain optimization passes are complete, propagate graceful cancellation from supervisor through actor, repository, and storage APIs in one coordinated solution-wide change. Partial domain-only cancellation plumbing is intentionally avoided.
