# IBKR emulator E10 verification manifest v1.0

**Status:** Engineering verification passed; E11 human acceptance recorded\
**Accepted:** 2026-09-16T13:15:53.3545173Z by `basil`\
**Approval ID:** `ec68a379-93a0-4f7a-8ef6-48e212e4a6bc`\
**Verified:** 2026-09-16\
**Account:** `IFM-EMULATOR-PAPER`\
**Environment:** `Emulator`\
**Repository baseline:** `5fc4e9d1a379e0e930b81e592d2b0e63ab60c471` plus the uncommitted implementation represented by the hashes below\
**SDK:** .NET SDK `10.0.302`

This manifest records the evidence for E10 of the [emulator implementation plan](Trade-Broker-IBKR-Emulator-Implementation-Plan-v1.0.md). It is evidence for review, not an approval. It does not open the Emulator new-risk gate and cannot authorize Paper or Live trading.

## Qualified scope

- One active synthetic cash account shared by the application order and account broker ports.
- Broker-neutral string ContractIds. No IBKR API, credential, TWS session, or live broker mutation is present.
- Opening and Closing orders for one-leg Futures, two-leg Futures Option Vertical Spread, and four-leg Futures Option Iron Condor components.
- Portfolio-approved identity and authority flow through TradeOrder, OrderExecution, BrokerOrder, fills, commissions, and Portfolio accounting.
- Exact signed net debit or credit settlement, commission posted separately, and realized P&L calculated on close.
- Durable BrokerOrder and BrokerAccount actors using the standard parse, validation, receive, and extension-handler mappings.
- Deterministic balanced partial fills, checkpoint recovery, price-only modification, cancellation, late fills after cancellation, fee-before-fill joining, and classified no-quote, stale-quote, mixed-epoch, insufficient-cash, and unsupported-shape behavior.
- Declarative qualification faults for ambiguous place outcome, missing acknowledgement, commission-before-execution ordering, exact duplicate execution/commission callbacks, cancel/fill race, and disconnected matching.
- Desktop manual entry and evidence views for Futures, Vertical Spread, and Iron Condor. Strategy workflow execution was deliberately excluded from this verification at the product owner's request.

## Version-bound hashes

The aggregate source hash is SHA-256 over the sorted relative path and SHA-256 pairs for 67 source/project files under the application broker, framework broker, emulator, BrokerAccount, and BrokerOrder roots.

| Artifact | SHA-256 |
|---|---|
| Aggregate TradeBroker implementation source manifest | `D485F7FC37E697BDFF62C5C81FFB0F6395C106A1B46222B4032B4818AB43F531` |
| E0 contract/economic manifest | `6043C879F7A9D96E8DE2B2D2A11DE5663942C586E2CA1C6E4F9DFA391CE54654` |
| Application `ITradeBroker` contract | `5BE9023BCD1D6C056E41AF5493CD45405F3A830B810420CEF30DEC344BE8FBD2` |
| Framework order-execution port | `551997224DF3BAB956CB524471A6EC1685A9C91DAB66DC997DC72A4CA262232B` |
| Framework broker-account port | `ECE0F65E333E7776F769A1FA8CE3FFD21AED336E009880E1510EF9BF133D53FC` |
| Emulator ledger | `E9222B4DA4DAD57408491D473481B4A94E9382FB74F40EE23EF8E24D304A2F19` |
| BrokerOrder messages/state schema | `06E12A048C8C9367C23151BC6E5DA8C02E175540052BA490455E7C9EA130120B` |
| Shared trade/execution aggregate schema | `F406E65CA2B81094CCBE225E95241225B0E3A52D2F63B3928376B8E65D1C8F2C` |

Any material change to these contracts, economic rules, emulator engine, account identity, order shapes, or qualification behavior requires a new manifest or an explicitly reviewed compatibility decision.

## Verification results

| Verification | Result |
|---|---|
| TradeBroker application/framework/emulator and scoped actor-convention unit suite | **36 passed, 0 failed** |
| Trade domain unit suite | **1,100 passed, 0 failed** |
| Portfolio domain unit suite | **226 passed, 0 failed** |
| Manual-trading UI system tests | **11 passed, 0 failed** |
| PostgreSQL Portfolio financial integration selection | **7 passed, 0 failed** |
| Isolated API/emulator/NATS actor-host integration | **1 passed, 0 failed**; supervisor and test host exited cleanly |
| API Debug build | **Succeeded**, 0 warnings, 0 errors |
| Desktop UI Debug build | **Succeeded**, 0 warnings, 0 errors |
| Git whitespace verification | **Passed** |

The isolated host test verifies one singleton ledger, both framework ports, the application facade, BrokerOrder Command/Event/Query/Realtime contexts, BrokerAccount Command/Event/Query contexts, a coherent account snapshot, JetStream delivery for durable observation events, and clean asynchronous/synchronous container disposal. The temporary NATS container is removed after the test.

The manual UI tests cover exact strategy shape and contract data, Portfolio command construction, TradeOrder lifecycle submission, dark strategy-specific editors/blotters, shared BrokerOrder/OrderExecution/fill evidence, Iron Condor evidence integration, and the human qualification dialog. Portfolio accounting and API-host behavior are verified separately against their real integration boundaries; no strategy workflow was started.

## Deterministic replay and soak evidence

The qualification soak ran 96 orders evenly across Futures, Vertical Spread, and Iron Condor. Every order filled in two balanced strategy-unit cycles. The ledger restarted from its checkpoint three times during each run. Two complete runs produced identical final state:

- partial-fill cycles: `192`
- checkpoint restarts per run: `3`
- final generation: `193`
- final synthetic cash: `9,998,684.80 USD`
- final ledger SHA-256: `050EBEA46074076926C564DDD6C7F9DF5AE1BEA5DA81CEA9590C928DAF6EAD38`
- execution time of the repeated deterministic test: approximately `297 ms` on this development host

The test also proves that an undersized or unbalanced quote cannot create one-sided strategy exposure, while a balanced partial fill survives restart and can either complete or leave only confirmed units after cancellation.

## BenchmarkDotNet evidence

The BenchmarkDotNet qualification run reported:

| Operation | Mean | Allocated |
|---|---:|---:|
| Map approved order DTO | `873.61 ns` | `1,160 B/op` |
| Filter material quote | `310.72 ns` | `480 B/op` |
| Read immutable account snapshot | `142.34 ns` | `288 B/op` |
| Decide micro-execution action | `70.58 ns` | `56 B/op` |

These are development-host microbenchmarks of application logic. They do not predict IBKR network or exchange throughput.

## Findings and review notes

- The isolated host verification found and corrected two lifecycle defects before this manifest was issued: observation bridges now use JetStream for Event subjects, and the account bridge waits for supervisor readiness before its initial snapshot publication. Shutdown cancellation is clean and does not create a transport-failure loop.
- The E5 review found and corrected missing balanced partial-fill behavior. Checkpoints now retain filled strategy units and reservations reduce with remaining units.
- BrokerOrder retains the prior accepted operation so an authoritative execution or commission arriving after update/cancel can be correlated. Cancel acknowledgement is not treated as proof that exposure cannot arrive.
- OrderExecution retains commission evidence received before its fill, joins it by external execution ID, rejects over-allocation, and preserves an authoritative late fill after cancellation.
- The repository-wide actor-convention PowerShell scripts contain historical hard-coded actor counts and report unrelated legacy actors. The new BrokerOrder and BrokerAccount trees are covered by scoped reflection tests that verify map parity and exactly one public handler method per mapped message. Updating the repository-wide script baselines is separate repository maintenance and is not used to claim that unrelated actors were verified here.
- The emulator uses immediate deterministic callbacks in normal mode. Its declared failure profile covers ordering, duplication, ambiguity, cancellation races, acknowledgement loss, and disconnect behavior. Wall-clock latency simulation and stochastic exchange behavior are outside this V1 qualification scope; the live adapter must not infer its timing behavior from the emulator.

## E11 gate

E11 was accepted by `basil` at `2026-09-16T13:15:53.3545173Z` through the durable BrokerAccount command path. The recorded acceptance contains:

- this exact aggregate source hash;
- approval ID `ec68a379-93a0-4f7a-8ef6-48e212e4a6bc`;
- reviewer identity and UTC review time; and
- the exact `IFM-EMULATOR-PAPER` account and `Emulator` environment.

The verified durable state is `QualificationStatus=Accepted`, `Gate=Open`, and revision `3`. Tests, startup, configuration, and this document did not grant acceptance; they record the human decision executed through `AcceptAccountQualificationCommand`. Revocation, a changed material hash, an incomplete account snapshot, or a manual hold closes new-risk dispatch again.
