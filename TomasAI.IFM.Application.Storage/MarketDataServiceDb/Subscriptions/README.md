# Stage 4 standalone durable intent subset

This is disabled, unregistered G03 persistence engineering, not a completed Stage 4 gate.
`Stage4SubscriptionSchemaSql.Create` is additive and is applied explicitly by the dedicated
integration fixture only. Application startup does not apply it or create the store.

Startup repository discovery must not register the store's private nested `Repository`.
It requires the owning store's per-operation connection and is deliberately not a global
DI service. The shared public-repository discovery boundary excludes this helper; a
regression test exercises the actual type and the real API startup verifier.

The store transactionally maintains one bounded typed current-intent snapshot per scope/dataset,
operation outcomes, an ownership-audit outbox, independent per-source watermark rows, and immutable
lease-ID reservations/tombstones. It uses
the existing PostgreSQL `ObjectDataRepository` implementation, a fresh repository per operation,
a dataset row lock and optimistic revision checks. Two-/four-leg batches are all-or-nothing.
Only explicit add/release deltas or an exact-owner terminal fact remove ownership; empty active
observations and unknown authority retain existing leases. No prices or runtime resources are stored.

Supported source semantics are deliberately narrow: one authenticated source stream per owner,
contiguous logical source versions starting at one, and new lease IDs for reacquisition. Lease IDs
are reserved in the same transaction, retained after explicit release/terminal state and never
reused within a scope/dataset, even by another source. Source versions and exact release versions
also reject reordered facts and stale handles. This is not a raw broker EventId adapter. The eventual authority
adapter must establish ordering, identity and authorization; this internal store does not do so.
Terminal and unknown source records remain in the snapshot and watermark table, so late facts
cannot resurrect an earlier state. Capacity exhaustion is explicit; records are not silently evicted.

Operation retries must preserve the entire original request, including expected revision and
correlation ID. A matching retry returns the committed original outcome even after further state
changes. A revised request needs a new operation ID. Semantic conflicts are also recorded; a
transport failure may have committed and must be reconciled using `FindOperationAsync`.

The bounded outbox API permits a single dispatcher to read/retry/acknowledge pending transitions.
It does not implement multi-dispatcher claiming or exactly-once transport. No retention worker,
chain-intent or unfinished cross-source handoff persistence, source snapshot paging, tombstone pruning, coordinator registration,
production authority adapter or live recovery integration is included. Retention is intentionally
absent: records and watermarks are not deleted automatically, and storage monitoring/capacity
planning remains prerequisite to enablement. A bounded current snapshot is not a bound on audit
history accumulated on disk.

The shared repository provider has synchronous transaction open/commit calls; connection/command
timeouts must be 1-30 seconds. Statements also use a five-second lock timeout and ten-second
statement timeout. The eventual coordinator must not await this work while holding its mutation
gate. The store is not wired into that coordinator in this subset. Commit/rollback exceptions get
local best-effort disposal of the transaction/connection still owned by this call. A failure inside
the shared provider's `BeginTransaction` before it returns does not expose its partially opened
connection to the caller; that framework cleanup gap remains separate work. This subset does not
claim database-disconnect resource qualification.

The first real PostgreSQL run exposed an existing shared-provider distinction: command execution
honors `Repository.InTransaction()`, but query methods open separate connections. The initial
uncommitted insert was therefore invisible to its supposed locked read. The isolated store now
uses that repository's ambient `NpgsqlCommand` for its transactional current/operation/identity
reads, reusing `AdoNetDataRecord` mapping and disposing only commands/readers. Ordinary external
queries and all writes retain the existing repository boundary. No shared-provider behavior was
changed; this is a scoped correction, not an accepted wider Stage 4 architecture redesign.

`Stage4DurableIntentPostgresTests` first verifies the connection targets
`localhost:5432/event-source-test-db` (loopback aliases also accepted) and checks `current_database()`
before schema application. Each case owns a randomized `stage4-test-...` scope. Cleanup deletes
only those exact registered scopes from these five new tables; it never truncates or drops tables.
An internal transaction-write observer exists solely for deterministic rollback/cancellation tests.

See `Documents/system/Stage4-Durable-Pricing-Dependency-Decisions.md` for unresolved authority,
financial-convention, composition and rollout decisions. Passing these isolated PostgreSQL tests
does not establish production authority correctness or full G03 acceptance.
