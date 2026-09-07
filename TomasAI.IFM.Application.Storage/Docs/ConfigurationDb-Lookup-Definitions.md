# ConfigurationDb lookup definitions

`reference_configuration.lookup_definition` is one PostgreSQL table owned by `ConfigurationDbContext`. It supplies named groups of predefined choices; it is independent of the strategy family catalog and the Scylla ReferenceDb lookup-type editor.

| Column | Definition |
|---|---|
| `id` | Integer, generated always as identity, primary key |
| `group_name` | Required varchar(64); group identifier |
| `internal_value` | Required varchar(64); canonical application value |
| `display_name` | Required nonempty varchar(100); operator-facing label |
| `description` | Required text, default empty |
| `display_order` | Nonnegative integer, default zero |
| `is_enabled` | Boolean, default true; available for new selections |
| `created_utc` | timestamptz, default now() |
| `updated_utc` | timestamptz, default now(); updated by trigger |

`(group_name, internal_value)` is unique. Identity, group, internal value and creation time are immutable. Display labels, descriptions, ordering and enabled flags can change without changing stored Fund values. No operator-entered identifier is exposed in the Fund editor.

Configuration schema creation seeds 12 rows with `ON CONFLICT DO NOTHING`, preserving configured labels and disabled flags on subsequent startup:

- `AssetTypes`: Futures, FuturesOption.
- `Directions`: Bullish, Bearish, Neutral.
- `MarketConditions`: Directional, RangeBound, Transition, VolatilityExpansion, VolatilityContraction, Dislocated, NoOpportunity.

The last two conditions are valid classifications, not an override of market tradeability. Undefined values are excluded. Unsupported application values cannot become selectable merely by inserting a lookup row.

`IReferenceQueryApi.GetLookupDefinitionsAsync(groupName)` uses the ReferenceQuery actor's `GetLookupDefinitions` verb and calls `ConfigurationDbContext.GetLookupDefinitionsAsync`. Both NATS and HTTP adapters are implemented; the HTTP endpoint is `POST /api/reference/lookup-definitions/query` with `{ "GroupName": "AssetTypes" }`. Responses include enabled and disabled rows so existing selections can be displayed accurately. Queries use a bound PostgreSQL parameter, order by display order/ID, and reject oversized groups rather than silently truncating them.

`FundSelectionCatalog` loads all three groups and the Futures/FuturesOption symbol catalogs concurrently. Underlyings are unique roots from the atomically published Databento definition product index in Scylla; they are not copied to this table. A failed product query cannot be mistaken for a complete list. Both the Fund editor and schema-v3 Fund create/change command validation use these sources. Historical replay remains independent of current lookup availability.

Development population was verified with 2 AssetTypes, 3 Directions and 7 MarketConditions. The stored Databento snapshot `62764663-2e6f-41e5-bbfe-0a87c40d18fe` contains 2,735 futures products and 1,023 futures-option products, yielding 2,735 unique underlying roots from GLBX.MDP3. This covers the configured downloaded dataset, not all Databento datasets globally.

Tests run against the separate `ifm-configuration-integration-tests` database. UI tests use mocked commands and add no Fund or strategy fixtures to Development. Rebuild/restart API and UI to use the new transport and control; the Development lookup table has already been populated.

Verification passed: 10 reference/selection/transport unit tests, 2 PostgreSQL integration tests and 13 UI tests. The API and UI compiled as dependencies of the UI suite. Actual WinForms renders of Create Fund and the checked popup were inspected; evidence and query output are under `.test-results/lookup-definitions/`. The integration tests verify group ordering/generated IDs and that repeated schema initialization preserves configured labels and disabled flags.
