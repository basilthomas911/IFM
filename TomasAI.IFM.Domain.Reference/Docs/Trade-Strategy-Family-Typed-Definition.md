# Trade strategy family: typed catalog definition

> **Implemented catalog replacement (2026-09-06):** ConfigurationDb now owns active strategy catalog authoring. Reference Data Manager edits all seven catalog sections, including balanced/directional variants; Portfolio mandates, assignments and policy limits use exact deployment GUID/version references. Existing family records are imported as Drafts without automatic permissions. The old family UI/write path is Legacy; historical contracts remain readable. [Integration details](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md) supersede the older family-authoring descriptions below. TradeSelection execution remains on hold.

> **Strategy catalog direction (2026-09-06):** The current family record and Futures/FuturesOption symbol filter remain instrument/product/timeframe compatibility contracts. The future reusable strategy-family/definition/structure/variant catalog belongs to PostgreSQL ConfigurationDb, with exact deployment references and Portfolio-owned Fund permissions. Do not extend the closed TradeStrategyType enum for each new strategy or reinterpret existing integer family IDs. The current editor, tables and historical tests below describe existing behavior; no migration or new editor is implemented. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

Current implementation details, configuration, storage, compatibility and verification are in [Trade strategy product catalog and family creation](Trade-Strategy-Symbol-Catalog-Implementation.md).

## Contract

MessagePack keys 0–11 remain: TradeStrategyFamilyId (int), DefinitionVersion (long), SystemKey, Family, Strategy, TimeFrame, Symbol, Currency, Description, State, CreatedOnUtc (UTC), CreatedBy. Key 12 appends TradeStrategySymbolId (int); key 13 appends Exchange. Display Exchange before Description without renumbering the wire contract.

`TradeStrategyFamilyType`: Unknown=0, Futures=1, FuturesOption=2, Equity=3, EquityOptions=4, FixedIncome=5, FixedIncomeOptions=6.
`TradeStrategyType`: Unknown=0, Futures=1, IronCondor=2, VerticalSpread=3.

SystemKey is exactly `Family-Strategy`. It is classification, not unique identity. Multiple products/timeframes may share it. Every selected definition is identified by its exact TradeStrategyFamilyId/DefinitionVersion. New natural duplicates (Family, Strategy, TradeStrategySymbolId, TimeFrame) are rejected.

## Initial definitions and creation

The preserved legacy seeds remain Daily ES Futures-Futures, Weekly ES FuturesOption-VerticalSpread and Monthly ES FuturesOption-IronCondor, USD, Active, version 1. Migration preserves legacy identity/audit fields and leaves their product link unset; no Exchange is guessed. Additional product-linked definitions are created through the dedicated Reference command, with sequence-generated IDs and mandatory provider-derived Symbol, Currency and Exchange.

References > `trade strategy families` lists the latest active definition for each family ID. Repeated strategy names display `strategy-symbol-currency-timeframe exchange`; selecting an entry shows its details directly. Add and Change use the inline editor with Save/Cancel. Change preserves the ID and appends a version. Remove asks `Remove strategy-symbol-currency-timeframe exchange ?` with a question-mark icon, Yes/No buttons and No as default; confirmation appends a Retired version and removes the entry from current choices. Exact historical versions remain stored. Inputs use Microsoft Sans Serif 10pt, black backgrounds and white text. Currency/Exchange/SystemKey are derived; Description is enabled and multiline. Symbol selection uses the stored ReferenceDb instrument-definition catalog through the Market Data query API.

## Portfolio references

Fund and assignment selectors display product/timeframe plus exact ID/version, allowing identical SystemKeys. New editor saves use SchemaVersion 2: FundMandateReadModel key 21 PermittedTradeStrategyFamilies and assignment key 22 TradeStrategyFamily. Existing string fields are classification mirrors. Server commands validate active exact references and assignments enforce exact Fund membership. Typed mandates cannot downgrade to name-only permissions.

For legacy name-only records, a single active matching row may be resolved in the editor. Unknown, retired or ambiguous references stay visible as unavailable until explicitly replaced; they are never silently dropped or guessed. Legacy pre-v2 command compatibility remains for existing integrations, which should migrate to the typed contract. Catalog query failure blocks editing; there is no seed fallback in the UI.

## Storage and deployment

Normal startup additively creates trade_strategy_symbol_v1 and trade_strategy_family_catalog_v4. List queries combine immutable v3 seeds with every v4 definition version. Current choices select the highest version per ID before filtering for Active; original exact versions remain readable. The older v2→v3 bootstrap remains idempotent and preserves IDs/version/audit; legacy tables are not removed. A CAS catalog document atomically stores creation, version changes, retirement and retry receipts. Expected-version checks prevent lost updates; stable operation IDs prevent duplicate writes after uncertain replies. Bootstrap preserves the original seeds without reactivating retired IDs.

The earlier seven-key payload is incompatible with the typed layout. Deploy API/UI together, initialize schemas normally before reconnecting the UI, and requery cached catalog data. No application restart or live-table migration was performed as part of the source edit. See the implementation document for rollback cautions, bounded discovery configuration, test commands and live qualification limits.
