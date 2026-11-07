//! Concrete Blotter publications shared by native producers and cached renderers.
use super::{
    cells::{Cell, Cells, ScalarCell},
    trade_contracts::*,
    wire_types::*,
};
use serde::{Deserialize, Serialize};

/// Market Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct MarketDisplay {
    /// Optional underlying contract id; `None` represents an absent value.
    pub underlying_contract_id: Option<String>,
    /// Optional expiry date; `None` represents an absent value.
    pub expiry_date: Option<DateText>,
    /// Optional underlying price; `None` represents an absent value.
    pub underlying_price: Option<DecimalText>,
    /// Optional lower strike bound; `None` represents an absent value.
    pub lower_strike_bound: Option<DecimalText>,
    /// Optional upper strike bound; `None` represents an absent value.
    pub upper_strike_bound: Option<DecimalText>,
    /// Optional window method; `None` represents an absent value.
    pub window_method: Option<String>,
    /// Optional as of utc; `None` represents an absent value.
    pub as_of_utc: Option<OffsetText>,
    /// Optional bollinger standard deviation20; `None` represents an absent value.
    pub bollinger_standard_deviation20: Option<DecimalText>,
    /// Optional previous session close; `None` represents an absent value.
    pub previous_session_close: Option<DecimalText>,
    /// Optional iv rank; `None` represents an absent value.
    pub iv_rank: Option<DecimalText>,
    /// Optional iv rank status; `None` represents an absent value.
    pub iv_rank_status: Option<String>,
    /// Optional bollinger as of utc; `None` represents an absent value.
    pub bollinger_as_of_utc: Option<OffsetText>,
    /// Optional last underlying price; `None` represents an absent value.
    pub last_underlying_price: Option<DecimalText>,
    /// Optional last underlying trade at utc; `None` represents an absent value.
    pub last_underlying_trade_at_utc: Option<OffsetText>,
    /// Optional contracts; `None` represents an absent value.
    pub contracts: Option<Vec<EvaluatedOptionContractReadModel>>,
}
impl Cells for MarketDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "underlyingContractId" => self.underlying_contract_id.scalar_cell(),
            "expiryDate" => self.expiry_date.scalar_cell(),
            "underlyingPrice" => self.underlying_price.scalar_cell(),
            "lowerStrikeBound" => self.lower_strike_bound.scalar_cell(),
            "upperStrikeBound" => self.upper_strike_bound.scalar_cell(),
            "windowMethod" => self.window_method.scalar_cell(),
            "asOfUtc" => self.as_of_utc.scalar_cell(),
            "bollingerStandardDeviation20" => self.bollinger_standard_deviation20.scalar_cell(),
            "previousSessionClose" => self.previous_session_close.scalar_cell(),
            "ivRank" => self.iv_rank.scalar_cell(),
            "ivRankStatus" => self.iv_rank_status.scalar_cell(),
            "bollingerAsOfUtc" => self.bollinger_as_of_utc.scalar_cell(),
            "lastUnderlyingPrice" => self.last_underlying_price.scalar_cell(),
            "lastUnderlyingTradeAtUtc" => self.last_underlying_trade_at_utc.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Leg Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct LegDisplay {
    /// Optional contract id; `None` represents an absent value.
    pub contract_id: Option<String>,
    /// Optional role; `None` represents an absent value.
    pub role: Option<String>,
    /// Optional strike; `None` represents an absent value.
    pub strike: Option<DecimalText>,
    /// Optional bid; `None` represents an absent value.
    pub bid: Option<DecimalText>,
    /// Optional ask; `None` represents an absent value.
    pub ask: Option<DecimalText>,
    /// Optional delta; `None` represents an absent value.
    pub delta: Option<f64>,
    /// Optional is call; `None` represents an absent value.
    pub is_call: Option<bool>,
    /// Optional vega; `None` represents an absent value.
    pub vega: Option<f64>,
    /// Optional multiplier; `None` represents an absent value.
    pub multiplier: Option<DecimalText>,
    /// Optional quote at utc; `None` represents an absent value.
    pub quote_at_utc: Option<OffsetText>,
    /// The displayed bid and ask are last-known values.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub stale: Option<bool>,
    /// Optional frozen; `None` represents an absent value.
    pub frozen: Option<bool>,
    /// Optional is future; `None` represents an absent value.
    pub is_future: Option<bool>,
    /// Optional sign; `None` represents an absent value.
    pub sign: Option<i32>,
}
impl Cells for LegDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "contractId" => self.contract_id.scalar_cell(),
            "role" => self.role.scalar_cell(),
            "strike" => self.strike.scalar_cell(),
            "bid" => self.bid.scalar_cell(),
            "ask" => self.ask.scalar_cell(),
            "delta" => self.delta.scalar_cell(),
            "isCall" => self.is_call.scalar_cell(),
            "vega" => self.vega.scalar_cell(),
            "multiplier" => self.multiplier.scalar_cell(),
            "quoteAtUtc" => self.quote_at_utc.scalar_cell(),
            "frozen" => self.frozen.scalar_cell(),
            "isFuture" => self.is_future.scalar_cell(),
            "sign" => self.sign.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Preview Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct PreviewDisplay {
    /// Keyed fields retained by PreviewDisplay.
    pub fields: std::collections::BTreeMap<String, String>,
    /// Optional order price; `None` represents an absent value.
    pub order_price: Option<DecimalText>,
}
impl Cells for PreviewDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "orderPrice" => self.order_price.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Qualification Report used by the broker presentation component.
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct QualificationReport {
    /// Path associated with this qualification report.
    pub path: String,
    /// Hash associated with this qualification report.
    pub hash: String,
    /// Preview associated with this qualification report.
    pub preview: String,
}
impl Cells for QualificationReport {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "path" => self.path.scalar_cell(),
            "hash" => self.hash.scalar_cell(),
            "preview" => self.preview.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Order Draft Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct OrderDraftDisplay {
    /// Order id associated with this order draft display.
    pub order_id: i64,
    /// Order date associated with this order draft display.
    pub order_date: String,
    /// Order status associated with this order draft display.
    pub order_status: String,
}
impl Cells for OrderDraftDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "orderId" => self.order_id.scalar_cell(),
            "orderDate" => self.order_date.scalar_cell(),
            "orderStatus" => self.order_status.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Market Metrics used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default)]
pub struct MarketMetrics {
    /// Dte associated with this market metrics.
    #[serde(rename = "DTE")]
    pub dte: String,
    /// Optional underlying; `None` represents an absent value.
    #[serde(rename = "Underlying")]
    pub underlying: Option<String>,
    /// Optional multiplier; `None` represents an absent value.
    #[serde(rename = "Multiplier")]
    pub multiplier: Option<String>,
    /// Tick associated with this market metrics.
    #[serde(rename = "Tick")]
    pub tick: String,
    /// Settlement associated with this market metrics.
    #[serde(rename = "Settlement")]
    pub settlement: String,
    /// Daily std dev associated with this market metrics.
    #[serde(rename = "Daily Std Dev")]
    pub daily_std_dev: String,
    /// Last associated with this market metrics.
    #[serde(rename = "Last")]
    pub last: String,
    /// Daily price change associated with this market metrics.
    #[serde(rename = "Daily Price Change")]
    pub daily_price_change: String,
    /// Iv associated with this market metrics.
    #[serde(rename = "IV")]
    pub iv: String,
    /// Iv rank associated with this market metrics.
    #[serde(rename = "IV Rank")]
    pub iv_rank: String,
    /// Quote age associated with this market metrics.
    #[serde(rename = "Quote Age")]
    pub quote_age: String,
}
impl Cells for MarketMetrics {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "DTE" => self.dte.scalar_cell(),
            "Underlying" => self.underlying.scalar_cell(),
            "Multiplier" => self.multiplier.scalar_cell(),
            "Tick" => self.tick.scalar_cell(),
            "Settlement" => self.settlement.scalar_cell(),
            "Daily Std Dev" => self.daily_std_dev.scalar_cell(),
            "Last" => self.last.scalar_cell(),
            "Daily Price Change" => self.daily_price_change.scalar_cell(),
            "IV" => self.iv.scalar_cell(),
            "IV Rank" => self.iv_rank.scalar_cell(),
            "Quote Age" => self.quote_age.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Broker Summary used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct BrokerSummary {
    /// Order id associated with this broker summary.
    pub order_id: String,
    /// Current status text or state exposed by this component.
    pub status: Option<super::labels::Label>,
    /// Optional broker status; `None` represents an absent value.
    pub broker_status: Option<super::labels::Label>,
    /// Optional execution status; `None` represents an absent value.
    pub execution_status: Option<super::labels::Label>,
    /// Optional price; `None` represents an absent value.
    pub price: Option<DecimalText>,
    /// Optional broker revision; `None` represents an absent value.
    pub broker_revision: Option<i32>,
    /// Optional time in force; `None` represents an absent value.
    pub time_in_force: Option<String>,
    /// Optional broker algorithm; `None` represents an absent value.
    pub broker_algorithm: Option<super::labels::Label>,
    /// Optional algorithm pace; `None` represents an absent value.
    pub algorithm_pace: Option<String>,
    /// Optional requested; `None` represents an absent value.
    pub requested: Option<i32>,
    /// Optional filled; `None` represents an absent value.
    pub filled: Option<i32>,
    /// Optional remaining; `None` represents an absent value.
    pub remaining: Option<i64>,
    /// Optional dispatch category; `None` represents an absent value.
    pub dispatch_category: Option<String>,
    /// Optional dispatch detail; `None` represents an absent value.
    pub dispatch_detail: Option<String>,
    /// Optional changed at utc; `None` represents an absent value.
    pub changed_at_utc: Option<UtcText>,
}
impl Cells for BrokerSummary {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "OrderId" | "orderId" => self.order_id.scalar_cell(),
            "Status" | "status" => self.status.scalar_cell(),
            "BrokerStatus" | "brokerStatus" => self.broker_status.scalar_cell(),
            "ExecutionStatus" | "executionStatus" => self.execution_status.scalar_cell(),
            "Price" | "price" => self.price.scalar_cell(),
            "BrokerRevision" | "brokerRevision" => self.broker_revision.scalar_cell(),
            "TimeInForce" | "timeInForce" => self.time_in_force.scalar_cell(),
            "BrokerAlgorithm" | "brokerAlgorithm" => self.broker_algorithm.scalar_cell(),
            "AlgorithmPace" | "algorithmPace" => self.algorithm_pace.scalar_cell(),
            "Requested" | "requested" => self.requested.scalar_cell(),
            "Filled" | "filled" => self.filled.scalar_cell(),
            "Remaining" | "remaining" => self.remaining.scalar_cell(),
            "DispatchCategory" | "dispatchCategory" => self.dispatch_category.scalar_cell(),
            "DispatchDetail" | "dispatchDetail" => self.dispatch_detail.scalar_cell(),
            "ChangedAtUtc" | "changedAtUtc" => self.changed_at_utc.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Broker Mutation used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct BrokerMutation {
    /// Whether can mutate is enabled or currently true.
    pub can_mutate: bool,
    /// Optional minimum; `None` represents an absent value.
    pub minimum: Option<DecimalText>,
    /// Optional maximum; `None` represents an absent value.
    pub maximum: Option<DecimalText>,
    /// Optional tick; `None` represents an absent value.
    pub tick: Option<DecimalText>,
    /// Whether pending is enabled or currently true.
    pub pending: bool,
}
impl Cells for BrokerMutation {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "CanMutate" | "canMutate" => self.can_mutate.scalar_cell(),
            "Minimum" | "minimum" => self.minimum.scalar_cell(),
            "Maximum" | "maximum" => self.maximum.scalar_cell(),
            "Tick" | "tick" => self.tick.scalar_cell(),
            "Pending" | "pending" => self.pending.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Property used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Property {
    /// Name associated with this property.
    pub name: String,
    /// Display associated with this property.
    pub display: String,
}
impl Cells for Property {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Name" | "name" => self.name.scalar_cell(),
            "Display" | "display" => self.display.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Properties used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Properties {
    /// Ordered properties retained by Properties.
    pub properties: Vec<Property>,
}
impl Cells for Properties {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            _ => Cell::Missing,
        }
    }
}

/// Caption used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Caption {
    /// Text associated with this caption.
    pub text: String,
}
impl Cells for Caption {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Text" | "text" => self.text.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Order Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct OrderDisplay {
    /// Optional order id; `None` represents an absent value.
    pub order_id: Option<i32>,
    /// Current status text or state exposed by this component.
    pub status: Option<String>,
    /// Optional created on utc; `None` represents an absent value.
    pub created_on_utc: Option<String>,
    /// Optional operator reference; `None` represents an absent value.
    pub operator_reference: Option<String>,
    /// Optional source; `None` represents an absent value.
    pub source: Option<String>,
}
impl Cells for OrderDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "OrderId" | "orderId" => self.order_id.scalar_cell(),
            "Status" | "status" => self.status.scalar_cell(),
            "CreatedOnUtc" | "createdOnUtc" => self.created_on_utc.scalar_cell(),
            "OperatorReference" | "operatorReference" => self.operator_reference.scalar_cell(),
            "Source" | "source" => self.source.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Setup Trade Display used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct SetupTradeDisplay {
    /// Index associated with this setup trade display.
    pub index: usize,
    /// Optional trade id; `None` represents an absent value.
    pub trade_id: Option<i32>,
    /// Optional trade type; `None` represents an absent value.
    pub trade_type: Option<String>,
    /// Optional effective date; `None` represents an absent value.
    pub effective_date: Option<DateText>,
    /// Optional trade date; `None` represents an absent value.
    pub trade_date: Option<DateText>,
    /// Optional maturity date; `None` represents an absent value.
    pub maturity_date: Option<DateText>,
    /// Optional trade state; `None` represents an absent value.
    pub trade_state: Option<String>,
    /// Optional trade action; `None` represents an absent value.
    pub trade_action: Option<String>,
    /// Optional broker outcome detail for the setup trade.
    pub trade_status: Option<String>,
    /// Editable operator reference.
    pub trade_reference: Option<String>,
    /// Generated instruction reference.
    pub instruction_reference: Option<String>,
    /// Optional base contract id; `None` represents an absent value.
    pub base_contract_id: Option<String>,
}
impl Cells for SetupTradeDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Index" | "index" => Cell::Unsigned(self.index as u64),
            "TradeId" | "tradeId" => self.trade_id.scalar_cell(),
            "TradeType" | "tradeType" => self.trade_type.scalar_cell(),
            "EffectiveDate" | "effectiveDate" => self.effective_date.scalar_cell(),
            "TradeDate" | "tradeDate" => self.trade_date.scalar_cell(),
            "MaturityDate" | "maturityDate" => self.maturity_date.scalar_cell(),
            "TradeState" | "tradeState" => self.trade_state.scalar_cell(),
            "TradeAction" | "tradeAction" => self.trade_action.scalar_cell(),
            "TradeStatus" | "tradeStatus" => self.trade_status.scalar_cell(),
            "TradeReference" | "tradeReference" => self.trade_reference.scalar_cell(),
            "InstructionReference" | "instructionReference" => self.instruction_reference.scalar_cell(),
            "BaseContractId" | "baseContractId" => self.base_contract_id.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Confirmation used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Confirmation {
    /// Id associated with this confirmation.
    pub id: String,
    /// Whether submitting is enabled or currently true.
    pub submitting: bool,
    /// Order associated with this confirmation.
    pub order: ConfirmationOrder,
    /// Ordered legs retained by Confirmation.
    pub legs: Vec<ConfirmationLeg>,
}
impl Cells for Confirmation {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Id" | "id" => self.id.scalar_cell(),
            "Submitting" | "submitting" => self.submitting.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Confirmation Order used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ConfirmationOrder {
    /// Order quantity associated with this confirmation order.
    pub order_quantity: i32,
    /// Optional order price; `None` represents an absent value.
    pub order_price: Option<DecimalText>,
    /// Optional order amount; `None` represents an absent value.
    pub order_amount: Option<DecimalText>,
    /// Optional commission; `None` represents an absent value.
    pub commission: Option<DecimalText>,
    /// Optional total amount; `None` represents an absent value.
    pub total_amount: Option<DecimalText>,
}
impl Cells for ConfirmationOrder {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "OrderQuantity" | "orderQuantity" => self.order_quantity.scalar_cell(),
            "OrderPrice" | "orderPrice" => self.order_price.scalar_cell(),
            "OrderAmount" | "orderAmount" => self.order_amount.scalar_cell(),
            "Commission" | "commission" => self.commission.scalar_cell(),
            "TotalAmount" | "totalAmount" => self.total_amount.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Confirmation Leg used by the broker presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ConfirmationLeg {
    /// Side associated with this confirmation leg.
    pub side: String,
    /// Role associated with this confirmation leg.
    pub role: String,
    /// Contract id associated with this confirmation leg.
    pub contract_id: String,
    /// Optional leg commission; `None` represents an absent value.
    pub leg_commission: Option<DecimalText>,
}
impl Cells for ConfirmationLeg {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Side" | "side" => self.side.scalar_cell(),
            "Role" | "role" => self.role.scalar_cell(),
            "ContractId" | "contractId" => self.contract_id.scalar_cell(),
            "LegCommission" | "legCommission" => self.leg_commission.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
