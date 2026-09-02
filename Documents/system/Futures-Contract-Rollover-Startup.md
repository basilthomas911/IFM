# Futures contract on-the-run and rollover startup

## Purpose

Market-data feeds may start only from a coherent, persisted futures rollover assignment. The
assignment distinguishes two operational facts:

- `OnTheRun`: the one primary contract for a futures root on the authoritative value date;
- `Rollover`: every contract admitted to live subscription and rollover/term-structure use.

Provider availability is reference data only. An active or unexpired DataBento definition is not
operationally selected until the rollover procedure assigns these flags.

The v1 minimum set is exactly:

| Root | Role | `OnTheRun` | `Rollover` |
| --- | --- | ---: | ---: |
| ES | active quarterly | `true` | `true` |
| VX | front month | `true` | `true` |
| VX | next month | `false` | `true` |

`OnTheRun=true, Rollover=false` is invalid. Inactive catalog rows are `false/false`.

## Durable authority

`futures_contract_rollover`, keyed by root symbol, is the authoritative pointer. Its `contractId`
must identify the singular on-the-run row and `nextRolloverDate` is the value date on which the next
assignment becomes effective. It is not a preparation timestamp.

The canonical `futures_contract_v3` row and `futures_contract_by_symbol_v3` query projection carry
both flags. The v3 projection key is logically:

```text
PRIMARY KEY ((symbol), rollover, onTheRun, lastTradeDate, contractId)
```

The storage API exposes:

- `GetOnTheRunFuturesContractAsync(symbol)` for the singular primary contract;
- `GetRolloverFuturesContractsAsync(symbol)` for the ordered subscription set;
- `ReplaceFuturesRolloverSetAsync(pointer, contracts)` for a complete root replacement.

Replacement validates root, unique IDs, exact ES/VX cardinality, expiry order, one on-the-run row,
rollover membership, and pointer identity. Same-root writes are serialized. Canonical rows,
projection rows and the pointer are submitted in one logged Scylla mutation and verified afterward.
Superseded rows remain as inactive `false/false` reference rows. Runtime state is never published
before durable verification succeeds.

## Resolution and transition

Reconciliation runs when the pointer is incomplete or the requested authoritative value date is on
or after `nextRolloverDate`. A complete, valid, non-due assignment is reused without requiring a
historical provider request.

ES selects the nearest eligible quarterly maturity and persists one `true/true` row. At rollover,
the old quarterly row is retired and the successor becomes the one durable and runtime assignment.

VX selects two distinct eligible maturities in ascending expiry order. The front row is `true/true`
and the back row is `false/true`. At rollover, the old back is promoted to front, the next maturity
becomes back, and the old front is retired. Both active rows are subscribed, while singular/current
lookups return only the front.

The runtime registry publishes one immutable per-root snapshot. An existing market-data epoch keeps
its original snapshot; a new value-date epoch receives the fully replaced set. This prevents readers
from observing a missing VX leg or a half-applied rollover.

## Business-day preparation

The replacement is prepared during the 17:00 inclusive to 18:00 exclusive Eastern closed interval
on the exchange business day preceding the effective rollover value date. Weekends and configured
exchange closures are skipped through `IFuturesExchangeBusinessCalendar`; calendar-day subtraction
is prohibited.

The preparation worker evaluates once per minute. During the window it resolves the next business
day and invokes the same idempotent startup check used for catch-up. It retries failures without
faulting the API host and reports structured failures to the application log and Status Console.
The assignment cannot be consumed before its effective value-date epoch opens at 18:00 Eastern.

If the window was missed or the API starts mid-session, the actor-owned application startup activity
reconciles the authoritative operational value date before market-data feed admission.

## Startup admission and failure behavior

Application startup:

1. creates the v3 Securities schema and idempotent ES/VX pointer rows;
2. resolves or reuses the ES assignment and VX pair;
3. verifies exact durable flags, cardinality, order and pointer identity;
4. publishes atomic runtime registry snapshots;
5. starts the DataBento epoch only after the assignments are admitted.

Market data remains an optional application capability: a resolver or storage failure degrades feed
admission but does not take down the core API or UI. Each startup activity catches and reports its
own failure so later independent startup activities still execute. Long-running hosted services
treat host cancellation as normal completion and contain unexpected exceptions at their service
boundary.

HTTP binding completes before NATS actors are exposed. Therefore, a duplicate API process that
cannot acquire its HTTP port terminates before it can consume actor messages, and it cannot leave
actor loops running against a disposed dependency-injection container.

## Operational verification

The API health payload reports the source value-date revision plus, for ES and VX, the effective
rollover date, preceding preparation date and ordered rollover-set IDs. Typed REST and NATS queries
independently expose the on-the-run identity and full rollover set.

Qualification covers:

- model invariants and MessagePack/JSON transport round trips;
- prior-business-day, weekend, configured-closure and DST-safe preparation boundaries;
- unordered/duplicate provider candidates and ES quarterly selection;
- VX front/back ordering and atomic immutable runtime publication;
- Scylla replacement, retirement, idempotency and concurrent same-root commands;
- NATS and REST query contracts;
- storage-failure fencing so runtime state cannot advance;
- deterministic verification across two consecutive ES and VX rollover cycles;
- complete MarketData analytics and feed regressions.

The executable gate record and exact test evidence are maintained in
`Futures-Contract-On-The-Run-and-Rollover-Set-Implementation-Plan-v1.0.md`.
