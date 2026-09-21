# Current-policy Portfolio/Fund lifecycle qualification

## Result

The isolated real API passed current-catalog Portfolio/Fund lifecycle acceptance and fresh-process recovery. Five selected tests passed across four runs: lifecycle (1), lifecycle restart (1), starter catalog/identity regression (2), and retained 256-Portfolio retry regression (1). Release builds completed with zero warnings/errors.

This closes the obsolete-fixture obstacle for the Portfolio/Fund state-transition protocol. It is not full strategy-engine, broker execution, general-ledger posting or UI acceptance.

## Fixture implementation

`IsolatedWorkflowCatalogFixture` rejects any connection outside the exact disposable PostgreSQL database pattern and NATS endpoint. It uses the corresponding run-specific ReferenceDb keyspace on port 29042.

The fixture:

1. Stores one explicitly synthetic ES futures instrument definition and publishes its product index using the real ReferenceDb stores and PostgreSQL identity generator. It does not download market data. An existing snapshot is retained rather than overwritten.
2. Inserts and publishes a versioned daily trade-selection profile and construction-constraints profile through the real ConfigurationDb APIs.
3. Creates separate qualification-only family, Future structure, strategy, long-future variant and deployment definitions. Publication goes through the live NATS Reference command actor with the production capability/dependency/product/hash checks enabled.
4. Uses an explicit long-future delta of 1, zero option wing widths and `UnderlyingEquivalent` units. It does not publish incomplete starter examples.
5. Pins the exact published deployment and profile identities in the Portfolio risk limits, schema-v3 Fund mandate and assignment. Directions and market conditions come from the actual selectable lookups.

The original 22 starter definitions remain unchanged, with their original hashes and Draft/unpublished status. The starter regression now checks those exact identities independently of additional, explicitly published qualification definitions.

## Lifecycle checks that passed

- Allocate fresh Portfolio/Fund/policy IDs through the real identity actor.
- Create Portfolio and policy, activate/assign policy, delegate Fund allocation and risk envelope, and activate Portfolio.
- Replay identical Portfolio/Fund creates; reject changed-payload replays.
- Create the schema-v3 Fund mandate, assign the published deployment with matching profiles, and activate Fund.
- Create a manual draft order with no trades and replay it idempotently.
- Resolve a current strategy snapshot through the real query actor.
- Reserve automated order/trade identities concurrently; both callers converge on the same reservation. A subsequent replay preserves those IDs.
- Record Composing, RiskPending and RiskApproved transitions using supplied synthetic completion evidence.
- Reload event authority and verify Fund revision 8 with the expected order.
- After stopping and starting a new API process, compare active Portfolio/Fund projections, workflow lookup, order/trade projections and rehydrated Fund authority.

Successful checkpoint: Portfolio `801`, Fund `201`, workflow `54fc6723-b5c1-42ca-8497-1dd803ece087`. The checkpoint is written only after successful lifecycle verification. New attempts allocate fresh identities; restart mode reads the last successful checkpoint.

## Initial fixture failure and correction

The first current-policy attempt passed instrument/profile/catalog publication but failed assigning the template because the old test used AssignmentVersion 1. Schema v3 requires the next Fund aggregate revision: 2 after mandate creation. The test was corrected to 2. No runtime rule was relaxed. The restart test's stale revision-7 assertion was corrected to 8 because the write test also creates a manual draft order. The failed attempt's synthetic data is retained for diagnosis.

## Evidence

Under `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/`:

- `Workflow-20260920022029.trx`: initial assignment-version fixture failure.
- `Workflow-20260920022144.trx`: current-policy lifecycle passed.
- `WorkflowRestart-20260920022222.trx`: fresh-process lifecycle recovery passed.
- `Basic-20260920022317.trx`: starter definitions and identity allocation passed.
- `LoadRestart-20260920022339.trx`: all prior 256 retries passed; each Portfolio remains at business revision 1.
- `workflow-checkpoint.json`: successful lifecycle identities.

Reproduce against prepared isolated stores:

```powershell
scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run -AcceptanceSuite Workflow
scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run -AcceptanceSuite WorkflowRestart
```

WorkflowRestart should follow Workflow promptly because the synthetic allocation/risk snapshot has intentionally bounded validity. Failure after expiry must not be interpreted as a storage recovery defect.

## Safety and remaining gates

No production runtime rules or normal application databases changed in this increment. The isolated event log still has exactly three indexes and `financial_legacy_event_fence`. Owned API children were stopped; fixtures and reports remain available.

The test supplies selection/composition/risk result IDs and hashes; it does not execute those engines, validate real pricing decisions, submit broker orders, record fills or post to the ledger. End-to-end engine/execution/accounting and UI checks, longer-duration workload qualification and explicit production cutover remain outstanding. The measured incremental schema benefit remains the separate paired benchmark's 3.35%; this lifecycle run makes no new performance claim.
