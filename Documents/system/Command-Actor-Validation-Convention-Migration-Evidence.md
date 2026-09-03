# CommandActor Validation Migration Evidence

## Scope

This record covers gates V-00 through V-12 in
`Command-Actor-Validation-Convention-Migration-Plan.md`.

## Gate results

| Gate | Result | Evidence |
| --- | --- | --- |
| V-00 | Complete | Original inventory fixed at 37 domain CommandActors and 156 concrete command contracts; the current inventory contains 39 actors and 178 command contracts. |
| V-01 | Complete | `ParseMappedCommand` and `ValidateMappedCommand` base dispatch, template, aggregate validation adapter, and source conformance checks implemented. |
| V-02 | Complete | Application and Lookup Type maps migrated; identifier/read-model validators co-located. |
| V-03 | Complete | Fund Transaction migrated with conditional transaction identity rules and index-aware batch validation. |
| V-04 | Complete | Market Data core and Securities maps migrated; reference lookups separated from deterministic map validation. |
| V-05 | Complete | All Feed maps migrated; Futures Option reference validation separated from deterministic map validation. |
| V-06 | Complete | ADX, ATR, MACD, and RSI maps migrated. |
| V-07 | Complete | Remaining Analytics maps migrated; legacy throwing validators adapted to aggregate validation. |
| V-08 | Complete | Spread Distribution and job maps migrated. |
| V-09 | Complete | Option Trade and both Trade Plan maps migrated. |
| V-10 | Complete | Workflow, Regime Discovery, and parameter-set configuration maps migrated. Workflow receive dispatch is now explicit and parity-checked. |
| V-11 | Complete | Database Backup exposes an explicit 28-entry exact-type validation map. |
| V-12 | Qualified with recorded test-harness gaps | No allowlist remains; all 39 actors pass conformance; unit/BDD tests and the serial full-solution build are green where runnable. See gaps below. |

## Mechanical conformance

`scripts/Test-CommandActorConventions.ps1` verifies:

- exactly 39 domain CommandActors are discovered;
- read-only parse maps delegate to `ParseMappedCommand`;
- validation maps use exact concrete `Type` keys and return `List<ValidationError>`;
- validation dispatch delegates to `ValidateMappedCommand`;
- parse, validation, and receive command sets are equal, including Database Backup's generated parse/receive boundaries;
- every explicit validation entry visibly validates `CommandId` and `EntityId` (Database Backup uses its explicit common typed pattern);
- no domain-local audit tracker or direct audit-log write remains.

Result: **39/39 actors and 178/178 commands pass; no legacy allowlist.**

## Test evidence

### Unit

Ten affected unit projects passed **2,057/2,057** tests:

- Application 5;
- Fund 252;
- Market Data 102;
- Market Data Securities 11;
- Market Data Feed 489;
- Market Data Analytics 941;
- Option Pricer 59;
- Reference 8;
- System Administration 33;
- Trade 157.

### BDD

Affected BDD projects passed **860/860 discovered** scenarios. The Reference BDD assembly currently discovers no tests; this is a pre-existing coverage gap and is not counted as passing behavior evidence.

### Integration/integrated

The following completed successfully:

- Application: 1;
- Fund: 30;
- Market Data focused Economic Calendar command class: 4;
- Market Data Securities: 14;
- Option Pricer: 8;
- Reference: 14;
- System Administration: 3;
- Trade: 41 passed, 2 explicitly skipped.

Market Data Feed reports four explicitly skipped transport cases. Market Data Analytics did not complete and required termination after exceeding its configured hang timeout. The complete Market Data integration assembly passed 21 tests but returned a cleanup failure once; its focused Economic Calendar command class subsequently passed 4/4. These are test-harness/environment qualification gaps, not compile or conformance failures.

## Build evidence

`dotnet build TomasAI.IFM.sln --no-restore --verbosity minimal -m:1`

Result: **0 warnings, 0 errors**. Serial build is required because two solution projects otherwise invoke the same native Databento build concurrently and can contend on the native build-state file.

## Remaining qualification gaps

The V-12 exit condition is not represented as fully green until:

1. Reference BDD contains discoverable scenarios;
2. the four skipped Feed transport tests and two skipped Trade Plan tests are enabled or explicitly retired;
3. the Analytics integration test host completes deterministically; and
4. the Market Data integration fixture no longer has an intermittent class-cleanup failure; and
5. the remaining pre-existing domain-named primitive helpers in the global
   `ValidationErrorsExtension` are either generalized as truly universal scalar rules or moved to
   their owning domain validation folders.

The exact-type validation-map conversion, map parity, compilation, and all runnable unit/BDD
behavior are complete. The final validator-ownership cleanup and environment-backed qualification
remain open V-12 exit items.
