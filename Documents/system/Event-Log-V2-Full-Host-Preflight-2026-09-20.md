# Full-host acceptance preflight

## Verified outcome

The real API host built in Release with zero warnings/errors. All three ApiStartupVerificationProcessTests passed: Development composition, valid Production feed configuration, and rejection of Production synthetic persistence. Artifact: BenchmarkDotNet.Artifacts/event-log-v2/full-host-preflight-20260920/api-composition.trx.

These invoke --verify-startup-only and assert that schemas, actors, feeds and HTTP listeners do not start. They also verify that this flag takes precedence over the otherwise mutating reference-bootstrap switch. They are composition checks, not running-application acceptance.

## Concrete remaining prerequisites

- No appsettings.Test.json exists in the API server.
- Development settings use shared PostgreSQL port 5432, Scylla port 9042/keyspaces, Redis 6379 and DatabentoLive. Launching that configuration is not an isolated acceptance run.
- Startup registers EventSourceActorDbContext through its public constructor. Batched marker routing is accessible only through an internal guarded benchmark constructor, so setting the three-index schema alone does not activate the complete candidate.
- Existing live-host tests expect a running API host and default to the normal NATS broker unless overridden. They were not run against that endpoint.
- The current fixtures do not provide an isolated UI launch and all-store seed lifecycle.

## Required acceptance-host implementation

1. Add a dedicated fail-closed qualification host/profile. It must validate all data-store endpoints and namespaces before initialization, reject application databases and default broker endpoints, and prohibit live Databento/IBKR connections.
2. Add narrowly scoped qualification-only candidate registration in the composition root, preserving normal public/production defaults. Assert the selected writer and exact name-preserving three-index schema.
3. Provision disposable PostgreSQL event/sequence/config stores, Scylla keyspaces, Redis and NATS/JetStream with validated ownership. Seed only synthetic/reference data required by startup; never copy production financial data implicitly.
4. Run actual API startup, ready endpoints, actor reads/writes, financial workflows, controlled stop/restart and durable replay checks. Launch the UI only against the qualification API/broker and verify its readiness/read views.
5. Capture logs and durable reconciliation, then remove only owned fixtures. Keep rollback/activation as a separate explicit deployment decision.

This is additional acceptance infrastructure, not another existing benchmark flag. It has not been implemented in this preflight. No production service, schema or feed was started or changed.
