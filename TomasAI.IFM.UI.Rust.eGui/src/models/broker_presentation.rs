//! Concrete Blotter publications shared by native producers and cached renderers.
use super::{
    cells::{Cell, Cells, ScalarCell},
    trade_contracts::*,
    wire_types::*,
};
use serde::{Deserialize, Serialize};
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct MarketDisplay {
    pub underlying_contract_id: Option<String>,
    pub expiry_date: Option<DateText>,
    pub underlying_price: Option<DecimalText>,
    pub lower_strike_bound: Option<DecimalText>,
    pub upper_strike_bound: Option<DecimalText>,
    pub window_method: Option<String>,
    pub as_of_utc: Option<OffsetText>,
    pub bollinger_standard_deviation20: Option<DecimalText>,
    pub previous_session_close: Option<DecimalText>,
    pub iv_rank: Option<DecimalText>,
    pub iv_rank_status: Option<String>,
    pub bollinger_as_of_utc: Option<OffsetText>,
    pub last_underlying_price: Option<DecimalText>,
    pub last_underlying_trade_at_utc: Option<OffsetText>,
    pub contracts: Option<Vec<EvaluatedOptionContractReadModel>>,
}
impl Cells for MarketDisplay {
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
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct LegDisplay {
    pub contract_id: Option<String>,
    pub role: Option<String>,
    pub strike: Option<DecimalText>,
    pub bid: Option<DecimalText>,
    pub ask: Option<DecimalText>,
    pub delta: Option<f64>,
    pub is_call: Option<bool>,
    pub vega: Option<f64>,
    pub multiplier: Option<DecimalText>,
    pub quote_at_utc: Option<OffsetText>,
    pub frozen: Option<bool>,
    pub is_future: Option<bool>,
    pub sign: Option<i32>,
}
impl Cells for LegDisplay {
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
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct PreviewDisplay {
    pub fields: std::collections::BTreeMap<String, String>,
    pub order_price: Option<DecimalText>,
}
impl Cells for PreviewDisplay {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "orderPrice" => self.order_price.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct QualificationReport {
    pub path: String,
    pub hash: String,
    pub preview: String,
}
impl Cells for QualificationReport {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "path" => self.path.scalar_cell(),
            "hash" => self.hash.scalar_cell(),
            "preview" => self.preview.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct OrderDraftDisplay {
    pub order_id: i64,
    pub order_date: String,
    pub order_status: String,
}
impl Cells for OrderDraftDisplay {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "orderId" => self.order_id.scalar_cell(),
            "orderDate" => self.order_date.scalar_cell(),
            "orderStatus" => self.order_status.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default)]
pub struct MarketMetrics {
    #[serde(rename = "DTE")]
    pub dte: String,
    #[serde(rename = "Underlying")]
    pub underlying: Option<String>,
    #[serde(rename = "Multiplier")]
    pub multiplier: Option<String>,
    #[serde(rename = "Tick")]
    pub tick: String,
    #[serde(rename = "Settlement")]
    pub settlement: String,
    #[serde(rename = "Daily Std Dev")]
    pub daily_std_dev: String,
    #[serde(rename = "Last")]
    pub last: String,
    #[serde(rename = "Daily Price Change")]
    pub daily_price_change: String,
    #[serde(rename = "IV")]
    pub iv: String,
    #[serde(rename = "IV Rank")]
    pub iv_rank: String,
    #[serde(rename = "Quote Age")]
    pub quote_age: String,
}
impl Cells for MarketMetrics {
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

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct BrokerSummary {
    pub order_id: String,
    pub status: Option<super::labels::Label>,
    pub broker_status: Option<super::labels::Label>,
    pub execution_status: Option<super::labels::Label>,
    pub price: Option<DecimalText>,
    pub broker_revision: Option<i32>,
    pub time_in_force: Option<String>,
    pub broker_algorithm: Option<super::labels::Label>,
    pub algorithm_pace: Option<String>,
    pub requested: Option<i32>,
    pub filled: Option<i32>,
    pub remaining: Option<i64>,
    pub dispatch_category: Option<String>,
    pub dispatch_detail: Option<String>,
    pub changed_at_utc: Option<UtcText>,
}
impl Cells for BrokerSummary {
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

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct BrokerMutation {
    pub can_mutate: bool,
    pub minimum: Option<DecimalText>,
    pub maximum: Option<DecimalText>,
    pub tick: Option<DecimalText>,
    pub pending: bool,
}
impl Cells for BrokerMutation {
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

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Property {
    pub name: String,
    pub display: String,
}
impl Cells for Property {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Name" | "name" => self.name.scalar_cell(),
            "Display" | "display" => self.display.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Properties {
    pub properties: Vec<Property>,
}
impl Cells for Properties {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Caption {
    pub text: String,
}
impl Cells for Caption {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Text" | "text" => self.text.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct OrderDisplay {
    pub order_id: Option<i32>,
    pub status: Option<String>,
    pub created_on_utc: Option<String>,
    pub operator_reference: Option<String>,
    pub source: Option<String>,
}
impl Cells for OrderDisplay {
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

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct SetupTradeDisplay {
    pub index: usize,
    pub trade_id: Option<i32>,
    pub trade_type: Option<String>,
    pub effective_date: Option<DateText>,
    pub trade_date: Option<DateText>,
    pub maturity_date: Option<DateText>,
    pub trade_state: Option<String>,
    pub trade_action: Option<String>,
    pub base_contract_id: Option<String>,
}
impl Cells for SetupTradeDisplay {
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
            "BaseContractId" | "baseContractId" => self.base_contract_id.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Confirmation {
    pub id: String,
    pub submitting: bool,
    pub order: ConfirmationOrder,
    pub legs: Vec<ConfirmationLeg>,
}
impl Cells for Confirmation {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "Id" | "id" => self.id.scalar_cell(),
            "Submitting" | "submitting" => self.submitting.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ConfirmationOrder {
    pub order_quantity: i32,
    pub order_price: Option<DecimalText>,
    pub order_amount: Option<DecimalText>,
    pub commission: Option<DecimalText>,
    pub total_amount: Option<DecimalText>,
}
impl Cells for ConfirmationOrder {
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

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ConfirmationLeg {
    pub side: String,
    pub role: String,
    pub contract_id: String,
    pub leg_commission: Option<DecimalText>,
}
impl Cells for ConfirmationLeg {
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
