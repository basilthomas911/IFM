# Stage 4 offline host-intent foundation

This directory is an implemented **offline subset**, not enabled application behavior or full
Stage 4 acceptance. `Stage4SubscriptionOptions.Enabled` defaults to false and application startup
rejects true. No coordinator or durable authority adapter is registered. New typed `IMarketDataApi`
methods return `Disabled` rather than forwarding to legacy ownerless routes. Legacy signatures,
the Stage 3 non-Synthetic guard and existing feed ownership remain unchanged.

## Coordinator contract

- One coordinator owns one validated scope/dataset/value date and a new host epoch. It begins
  `Recovering`; explicit serialized availability admission is necessary before new acquisitions.
- Bounded, nonblocking enqueue, one mutation reader, immutable snapshots and no native/provider,
  network or database calls in the mutation loop. Timer callbacks coalesce and never create
  unbounded pending tasks. Queue-full controls return false, not an applied acknowledgment.
- Ephemeral Discovery/Composer leases use 120-second monotonic TTL, 30-second renewal advice,
  15-second expiry sweep, host-issued lease UUIDs and increasing renewal versions. UTC display
  expiry is informational. Queries and commands sweep before access; renewal checks TTL again
  at commit. Recovery/Closed blocks new ownership but permits valid renewal/release.
- Atomic desired-intent batches accept up to 128 distinct owner/target selections within the
  same authorized workflow scope. All limits/conflicts are checked before one revision commits.
  This is **not** a ready-price handoff, strategy validation or physical-source reconfiguration.
- Identical exact chain universes share intent; conflicting universes for the same underlying,
  date and maturity reject without mutation. Chain and individual option intent include explicit
  underlying references. Route counts are derived from distinct targets times owner multiplicity,
  avoiding millions of repeated references for one widely shared chain.
- Default limits are 10,000 leases, 128/workflow, 8 chains, 2,048 option routes, 256 futures routes,
  256 queued commands and 50,000 remembered in-window results. No accepted lease is evicted to
  accommodate new capacity. These are engineering bounds, not provider entitlement approval.
- A desired manifest always reports intent, not an acknowledged worker or fresh prices.
  Results currently have `RealizedRevision=0`; no `Active`/ready result is manufactured.

## Ephemeral command identities

Acquisition includes the current `HostEpochId`. Every mutating ephemeral request requires a
UUIDv7 `OperationId`, stable content/correlation and a UTC deadline no later than its embedded
issue time plus the configured command timeout (10 seconds by default, one millisecond precision
allowance). Clients must use their synchronized clock and retain the exact request for retries.
Future-issued/UUIDv4 identities are rejected. After a clock rewind the server's admission clock
continues monotonically: it fails closed rather than extending an old command's lifetime.

Within that immutable window, the exact operation returns its recorded outcome; different content
conflicts. After pruning, even an edited deadline cannot make an old UUIDv7 valid again. A new
operation after expiry must use a new ID. Deadlines/cancellation are checked again before commit;
cancellation after a completed mutation cannot undo that committed outcome. API restart rejects
old acquisition epochs and old lease tokens. This deliberately bounded **ephemeral** retry window
is separate from the planned 30-day durable operation-result retention in PostgreSQL.

## Not implemented by this coordinator

Durable mutations return `PersistenceUnavailable`: the separately tested store is not connected.
No application/principal authorization is inferred from caller-supplied scope or owner fields.
No contract catalog validation, maturity-profile rules, Greeks, pricing, actual provider subscription,
core-feed reference union, worker manifest acknowledgment, API restart hydration or UI delivery is
claimed. Exact-owner paged queries include `LegId`; they are not workflow-wide authorization queries.

The remaining implementation/decision gates are tracked in
`Documents/system/Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md` and
`Documents/system/Stage4-Durable-Pricing-Dependency-Decisions.md`.
