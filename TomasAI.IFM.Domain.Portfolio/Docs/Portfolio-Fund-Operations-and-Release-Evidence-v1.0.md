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

Typed Portfolio command/query envelopes carry the asserted principal and roles as additive MessagePack fields. The production actor maps its fixed verb to a bounded operation and revalidates the role before authority is mutated. The typed UI/operator client defaults to an administrator identity, strategy continuation methods default to `StrategyWorkflow`, queries default to `PortfolioReader`, and `PortfolioAccessScope` allows the authenticated application boundary to supply its actual principal. NATS account authentication and subject ACLs remain the transport trust boundary; untrusted publishers must not receive Portfolio command/query subject permission.

## Rollback and health controls

`Portfolio:Operations` exposes independently deployable `Enabled`, `QueriesEnabled`, `MutationsEnabled`, and `AuthorizationRequired` settings. `AuthorizationRequired` remains `true` in every supported deployment. Operators may set `Portfolio__Operations__MutationsEnabled=false` to stop all Portfolio authority changes while retaining read-only diagnosis; the actor returns error `34017` and `/health/ready` declares the effective mode. Full disable is also valid and keeps host readiness observable.

## Release evidence status

PF-30 operational qualification is complete. In addition to the PF-29 matrix, seven focused unit/BDD tests and two isolated real-storage integration tests capture authorization, telemetry, configuration, rollback and rebuild behavior. Production-host tests prove reader/admin/anonymous outcomes, clean restart, health, and rollback. A bounded 128-query/eight-worker Portfolio NATS run completed in 334.5 ms with 42.9 ms p95 and exact results; the 64-query Reference run remains green.

The release recommendation covers the Portfolio/Reference/Trade Orders pre-execution bounded context. PF-13 through PF-15 remain explicit downstream blockers for a complete automated TradeSelection, OrderComposition, and RiskManagement runtime release; no Portfolio release statement waives or simulates those actors. Detailed evidence is recorded in `Portfolio-Fund-PF30-Release-Qualification-v1.0.md`.

## Manual Portfolio UI review checkpoint

1. Start the repository NATS, PostgreSQL, and Scylla dependencies, then start `TomasAI.IFM.Application.Api.Server` so the Portfolio actors and projector have responders.
2. Start `TomasAI.IFM.UI.Net` and select the separate **Portfolio** menu. The existing **Funds** menu remains unchanged for legacy comparison.
3. Create a Draft Portfolio. Confirm the allocated integer Portfolio ID is visible and read-only.
4. Select the Portfolio, create a child Fund, then exercise new-version and state controls. Refresh after a pending-projection message before issuing the next independent edit.
5. Configure Allocation, Risk Envelope, and Trade Assignment. The UI reads Scylla projection aggregate revisions through typed NATS queries and uses those revisions for optimistic concurrency.
6. Open **Trade Orders**, select Portfolio and then Fund, and check origin/status/month filters, integer OrderId/TradeId lookup, refresh, selection, manual Draft creation, stale-response fencing, and the absence of execution controls.
7. Record layout, labels, field ordering, defaults, and interaction changes for PF-30 operator release approval. PF-17's obsolete Planned Compositions surface is superseded by the PF-28 Trade Orders journey.
