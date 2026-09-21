# ES Trade Blotter Integration with Trade Orders UI v2

> [!IMPORTANT]
> Superseded by [ES Trade Blotter, Contract Reference, and Option Pricing Specification v3](ES-Trade-Blotter-Contract-Reference-and-Option-Pricing-Specification-v3.md).

## 1. Status and purpose

This document supersedes `ES-Trade-Blotter-Option-Chain-and-Order-Management-UI.md`.

It defines how the ES option-chain and order-management blotter integrates into the existing WinForms **Trade Orders** form without replacing the form's Portfolio, Fund, Fund Order, Trade, history, or lifecycle workflow.

The implementation replaces only the strategy-specific control currently hosted by `pnlTradeControl`. As part of this work, that host is renamed to `pnlTradeBlotter`.

The resulting blotter supports two purposes:

1. An editable manual-order workspace for an eligible manually created Fund Order Trade.
2. A read-only decision and execution viewer for orders created by automated strategy, Risk Manager, and exit-position workflows.

The current domain and application policies remain authoritative. The UI displays capabilities derived from those policies; it must not recreate or weaken them.

## 2. Scope

### 2.1 In scope

- Rename the embedded host from `pnlTradeControl` to `pnlTradeBlotter`.
- Replace the controls created by the current `TradeBlotterFactory` with one unified blotter control.
- Provide three tabs using the existing IFM dark-tab convention.
- Place a strategy selector to the right of the tab headers on the same visual row.
- Support Iron Condor, Vertical Spread, and Futures Outright.
- Move manual order commands currently positioned to the right of the blotter into the appropriate tabs.
- Display automated entry and exit decisions in read-only mode.
- Preserve broker-authoritative parent-order, child-leg, fill, cancel, and replace state.
- Preserve current Fund Order and Trade lifecycle rules.

### 2.2 Out of scope

- Replacing the Trade Orders form.
- Changing Portfolio or Fund selection.
- Changing the Fund Orders or Trades lists.
- Changing the temporary two-trade Fund Order policy.
- Allowing manual editing of an automated Risk Manager decision.
- Sending orders directly from option-chain clicks.
- Moving pricing, risk authorization, or broker-state authority into WinForms.
- Treating Databento as an execution broker.

## 3. Existing Trade Orders form retained

The following remain part of `TradeOrderEditorForm` with their current responsibilities:

- Portfolio and Fund selectors;
- composition-source filter;
- current versus legacy-history mode;
- order-date range;
- Fund Orders list and create, load, delete, and close commands;
- Trades list and add/remove commands; and
- selection of the active Fund Order and Fund Order Trade.

The replacement boundary is:

```text
TradeOrderEditorForm
├── Portfolio and Fund context                    retained
├── Fund Orders list and lifecycle commands       retained
├── Trades list and lifecycle commands            retained
└── pnlTradeBlotter                               renamed host
    └── EsTradeBlotterControl                     new unified control
        ├── Market Selection
        ├── Leg Staging
        └── Orders and Fills
```

The blotter is created or rebound when the selected Trade changes. It does not create an independent Portfolio/Fund hierarchy.

## 4. Host-panel rename

The existing `pnlTradeControl` name is replaced consistently with `pnlTradeBlotter` in:

- `TradeOrderEditorForm.Designer.cs` field and initialization;
- `TradeOrderEditorForm.cs` layout, control-add/remove, sizing, and selection logic;
- UI and system tests;
- accessibility identifiers; and
- factory or helper parameters whose meaning is the blotter host.

This is a semantic rename. The panel remains the location in which the selected Trade's strategy and execution workspace is displayed.

## 5. Unified blotter layout

### 5.1 Header

```text
[ Market Selection ] [ Leg Staging ] [ Orders and Fills ]    Strategy: [ Iron Condor      v ]
```

The tabs occupy the left side. The strategy label and selector are right-aligned on the same vertical level as the tab headers.

Use the existing `DarkTabControl` convention: black chrome, command-surface selected tab, gray selected border, bold selected text, light-gray inactive text, and double-buffered painting.

A native WinForms `TabControl` does not safely host arbitrary controls inside its tab strip. Use a composite header: `DarkTabControl` and the strategy selector are siblings in one header layout but appear as one continuous strip.

### 5.2 Strategy selector

The selector is a WinForms `ComboBox` with `DropDownStyle = ComboBoxStyle.DropDownList` and exactly these display choices:

- Iron Condor
- Vertical Spread
- Futures Outright

| Display strategy | Existing concrete Trade types |
| --- | --- |
| Iron Condor | Short Iron Condor / Long Iron Condor |
| Vertical Spread | Put Credit, Put Debit, Call Credit, or Call Debit Spread |
| Futures Outright | Futures Outright |

For a new eligible manual Trade, the selector may be enabled until composition is committed. For an existing submitted Trade, an automated Trade, or historical data, it displays the authoritative strategy and is disabled.

Changing strategy clears only uncommitted staged legs after explicit confirmation. It must never reinterpret a submitted order or position.

## 6. Operating modes

The blotter receives an explicit mode rather than inferring authority from control state:

```csharp
public enum TradeBlotterMode
{
    ManualEditable,
    ManualSubmitted,
    AutomatedReadOnly,
    HistoricalReadOnly
}
```

### ManualEditable

- Market selection and leg staging are editable.
- Strategy may be selected while the Trade is pristine.
- Submit is available only after structural, quote, policy, account, and risk validation.
- Option-chain clicks stage legs and never submit.

### ManualSubmitted

- The submitted composition is immutable.
- Orders and Fills becomes the primary operational tab.
- Only broker-valid cancel/replace actions are enabled.
- End-of-day and permitted lifecycle actions appear in the relevant tab.

### AutomatedReadOnly

- Strategy comes from the automated workflow and the selector is disabled.
- All three tabs display persisted workflow evidence.
- Manual selection, staging, submission, cancel, replace, and state mutation controls are hidden or disabled unless a future policy explicitly delegates an operator action.
- Risk Manager and the execution workflow remain authoritative.

### HistoricalReadOnly

- All data is immutable.
- No commands are available.
- Live subscriptions are not required unless explicitly requested for comparison.

The mode is determined upstream from composition origin, current/history mode, Fund Order policy, Trade state, execution evidence, and workflow ownership. The form must not infer editability from the active tab.

## 7. Current Fund Order and Trade policy preserved

These rules are normative and remain enforced by `FundOrderTradingPolicy` and command handlers.

### 7.1 Two-trade temporary limit

- A Fund Order currently contains at most two Trades.
- The first Trade is the primary opening Trade.
- The second Trade is the non-primary closing Trade.
- This may change when active hedging is introduced, but this design does not change it.

### 7.2 Opening Trade

An empty, open Fund Order accepts one primary opening Trade. The new blotter becomes editable after that Trade is added and selected.

### 7.3 Closing Trade

A closing Trade may be added only after the primary opening Trade reaches `TradeToOpen`. It must be non-primary, use the compatible closing type, use the same base-contract symbol, and use the same reference.

| Opening type | Closing type |
| --- | --- |
| Short Iron Condor | Long Iron Condor |
| Long Iron Condor | Short Iron Condor |
| Put Credit Spread | Put Debit Spread |
| Put Debit Spread | Put Credit Spread |
| Call Credit Spread | Call Debit Spread |
| Call Debit Spread | Call Credit Spread |
| Futures Outright | Futures Outright |

For the second Trade, the selector displays the strategy family but is locked to the compatible closing implementation.

### 7.4 Removal and deletion

A Trade can be removed only when:

- its Fund Order is open;
- its Trade state is `NewTrade` or zero-fill `OrderCancelled`; and
- the Fund Order contains no closing or completed execution evidence.

`OrderSubmitted`, `OrderPartiallyFilled`, `OrderFilled`, `TradeToOpen`, and `OrderCompleted` are economic evidence and prevent removal.

A Fund Order can be deleted only when all its Trades are individually removable. Opening, MTM, EOD, fill, closing, or completion evidence prohibits deletion.

### 7.5 Closing the Fund Order

The Fund Order can be closed only after it contains exactly the opening and closing Trades, exactly one is primary, and the non-primary closing Trade has reached `OrderCompleted`.

### 7.6 Ledger boundary

- Drafting a Fund Order or Trade does not post to the general ledger.
- Completion with zero cumulative fills creates no ledger position.
- A completed execution with cumulative filled quantity greater than zero is eligible for ledger posting under the accounting workflow.
- The blotter displays order quantity, cumulative filled quantity, remaining quantity, and posting status; it does not post ledger entries.

## 8. Tab 1: Market Selection

Market Selection displays the current ES futures contract or ES futures-option chain and captures manual contract-selection intent.

Common data includes underlying and last price, net change, IV/rank, expiration and DTE, multiplier, tick size, settlement/exercise style, expected move, quote age, and liquidity quality.

Iron Condor uses four roles: long put, short put, short call, and long call. Vertical Spread uses two compatible option legs. Futures Outright uses one futures contract.

Selection aids include the underlying marker, expected-move bands, target-delta snapping, wing-width presets, role tags, and staged-leg count. Clicking bid or ask stages a leg; it never submits.

For automated and historical Trades, this tab shows the persisted market snapshot, chosen contracts, role tags, quote timestamps, selection parameters, and workflow correlation. It must not reconstruct the original decision from current quotes.

## 9. Tab 2: Leg Staging

Leg Staging is the manual composition/validation surface and automated decision-evidence surface.

| Strategy | Minimum valid structure |
| --- | --- |
| Iron Condor | Four unique and correctly ordered option legs |
| Vertical Spread | Two compatible option legs |
| Futures Outright | One valid futures contract |

Submission stays disabled until all roles, underlying/expiration compatibility, option and strike ordering, quantities/ratios, quote freshness, price tick/sign conventions, Fund Order policy, broker qualification, and Risk Manager authorization pass.

Display action, contract, option type, strike, expiration, role, bid/ask/midpoint, quote age, quantity, ratio, multiplier, net debit/credit, maximum profit/loss, aggregate Greeks, and risk decision evidence.

UI calculations are provisional. Order Composer and Risk Manager independently validate the candidate.

Commands moved into this tab:

- Clear staged legs
- Recalculate/validate
- Submit manual opening or closing order

The current outer `Submit Order` and order-action controls are removed after equivalent tab-owned commands are operational.

## 10. Tab 3: Orders and Fills

The parent row represents the complete strategy order; child rows represent execution legs.

Required data:

- IFM parent order ID;
- Portfolio, Fund, Fund Order, and Trade identity;
- strategy and opening/closing intent;
- requested order quantity;
- cumulative filled quantity;
- remaining quantity;
- order type, limit, and current midpoint;
- parent and child status;
- average fill prices;
- broker/exchange identifiers;
- timestamps, fees, and commissions;
- cancel/replace lineage; and
- accounting/posting status.

Display states include Staged, Sent, Working, PartiallyFilled, Filled, CancelPending, Cancelled, ReplacePending, and Rejected. Broker-confirmed state is authoritative; API acceptance is not terminal confirmation.

Commands moved into this tab:

- Cancel unfilled quantity
- Submit cancel/replace
- Refresh or resynchronize execution evidence
- End-of-day processing when permitted
- Permitted Trade/position lifecycle transitions

The current outer `End Of Day`, target Trade State, and equivalent execution controls are removed after these replacements exist. Partial fills constrain replace quantity, and repeat actions remain disabled while acknowledgement is pending.

## 11. Manual workflow

```text
Select Portfolio and Fund
        |
Create/select Fund Order using existing form
        |
Add/select eligible Fund Order Trade using existing form
        |
pnlTradeBlotter loads ManualEditable
        |
Select strategy/contracts -> stage legs -> validate
        |
Order Composer -> Risk Manager -> confirmation -> execution gateway
        |
pnlTradeBlotter becomes ManualSubmitted
        |
Monitor fills, cancel/replace, EOD, and lifecycle evidence
```

Existing create/add commands remain responsible for creating Fund Order and Trade identities. The blotter operates within those identities.

## 12. Automated entry and exit workflow

```text
Strategy workflow selects opportunity or exit
        |
Order Composer creates authoritative composition
        |
Risk Manager approves/rejects and reserves risk/capital
        |
Fund Order and Trade projections are created or updated
        |
Trade Orders form selects the resulting Trade
        |
pnlTradeBlotter loads AutomatedReadOnly
        |
All tabs display persisted decision and execution evidence
```

Automated entry displays the opening Trade and approved composition. Automated exit displays the closing Trade, original position reference, exit reason, selected closing legs, approved quantity, and execution results.

The view explains why the order exists without granting the UI authority to alter Risk Manager's decision.

## 13. Responsibility boundaries

| Component | Responsibility |
| --- | --- |
| Trade Orders form | Portfolio/Fund/Order/Trade selection and lifecycle commands |
| Unified blotter | Visualization, manual staging, operator intent, execution monitoring |
| Databento adapter | Definitions, symbology, quotes, trades, market events |
| Market projection | Read-optimized underlying and option-chain snapshots |
| Option pricer | IV, Greeks, theoretical values, expected move |
| Order Composer | Canonical parent/leg construction and structural validation |
| Risk Manager | Trading authorization and risk/capital approval |
| Execution gateway | Submit, cancel, replace, broker communication |
| Order/fill projection | Broker-confirmed parent, leg, fill, replacement state |
| Fund domain | Two-trade lifecycle, removal/deletion, close policy |
| Accounting workflow | Ledger posting after completed non-zero execution |

## 14. View and contract requirements

Consume immutable or versioned read-optimized snapshots rather than raw ticks. Recommended contracts:

- `TradeBlotterContextSnapshot`
- `EsUnderlyingSnapshot`
- `EsOptionChainSnapshot`
- `OptionChainRowSnapshot`
- `StagedStrategySnapshot`
- `RiskDecisionSnapshot`
- `ParentOrderExecutionSnapshot`
- `ChildLegExecutionSnapshot`
- `FillSnapshot`

Context explicitly carries Portfolio/Fund/Order/Trade IDs, composition origin, workflow/correlation IDs, mode, opening/closing intent, strategy family and concrete Trade type, policy capability flags, and revisions required to reject stale commands.

## 15. Performance and GC requirements

- Do not update WinForms controls on every tick.
- Maintain market and execution state outside the UI thread.
- Publish compact immutable/versioned snapshots.
- Coalesce updates and refresh visible data at a controlled initial cadence of 100-200 ms.
- Perform one UI-thread dispatch per batch.
- Reuse row/view objects where safe instead of rebuilding full object graphs.
- Enable double buffering.
- Use virtual mode or a custom grid for a large option chain.
- Repaint only changed visible rows.
- Stop subscriptions when the blotter is unbound or disposed.
- Do not parse symbols, join definitions, price options, or execute risk logic during paint events.

## 16. Operational safeguards

- Market Selection clicks only stage contracts.
- Submit remains disabled until authoritative validation succeeds.
- Quote age and stale state remain visible.
- Manual actions use idempotent command IDs.
- Cancel and replace are asynchronous transitions.
- Parent identity and replacement lineage are preserved.
- Partial fills constrain remaining modifiable quantity.
- Broker state is authoritative.
- Automated and historical compositions are read-only.
- Every submit, acknowledgement, rejection, fill, cancel, and replacement is auditable.
- Manual and automated orders use the same Order Composer, Risk Manager, execution gateway, and order/fill projection.

## 17. Migration plan

### Phase 1: Shell and integration

- Rename `pnlTradeControl` to `pnlTradeBlotter`.
- Add the unified shell, dark tabs, and right-aligned strategy selector.
- Bind the selected Trade and explicit mode.
- Retain existing controls behind a temporary adapter if required.

### Phase 2: Futures Outright vertical slice

- Implement futures selection and one-leg staging.
- Submit through the existing portfolio trade-order pipeline.
- Display parent order, fills, and broker evidence.

### Phase 3: Execution management

- Add cumulative filled and remaining quantities.
- Add broker-confirmed parent/child state.
- Add cancel/replace.
- Move EOD and permitted lifecycle actions into Orders and Fills.

### Phase 4: Vertical Spread

- Add option-chain projection, two-leg selection, spread pricing, validation, and risk display.

### Phase 5: Iron Condor

- Add four role-based selections, presets, expected-move bands, pricing, and Greeks.
- Replace the existing specialized Iron Condor control.

### Phase 6: Automated and historical views

- Bind persisted entry/exit decision evidence.
- Enforce automated and historical read-only modes.
- Retain legacy history through its current read-only route until migrated.

### Phase 7: Remove compatibility UI

- Remove old strategy-specific controls from `TradeBlotterFactory`.
- Remove the outer blotter command column only after tab-owned replacements exist.
- Remove obsolete layout/event code.
- Update UI/system tests and accessibility automation IDs.

## 18. Acceptance criteria

1. The existing form continues to select Portfolio, Fund, Fund Order, and Trade.
2. The embedded host is named `pnlTradeBlotter`.
3. One unified blotter supports all three strategy families.
4. Three dark tabs appear with the selector right-aligned on the same header level.
5. The selector is a `DropDownList` with exactly the approved choices.
6. Eligible manual Trades select, stage, validate, and submit without the old outer command column.
7. Automated entry and exit Trades use the same control in read-only mode.
8. Persisted evidence, not reconstructed current market state, explains automated choices.
9. Existing two-trade, compatibility, removal, deletion, closing, and ledger policies remain enforced.
10. Order quantity, cumulative filled quantity, remaining quantity, child-leg state, and fill history are visible.
11. Cancel/replace remains pending until broker acknowledgement.
12. UI market updates are batched and bounded rather than applied per tick.

## 19. Final outcome

The Trade Orders form remains the Portfolio/Fund/Order/Trade lifecycle workspace. Its embedded blotter becomes a unified strategy, composition, and execution surface. Manual operators use it to select and submit eligible orders; automated workflows use the same visual model as an immutable audit and monitoring view. Domain policy, Risk Manager, broker state, and accounting remain authoritative outside the UI.
