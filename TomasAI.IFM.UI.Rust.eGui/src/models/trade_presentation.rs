//! Typed, borrowed presentation adapters. Extra display getters never mutate backend evidence.
use super::cells::{Cell, Cells, ScalarCell};
use super::{trade_contracts::*, wire_types::*};
use serde::{Deserialize, Serialize, ser::SerializeMap};

/// Position History Row used by the trade presentation component.
#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct PositionHistoryRow {
    /// Optional order id; `None` represents an absent value.
    pub order_id: Option<i32>,
    /// Optional trade id; `None` represents an absent value.
    pub trade_id: Option<i32>,
    /// Trade type associated with this position history row.
    pub trade_type: String,
    /// Optional value date; `None` represents an absent value.
    pub value_date: Option<DateText>,
    /// Optional days to expiry; `None` represents an absent value.
    pub days_to_expiry: Option<i32>,
    /// Trade status associated with this position history row.
    pub trade_status: String,
    /// Optional commission; `None` represents an absent value.
    pub commission: Option<DecimalText>,
    /// Optional net spread; `None` represents an absent value.
    pub net_spread: Option<DecimalText>,
    /// Optional trade pnl; `None` represents an absent value.
    pub trade_pnl: Option<DecimalText>,
    /// Whether valid is enabled or currently true.
    pub is_valid: bool,
}

/// Limit Display Row used by the trade presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct LimitDisplayRow {
    /// Order id associated with this limit display row.
    pub order_id: i32,
    /// Optional trade limit; `None` represents an absent value.
    #[serde(serialize_with = "serialize_monitor")]
    pub trade_limit: Option<TradeLimitReadModel>,
    /// Optional fund balance; `None` represents an absent value.
    pub fund_balance: Option<DecimalText>,
}

/// Observation Row used by the trade presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct ObservationRow {
    /// Key associated with this observation row.
    pub key: String,
    /// Value associated with this observation row.
    #[serde(serialize_with = "serialize_monitor")]
    pub value: FuturesOptionTickDataV2ReadModel,
}

/// Workspace Tab Row used by the trade presentation component.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct WorkspaceTabRow {
    /// Key associated with this workspace tab row.
    pub key: String,
    /// Title associated with this workspace tab row.
    pub title: String,
    /// Kind associated with this workspace tab row.
    pub kind: String,
    /// Whether active is enabled or currently true.
    pub active: bool,
}

/// Every retained monitor row has a concrete schema; arbitrary documents are not accepted.
#[derive(Clone, Debug, Default)]
pub enum TradeDisplayRow {
    #[default]
    Empty,
    Established(std::sync::Arc<EstablishedTradeDefinition>),
    Position(std::sync::Arc<PositionHistoryRow>),
    PositionDetails(std::sync::Arc<Option<StrategyPositionSnapshot>>),
    Limits(std::sync::Arc<LimitDisplayRow>),
    Plan(std::sync::Arc<StrategyTradePlanSnapshot>),
    Realtime(std::sync::Arc<TradeRealtimeSnapshot>),
    Observation(std::sync::Arc<ObservationRow>),
    WorkspaceTab(std::sync::Arc<WorkspaceTabRow>),
}
impl Cells for TradeDisplayRow {
    /// Resolves a named display cell; unsupported columns remain missing.
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
    /// Provides established for the trade display row implementation.
    pub fn established(&self) -> Option<&EstablishedTradeDefinition> {
        if let Self::Established(value) = self {
            Some(value)
        } else {
            None
        }
    }


    /// Provides position for the trade display row implementation.
    pub fn position(&self) -> Option<&StrategyPositionSnapshot> {
        if let Self::PositionDetails(value) = self {
            value.as_ref().as_ref()
        } else {
            None
        }
    }


    /// Provides plan for the trade display row implementation.
    pub fn plan(&self) -> Option<&StrategyTradePlanSnapshot> {
        if let Self::Plan(value) = self {
            Some(value)
        } else {
            None
        }
    }


    /// Provides realtime for the trade display row implementation.
    pub fn realtime(&self) -> Option<&TradeRealtimeSnapshot> {
        if let Self::Realtime(value) = self {
            Some(value)
        } else {
            None
        }
    }


    /// Provides limits for the trade display row implementation.
    pub fn limits(&self) -> Option<&LimitDisplayRow> {
        if let Self::Limits(value) = self {
            Some(value)
        } else {
            None
        }
    }


    /// Provides observation for the trade display row implementation.
    pub fn observation(&self) -> Option<&ObservationRow> {
        if let Self::Observation(value) = self {
            Some(value)
        } else {
            None
        }
    }


    /// Provides tab for the trade display row implementation.
    pub fn tab(&self) -> Option<&WorkspaceTabRow> {
        if let Self::WorkspaceTab(value) = self {
            Some(value)
        } else {
            None
        }
    }
}
impl Cells for PositionHistoryRow {
    /// Resolves a named display cell; unsupported columns remain missing.
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
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Plan Grid Row used by the trade presentation component.
#[derive(Clone, Debug, Default)]
pub struct PlanGridRow {
    /// Plan associated with this plan grid row.
    pub plan: std::sync::Arc<StrategyTradePlanSnapshot>,
    /// Whether action time overridden is enabled or currently true.
    pub action_time_overridden: bool,
    /// Optional action time; `None` represents an absent value.
    pub action_time: Option<String>,
}
impl PlanGridRow {
    /// Constructs PlanGridRow from the supplied inputs.
    pub fn new(plan: std::sync::Arc<StrategyTradePlanSnapshot>, action_time: Option<&str>) -> Self {
        Self {
            plan,
            action_time_overridden: action_time.is_some(),
            action_time: action_time
                .filter(|value| !value.is_empty())
                .map(str::to_owned),
        }
    }


    /// Checks has action reason against the current trade presentation state.
    pub fn has_action_reason(&self) -> bool {
        if let Some(detail) = &self.plan.iron_condor_trade_plan_snapshot {
            detail.action_reason.is_some()
        } else {
            self.plan.explanation.is_some()
        }
    }
}
impl Cells for PlanGridRow {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Leg Display used by the trade presentation component.
#[derive(Clone, Debug, Default)]
pub struct LegDisplay {
    /// Optional strike; `None` represents an absent value.
    pub strike: Option<DecimalText>,
    /// Optional price; `None` represents an absent value.
    pub price: Option<DecimalText>,
    /// Optional bid; `None` represents an absent value.
    pub bid: Option<f64>,
    /// Optional ask; `None` represents an absent value.
    pub ask: Option<f64>,
    /// Optional delta; `None` represents an absent value.
    pub delta: Option<f64>,
    /// Optional gamma; `None` represents an absent value.
    pub gamma: Option<f64>,
    /// Optional theta; `None` represents an absent value.
    pub theta: Option<f64>,
    /// Optional vega; `None` represents an absent value.
    pub vega: Option<f64>,
    /// Optional rho; `None` represents an absent value.
    pub rho: Option<f64>,
    /// Optional iv; `None` represents an absent value.
    pub iv: Option<f64>,
}
impl Cells for LegDisplay {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Spread Display Row used by the trade presentation component.
#[derive(Clone, Debug, Default)]
pub struct SpreadDisplayRow {
    /// Spread associated with this spread display row.
    pub spread: String,
    /// Short associated with this spread display row.
    pub short: LegDisplay,
    /// Long associated with this spread display row.
    pub long: LegDisplay,
    /// Optional net spread; `None` represents an absent value.
    pub net_spread: Option<f64>,
    /// Optional trade value; `None` represents an absent value.
    pub trade_value: Option<f64>,
    /// Optional trade pnl; `None` represents an absent value.
    pub trade_pnl: Option<f64>,
    /// Optional otm probability; `None` represents an absent value.
    pub otm_probability: Option<f64>,
    /// Optional forward price; `None` represents an absent value.
    pub forward_price: Option<f64>,
}
impl Cells for SpreadDisplayRow {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Defines the presentation record operations shared by its implementations.
pub trait PresentationRecord {
    /// Writes the presentation fields, applying the requested history omission policy.
    fn serialize_presentation<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error>;
}

/// Defines the presentation value operations shared by its implementations.
pub trait PresentationValue {
    /// Serializes this value using the requested presentation mode.
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error>;
}

/// Field Value used by the trade presentation component.
pub struct FieldValue<'a, T: ?Sized> {
    /// Value associated with this field value.
    pub value: &'a T,
    /// Mode associated with this field value.
    pub mode: PresentationMode,
}
impl<T: PresentationValue + ?Sized> Serialize for FieldValue<'_, T> {
    /// Serializes this value through the supplied serializer.
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.value.serialize_value(serializer, self.mode)
    }
}
// Primitive scalar values do not vary with the enclosing presentation mode.
macro_rules! presentation_scalar {
    ($($ty:ty),*) => {
        $(
            impl PresentationValue for $ty {
                /// Serializes the scalar unchanged for every presentation mode.
                fn serialize_value<S: serde::Serializer>(
                    &self,
                    serializer: S,
                    _mode: PresentationMode,
                ) -> Result<S::Ok, S::Error> {
                    self.serialize(serializer)
                }
            }
        )*
    };
}
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
    /// Serializes this value using the requested presentation mode.
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
    /// Serializes this value using the requested presentation mode.
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
    /// Serializes this value using the requested presentation mode.
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        (**self).serialize_value(serializer, mode)
    }
}
impl<T: PresentationValue + ?Sized> PresentationValue for &T {
    /// Serializes this value using the requested presentation mode.
    fn serialize_value<S: serde::Serializer>(
        &self,
        serializer: S,
        mode: PresentationMode,
    ) -> Result<S::Ok, S::Error> {
        (**self).serialize_value(serializer, mode)
    }
}

/// Defines the presentation extras operations shared by its implementations.
pub trait PresentationExtras {
    /// Appends the computed presentation fields without changing backend values.
    fn serialize_extras<M: SerializeMap>(&self, _map: &mut M) -> Result<(), M::Error> {
        Ok(())
    }
}

/// Display Record used by the trade presentation component.
pub struct DisplayRecord<'a, T> {
    /// Value associated with this display record.
    pub value: &'a T,
    /// Retained historical data used by this component.
    pub history: bool,
}
impl<T: PresentationValue> Serialize for DisplayRecord<'_, T> {
    /// Serializes this value through the supplied serializer.
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


/// Writes the compact monitor presentation while keeping backend financial values authoritative.
pub fn serialize_monitor<T: PresentationValue, S: serde::Serializer>(
    value: &T,
    serializer: S,
) -> Result<S::Ok, S::Error> {
    value.serialize_value(serializer, PresentationMode::Monitor)
}
impl TradeEntityId {
    /// Checks is valid against the current trade presentation state.
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
    /// Appends the computed presentation fields without changing backend values.
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isValid", &self.is_valid())
    }
}
impl StrategyPositionId {
    /// Checks is valid against the current trade presentation state.
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
    /// Appends the computed presentation fields without changing backend values.
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isValid", &self.is_valid())
    }
}
impl PresentationExtras for EstablishedTradeDefinition {
    /// Appends the computed presentation fields without changing backend values.
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
    /// Projects published forward-risk values into the compatible display fields.
    pub fn present_forward_risk(&mut self) {
        if let Some(risk) = self.iron_condor_forward_risk.as_ref() {
            self.forward_loss = risk.forward_loss.clone();
            self.forward_loss_ratio = risk.forward_loss_ratio;
            self.loss_headroom = risk.forward_loss_headroom.clone();
        }
    }


    /// Checks is complete against the current trade presentation state.
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


    /// Provides forward spread for chart for the iron condor trade plan snapshot implementation.
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
    /// Appends the computed presentation fields without changing backend values.
    fn serialize_extras<M: SerializeMap>(&self, map: &mut M) -> Result<(), M::Error> {
        map.serialize_entry("isComplete", &self.is_complete())?;
        map.serialize_entry("forwardSpreadForChart", &self.forward_spread_for_chart())
    }
}

/// Tick Identity used by the trade presentation component.
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct TickIdentity<'a> {
    /// Contract id associated with this tick identity.
    contract_id: &'a Option<String>,
    /// Value date associated with this tick identity.
    value_date: &'a Option<DateText>,
    /// Monitoring owner id associated with this tick identity.
    monitoring_owner_id: &'static str,
    /// Whether valid is enabled or currently true.
    is_valid: bool,
}
impl PresentationExtras for FuturesOptionTickDataV2ReadModel {
    /// Appends the computed presentation fields without changing backend values.
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


/// Formats the published action timestamp for the trade display.
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
    /// Trade associated with this trade info row.
    pub trade: &'a EstablishedTradeDefinition,
    /// Optional trade type; `None` represents an absent value.
    pub trade_type: Option<&'a str>,
    /// Optional action; `None` represents an absent value.
    pub action: Option<&'a str>,
    /// Optional quantity; `None` represents an absent value.
    pub quantity: Option<f64>,
}
impl Cells for TradeInfoRow<'_> {
    /// Resolves a named display cell; unsupported columns remain missing.
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
    /// Checks that the published identity contains the required nonempty identifiers.
    fn published_is_valid(&self) -> bool;
}
