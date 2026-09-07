# ConfigurationDb strategy catalog in Reference Data Manager

Updated: 2026-09-06

Selecting **trade strategy families** opens `StrategyCatalogReferenceView`, backed by the PostgreSQL ConfigurationDb catalog through Reference command/query APIs. The old family editor is retained as Legacy and is no longer selected by Reference Data Manager. The UI has no direct database connection.

## Initial defaults

The initial Family list shows **Futures**, **Vertical Spreads** and **Iron Condor**, in that order. The Strategy section uses the same three names. These are persisted defaults, with separate supporting structures and variants. The default view excludes the earlier generic Directional/RegimeAligned entries and integration-test records. **Show all catalog data** exposes other definitions when needed. Saving a new custom definition switches to the full catalog so the new entry stays visible.

Startup seeds three families, three matching strategies, four structures and twelve variants (22 normalized definitions). Three visible families do not mean only three database rows. Existing historical records are preserved. New legacy imports link to the appropriate named strategy; existing immutable imports retain their original exact references.

## Editing

The **Manage** selector exposes seven related catalog sections:

| Section | Editable information |
| --- | --- |
| Family | Stable code, name, multiline description; logical grouping rather than an instrument class |
| Strategy | Exact family memberships, permitted structures, evaluator/data requirements and specialized settings |
| Structure | Expiry groups, leg instrument classes, rights, sides, ratios and builder/risk requirements |
| Variant | Exact parent structure; Long/Short/Custom side; Balanced/Bullish/Bearish/Custom bias; None/Credit/Debit/Custom premium; target net delta, balance tolerance, wing symmetry/widths, leg overrides and specialized settings |
| Parameter schema | Nested Object/Array/String/Decimal/Integer/Boolean fields, required fields, units, bounds, lengths and string choices |
| Parameter set | Exact schema version and typed values |
| Deployment | Exact strategy and permitted variants; Daily/Weekly/Monthly timeframe; products; pipeline profile ID/version/hash bindings; specialized parameter sets; legacy provenance |

**Start from** offers custom definitions and relevant starter definitions. Starters include long/short futures, four credit/debit call/put verticals, and six long/short balanced/bullish/bearish iron condors. These are editable draft examples, not qualified trading algorithms. Target delta and wing values are engineering examples to review, not calibrated strategy recommendations.

Products use a dropdown populated from the stored Futures and FuturesOption product catalog. Selecting a product fills its durable ID, exchange and currency; those fields are not independently editable. This catalog continues to use the Databento instrument-definition projection in Scylla. Strategy configuration moves to PostgreSQL; market-data product ownership stays unchanged.

Parameter schema paths start at `$`; object children use `$/properties/Name`, and array element schemas use `/items`. Every parent must exist. Nested values use slash-separated property/index paths. Names containing `/` or `~` use `~1` or `~0`. Object and array containers, booleans, strings and exact decimals are supported without entering a raw JSON document. Backend shape/capability validation remains authoritative.

## Buttons and lifecycle

Normal startup seeds only Futures, Vertical Spreads and Iron Condor and their supporting definitions. It does not import the legacy catalog automatically. Legacy import remains available through the API server's explicit `--migrate-strategy-catalog-only` maintenance mode. Integration tests use a separate configuration database so their fixtures do not refill the Reference list.

- **Add** opens a new definition; its button becomes **Save**.
- **Change** opens the next immutable version; its button becomes **Save**. The stable identity/code remains fixed.
- **Save** persists a Draft, reloads the stored version and returns to viewing mode.
- **Cancel** discards the current edit and becomes **Close**. **Close** dismisses the Reference dialog.
- A failed save retains the edited values. An unchanged retry retains its operation ID.
- **Publish** validates exact dependencies, parameters, products and registered capabilities.
- **Remove** asks a Yes/No question with a question-mark icon and retires a Published version. Historical data remains available. Draft removal is disabled because draft deletion is not part of the catalog lifecycle.

The editor follows [Dark Trading Theme](Dark-Trading-Theme.md): Microsoft Sans Serif 10 pt, black data controls, white enabled text, gray disabled text and blue selection backgrounds. Editable text boxes are bold; labels and fields use aligned rows; long descriptions are multiline. Tabs keep topology, variants and parameters out of the main metadata area.

## Portfolio and Fund use

Mandate permissions, assignment selection and policy limit rows now use exact ConfigurationDb Deployment GUID/version references. The UI retains old permissions as unavailable entries requiring an explicit replacement; a matching name does not grant a new permission. New policy deployment limits start disabled with zero amounts, and new Fund assignments start disabled. Assignment product/timeframe/profile fields come from the selected deployment.

Schema-v3 Fund assignments use the next Fund aggregate revision as their assignment version, under the existing expected-revision concurrency guard. This allows assignment creation after legacy history and across mandate versions. A disabled draft assignment does not block a later enabled assignment; overlapping enabled assignments for the same deployment within one mandate remain invalid.

Publishing a definition does not grant Fund permission or activate a workflow. The production capability registry currently has no TradeSelection strategy builders/evaluators registered: executable definitions remain Draft until qualified implementations are supplied. TradeSelection TS-01 through TS-08 remain on hold.

See [storage and integration](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [Legacy retirement](../../TomasAI.IFM.Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md).
