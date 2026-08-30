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

The production API host initializes the idempotent Portfolio schema before actors and projectors start. Prior live qualification executed Portfolio/Fund create and configuration, deterministic snapshot resolution, concurrent idempotent integer identity reservation, composition, Risk-reference acceptance, typed queries, and a full stop/start recovery against real NATS, PostgreSQL, and ScyllaDB. The current non-host matrix is unit 58, BDD 17, integration 18, verification 22, full UI presentation 244, and full UI system 50 with zero skips; the Portfolio UI application graph builds with zero warnings/errors.

This is not a release waiver. Remaining evidence is listed gate-by-gate in the implementation ledger. PF-13 through PF-15 are blocked on production TradeSelection, OrderComposition, and RiskManagement actors; PF-16/17 require the real-host administrative UI journey and operator review; PF-20 requires authenticated NATS identity enforcement, captured telemetry/health, and bounded load results. PF-09 rebuild and failed-mutation evidence is complete.

## Manual Portfolio UI review checkpoint

1. Start the repository NATS, PostgreSQL, and Scylla dependencies, then start `TomasAI.IFM.Application.Api.Server` so the Portfolio actors and projector have responders.
2. Start `TomasAI.IFM.UI.Net` and select the separate **Portfolio** menu. The existing **Funds** menu remains unchanged for legacy comparison.
3. Create a Draft Portfolio. Confirm the allocated integer Portfolio ID is visible and read-only.
4. Select the Portfolio, create a child Fund, then exercise new-version and state controls. Refresh after a pending-projection message before issuing the next independent edit.
5. Configure Allocation, Risk Envelope, and Trade Assignment. The UI reads Scylla projection aggregate revisions through typed NATS queries and uses those revisions for optimistic concurrency.
6. Open **Planned Compositions** and check Portfolio/Fund selection, month filtering, integer OrderId/TradeId lookup, refresh, selection, and close behavior. This view is planned composition only and has no Fund creation or execution control.
7. Record layout, labels, field ordering, defaults, and interaction changes desired before PF-16/PF-17 are accepted as complete.
