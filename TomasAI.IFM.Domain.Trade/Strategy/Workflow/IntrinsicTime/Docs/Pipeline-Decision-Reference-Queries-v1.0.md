# Intrinsic Time Pipeline Decision Reference Queries v1.0

| Field | Value |
|---|---|
| Status | Implemented; PDR-01 through PDR-08 |
| Transport | Core NATS actor request/reply |
| Persistence | None |
| Initial operators | Regime Discovery and Market Condition |
| Coverage | Twelve stable representative pairwise anchors per operator |

## Purpose

Decision-reference queries expose a bounded, reviewable cross-section of each pipeline operator's decision language.
They are design, verification, and analysis aids. They are not a complete input enumeration, runtime configuration,
trade policy, or authoritative definition of every result an operator may produce.

Every row sets `CoverageKind = RepresentativePairwise`, `IsAuthoritative = false`, and
`IsCompleteEnumeration = false`. Stable generator and decision-schema versions make saved exports interpretable.

## Runtime boundary

The caller sends `GetRegimeDiscoveryDecisionReferenceQuery` to `RegimeDiscoveryPipelineQuery` or
`GetMarketConditionDecisionReferenceQuery` to `MarketConditionPipelineQuery`. The actor constructs synthetic,
deterministic inputs in memory, invokes the same production decision model used by workflow execution, and returns a
typed MessagePack DTO array through NATS request/reply.

The reference handlers do not read or write ScyllaDB, ConfigurationDb, PostgreSQL, a file, a workflow stream, or any
other external state. They publish no event, execute no command, and cannot advance a workflow. They also do not fan
out to live specialist actors or capture live market data.

Actual workflow decisions remain authoritative immutable execution results and continue to use their existing
projection and event-storage paths. Reference examples never replace or influence those results.

## Initial catalogs

Regime Discovery generates the existing twelve named Trend/Volatility/Market Structure pairwise anchors and passes
each specialist-result combination through `MarketRegimeFusionModel`. Market Condition generates the existing twelve
Daily/Weekly/Monthly market-language and hint anchors and passes each complete synthetic input through
`MarketConditionCalculationModel`.

The Market Condition output continues to obey the primary rule: inputs determine the market decision; hints describe
possible downstream use. The generated hints are advisory and may be accepted, rejected, reranked, or augmented by
Trade Selection.

When an operator gains another decision component or specialist, its reference generator may add stable dimensions
and representative cases. It must preserve meaningful named anchors, bound the result size, use stable ordering, and
exercise the production calculation model. It must not generate the full Cartesian product by default.

## NATS client API

`IIntrinsicTimePipelineDecisionReferenceQueryApi` provides:

```csharp
ValueTask<ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>>
    GetRegimeDiscoveryAsync(CancellationToken cancellationToken = default);

ValueTask<ServiceResult<MarketConditionDecisionReferenceDto[]>>
    GetMarketConditionAsync(CancellationToken cancellationToken = default);
```

`IntrinsicTimePipelineDecisionReferenceQueryApi` is the NATS implementation and routes each request to its owning
stage Query actor. There is no HTTP or direct in-process calculation shortcut in this API.

## CSV export

CSV conversion occurs only in the caller process. `Domain.Trade.Shared/DataExport` contains two typed services:

- `IRegimeDiscoveryDecisionReferenceCsvAdapter`
- `IMarketConditionDecisionReferenceCsvAdapter`

Each `ExportAsync` method accepts its typed result collection, a filename, `overwrite = true`, and a cancellation
token. The format uses a UTF-8 BOM, CRLF records, invariant-culture values, explicit stable columns, and RFC 4180
double-quote escaping. Repeated values such as tags, restrictions, evidence features, and reasons use `|` inside one
CSV field. A successful export atomically moves a completed sibling temporary file over the destination. With
`overwrite = false`, an existing target produces `IOException` and remains unchanged. The target directory must
already exist.

Example console usage:

```csharp
var queryApi = new IntrinsicTimePipelineDecisionReferenceQueryApi(actorProducer);
var regimeResponse = await queryApi.GetRegimeDiscoveryAsync(cancellationToken);
var conditionResponse = await queryApi.GetMarketConditionAsync(cancellationToken);

if (!regimeResponse.Success || !conditionResponse.Success)
    throw new InvalidOperationException("A decision-reference query failed.");

await new RegimeDiscoveryDecisionReferenceCsvAdapter().ExportAsync(
    regimeResponse.Value, "regime-discovery-reference.csv", cancellationToken: cancellationToken);
await new MarketConditionDecisionReferenceCsvAdapter().ExportAsync(
    conditionResponse.Value, "market-condition-reference.csv", cancellationToken: cancellationToken);
```

## PDR implementation record

| Gate | Result |
|---|---|
| PDR-01 | Append-only MessagePack queries and typed row DTOs implemented. |
| PDR-02 | Regime Discovery production-model-backed 12-anchor generator implemented. |
| PDR-03 | Market Condition production-model-backed 12-anchor generator implemented. |
| PDR-04 | Both Query actors and the NATS client facade implemented. |
| PDR-05 | Two typed shared CSV adapters and common deterministic writer implemented. |
| PDR-06 | Serialization, generator, actor-map, CSV, overwrite, culture, cancellation, and path tests implemented. |
| PDR-07 | BDD, live-NATS integration, and pairwise verification coverage implemented. |
| PDR-08 | Governing documentation and qualification commands synchronized. |

## Qualification requirements

Qualification must prove deterministic catalogs, exact twelve-anchor continuity, production-model execution, typed
MessagePack round trips, actual NATS request/reply to each Query actor, no storage dependency in the handlers,
Excel-compatible CSV output, overwrite/create-new behavior, cancellation cleanup, and no regression in the existing
Regime Discovery or Market Condition suites.

Final qualification on 2026-08-29 produced:

| Gate | Result |
|---|---|
| Focused unit/contract | 8 passed; 0 failed; 0 skipped |
| Trade unit suite | 337 passed; 0 failed; 0 skipped |
| Trade BDD suite | 23 passed; 0 failed; 0 skipped |
| Focused decision-combination verification | 26 passed; 0 failed; 0 skipped |
| Trade verification suite | 81 passed; 0 failed; 0 skipped |
| New live-NATS/CSV integration | 1 passed; both typed 12-row actor results exported to unique random files, parsed column-by-column, compared to their DTOs, and deleted in `finally` |
| Trade integration suite | 47 passed; 0 failed; 2 pre-existing TradePlan skips |
| Serialized full solution build | Passed; 0 warnings; 0 errors |
| Diff hygiene | `git diff --check` passed; only repository line-ending notices |

The initial parallel solution build reproduced the existing Databento CMake configuration race. The tracked input
was present; the required serialized `dotnet build TomasAI.IFM.sln --no-restore -m:1` rerun passed with no warnings or
errors. Repository-wide formatting verification continues to report pre-existing whitespace and mixed-line-ending
findings in untouched files; changed-code compilation, tests, and diff hygiene are clean.
