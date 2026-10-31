//! Publication-owned decoded trade display data.
//!
//! Intake prepares rows once using a `OnceLock`; pointer-only frames borrow shared
//! `Arc` data. Cache validity requires the native render token, generation, and row
//! count. Explicit invalidation is required after in-place publication mutation.
//!
//! A bounded cleanup worker retires large publications/models away from input frames.
//! Queue saturation or worker shutdown falls back to local destruction instead of
//! leaking or accumulating an unbounded history. This cache formats/presents published
//! values; it does not calculate option Greeks, P&L, or contract multipliers.

//! Publication-owned display preparation. Pointer-only frames borrow the prepared rows.
use super::*;
use crate::models::{
    broker_presentation::*,
    cells::{Cell, Cells},
};
use crate::models::{
    trade_contracts::*,
    trade_presentation::{
        LimitDisplayRow, ObservationRow, PlanGridRow, PositionHistoryRow, TradeDisplayRow,
        WorkspaceTabRow,
    },
};
use std::sync::Arc;

/// Fixed contract variants decoded once on publication intake.
#[derive(Default)]
pub(super) enum BusinessRow {
    #[default]
    Missing,
    Order(Box<OrderDisplay>),
    Trade(Box<SetupTradeDisplay>),
    OrderDraft(Box<OrderDraftDisplay>),
    BrokerOrder(Box<BrokerOrderDefinition>),
    Execution(Box<OrderExecutionDefinition>),
    Fill(Box<ExecutionFillEvidence>),
    Summary(Box<BrokerSummary>),
    Mutation(Box<BrokerMutation>),
    Properties(Box<Properties>),
    Caption(Box<Caption>),
    Preview(Box<PreviewDisplay>),
    Account(Box<BrokerAccountDefinition>),
    Report(Box<QualificationReport>),
}
impl Cells for BusinessRow {
    fn cell(&self, key: &str) -> Cell<'_> {
        match self {
            Self::Order(v) => v.cell(key),
            Self::Trade(v) => v.cell(key),
            Self::OrderDraft(v) => v.cell(key),
            Self::BrokerOrder(v) => v.cell(key),
            Self::Execution(v) => v.cell(key),
            Self::Fill(v) => v.cell(key),
            Self::Summary(v) => v.cell(key),
            Self::Mutation(v) => v.cell(key),
            Self::Properties(v) => v.cell(key),
            Self::Caption(v) => v.cell(key),
            Self::Preview(v) => v.cell(key),
            Self::Account(v) => v.cell(key),
            Self::Report(v) => v.cell(key),
            Self::Missing => Cell::Missing,
        }
    }
}
impl BusinessRow {
    pub fn is_missing(&self) -> bool {
        matches!(self, Self::Missing)
    }
    pub fn broker_order(&self) -> Option<&BrokerOrderDefinition> {
        if let Self::BrokerOrder(v) = self {
            Some(v)
        } else {
            None
        }
    }
    pub fn execution(&self) -> Option<&OrderExecutionDefinition> {
        if let Self::Execution(v) = self {
            Some(v)
        } else {
            None
        }
    }
    pub fn fill(&self) -> Option<&ExecutionFillEvidence> {
        if let Self::Fill(v) = self {
            Some(v)
        } else {
            None
        }
    }
    pub fn properties(&self) -> Option<&Properties> {
        if let Self::Properties(v) = self {
            Some(v)
        } else {
            None
        }
    }
    pub fn preview(&self) -> Option<&PreviewDisplay> {
        if let Self::Preview(v) = self {
            Some(v)
        } else {
            None
        }
    }
    pub fn account(&self) -> Option<&BrokerAccountDefinition> {
        if let Self::Account(v) = self {
            Some(v)
        } else {
            None
        }
    }
}
/// Native-only prepared data; never serialized into bridge messages.
#[derive(Default)]
pub struct PreparedTradeDisplay {
    /// Once-initialized generation, row count, and shared decoded display model for this publication.
    data: std::sync::OnceLock<(u64, usize, Arc<TradeDisplayData>)>,
}

/// Large display model or wire publication queued for destruction outside an input frame.
enum RetiredDisplay {
    /// Decoded display model whose final release should leave the input frame.
    Model(Arc<TradeDisplayData>),
    /// Replaced wire publication whose final release should leave the input frame.
    Publication(Box<ContractPublication>),
}
/// Queue a large model/publication for bounded background destruction; saturation or shutdown falls back to local drop.
fn retire(value: RetiredDisplay) {
    // A single bounded cleanup worker avoids freeing large histories on an input frame.
    // On saturation or shutdown, fall back to local destruction instead of growing memory.
    /// Lazily initialized sender for the single bounded trade-display cleanup worker.
    static RETIRED: std::sync::OnceLock<std::sync::mpsc::SyncSender<RetiredDisplay>> =
        std::sync::OnceLock::new();
    let sender = RETIRED.get_or_init(|| {
        let (sender, receiver) = std::sync::mpsc::sync_channel(2);
        let _ = std::thread::Builder::new()
            .name("ifm-trade-retire".into())
            .spawn(move || {
                for value in receiver {
                    match value {
                        RetiredDisplay::Model(model) => drop(model),
                        RetiredDisplay::Publication(publication) => drop(publication),
                    }
                }
            });
        sender
    });
    let _ = sender.try_send(value);
}

/// Decoded row groups, flattened plan display rows, date lookup, and recent chart series for one publication.
#[derive(Default)]
pub(super) struct TradeDisplayData {
    /// Decoded trade rows indexed by their published kind.
    groups: BTreeMap<String, Vec<TradeDisplayRow>>,
    /// Flattened display-ready rows for the currently published trade-plan window.
    pub plans: Vec<PlanGridRow>,
    pub business: BTreeMap<String, BusinessRow>,
    pub confirmation: Option<Confirmation>,
    pub broker_facts: BTreeMap<String, String>,
    pub market: Option<Box<crate::models::broker_presentation::MarketDisplay>>,
    pub market_metrics: Option<Box<crate::models::broker_presentation::MarketMetrics>>,
    pub selected_legs: Vec<crate::models::broker_presentation::LegDisplay>,
    /// Concrete EOD display, decoded once per publication.
    pub eod: Option<Box<crate::models::eod::EodDocument>>,
    /// Whether any displayed plan has an action reason, used to choose visible columns.
    pub has_action_reason: bool,
    /// First matching plan index for each serialized value-date key.
    pub plan_dates: BTreeMap<String, usize>,
    /// Cached recent net-spread x/y points decoded from the dedicated chart publication row.
    pub chart_points: Vec<[f64; 2]>,
    /// Cached recent forward-spread x/y points decoded from the dedicated chart row.
    pub forward_chart_points: Vec<[f64; 2]>,
    /// Backend chart window end decoded once by the typed chart-row model.
    pub chart_end_utc_seconds: Option<f64>,
}

impl TradeDisplayData {
    pub fn row(&self, id: &str) -> &BusinessRow {
        static MISSING: BusinessRow = BusinessRow::Missing;
        self.business.get(id).unwrap_or(&MISSING)
    }

    /// Decode the relevant trade row groups once and prepare flattened plans, date indexes, and recent chart points.
    fn build(s: &ContractPublication) -> Self {
        let mut model = Self::default();
        model.confirmation = s
            .result
            .documents
            .get("confirmation")
            .and_then(|bytes| crate::wire::decode(bytes).ok());
        // Decode once per received publication, not once per widget or pointer event.
        let mut eod_seen = false;
        let mut market_seen = false;
        let mut metrics_seen = false;
        for row in &s.result.rows {
            let Some(kind) = row.fields.get("kind") else {
                continue;
            };
            let business = match kind.as_str() {
                "order" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<OrderDisplay>(bytes).ok())
                        .map(|v| BusinessRow::Order(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "trade" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<SetupTradeDisplay>(bytes).ok())
                        .map(|v| BusinessRow::Trade(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "order-draft" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<OrderDraftDisplay>(bytes).ok())
                        .map(|v| BusinessRow::OrderDraft(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-order" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<BrokerOrderDefinition>(bytes).ok())
                        .map(|v| BusinessRow::BrokerOrder(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "execution" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| {
                            crate::wire::decode::<OrderExecutionDefinition>(bytes).ok()
                        })
                        .map(|v| BusinessRow::Execution(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "fill" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<ExecutionFillEvidence>(bytes).ok())
                        .map(|v| BusinessRow::Fill(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-summary" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<BrokerSummary>(bytes).ok())
                        .map(|v| BusinessRow::Summary(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-mutation" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<BrokerMutation>(bytes).ok())
                        .map(|v| BusinessRow::Mutation(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-properties" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<Properties>(bytes).ok())
                        .map(|v| BusinessRow::Properties(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "fill-properties" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<Properties>(bytes).ok())
                        .map(|v| BusinessRow::Properties(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-caption" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<Caption>(bytes).ok())
                        .map(|v| BusinessRow::Caption(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "broker-preview" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<PreviewDisplay>(bytes).ok())
                        .map(|v| BusinessRow::Preview(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "account" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| {
                            crate::wire::decode::<BrokerAccountDefinition>(bytes).ok()
                        })
                        .map(|v| BusinessRow::Account(Box::new(v)))
                        .unwrap_or_default(),
                ),
                "qualification-report" => Some(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode::<QualificationReport>(bytes).ok())
                        .map(|v| BusinessRow::Report(Box::new(v)))
                        .unwrap_or_default(),
                ),
                _ => None,
            };
            if let Some(value) = business {
                if let Some(fact) = value
                    .broker_order()
                    .and_then(|v| v.last_observation.as_ref())
                {
                    if let Ok(text) = serde_json::to_string(fact) {
                        model.broker_facts.insert(row.id.clone(), text);
                    }
                }
                model.business.insert(row.id.clone(), value);
                continue;
            }
            if kind == "market" {
                if !market_seen {
                    market_seen = true;
                    model.market = row
                        .data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode(bytes).ok())
                        .map(Box::new);
                }
                continue;
            }
            if kind == "market-metrics" {
                if !metrics_seen {
                    metrics_seen = true;
                    model.market_metrics = row
                        .data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode(bytes).ok())
                        .map(Box::new);
                }
                continue;
            }
            if kind == "leg" {
                model.selected_legs.push(
                    row.data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode(bytes).ok())
                        .unwrap_or_default(),
                );
                continue;
            }
            if kind == "eod" {
                if !eod_seen {
                    eod_seen = true;
                    model.eod = row
                        .data
                        .as_ref()
                        .and_then(|bytes| crate::wire::decode(bytes).ok())
                        .map(Box::new);
                }
                continue;
            }
            if ![
                "established",
                "position",
                "position-details",
                "limits",
                "plan",
                "realtime",
                "observation",
                "workspace-tab",
                "chart",
            ]
            .contains(&kind.as_str())
            {
                continue;
            }
            if kind == "chart" {
                let chart = row
                    .data
                    .as_ref()
                    .map(|bytes| {
                        crate::models::trade_chart::PreparedTradeChart::from_messagepack(bytes)
                    })
                    .and_then(Result::ok)
                    .unwrap_or_default();
                model.chart_end_utc_seconds = chart.end_utc_seconds;
                model.chart_points = chart.net;
                model.forward_chart_points = chart.forward;
                continue;
            }
            let mut value = row
                .data
                .as_ref()
                .ok_or_else(|| "Missing monitor row data".to_owned())
                .and_then(|bytes| match kind.as_str() {
                    "established" => crate::wire::decode::<EstablishedTradeDefinition>(bytes)
                        .map(|value| TradeDisplayRow::Established(Box::new(value))),
                    "position" => crate::wire::decode::<PositionHistoryRow>(bytes)
                        .map(|value| TradeDisplayRow::Position(Box::new(value))),
                    "position-details" => crate::wire::decode::<Option<StrategyPositionSnapshot>>(
                        bytes,
                    )
                    .map(|value| {
                        value
                            .map(|value| TradeDisplayRow::PositionDetails(Box::new(value)))
                            .unwrap_or_default()
                    }),
                    "limits" => crate::wire::decode::<LimitDisplayRow>(bytes)
                        .map(|value| TradeDisplayRow::Limits(Box::new(value))),
                    "plan" => crate::wire::decode::<StrategyTradePlanSnapshot>(bytes)
                        .map(|value| TradeDisplayRow::Plan(Arc::new(value))),
                    "realtime" => crate::wire::decode::<TradeRealtimeSnapshot>(bytes)
                        .map(|value| TradeDisplayRow::Realtime(Box::new(value))),
                    "observation" => crate::wire::decode::<ObservationRow>(bytes)
                        .map(|value| TradeDisplayRow::Observation(Box::new(value))),
                    "workspace-tab" => crate::wire::decode::<WorkspaceTabRow>(bytes)
                        .map(|value| TradeDisplayRow::WorkspaceTab(Box::new(value))),
                    _ => Err("Unsupported monitor presentation schema".into()),
                })
                .unwrap_or_default();
            if let TradeDisplayRow::Realtime(snapshot) = &mut value {
                if snapshot.plan.is_some() {
                    for plan in [&mut snapshot.plan, &mut snapshot.last_available_plan]
                        .into_iter()
                        .flatten()
                    {
                        if let Some(detail) = plan.iron_condor_trade_plan_snapshot.as_mut() {
                            detail.present_forward_risk();
                        }
                    }
                }
            }
            if let TradeDisplayRow::Plan(plan) = &mut value {
                if let Some(detail) = Arc::get_mut(plan)
                    .and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_mut())
                {
                    detail.present_forward_risk();
                }
                let index = model.groups.get("plan").map_or(0, Vec::len);
                let date = plan
                    .value_date
                    .as_ref()
                    .map(ToString::to_string)
                    .unwrap_or_else(|| "null".into());
                model.plan_dates.entry(date).or_insert(index);
                let prepared = PlanGridRow::new(
                    plan.clone(),
                    row.fields.get("actionDateTimeEastern").map(String::as_str),
                );
                model.has_action_reason |= prepared.has_action_reason();
                model.plans.push(prepared);
            } else if kind == "plan" {
                let index = model.groups.get("plan").map_or(0, Vec::len);
                model.plan_dates.entry("null".into()).or_insert(index);
                model.plans.push(PlanGridRow::default());
            }
            model.groups.entry(kind.clone()).or_default().push(value);
        }
        model
    }

    /// Borrow all decoded rows for a requested trade group; an absent group yields an empty slice.
    pub fn values(&self, kind: &str) -> &[TradeDisplayRow] {
        self.groups.get(kind).map_or(&[], Vec::as_slice)
    }
}

/// Active display model keyed by native publication token, generation, and row count.
pub(super) struct TradeDisplayCache {
    /// Native publication identity retained for cache-validity comparisons.
    token: Arc<()>,
    /// Publication generation used for change detection and request correlation.
    generation: u64,
    /// Published row count included in the display-cache validity check.
    row_count: usize,
    /// Shared decoded trade display model for the active publication.
    pub model: Arc<TradeDisplayData>,
}

impl TradeWorkspace {
    /// Retire the previous immutable wire publication away from the GUI thread.
    pub fn replace_publication(current: &mut ContractPublication, next: ContractPublication) {
        retire(RetiredDisplay::Publication(Box::new(std::mem::replace(
            current, next,
        ))));
    }
    /// Release the active display-cache reference through the bounded retirement worker.
    pub(super) fn clear_trade_display(&mut self) {
        if let Some(old) = self.display_cache.take() {
            retire(RetiredDisplay::Model(old.model));
        }
    }
    /// Called by publication intake before enqueueing to the GUI thread.
    pub fn prepare_publication(s: &ContractPublication) {
        if s.result.rows.iter().any(|row| {
            row.fields.get("kind").is_some_and(|kind| {
                matches!(
                    kind.as_str(),
                    "established"
                        | "eod"
                        | "market"
                        | "leg"
                        | "order"
                        | "trade"
                        | "account"
                        | "broker-order"
                        | "order-draft"
                )
            })
        }) {
            s.prepared_trade_display.data.get_or_init(|| {
                (
                    s.generation,
                    s.result.rows.len(),
                    Arc::new(TradeDisplayData::build(s)),
                )
            });
        }
    }

    /// Reuse prepared trade rows only when native token, generation, and count match; otherwise prepare and replace the shared display model.
    pub(super) fn prepare_trade_display(
        &mut self,
        s: &ContractPublication,
    ) -> Arc<TradeDisplayData> {
        let matches = self.display_cache.as_ref().is_some_and(|cached| {
            Arc::ptr_eq(&cached.token, &s.render_token)
                && cached.generation == s.generation
                && cached.row_count == s.result.rows.len()
        });
        if !matches {
            let model = s
                .prepared_trade_display
                .data
                .get()
                .filter(|(generation, count, _)| {
                    *generation == s.generation && *count == s.result.rows.len()
                })
                .map(|(_, _, model)| model.clone())
                .unwrap_or_else(|| Arc::new(TradeDisplayData::build(s)));
            let next = TradeDisplayCache {
                token: s.render_token.clone(),
                generation: s.generation,
                row_count: s.result.rows.len(),
                model,
            };
            if let Some(old) = self.display_cache.replace(next) {
                retire(RetiredDisplay::Model(old.model));
            }
        }
        self.display_cache.as_ref().unwrap().model.clone()
    }
}

/// Local regression coverage for the surrounding module invariants.
#[cfg(test)]
mod tests {
    use super::*;
    use crate::contract_editor::ContractRow;
    use crate::models::cells::Cells;
    use serde_json::json;
    /// Build a small deterministic trade publication for the display-cache regression tests.
    fn publication(value: i32) -> ContractPublication {
        serde_json::from_value(json!({"schema":1,"generation":3,"status":"Completed","busy":false,"outcomes":[],
            "result":{"view":"trade-orders","choices":{},"hasMore":false,"rows":[{"id":"plan:1","fields":{"kind":"plan"},
                "data":crate::wire::encode_value(&json!({"totalPnl":value.to_string()})).unwrap()}]}})).unwrap()
    }
    /// Regression check: unchanged frames reuse rows but same generation replacement is fresh.
    #[test]
    fn unchanged_frames_reuse_rows_but_same_generation_replacement_is_fresh() {
        let mut view = TradeWorkspace::default();
        let a = publication(1);
        let first = view.prepare_trade_display(&a);
        assert!(Arc::ptr_eq(&first, &view.prepare_trade_display(&a)));
        let b = publication(2);
        let next = view.prepare_trade_display(&b);
        assert!(!Arc::ptr_eq(&first, &next));
        assert_eq!(first.plans[0].cell("dailyPnl").number(), Some(1.));
        assert_eq!(next.plans[0].cell("dailyPnl").number(), Some(2.));
    }
    /// Regression check: explicit invalidation covers in place updates and generation restart.
    #[test]
    fn explicit_invalidation_covers_in_place_updates_and_generation_restart() {
        let mut view = TradeWorkspace::default();
        let mut s = publication(1);
        let first = view.prepare_trade_display(&s);
        s.result.rows[0].data = Some(
            crate::wire::encode_value(&json!({"totalPnl":"9"}))
                .unwrap()
                .into(),
        );
        s.invalidate_render_cache();
        assert_eq!(
            view.prepare_trade_display(&s).plans[0]
                .cell("dailyPnl")
                .number(),
            Some(9.)
        );
        s.generation = 0;
        assert!(!Arc::ptr_eq(&first, &view.prepare_trade_display(&s)));
    }
    #[test]
    fn unavailable_plan_rows_keep_their_page_positions() {
        let mut s = publication(1);
        s.result.rows.insert(
            0,
            ContractRow {
                id: "plan:unavailable".into(),
                fields: BTreeMap::from([("kind".into(), "plan".into())]),
                data: Some(crate::wire::encode(&true).unwrap().into()),
            },
        );
        let model = TradeDisplayData::build(&s);
        assert_eq!(model.plans.len(), 2);
        assert_eq!(model.values("plan").len(), 2);
        assert!(model.plans[0].cell("dailyPnl").is_missing());
        assert_eq!(model.plans[1].cell("dailyPnl").number(), Some(1.));
    }
    /// Regression check: gui reuses background preparation without decoding again.
    #[test]
    fn gui_reuses_background_preparation_without_decoding_again() {
        let mut s = publication(1);
        s.result.rows.push(ContractRow {
            id: "established:1".into(),
            fields: BTreeMap::from([("kind".into(), "established".into())]),
            data: None,
        });
        TradeWorkspace::prepare_publication(&s);
        let prepared = &s.prepared_trade_display.data.get().unwrap().2;
        let mut view = TradeWorkspace::default();
        assert!(Arc::ptr_eq(prepared, &view.prepare_trade_display(&s)));
    }
    /// Regression check: closing the view releases retired history.
    #[test]
    fn closing_the_view_releases_retired_history() {
        let mut view = TradeWorkspace::default();
        let s = publication(1);
        let weak = Arc::downgrade(&view.prepare_trade_display(&s));
        view.clear_trade_display();
        let until = std::time::Instant::now() + std::time::Duration::from_secs(2);
        while weak.upgrade().is_some() && std::time::Instant::now() < until {
            std::thread::sleep(std::time::Duration::from_millis(1));
        }
        assert!(weak.upgrade().is_none());
        let mut current = publication(1);
        TradeWorkspace::replace_publication(&mut current, publication(2));
        assert_eq!(
            view.prepare_trade_display(&current).plans[0]
                .cell("dailyPnl")
                .number(),
            Some(2.)
        );
    }
}
