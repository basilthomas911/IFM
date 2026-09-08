# Order Composition reference publication and qualification

Updated September 8, 2026. This record supersedes earlier statements that reviewed option reference data is unpublished. See the [closure audit](OrderComposition-Closure-Audit-v1.0.md) for current test completion.

## Published reference scope

All seven nonexpired September Tuesday/Thursday scopes covered by this reviewed profile were published and read back: **4,268 exact conventions**. The four-leg live canary uses the September 10 bundle; the table below also preserves the exact IDs of the other complete expiry bundles. Reference publication for an expiry does not claim a separate live-soak run for that expiry.

| Expiry | Root | Definitions | Native underlying | Bundle ID |
| --- | --- | ---: | --- | --- |
| 2026-09-08 | E2B | 728 | ESU6 / 42140870 | `228781f0d68fbe8f1d0f02fc4a2075657b0a76eb154367a5923a1b3e76e325ac` |
| 2026-09-10 | E2D | 726 | ESU6 / 42140870 | `f5f374a79761b05054db6e204fa53bd3a02f576f8fd44fa59d33bbe7c5e512f1` |
| 2026-09-15 | E3B | 696 | ESU6 / 42140870 | `189a8999bd8af37f244eb7156c618c85b6d7a505d5579839916faf02c35ddbdf` |
| 2026-09-17 | E3D | 686 | ESU6 / 42140870 | `53898e3590f5477f7f54e301a35e8c2535d26f5ebb816aec3e1fce3341754777` |
| 2026-09-22 | E4B | 492 | ESZ6 / 10252 | `9f9e40ae8135ddfa2629846eee074ce8de24ba78f4fd472caff10908eca262fd` |
| 2026-09-24 | E4D | 478 | ESZ6 / 10252 | `abc0661189ef73f9b1f46c8b0c63f8bf6484be9004e14ddfb910c1d40f156907` |
| 2026-09-29 | E5B | 462 | ESZ6 / 10252 | `70bafe9c94f786e67599c2e8d21b778c2b4ba406c20362913d255f76330de3bb` |

Publication evidence: `.test-results/ocp/reference-publication-v2.log` and `reference-publication-september.log`. Earlier expired September scopes were not reintroduced. Remaining profile/calendar coverage ends September 30 and must be renewed explicitly.

The canary bundle's detailed contract is:

| Field | Published value |
| --- | --- |
| ReferenceDb | API-configured `reference_test_db` on local Scylla |
| Exact convention table | `option_pricing_convention`, immutable `(contract_id, mapping_version)` records |
| Bundle table | `option_pricing_reference_bundle`, immutable content-addressed JSON document |
| Bundle ID | `f5f374a79761b05054db6e204fa53bd3a02f576f8fd44fa59d33bbe7c5e512f1` |
| Product profile | `CME-ES-TueThu-202609/v2` |
| Expiry scope | All 726 DataBento `E2D` call/put definitions expiring September 10, 2026 |
| Exact underlying | `ESU6`, publisher 1, instrument 42140870, canonical `ES20260918` |
| Dataset/currency/exchange | `GLBX.MDP3` / USD / XCME |
| Exercise and settlement | European; delivery into the specified ES future |
| Multiplier | USD 50 per index point, independently reviewed; absent native multiplier is not invented as a provider field |
| Last trading/expiry | September 10, 2026, 20:00 UTC / 16:00 Eastern, checked against each native definition |
| Premium increments | Current CME 358A four-band rule; `CME-358A01.C-2026-09-08/v1` |
| Day count | Explicit application ACT/365F convention; fractional UTC time |
| Exchange calendar | `CME-ES-202609/v2`, September coverage, excludes weekends and Labor Day's non-business trade date |
| Treasury | Official `USTreasury` observations, pinned nominal-semiannual conversion and `USTreasury-2026-18ET/v1` admission policy |

The source query returned 922 `E2D` definitions: 726 for September 10 and 196 for October 8. October is outside this reviewed September profile and was not silently treated as supported. Every September 10 definition was published. A requested strike interval is enumerated completely; empty or greater-than-512 scopes fail instead of selecting the first page. A complete expiry bundle can exceed the per-request chain bound.

The legacy `FuturesOptionContractId` parser now locates the call/put marker instead of assuming a four-digit strike suffix. This allows the actual 10000+ strikes in the published scope to retain the same canonical symbol/date/right/strike business key. Invalid rights, dates, nonpositive/fractional strikes and integer overflow are rejected.

Publication first validates all native records, writes exact immutable conventions, verifies each persisted mapping, and then publishes the sealed bundle. Repeating publication is idempotent. Different content cannot overwrite an existing exact convention version. Bundle reads validate identity, digest and the 4 MiB bound. `ReferenceDbContext.OptionPricingReferenceBundles` and the API DI registration expose the store; callers request an exact bundle ID. Publication does not alter Fund permissions or silently assign new construction policies to deployments.

The initially published `/v1` calendar counted September 7. Exchange review showed that Labor Day activity belongs to September 8's business trade date. `/v2` corrects that calendar; select the bundle above. The immutable `/v1` records remain historical evidence, not the current qualification input. Both calendars give the same remaining-day count for September 8-to-10, but their versions are deliberately distinct.

## Source evidence

- [CME Tuesday/Thursday ES option FAQ](https://www.cmegroup.com/articles/faqs/e-mini-s-p-500-tuesday-and-thursday-options-frequently-asked-questions.html): series roots, European exercise, underlying delivery, multiplier and expiry time.
- [Current Chapter 358A](https://www.cmegroup.com/rulebook/CME/IV/350/358A/358A.pdf): premium-dependent increments; it supersedes the older FAQ's tick summary.
- [CME trading calendar](https://www.cmegroup.com/trading-hours.html) and [2026 Labor Day settlement notice](https://www.cmegroup.com/tools-information/holiday-calendar/files/2026/labor-day-holiday-settlement-times-2026.pdf): holiday business-date treatment. This finite calendar is not an unrestricted all-year session schedule.
- The actual DataBento definition response is retained in `.test-results/ocp/closure/live-reference-definitions.json`. Each convention includes the full original record's semantic digest, exact provider identity and reviewed evidence URL.

## Reproduction

From the API project directory with its configured ReferenceDb and existing provider credentials:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile -- --publish-option-pricing-reference-only true --OptionReference:Root E2D --OptionReference:Expiry 2026-09-10
```

The `true` is required before the following configuration arguments. This maintenance mode initializes only the additive reference tables and publication; it does not start actors or subscriptions. Failures return a nonzero exit code and bounded redacted diagnostics.

From the repository root, after restore/build prerequisites and local PostgreSQL/Scylla are available:

```powershell
./scripts/Test-OrderCompositionPrerequisites.ps1 -Live -SustainedLoad -BundleId f5f374a79761b05054db6e204fa53bd3a02f576f8fd44fa59d33bbe7c5e512f1
```

The runner creates a distinct evidence directory and rejects empty test runs. PostgreSQL tests independently require the dedicated local `event-source-test-db` connection and isolate business facts by a random schema and authority scope. They do not submit orders to a broker.

## Qualification boundaries

The combined live case uses native DataBento in StrictProduction, a standalone supervised worker, official Treasury rates, real Black-76 calculations, immutable Scylla preparation readback, real committed PostgreSQL order/position facts, discovery-to-business handoff, process killing/replacement, stale-generation refusal and explicit position closure. Daily/Weekly/Monthly identify snapshot requests for the one triggering horizon; this is not a claim that the future composer has constructed strategies on each horizon.

The controlled maximum-scope case exercises the real managed chain consumer, last-price store, pricer and snapshot implementation with 512 options. It performs 100 runtime reconstructions before a requested elapsed load interval and records quote count, allocation rate, collection counts, GC pause time, heap/RSS and snapshot/reconstruction latency. Controlled transport and UTC observations are identified as fixtures. They are not relabelled native live quotes. The separate real PostgreSQL two-/four-leg cases cover replacement both within one value date and on the following value date.

Worker diagnostic MessagePack keys 31-40 append process allocation/heap/GC measurements and native option-feed produced/consumed/pending/overrun counters. Existing keys and the standard serializer remain unchanged. These are observations, not invented performance budgets. Compare the elapsed load rate with the measured live option-feed rate before claiming the prescribed two-times load criterion.

Independent process qualification also exercises 100 supervised recovery cycles (90 cooperative resets and ten actual child-process kills/replacements), plus 10,000 health reads during native processing. Those tests run in separate processes from the allocation-measured load case.

Full-session, OffTrading/Closed, natural overnight reconstruction, other native platforms, all deployment profiles and UI reconnect acceptance remain the broader Stage 4 rollout matrix. OCP-07 explicitly depends on design/runtime prerequisites, not completion of the later OCP-T25 production acceptance matrix. No shorter or accelerated test substitutes for that elapsed evidence.
