//! Typed, borrowed presentation adapters. Extra display getters never mutate backend evidence.
use super::cells::{Cell, Cells, ScalarCell};
use super::{trade_contracts::*, wire_types::*};
use serde::{Deserialize, Serialize, ser::SerializeMap};

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct PositionHistoryRow {
    pub order_id: Option<i32>,
    pub trade_id: Option<i32>,
    pub trade_type: String,
    pub value_date: Option<DateText>,
    pub days_to_expiry: Option<i32>,
    pub trade_status: String,
    pub commission: Option<DecimalText>,
    pub net_spread: Option<DecimalText>,
    pub trade_pnl: Option<DecimalText>,
    pub is_valid: bool,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct LimitDisplayRow {
    pub order_id: i32,
    #[serde(serialize_with = "serialize_monitor")]
    pub trade_limit: Option<TradeLimitReadModel>,
    pub fund_balance: Option<DecimalText>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ObservationRow {
    pub key: String,
    #[serde(serialize_with = "serialize_monitor")]
    pub value: FuturesOptionTickDataV2ReadModel,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct WorkspaceTabRow {
    pub key: String,
    pub title: String,
    pub kind: String,
    pub active: bool,
}

/// Every retained monitor row has a concrete schema; arbitrary documents are not accepted.
#[derive(Clone, Debug, Default)]
pub enum TradeDisplayRow {
    #[default]
    Empty,
    Established(Box<EstablishedTradeDefinition>),
    Position(Box<PositionHistoryRow>),
    PositionDetails(Box<StrategyPositionSnapshot>),
    Limits(Box<LimitDisplayRow>),
    Plan(std::sync::Arc<StrategyTradePlanSnapshot>),
    Realtime(Box<TradeRealtimeSnapshot>),
    Observation(Box<ObservationRow>),
    WorkspaceTab(Box<WorkspaceTabRow>),
}
impl Cells for TradeDisplayRow {
    fn cell(&self, column: &str) -> Cell<'_> {
        match self {
            Self::Established(value) => value.cell(column),
            Self::Position(value) => value.cell(column),
            Self::PositionDetails(value) => value.cell(column),
            Self::Limits(value) => value.cell(column),
            Self::Plan(value) => value.cell(column),
            Self::Realtime(value) => value.cell(column),
            Self::Observation(_) | Self::WorkspaceTab(_) | Self::Empty => Cell::Missing,
        }
    }
}
impl TradeDisplayRow {
    pub fn established(&self) -> Option<&EstablishedTradeDefinition> {
        if let Self::Established(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn position(&self) -> Option<&StrategyPositionSnapshot> {
        if let Self::PositionDetails(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn plan(&self) -> Option<&StrategyTradePlanSnapshot> {
        if let Self::Plan(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn realtime(&self) -> Option<&TradeRealtimeSnapshot> {
        if let Self::Realtime(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn limits(&self) -> Option<&LimitDisplayRow> {
        if let Self::Limits(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn observation(&self) -> Option<&ObservationRow> {
        if let Self::Observation(value) = self {
            Some(value)
        } else {
            None
        }
    }
    pub fn tab(&self) -> Option<&WorkspaceTabRow> {
        if let Self::WorkspaceTab(value) = self {
            Some(value)
        } else {
            None
        }
    }
}
impl Cells for PositionHistoryRow {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "orderId" => self.order_id.scalar_cell(),
            "tradeId" => self.trade_id.scalar_cell(),
            "tradeType" => self.trade_type.scalar_cell(),
            "valueDate" => self.value_date.scalar_cell(),
            "daysToExpiry" => self.days_to_expiry.scalar_cell(),
            "tradeStatus" => self.trade_status.scalar_cell(),
            "commission" => self.commission.scalar_cell(),
            "netSpread" => self.net_spread.scalar_cell(),
            "tradePnl" => self.trade_pnl.scalar_cell(),
            "isValid" => self.is_valid.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
impl Cells for LimitDisplayRow {
    fn cell(&self, column: &str) -> Cell<'_> {
        if self.trade_limit.is_none() {
            return Cell::Missing;
        }
        match column {
            "orderId" => self.order_id.scalar_cell(),
            "fundBalance" => self.fund_balance.scalar_cell(),
            _ => self.trade_limit.cell(column),
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct PlanGridRow {
    pub plan: std::sync::Arc<StrategyTradePlanSnapshot>,
    pub action_time_overridden: bool,
    pub action_time: Option<String>,
}
impl PlanGridRow {
    pub fn new(plan: std::sync::Arc<StrategyTradePlanSnapshot>, action_time: Option<&str>) -> Self {
        Self {
            plan,
            action_time_overridden: action_time.is_some(),
            action_time: action_time
                .filter(|value| !value.is_empty())
                .map(str::to_owned),
        }
    }
    pub fn has_action_reason(&self) -> bool {
        if let Some(detail) = &self.plan.iron_condor_trade_plan_snapshot {
            detail.action_reason.is_some()
        } else {
            self.plan.explanation.is_some()
        }
    }
}
impl Cells for PlanGridRow {
    fn cell(&self, column: &str) -> Cell<'_> {
        if column == "actionDateTime" && self.action_time_overridden {
            return self.action_time.scalar_cell();
        }
        if let Some(detail) = &self.plan.iron_condor_trade_plan_snapshot {
            return detail.cell(column);
        }
        match column {
            "actionDateTime" => self.plan.calculated_at_utc.scalar_cell(),
            "actionType" => self.plan.action.scalar_cell(),
            "actionState" => self.plan.state.scalar_cell(),
            "actionReason" => self.plan.explanation.scalar_cell(),
            "dailyPnl" => self.plan.total_pnl.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct LegDisplay {
    pub strike: Option<DecimalText>,
    pub price: Option<DecimalText>,
    pub bid: Option<f64>,
    pub ask: Option<f64>,
    pub delta: Option<f64>,
    pub gamma: Option<f64>,
    pub theta: Option<f64>,
    pub vega: Option<f64>,
    pub rho: Option<f64>,
    pub iv: Option<f64>,
}
impl Cells for LegDisplay {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "Strike" => self.strike.scalar_cell(),
            "Price" => self.price.scalar_cell(),
            "Bid" => self.bid.scalar_cell(),
            "Ask" => self.ask.scalar_cell(),
            "Delta" => self.delta.scalar_cell(),
            "Gamma" => self.gamma.scalar_cell(),
            "Theta" => self.theta.scalar_cell(),
            "Vega" => self.vega.scalar_cell(),
            "Rho" => self.rho.scalar_cell(),
            "IV" => self.iv.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default)]
pub struct SpreadDisplayRow {
    pub spread: String,
    pub short: LegDisplay,
    pub long: LegDisplay,
    pub net_spread: Option<f64>,
    pub trade_value: Option<f64>,
    pub trade_pnl: Option<f64>,
    pub otm_probability: Option<f64>,
    pub forward_price: Option<f64>,
}
impl Cells for SpreadDisplayRow {
    fn cell(&self, column: &str) -> Cell<'_> {
        if let Some(field) = column.strip_prefix("short") {
            return self.short.cell(field);
        }
        if let Some(field) = column.strip_prefix("long") {
            return self.long.cell(field);
        }
        match column {
            "spread" => self.spread.scalar_cell(),
            "netSpread" => self.net_spread.scalar_cell(),
            "tradeValue" => self.trade_value.scalar_cell(),
            "tradePnl" => self.trade_pnl.scalar_cell(),
            "otmProbability" => self.otm_probability.scalar_cell(),
            "forwardPrice" => self.forward_price.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}

/// Native broker documents preserve unknown enum numbers; the original monitor
/// formatter publishes Enum.ToString, including unnamed numeric labels.
#[derive(Clone, Copy, PartialEq, Eq)]
pub enum PresentationMode {
    Native,
    Monitor,
    History,
}
pub trait PresentationRecord {
    fn serialize_presentation<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error>;
}
pub trait PresentationValue {
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error>;
}
pub struct FieldValue<'a, T: ?Sized> {
    pub value: &'a T,
    pub mode: PresentationMode,
}
impl<T: PresentationValue + ?Sized> Serialize for FieldValue<'_, T> {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.value.serialize_value(serializer, self.mode)
    }
}
macro_rules! presentation_scalar{($($ty:ty),*)=>{$(impl PresentationValue for $ty{
 fn serialize_value<S:serde::Serializer>(&self,serializer:S,_mode:PresentationMode)->Result<S::Ok,S::Error>{self.serialize(serializer)}
})*};}
presentation_scalar!(
    String,
    bool,
    u8,
    u16,
    u32,
    u64,
    i8,
    i16,
    i32,
    i64,
    f32,
    f64,
    DecimalText,
    DateText,
    UtcText,
    OffsetText,
    TimeText,
    ByteBuffer
);
impl<T: PresentationValue> PresentationValue for Option<T> {
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        match self {
            Some(value) => serializer.serialize_some(&FieldValue { value, mode }),
            None => serializer.serialize_none(),
        }
    }
}
impl<T: PresentationValue> PresentationValue for Vec<T> {
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        use serde::ser::SerializeSeq;
        let mut sequence = serializer.serialize_seq(Some(self.len()))?;
        for value in self {
            sequence.serialize_element(&FieldValue { value, mode })?;
        }
        sequence.end()
    }
}
impl<T: PresentationValue + ?Sized> PresentationValue for Box<T> {
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        (**self).serialize_value(serializer, mode)
    }
}
impl<T: PresentationValue + ?Sized> PresentationValue for &T {
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        (**self).serialize_value(serializer, mode)
    }
}
pub trait PresentationExtras {
    fn serialize_extras<M: SerializeMap>(&self, _map: &mut M) -> Result<(), M::Error> {
        Ok(())
    }
}
pub struct DisplayRecord<'a, T> {
    pub value: &'a T,
    pub history: bool,
}
impl<T: PresentationValue> Serialize for DisplayRecord<'_, T> {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.value.serialize_value(
            serializer,
            if self.history {
                PresentationMode::History
            } else {
                PresentationMode::Monitor
            },
        )
    }
}
pub fn serialize_monitor<T: PresentationValue, S: serde::Serializer>(
    value: &T,
    serializer: S,
) -> Result<S::Ok, S::Error> {
    value.serialize_value(serializer, PresentationMode::Monitor)
}
impl TradeEntityId {
    pub fn is_valid(&self) -> bool {
        [
            self.portfolio_id,
            self.fund_id,
            self.order_id,
            self.trade_id,
        ]
        .into_iter()
        .all(|id| id.is_some_and(|id| id > 0))
    }
}
impl PresentationExtras for TradeEntityId {
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isValid", &self.is_valid())
    }
}
impl StrategyPositionId {
    pub fn is_valid(&self) -> bool {
        self.trade.as_ref().is_some_and(TradeEntityId::is_valid)
            && self
                .position_id
                .as_deref()
                .and_then(|id| uuid::Uuid::parse_str(id).ok())
                .is_some_and(|id| !id.is_nil())
    }
}
impl PresentationExtras for StrategyPositionId {
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isValid", &self.is_valid())
    }
}
impl PresentationExtras for EstablishedTradeDefinition {
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry(
            "tradeDate",
            &self
                .opening_history_valuation
                .as_ref()
                .and_then(|value| value.value_date.as_ref()),
        )?;
        let maturity = self
            .legs
            .as_ref()
            .filter(|legs| !legs.is_empty() && legs.iter().all(|leg| leg.expiry.is_some()))
            .and_then(|legs| legs.iter().filter_map(|leg| leg.expiry.as_ref()).max());
        map.serialize_entry("maturityDate", &maturity)
    }
}
impl IronCondorTradePlanSnapshot {
    pub fn present_forward_risk(&mut self) {
        if let Some(risk) = self.iron_condor_forward_risk.as_ref() {
            self.forward_loss = risk.forward_loss.clone();
            self.forward_loss_ratio = risk.forward_loss_ratio;
            self.loss_headroom = risk.forward_loss_headroom.clone();
        }
    }
    pub fn is_complete(&self) -> bool {
        self.calculation_status
            .as_deref()
            .map(|status| status == "Complete")
            .unwrap_or_else(|| {
                self.unavailable_reasons.as_ref().is_some_and(Vec::is_empty)
                    && self.trade_type.is_some()
                    && self.forward_price.is_some()
                    && self.forward_loss_ratio.is_some()
                    && self.asset_price.is_some()
                    && self.short_put_gamma.is_some()
                    && self.short_call_gamma.is_some()
                    && self.max_profit.is_some()
                    && self.max_loss.is_some()
            })
    }
    pub fn forward_spread_for_chart(&self) -> Option<DecimalText> {
        let price = self.combined_forward_price.as_ref()?;
        if self.calculation_version.as_deref() != Some("IronCondor/EntryCreditForwardPnl/v2") {
            return Some(price.clone());
        }
        let text = price.as_str();
        let zero = text.bytes().all(|byte| matches!(byte, b'0' | b'.' | b'-'));
        Some(DecimalText(if zero {
            text.trim_start_matches('-').to_owned()
        } else if let Some(value) = text.strip_prefix('-') {
            value.to_owned()
        } else {
            format!("-{text}")
        }))
    }
}
impl PresentationExtras for IronCondorTradePlanSnapshot {
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isComplete", &self.is_complete())?;
        map.serialize_entry("forwardSpreadForChart", &self.forward_spread_for_chart())
    }
}
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct TickIdentity<'a> {
    contract_id: &'a Option<String>,
    value_date: &'a Option<DateText>,
    monitoring_owner_id: &'static str,
    is_valid: bool,
}
impl PresentationExtras for FuturesOptionTickDataV2ReadModel {
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        let date_valid = self
            .value_date
            .as_ref()
            .is_some_and(|date| date.as_str() != "0001-01-01");
        let contract_valid = self
            .contract_id
            .as_deref()
            .is_some_and(|contract| !contract.is_empty());
        map.serialize_entry(
            "isEmpty",
            &(!contract_valid || !date_valid || self.tick_id == Some(0)),
        )?;
        map.serialize_entry(
            "isValid",
            &(self.contract_id.is_some() && date_valid && self.tick_id.is_some_and(|id| id > 0)),
        )?;
        map.serialize_entry(
            "entityId",
            &TickIdentity {
                contract_id: &self.contract_id,
                value_date: &self.value_date,
                monitoring_owner_id: "00000000-0000-0000-0000-000000000000",
                is_valid: contract_valid && date_valid,
            },
        )
    }
}

pub fn action_time(plan: &StrategyTradePlanSnapshot) -> Result<String, String> {
    let value = plan
        .iron_condor_trade_plan_snapshot
        .as_ref()
        .and_then(|plan| plan.action_date_time.as_ref())
        .or(plan.calculated_at_utc.as_ref())
        .ok_or("Missing Trade Plan action time")?;
    if value.as_str().starts_with("0001-") {
        return Ok(String::new());
    }
    let utc = chrono::DateTime::parse_from_rfc3339(value.as_str())
        .map_err(|_| "Invalid Trade Plan action time")?;
    Ok(utc
        .with_timezone(&chrono_tz::America::New_York)
        .format("%I:%M:%S%.3f %p")
        .to_string())
}

/// Borrowed summary cells for the trade header.
pub struct TradeInfoRow<'a> {
    pub trade: &'a EstablishedTradeDefinition,
    pub trade_type: Option<&'a str>,
    pub action: Option<&'a str>,
    pub quantity: Option<f64>,
}
impl Cells for TradeInfoRow<'_> {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "orderId" | "tradeId" => self.trade.id.cell(column),
            "tradeType" => self
                .trade_type
                .map(|v| Cell::Text(v.into()))
                .unwrap_or(Cell::Missing),
            "action" => self
                .action
                .map(|v| Cell::Text(v.into()))
                .unwrap_or(Cell::Missing),
            "quantity" => self.quantity.scalar_cell(),
            "tradeDate" => self
                .trade
                .established_at_utc
                .as_ref()
                .and_then(|v| v.as_str().get(..10))
                .map(|v| Cell::Text(v.into()))
                .unwrap_or(Cell::Missing),
            "expiry" => self
                .trade
                .legs
                .as_ref()
                .and_then(|legs| legs.first())
                .map(|leg| leg.expiry.scalar_cell())
                .unwrap_or(Cell::Missing),
            "state" => self.trade.cell("status"),
            _ => Cell::Missing,
        }
    }
}

/// Managed display identity getter; never execution authority.
pub trait PublishedIdentity {
    fn published_is_valid(&self) -> bool;
}
