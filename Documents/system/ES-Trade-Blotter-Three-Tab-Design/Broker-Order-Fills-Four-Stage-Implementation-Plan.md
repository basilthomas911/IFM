# Broker Order/Fills — Four-Stage Implementation Plan

Status: proposed. This implements [Tab 2a](Tab-2a-Broker-Order-and-Order-Fills.md); it does not describe the current running UI.

## Design rules

The upper pane composes an order and shows economics; the lower pane shows a date-wide broker-order tree and selected order/fill details. Iron condors use whole, atomic four-leg combo units on a qualified directed route: no silent leg split or SMART fallback. API command acceptance is not a fill. Broker observations and reconciled evidence determine execution state. Emulator behavior is development-only.

## Stage 1 — Complete mockup with dummy data

Implement the WinForms tab with an explicit preview-only provider. Selecting Portfolio, Fund, Fund Order and iron-condor Trade populates four short/long-colored legs and all fields: contract, expiry, side/role, ratio, bid/ask/mid, quote age, quantity, combo units, Open/Close, Limit/Market, TIF, directed venue, algorithm None, conditional Patient/Normal/Urgent pace, net-price anchors, tick stepper, worst acceptable limit, Fund/spread economics, Greeks, capacity, limits and validation. Mark sample amounts as estimates, not real risk approval.

Use a resizable divider and independently scrolling panes. Populate a sample date-wide tree with working/partial, filled and cancelled orders, selectable fill children and right-side order/fill details. Green/yellow/red circles always have status text. Preserve selection/expansion on refresh. Mock buttons cannot dispatch.

**Exit gate:** screenshot/UI tests at normal/minimum size and supported DPI; four visible legs; selector/tree/detail behavior; all fields populated; zero broker dispatch.

## Stage 2 — Emulator and API foundation

Extend the development IBKR emulator as needed for qualified directed atomic-combo Limit and policy-permitted Market requests. Validate leg IDs/ratios, size, signed net price/tick, TIF, route and idempotency. Emit accepted, working, partial/full fill, reject, update and cancel observations. Each whole-combo fill has balanced four-leg quantities, net price, fees, time and revision.

In a configurable development profile, schedule eligible fills after seeded pseudo-random delays of 1–60 seconds. A Limit fills only if marketable under a documented combo-quote model; an unmarketable order may remain working beyond 60 seconds. Market uses a contemporaneous qualified price and policy gates. Inject clock/scheduler/seed for fast deterministic tests; persist state/schedule across restart without duplicate fills. Support controlled no-fill, partial-fill, quote-loss and rejection.

Reuse existing place/modify/cancel/observe/reconcile and broker-order command contracts. Add missing authorized, indexed, paged value-date order query, bounded fill details, observation refresh and unknown-outcome resync. Distinguish API acceptance, broker acknowledgement, fill and posting.

**Exit gate:** deterministic tests for timing boundaries, Limit/Market eligibility, balanced partial/full fills, no-fill, replay/restart, pagination, scope and authorization; command-to-observation-to-query integration test.

## Stage 3 — Wire UI to emulator execution

Replace samples with selected Portfolio/Fund/Trade proposals and emulator-backed orders; keep samples only behind an explicit preview switch, never mixed with persisted orders. Market Selection supplies four contract IDs/roles. Validate structure, price, route, account/Fund capacity and current Risk Manager approval before submit. Algorithm None sends one qualified order without auto-repricing.

Show working and timed fills from broker observations, not synthetic success. Load the date-wide tree through the new query plus observations or bounded refresh. Merge by stable order/fill ID, retain selection/expansion/last known values, and show requested/filled/remaining whole units, net average, leg evidence, fees, posting and errors. Reconcile after reconnect.

**Exit gate:** UI/API/emulator test from selected iron condor through submit, working, timed partial/full fills and durable reload; two orders on one date, stable selection, no-fill, restart, duplicate observations and no live-broker dispatch.

## Stage 4 — Update/cancel and functional economics/limits

Wire tick minus/plus, direct limit entry and qualified bid/mid/ask anchors to a proposed amendment. Validate combo tick/sign, price envelope, revision, remaining whole units, route and approval; confirm before request. Use existing update/cancel commands and show pending until broker acknowledgement. Cancel only unfilled whole units. Guard overlapping requests, partial-fill races, rejection, unknown outcomes, stale approvals, imbalances and terminal orders. Fill selection hides mutations.

Replace placeholders with sourced net premium times multiplier, bid/mid/ask, iron-condor widths and max profit/loss, break-even/return where defined, fees, qualified Greeks/probabilities, Fund capacity, reserved capital, margin and approved Fund/strategy limits. Label estimated/authoritative/unavailable with source and as-of time. Reapprove after material price, quantity, leg, market or account changes. UI fields cannot override Risk Manager or ledger truth; failed required checks block action with reasons.

**Exit gate:** tests for tick and credit/debit arithmetic, multiplier/fees, risk/capacity, stale revision, partial-fill update/cancel, acknowledgement races, rejected mutation, resync and UI enablement. Confirm amended limit or cancelled remainder in date-wide tree after durable reload.

## Delivery

Stage 1 is visual-only; Stage 2 provides backend capability; Stage 3 enables development-emulator placement and fills; Stage 4 completes amendment/cancellation and authoritative economics/limits. Ship each stage after its exit gate. Audit current emulator/API behavior at Stage 2 kickoff to avoid duplicate contracts.
