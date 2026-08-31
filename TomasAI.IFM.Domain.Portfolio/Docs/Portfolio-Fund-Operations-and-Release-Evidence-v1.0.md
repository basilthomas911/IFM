# Portfolio/Fund Operations and Release Evidence v1.0

## Boundary

PostgreSQL EventSourceDb is authoritative. ScyllaDB PortfolioDb is rebuildable. `FundLegacyDbContext` is historical read-only access. Portfolio code has no broker, OrderExecution, fill, live TradeDb, or live-position authority.

## Runtime registration

`PortfolioActorAssembly` is included in the API server actor scan. Public consumers use the typed Portfolio command/query contracts over NATS. The new Portfolio UI uses those clients; Funds and the manual Trade blotter remain legacy paths.

## Recovery

1. Stop Portfolio mutations if EventSourceDb append health is red.
2. Do not promote a projection checkpoint until all mutations for its source event succeed.
3. Resume durable replay from the last fenced checkpoint after PostgreSQL, NATS, and Scylla health recover.
4. For a controlled rebuild, empty only the isolated PortfolioDb projection tables, replay Portfolio and PortfolioFund streams, and compare the representative catalog hashes before reopening reads.
5. Never rebuild from legacy Fund tables and never delete/migrate legacy history as part of this procedure.

## Required telemetry

Traces carry Portfolio/Fund versions and IDs, integer OrderId/TradeId where applicable, workflow/command/correlation/causation IDs, hashes in redacted form, outcome, and stable reason code. Metrics use the bounded names in `PortfolioOperationalPolicy`; business IDs and hashes are forbidden metric labels.

Alert on sustained command failure/conflict rates, resolution ambiguity, projection lag/failure, reservation latency/replay spikes, and query latency/unavailability. Health is degraded if authoritative writes are unavailable or projector lag exceeds the recorded operating threshold; stale projections must be shown as pending/unavailable rather than guessed current.

## Authorization

- `PortfolioReader`: projection queries only.
- `PortfolioAdministrator`: Portfolio/Fund lifecycle, membership, allocation/envelope, and assignment administration.
- `StrategyWorkflow`: resolve snapshots, reserve compositions, and record composition/Risk references.
- No role in this bounded context grants OrderExecution authority.

## Release evidence status

PF-29 cross-pipeline qualification is complete. The production API host initializes the idempotent Portfolio and Reference schemas before actors/projectors start and executes Portfolio/Fund configuration, policy activation/assignment, deterministic snapshot resolution, concurrent idempotent integer reservation, composition, Risk-reference acceptance, and typed queries against real NATS, PostgreSQL, and ScyllaDB. The final Portfolio matrix is unit 93, BDD 22, integration 29, verification 28, UI system 17, and UI presentation 4 with zero failures/skips. A bounded 64-query/eight-worker production NATS run completed in 597.2 ms with 204.6 ms p95 and exact results.

This is not a release waiver. PF-30 still requires authenticated NATS identity enforcement, captured telemetry/health, sustained performance baselines, rollback/disable qualification, and operator release approval. PF-13 through PF-15 remain blocked on production TradeSelection, OrderComposition, and RiskManagement actors. The complete PF-29 traceability and commands are recorded in `Portfolio-Fund-PF29-Qualification-Evidence-v1.0.md`.

## Manual Portfolio UI review checkpoint

1. Start the repository NATS, PostgreSQL, and Scylla dependencies, then start `TomasAI.IFM.Application.Api.Server` so the Portfolio actors and projector have responders.
2. Start `TomasAI.IFM.UI.Net` and select the separate **Portfolio** menu. The existing **Funds** menu remains unchanged for legacy comparison.
3. Create a Draft Portfolio. Confirm the allocated integer Portfolio ID is visible and read-only.
4. Select the Portfolio, create a child Fund, then exercise new-version and state controls. Refresh after a pending-projection message before issuing the next independent edit.
5. Configure Allocation, Risk Envelope, and Trade Assignment. The UI reads Scylla projection aggregate revisions through typed NATS queries and uses those revisions for optimistic concurrency.
6. Open **Trade Orders**, select Portfolio and then Fund, and check origin/status/month filters, integer OrderId/TradeId lookup, refresh, selection, manual Draft creation, stale-response fencing, and the absence of execution controls.
7. Record layout, labels, field ordering, defaults, and interaction changes for PF-30 operator release approval. PF-17's obsolete Planned Compositions surface is superseded by the PF-28 Trade Orders journey.
