# ES Trade Blotter: Option Chain and Order-Management UI

> [!IMPORTANT]
> This document is superseded by [ES Trade Blotter Integration with Trade Orders UI v2](ES-Trade-Blotter-Integration-with-Trade-Orders-UI-v2.md). The replacement preserves the current Trade Orders form and trade-entry policy while replacing its embedded blotter workspace.

## 1. Purpose

This document defines a WinForms user-interface design for selecting and staging ES futures and ES futures-option contracts, then monitoring and modifying submitted orders.

The design assumes that portfolio-level and trade-level information already exists in the main trade blotter. This workspace therefore concentrates on:

- selecting an Iron Condor, vertical spread, or futures outright;
- consuming Databento market data to populate the contract-selection views;
- staging and validating the selected instruments;
- submitting the completed order to the execution pipeline; and
- displaying working orders, fills, cancellations, and price amendments.

> [!NOTE]
> Databento supplies market data, instrument definitions, and symbology. Order submission, modification, cancellation, and execution reports must continue to flow through the execution broker or order gateway.

## 2. Workspace Architecture

Place a global **Trade Strategy Selector** above the tab control. The initial choices are:

- Iron Condor
- Vertical Spread
- Futures Outright

The selector changes the filtering, contract-selection behavior, leg-count rules, validation, and staging layout used by all tabs.

```text
+---------------------------------------------------------------------------------------+
|  TRADE STRATEGY: [ Iron Condor                                                   | v] |
+---------------------------------------------------------------------------------------+
| [1. Market Selection] [2. Leg Staging (4)] [3. Orders and Fills]                    |
+---------------------------------------------------------------------------------------+
|                                                                                       |
|                            Active tab content                                          |
|                                                                                       |
+---------------------------------------------------------------------------------------+
```

### 2.1 End-to-End Interaction

```text
[Trade Strategy Selector]
            |
            v
[Tab 1: Market Selection] -- select contracts --> [Tab 2: Leg Staging]
   Databento market data                            validation and pricing
                                                            |
                                                         submit
                                                            v
                                                [Tab 3: Orders and Fills]
                                                   execution responses
```

The tab order follows the operational workflow:

1. Select the market instruments.
2. Validate and price the staged structure.
3. Submit and monitor the resulting order.

## 3. Tab 1 — Option Chain and Futures Selection

### 3.1 Header and Filters

The market-selection header should contain:

- underlying futures contract and last price;
- net change;
- implied volatility and IV rank or percentile;
- expiration selector;
- DTE;
- contract multiplier;
- settlement style;
- expected move;
- strategy preset, such as **16 Delta Condor**; and
- liquidity and quote-quality filters.

For an Iron Condor, expiration tabs should show the expiration date, DTE, and expected move. The active strategy determines how clicks are interpreted and which contracts are highlighted.

### 3.2 Option-Chain Columns

Use the conventional side-by-side option-chain layout:

| Call side | Centre | Put side |
| --- | --- | --- |
| Staged role | Strike | Bid |
| Volume |  | Ask |
| Open interest |  | Delta |
| Delta |  | Open interest |
| Bid |  | Volume |
| Ask |  | Staged role |

Delta is the primary selection field for the short strikes. Bid, ask, volume, and open interest provide the minimum liquidity context needed to evaluate a candidate.

### 3.3 Selection Aids

The chain should provide these visual anchors:

- **Underlying marker:** clearly identifies the current ES futures price within the strike ladder.
- **Expected-move bands:** mark the upper and lower one-standard-deviation boundaries.
- **Delta snapping:** finds the closest available strike to the configured target delta.
- **Wing-width preset:** selects the protective long strike at the configured distance from the short strike.
- **Role tags:** show the selected leg role directly in the chain.
- **Staged-leg counter:** updates the Tab 2 caption as legs are selected.

Recommended Iron Condor role tags:

| Code | Action | Leg |
| --- | --- | --- |
| +LP | Buy | Long put |
| −SP | Sell | Short put |
| −SC | Sell | Short call |
| +LC | Buy | Long call |

Clicking a bid or ask stages a contract; it must not submit an order.

### 3.4 Option-Chain Text Mockup

```text
========================================================================================================================
 UNDERLYING: ESZ26 (Dec 26 Futures) | LAST: 5,420.50 | CHG: +14.25 (+0.26%) | IV: 16.4% | EXPECTED MOVE: ±45.00
========================================================================================================================
 EXPIRATION: 16 OCT 26 (28 DTE) | MULTIPLIER: $50 | SETTLEMENT: AM | PRESET: 16 Delta Condor [ON]
========================================================================================================================

 [STAGED]   VOL     OI    DELTA    BID     ASK   | STRIKE |   BID     ASK    DELTA     OI     VOL   [STAGED]
-------------------------------------------------+--------+-------------------------------------------------
            1.2K   4.3K    0.06    6.50    7.00 |  5620  | 81.25   82.75    -0.94     112      45
 [+LC]      2.1K   6.1K    0.10   11.00   11.50 |  5600  | 69.00   70.50    -0.90     420      88
            3.4K   8.0K    0.13   15.25   15.75 |  5580  | 55.50   57.00    -0.87     810     190
 [−SC]      5.6K  12.4K    0.16   19.50   20.00 |  5550  | 41.00   42.50    -0.84    1.4K     510
-------------------------------------------------+--------+-------------------------------------------------
                         UPPER EXPECTED MOVE: 5,465.50
-------------------------------------------------+--------+-------------------------------------------------
            8.9K  14.5K    0.22   28.50   29.00 |  5500  | 21.25   22.25    -0.78    2.3K     880
           12.1K  19.0K    0.35   44.00   44.75 |  5450  |  8.50    9.25    -0.65    4.1K    1.1K
-------------------------------------------------+--------+-------------------------------------------------
                         LAST UNDERLYING PRICE: 5,420.50
-------------------------------------------------+--------+-------------------------------------------------
           15.0K  22.1K    0.55   65.25   66.00 |  5400  |  3.10    3.50    -0.45   14.2K    9.2K
            9.4K  11.2K    0.72   89.00   90.25 |  5350  |  1.15    1.40    -0.28   11.0K    7.5K
-------------------------------------------------+--------+-------------------------------------------------
                         LOWER EXPECTED MOVE: 5,375.50
-------------------------------------------------+--------+-------------------------------------------------
 [−SP]      4.2K   8.9K    0.84  124.50  126.00 |  5300  | 22.00   22.50    -0.16   19.5K   11.2K
 [+LP]      1.1K   4.0K    0.90  159.00  161.00 |  5250  | 14.25   14.75    -0.10   12.1K    6.4K
              510   2.3K    0.94  192.50  194.50 |  5200  |  8.00    8.50    -0.06    8.0K    3.1K
========================================================================================================================
```

## 4. Tab 2 — Leg Staging

The staging area is the smart validation and order-construction layer. It receives contract selections from Tab 1 and calculates the combined structure.

### 4.1 Strategy-Specific Validation

| Strategy | Minimum valid structure |
| --- | --- |
| Iron Condor | Four unique legs: long put, short put, short call, and long call |
| Vertical Spread | Two compatible option legs |
| Futures Outright | One valid futures contract |

For an Iron Condor, submission remains disabled until:

- all four role slots are populated;
- every option has the same underlying and compatible expiration;
- put and call types are assigned correctly;
- the strike ordering is valid;
- quantities and ratios are consistent;
- quotes are current enough for the configured policy;
- the proposed limit price is valid; and
- the risk pipeline approves the order.

### 4.2 Staging-Area Text Mockup

```text
========================================================================================================================
 STRATEGY: IRON CONDOR | UNDERLYING: ESZ26 @ 5420.50 | EXP: 16 OCT 26 (28 DTE) | IV: 16.4% (Rank 32)
========================================================================================================================

 STAGED LEGS (4 / 4)
------------------------------------------------------------------------------------------------------------------------
 Action | Type | Strike | Delta |  Bid   |  Ask   |  Mid   | Multiplier | Exercise | Role
------------------------------------------------------------------------------------------------------------------------
 BUY    | PUT  | 5250   | -0.10 | 14.25  | 14.75  | 14.50  | $50        | American | +LP
 SELL   | PUT  | 5300   | -0.16 | 22.00  | 22.50  | 22.25  | $50        | American | −SP
 SELL   | CALL | 5550   |  0.16 | 19.50  | 20.00  | 19.75  | $50        | American | −SC
 BUY    | CALL | 5600   |  0.10 | 11.00  | 11.50  | 11.25  | $50        | American | +LC
------------------------------------------------------------------------------------------------------------------------

 STRATEGY AGGREGATES
========================================================================================================================
 Contracts:       [ 1 ]                         | Max Profit: $825.00
 Net Credit:      16.50 points                  | Max Risk:   $1,675.00
 Limit Price:     [ 16.50 ] (Auto-Mid)          | Est. PoP:   72.4%
------------------------------------------------------------------------------------------------------------------------
 Net Delta:       -0.02                         | Net Vega:   -32.40
 Net Gamma:       -0.004                        | Net Theta:  +45.10/day
========================================================================================================================

 [CLEAR ALL LEGS]                                             [SEND AS MULTI-LEG ORDER]
```

The displayed values are illustrative. Production calculations must use the system's authoritative pricing, risk, and margin components.

### 4.3 Staged-Leg View Model

```csharp
public enum TradingAction
{
    Buy,
    Sell
}

public enum OptionType
{
    Call,
    Put
}

public sealed class StagedOptionLeg
{
    public TradingAction Action { get; init; }
    public OptionType Type { get; init; }
    public double Strike { get; init; }
    public double Delta { get; init; }
    public decimal Bid { get; init; }
    public decimal Ask { get; init; }
    public decimal Mid => (Bid + Ask) / 2m;
    public int Multiplier { get; init; } = 50;
    public required string RoleCode { get; init; }
}

private readonly BindingList<StagedOptionLeg> _stagedLegs = new();

private void InitializeStagingArea()
{
    dataGridViewStagedLegs.DataSource = _stagedLegs;
    _stagedLegs.ListChanged += (_, _) => RecalculateStrategyMetrics();
}

private void RecalculateStrategyMetrics()
{
    if (_stagedLegs.Count != 4)
        return;

    decimal totalCredit = 0m;

    foreach (StagedOptionLeg leg in _stagedLegs)
    {
        decimal sign = leg.Action == TradingAction.Sell ? 1m : -1m;
        totalCredit += leg.Mid * sign;
    }

    lblNetCredit.Text = $"${totalCredit:N2} Credit";
}
```

This UI calculation is only a display projection. The authoritative Order Composer and Risk Manager should independently validate the order before submission.

## 5. Tab 3 — Orders and Fills

Use a parent-child presentation:

- the parent row represents the complete combo or outright order;
- child rows show the individual legs and their execution state;
- expanding or collapsing the parent controls leg visibility; and
- selecting a working parent displays its modification controls.

### 5.1 Recommended Columns

- parent order ID or contract;
- strategy;
- direction or credit/debit side;
- quantity;
- order type;
- limit and current midpoint;
- order status;
- filled quantity;
- average fill price;
- creation/update time;
- broker/exchange identifiers;
- fees and commissions; and
- available actions.

### 5.2 Order and Fill Log Mockup

```text
========================================================================================================================
 ORDER AND FILL LOG — LIVE WORKING ORDERS AND HISTORICAL FILLS
========================================================================================================================
 [X] Working Only | [ ] Today Only | Selected Parent: ORD-20261016-042
------------------------------------------------------------------------------------------------------------------------
 Order / Contract          | Side | Qty | Type | Limit / Mid  | Status  | Fill            | Time     | Actions
------------------------------------------------------------------------------------------------------------------------
 ▼ ORD-20261016-042 CONDOR | CRDT | 5   | LMT  | 16.50/16.25  | WORKING | 0/5             | 10:14:22 | EDIT CANCEL
   ├─ ESZ26 P5250 Long     | BUY  | 5   |      |              | WORKING | 0               | 10:14:22 |
   ├─ ESZ26 P5300 Short    | SELL | 5   |      |              | WORKING | 0               | 10:14:22 |
   ├─ ESZ26 C5550 Short    | SELL | 5   |      |              | WORKING | 0               | 10:14:22 |
   └─ ESZ26 C5600 Long     | BUY  | 5   |      |              | WORKING | 0               | 10:14:22 |
------------------------------------------------------------------------------------------------------------------------
 ▲ ORD-20261016-011 VERT   | DBIT | 2   | LMT  | 4.25         | FILLED  | 2/2 @ 4.20      | 09:45:10 | ARCHIVE
   ├─ ESZ26 C5450 Short    | SELL | 2   |      |              | FILLED  | 2 @ 8.80        | 09:45:10 |
   └─ ESZ26 C5400 Long     | BUY  | 2   |      |              | FILLED  | 2 @ 13.00       | 09:45:10 |
------------------------------------------------------------------------------------------------------------------------

 SELECTED ORDER MODIFICATION — ORD-20261016-042
------------------------------------------------------------------------------------------------------------------------
 Current Limit: 16.50 | New Limit: [16.25] | Current Mid: 16.25
 Current Qty:   5     | New Qty:   [5]

 [CANCEL UNFILLED QUANTITY]                              [SUBMIT CANCEL/REPLACE]
========================================================================================================================
```

### 5.3 Order State Model

```csharp
public enum OrderStatus
{
    Staged,
    Sent,
    Working,
    PartiallyFilled,
    Filled,
    CancelPending,
    Cancelled,
    ReplacePending,
    Rejected
}

public enum OrderType
{
    Limit,
    Market,
    Stop
}

public sealed class ParentOrder
{
    public required string ParentOrderId { get; init; }
    public required string StrategyType { get; init; }
    public required string Underlying { get; init; }
    public int Quantity { get; set; }
    public decimal LimitPrice { get; set; }
    public decimal CurrentMidPrice { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreationTime { get; init; }
    public List<ChildLegOrder> Legs { get; init; } = [];
}

public sealed class ChildLegOrder
{
    public required string ChildOrderId { get; init; }
    public required string Symbol { get; init; }
    public TradingAction Action { get; init; }
    public int Quantity { get; init; }
    public int FilledQuantity { get; set; }
    public decimal AverageFillPrice { get; set; }
    public OrderStatus Status { get; set; }
}
```

### 5.4 Selecting a Modifiable Order

```csharp
private ParentOrder? _selectedOrder;

private void Orders_SelectionChanged(object? sender, EventArgs e)
{
    if (dataGridViewOrders.CurrentRow?.DataBoundItem is not ParentOrder selected)
        return;

    _selectedOrder = selected;
    txtNewLimitPrice.Text = selected.LimitPrice.ToString("F2");
    txtNewQuantity.Text = selected.Quantity.ToString();

    bool modifiable =
        selected.Status is OrderStatus.Working or OrderStatus.PartiallyFilled;

    btnReplace.Enabled = modifiable;
    btnCancel.Enabled = modifiable;
}
```

### 5.5 Cancellation

```csharp
private async void Cancel_Click(object? sender, EventArgs e)
{
    if (_selectedOrder is null)
        return;

    DialogResult confirmation = MessageBox.Show(
        $"Cancel the unfilled quantity for {_selectedOrder.ParentOrderId}?",
        "Confirm cancellation",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning);

    if (confirmation != DialogResult.Yes)
        return;

    btnCancel.Enabled = false;

    await _executionGateway.RequestCancelAsync(
        _selectedOrder.ParentOrderId,
        CancellationToken.None);
}
```

A successful API call means the cancel request was accepted for processing; it does not mean the order is cancelled. Keep the order in **CancelPending** until the broker confirms the terminal state.

### 5.6 Price Amendment

```csharp
private async void Replace_Click(object? sender, EventArgs e)
{
    if (_selectedOrder is null)
        return;

    if (!decimal.TryParse(txtNewLimitPrice.Text, out decimal newLimit))
        return;

    if (!int.TryParse(txtNewQuantity.Text, out int newQuantity))
        return;

    btnReplace.Enabled = false;

    var request = new ReplaceOrderRequest
    {
        OriginalParentOrderId = _selectedOrder.ParentOrderId,
        NewLimitPrice = newLimit,
        NewTotalQuantity = newQuantity
    };

    await _executionGateway.RequestReplaceAsync(
        request,
        CancellationToken.None);
}
```

The UI should enter **ReplacePending** immediately, then wait for broker acknowledgement or rejection. Partial fills must be reconciled before allowing the replacement quantity.

## 6. Databento Integration

### 6.1 Instrument Definitions and Symbology

Maintain an internal, read-optimized mapping from Databento's numeric instrument identifier to the application's option-contract model. The model should include:

- raw symbol and display symbol;
- underlying futures contract;
- option type;
- strike;
- expiration;
- multiplier;
- exercise and settlement characteristics;
- exchange;
- tick size; and
- instrument lifecycle state.

The UI should not perform symbol parsing or definition joins for every repaint. Resolve definitions upstream and publish compact view snapshots.

### 6.2 Quote and Greek Projection

Databento quotes populate bid, ask, size, volume, and related market fields. The application's option-pricing service supplies calculated values such as:

- midpoint;
- implied volatility;
- delta, gamma, theta, and vega;
- expected move;
- strategy aggregate Greeks; and
- theoretical or model price.

### 6.3 Separation of Responsibilities

| Component | Responsibility |
| --- | --- |
| Databento adapter | Definitions, symbology, quotes, trades, and market events |
| Option-chain projection | Strike ladder and latest display snapshot |
| Option pricer | Implied volatility, Greeks, and theoretical values |
| UI | Selection, visualization, and operator intent |
| Order Composer | Validated multi-leg order construction |
| Risk Manager | Final trading authorization |
| Execution gateway | Submit, cancel, replace, and execution reports |
| Order/fill projection | Current broker-confirmed order state |

## 7. WinForms Performance Guidelines

- Do not update controls on every market-data tick.
- Maintain market state outside the UI thread.
- Publish immutable or versioned snapshots to the view.
- Refresh visible rows at a controlled cadence, such as 100–200 ms.
- Apply updates in batches through one UI-thread dispatch.
- Enable double buffering for the grids.
- Use virtual mode or a custom control if the chain becomes too large for a bound DataGridView.
- Repaint only changed and visible rows.
- Keep pricing, risk, symbology resolution, and order-state transitions outside the form classes.

Expected-move bands and role tags can be rendered with custom row or cell painting. Sort strikes from highest to lowest so out-of-the-money calls appear above the underlying and out-of-the-money puts below it.

## 8. Operational Safeguards

- A click in the market-selection tab stages a leg; it never directly routes an order.
- The submit button stays disabled until structure and risk validation succeed.
- Display quote age and mark stale market data explicitly.
- Treat cancel and replace as asynchronous state transitions.
- Disable repeat actions while cancel or replace acknowledgement is pending.
- Reconcile partial fills before calculating the remaining modifiable quantity.
- Show broker-confirmed state as authoritative.
- Preserve parent-child order identity through every replacement.
- Record every submit, replace, cancel, acknowledgement, rejection, and fill in the order log.
- Route manual orders through the same Order Composer, Risk Manager, and execution pipeline as algorithmic orders.

## 9. Implementation Outcome

The resulting workspace gives the operator one coherent workflow:

1. Choose the trade strategy.
2. Select ES futures or futures-option contracts from live market data.
3. Inspect and validate the staged structure.
4. Submit the structure through the normal risk-controlled pipeline.
5. Monitor parent and leg-level execution state.
6. Reprice or cancel the remaining order without bypassing broker acknowledgements or system risk controls.

This layout retains the familiar blotter workflow while replacing manual IBKR contract entry with an automatically populated Databento market-selection layer.
