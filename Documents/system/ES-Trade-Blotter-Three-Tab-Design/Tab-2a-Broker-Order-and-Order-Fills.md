# Tab 2a — Broker Order/Fills (two-tab revision)

## Purpose

The Trade Blotter has two tabs: **Market Selection** and **Broker Order/Fills**. This second tab combines the former Leg Staging and Orders and Fills designs. It lets a manual trader review and validate the proposed composition, submit an opening or closing order, and then monitor broker-confirmed execution without switching tabs. Automated and historical trades show the same evidence in read-only form.

The upper pane is the **Broker Order** workspace (staged legs, provisional price/risk, validation, and submission). The lower pane is **Orders and Fills** (durable broker orders, executions, fills, and permitted follow-up actions). A resizable horizontal divider separates them. Each pane scrolls independently; resizing the form or changing display scale must not hide its controls or the fourth iron-condor leg.

## Layout

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [ Market Selection ] [ Broker Order/Fills ]                  Strategy: Iron Condor                  │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ BROKER ORDER   ESZ26 @ 5,420.50   EXP 16 Oct 26 (28 DTE)   IV 16.4% (Rank 32)                       │
│ Staged legs 4 / 4   Action │ Contract      │ Type │ Strike │ Expiry │ Role │ Bid │ Ask │ Mid │ Age │ Qty │
│                     BUY   │ ESZ26 P5250   │ PUT  │ 5250   │ ...    │ +LP  │ ... │ ... │ ... │ ... │ 1   │
│                     SELL  │ ESZ26 P5300   │ PUT  │ 5300   │ ...    │ -SP  │ ... │ ... │ ... │ ... │ 1   │
│                     SELL  │ ESZ26 C5550   │ CALL │ 5550   │ ...    │ -SC  │ ... │ ... │ ... │ ... │ 1   │
│                     BUY   │ ESZ26 C5600   │ CALL │ 5600   │ ...    │ +LC  │ ... │ ... │ ... │ ... │ 1   │
│ Combo units [1 ↑↓]  Order type [Limit ▼]  TIF [Day ▼]  Intent [Opening ▼]                              │
│ Net bid 16.00 [Bid]   Mid 16.25 [Mid]   Net ask 16.50 [Ask]   Net limit [−] [16.50] [+] Tick 0.05   │
│ Algorithm [None ▼]  Pace [—]  Route [qualified venue] / Atomic combo / No SMART   Worst net limit ...│
│ FUND ECONOMICS: Available funds ...  Reserved capital ...  Risk margin ...  Max profit / loss ...   │
│ Max return ...  Fund risk limit ...  Min profit target ...  Credit 16.50   Δ / Γ / Vega / Theta      │
│ Put spread: trade value / OTM probability / limits   Call spread: same   Risk APPROVED (rev 47)     │
│ Profile/version —   Updates 0   Next decision —   Deadline —   Broker state: Not submitted          │
│ [ Clear staged legs ] [ Recalculate / validate ]                         [ Submit opening order ]  │
├══════════════════════ resizable divider: upper/lower panes scroll independently ════════════════════┤
│ ORDERS AND FILLS  Value date [16 Oct 26 ▼]  [✓] Working only  [ ] Today only  [Refresh / resync]  │
│ ┌─ ORDER TREE (all orders for value date) ───┬─ SELECTED ORDER / FILL DETAILS ─────────────────┐ │
│ │ ▼ ORD-042 Iron Condor 2/5  ● yellow        │ Portfolio P-01 / Fund F-04 / Order 16001       │ │
│ │   ├─ Fill 1: 1 whole combo 10:16:03        │ Trade T-02 / Broker BRK-88214 / Revision 47   │ │
│ │   └─ Fill 2: 1 whole combo 10:18:21        │ Requested 5 / Filled 2 / Remaining 3           │ │
│ │ ▼ ORD-041 Vertical 2/2  ● green            │ Limit 16.50 / Mid 16.25 / Avg fill 16.40       │ │
│ │   └─ Fill 1: 2 whole combos 09:45:10       │ Fees $12.50 / Posting pending / Leg evidence  │ │
│ │ ▸ ORD-040 Iron Condor 0/1  ● red           │ Proposed [−] [16.25] [+]  Tick 0.05           │ │
│ │                                            │ [Update Limit] [Cancel Unfilled]               │ │
│ └────────────────────────────────────────────┴───────────────────────────────────────────────────┘ │
│ [ End-of-day processing ] [ Permitted Trade / position transition ]                                │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The values are illustrative. The upper pane should have enough minimum height for the strategy's maximum number of rows and its validation/actions. At the form's minimum height, the lower pane may show fewer order rows, but it remains independently scrollable. A collapsed or empty lower pane still displays its heading, refresh action, and an explicit “No broker orders submitted” state.

## Broker Order: composition and submission

| Strategy | Minimum composition |
| --- | --- |
| Iron Condor | Four unique, compatible legs with correctly ordered wings and shorts |
| Vertical Spread | Two compatible legs |
| Futures Outright | One valid future |

Market Selection passes selected contract identities and roles into this pane. The staged-leg grid shows action, contract, option type, strike, expiry, role, bid/ask/mid, quote age, quantity, and ratio. Long and short rows retain their respective selection colors. The proposed order controls show contracts, signed net limit, order type (`Limit` or policy-permitted `Market`), time in force, opening/closing intent, and execution algorithm. Credit/debit, maximum profit/loss, and aggregate Greeks are provisional.

### Shared shell, strategy-specific composition

Use one shared Broker Order/Fills shell for account, Fund Order/Trade identity, order type, TIF, algorithm, venue qualification, validation, submission, and broker-confirmed orders/fills. Swap only the **composition and economics panel** when the selected strategy requires different information. Do not render four empty option rows or option-only Greeks on a futures outright.

| Strategy view | Composition | Relevant strategy economics |
| --- | --- | --- |
| Iron Condor | Four named/color-coded rows: long put, short put, short call, long call; common expiry, correct strike order and ratios. | Net credit/debit, put/call spread values and widths, OTM/PoP estimates when qualified, aggregate Greeks, max profit/loss, margin/capital, each spread's approved limits. |
| Vertical Spread | Two named/color-coded option rows; type, expiry, action and width appropriate to debit/credit variant. | Net debit/credit, spread width, break-even, max profit/loss, margin/capital, relevant Greeks and spread limit. No unused iron-condor half. |
| Futures Outright | One futures row with direction, contract, quote, size and multiplier. | Entry/mark price, tick value, notional exposure, initial/maintenance margin where available, stop/loss and Fund exposure limits. No option expiry/PoP or option Greeks. |

The common **Fund economics and limits** strip retains the original screen's useful values: authoritative available Fund balance/capacity, capital reserved for this order, required risk margin, estimated max profit, max loss, max return, minimum profit target, and approved Fund/strategy loss and profit limits. Label each figure `Authoritative`, `Estimated`, or `Unavailable` with as-of time/source. The original free-editable Fund Balance and Risk Margin fields must not become a way to override Portfolio/Risk Manager truth; any allowed margin/risk adjustment is a separate authorized request followed by revalidation. Missing authoritative values block submission when policy requires them.

For option strategies, retain four/two leg-level bid/ask and calculated mid, trade value, and relevant probability/limit evidence from the original iron-condor view, but use current evaluated contracts and approved risk inputs. The old BID/MID/ASK buttons were visual-only; in this design they are working **net combo-price anchors**. They set a proposed price, never immediately modify a broker order. The proposed net limit also has one-tick `−`/`+` buttons and optional direct entry. Tick increment comes from the qualified **combo net-price rule**, not a hard-coded 0.05 or a single option leg. Show signed credit/debit convention and the effect of each click. Quantize and validate against the approved price envelope before submission.

**Execution algorithm** defaults to `None`, meaning one standard qualified broker order with no IFM automated repricing. For an approved multi-leg strategy, `IFM Atomic Combo Microexecution` is an additional choice. It works the net Limit price of one directed exchange BAG order, never individual legs or a SmartRouted combo. When chosen, show **Execution pace** `Patient`, `Normal` (default), or `Urgent`; for `None`, hide/disable that control. Show the qualified destination and `Atomic combo / No SMART` as read-only facts. Display the approved worst net price, initial/current limit, selected profile/version, quote age, amendment count, next decision/deadline, and explanation when an action is blocked. This is an IFM policy—not IBKR Adaptive. See [IFM Execution Order Algorithms](../Trade-Broker-BrokerOrder-Execution-and-Accounting-Specification-v1.0.md#71-ifm-execution-order-algorithms--directed-atomic-combinations).

Before submission, preview all legs and ratios, signed net credit/debit, account, venue, order type, TIF, limit, algorithm/profile and approval revision. A Market order is not a normal opening choice under the V1.2 execution policy; it requires a separately permitted defensive escalation. TIF options, including GTC, require venue and policy qualification. A failed atomic-route qualification disables submission with a visible reason; it cannot silently switch to SMART or separate leg orders.

**Recalculate / validate** checks structure, contract compatibility, strike order, quantities, quote freshness, tick/sign and price conventions, Fund Order policy, broker qualification, and Risk Manager authorization. Show each result and the authoritative approval/revision/reservation identifiers. Missing quotes or failed checks remain visible and prevent submission; do not present provisional UI calculations as approval. Manual **Submit opening/closing order** is enabled only for a valid, authorized, revision-current composition. **Clear staged legs** clears the proposed composition, not any already-submitted broker order.

## Orders and Fills: execution evidence

The lower pane is a resizable left/right split. The left **order tree** lists every broker order for the chosen value date (not only the currently selected Trade Order), with broker-confirmed whole-combo fill groups as child branches. Selecting an order shows its full details and available actions on the right; selecting a fill shows that fill's time, quantity, net execution price, fees and underlying leg execution evidence, with order mutation actions hidden/disabled. An order with no fills remains visible. Show portfolio, fund, Fund Order, trade, order and broker/exchange identities; opening/closing intent; requested, filled and remaining quantities; order type, limit and midpoint; broker status and update time; average fill, commissions/fees, posting status, and cancel/replace lineage.

Each **order branch ends with a filled circular status indicator**, followed by a text status and fill count. Green means broker-confirmed `Filled` with complete reconciled quantity. Yellow means awaiting fills or remaining quantity: submitted/working/partially filled, including a pending update or cancel request until the broker confirms its outcome. Red means broker-confirmed `Cancelled` (and may also mark `Rejected`, always with its explicit text label). Do not infer green from a local submit receipt or parent status alone; incomplete/unbalanced leg evidence retains a yellow warning/reconciliation label. Use text and an accessible status name in addition to color, and retain the indicator when a branch is collapsed.

The current broker-order query is scoped to one Trade Order. Implementing this date-wide tree requires a paged value-date broker-order query/index and a bounded fill-group detail query. Preserve order selection across refreshes by stable broker-order/fill identity; a late fill or cancellation updates the node without collapsing the tree or changing selection.

States include `Staged`, `Sent`, `Working`, `PartiallyFilled`, `Filled`, `CancelPending`, `Cancelled`, `ReplacePending`, and `Rejected`. **Working only** and **Today only** filter the displayed evidence; **Refresh / resync** reloads it without changing the staged proposal. Pending stays pending until broker acknowledgement or rejection. API acceptance alone is not a fill or terminal confirmation.

For an atomic combo, show requested, filled and remaining **whole combo units**, each broker leg's fill evidence, and whether the ratios reconcile. Also show the last IFM action/reason, acknowledged order revision, pending price update, and any unknown-outcome or reconciliation lock. The UI never drives the microexecution timer itself.

Selecting a working parent loads its current acknowledged net limit and remaining **whole combo units** into an order-action strip. The proposed limit has qualified one-tick `−`/`+` controls and direct entry; BID/MID/ASK anchors may be offered for the selected order if quotes are usable. **Update Limit** submits one revision-bound price amendment only after explicit confirmation and envelope validation. **Cancel Unfilled** submits one cancellation request for the remaining whole units. Both buttons are disabled for a terminal order, an in-flight mutation, unknown outcome, unresolved fill imbalance, stale approval, or an account/venue gate that forbids the action. Show `UpdatePending`/`CancelPending` until broker acknowledgement or rejection, not merely API acceptance. Partial fills constrain the remainder; no button creates independent leg orders. Quantity modification is excluded from the initial microexecution workflow unless separately approved and specified.

End-of-day and Trade/position transitions are offered only when the workflow permits them. This pane displays posting status but does not post ledger entries.

## State and authority boundaries

```text
Market Selection ──selected contracts/roles──> Broker Order proposal
                                              │
                                              ├─ validate ──> Order Composer + Risk Manager
                                              │                authoritative approval/revision
                                              └─ submit ────> broker order/execution workflow
                                                               │
                                                               ▼
                                           Orders and Fills: broker-confirmed events
                                           and durable execution/fill evidence
```

Selection changes invalidate the relevant proposal validation. Submitted orders and their evidence persist independently of later selection changes. Selecting an order in the lower pane must not silently replace or clear the upper proposal. Historical and automated trades are read-only where manual actions are not permitted.

## Scope of this design

This is the target two-tab design, not a claim that the current UI already implements every control. The current Leg Staging grid and text-based broker evidence need layout and workflow work to match this specification. The former standalone Leg Staging and Orders and Fills tabs are superseded by this combined **Broker Order/Fills** tab; Market Selection remains Tab 1.
