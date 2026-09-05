# Trade strategy family: typed catalog definition

Current implementation details, configuration, storage, compatibility and verification are in [Trade strategy product catalog and family creation](Trade-Strategy-Symbol-Catalog-Implementation.md).

## Contract

MessagePack keys 0–11 remain: TradeStrategyFamilyId (int), DefinitionVersion (long), SystemKey, Family, Strategy, TimeFrame, Symbol, Currency, Description, State, CreatedOnUtc (UTC), CreatedBy. Key 12 appends TradeStrategySymbolId (int); key 13 appends Exchange. Display Exchange before Description without renumbering the wire contract.

`TradeStrategyFamilyType`: Unknown=0, Futures=1, FuturesOption=2, Equity=3, EquityOptions=4, FixedIncome=5, FixedIncomeOptions=6.
`TradeStrategyType`: Unknown=0, Futures=1, IronCondor=2, VerticalSpread=3.

SystemKey is exactly `Family-Strategy`. It is classification, not unique identity. Multiple products/timeframes may share it. Every selected definition is identified by its exact TradeStrategyFamilyId/DefinitionVersion. New natural duplicates (Family, Strategy, TradeStrategySymbolId, TimeFrame) are rejected.

## Initial definitions and creation

The preserved legacy seeds remain Daily ES Futures-Futures, Weekly ES FuturesOption-VerticalSpread and Monthly ES FuturesOption-IronCondor, USD, Active, version 1. Migration preserves legacy identity/audit fields and leaves their product link unset; no Exchange is guessed. Additional product-linked definitions are created through the dedicated Reference command, with sequence-generated IDs and mandatory provider-derived Symbol, Currency and Exchange.

References → Trade Strategy Families supports creation, not editing/deleting existing definitions. Symbol choices come through IReferenceQueryApi → IMarketDataApi and Databento metadata. Timeframe choices are exactly Daily, Weekly and Monthly. New immutable definitions are Active at version 1; family/strategy combinations are Futures/Futures or FuturesOption/IronCondor and FuturesOption/VerticalSpread.

## Portfolio references

Fund and assignment selectors display product/timeframe plus exact ID/version, allowing identical SystemKeys. New editor saves use SchemaVersion 2: FundMandateReadModel key 21 PermittedTradeStrategyFamilies and assignment key 22 TradeStrategyFamily. Existing string fields are classification mirrors. Server commands validate active exact references and assignments enforce exact Fund membership. Typed mandates cannot downgrade to name-only permissions.

For legacy name-only records, a single active matching row may be resolved in the editor. Unknown, retired or ambiguous references stay visible as unavailable until explicitly replaced; they are never silently dropped or guessed. Legacy pre-v2 command compatibility remains for existing integrations, which should migrate to the typed contract. Catalog query failure blocks editing; there is no seed fallback in the UI.

## Storage and deployment

Normal startup additively creates trade_strategy_symbol_v1 and trade_strategy_family_catalog_v4. List queries combine immutable v3 seeds and v4 created definitions. The older v2→v3 bootstrap remains idempotent and preserves IDs/version/audit; legacy tables are not removed. A CAS catalog document atomically stores new definitions and retry receipts, preventing duplicate creation after an uncertain reply.

The earlier seven-key payload is incompatible with the typed layout. Deploy API/UI together, initialize schemas normally before reconnecting the UI, and requery cached catalog data. No application restart or live-table migration was performed as part of the source edit. See the implementation document for rollback cautions, bounded discovery configuration, test commands and live qualification limits.
