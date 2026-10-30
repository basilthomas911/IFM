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
use serde_json::{Value, json};
use std::sync::Arc;

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
    groups: BTreeMap<String, Vec<Value>>,
    /// Flattened display-ready rows for the currently published trade-plan window.
    pub plans: Vec<Value>,
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
    /// Decode the relevant trade row groups once and prepare flattened plans, date indexes, and recent chart points.
    fn build(s: &ContractPublication) -> Self {
        let mut model = Self::default();
        // Decode once per received publication, not once per widget or pointer event.
        for row in &s.result.rows {
            let Some(kind) = row.fields.get("kind") else {
                continue;
            };
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
                let chart = row.data.as_ref()
                    .map(|bytes| crate::models::trade_chart::PreparedTradeChart::from_messagepack(bytes))
                    .and_then(Result::ok).unwrap_or_default();
                model.chart_end_utc_seconds = chart.end_utc_seconds;
                model.chart_points = chart.net;
                model.forward_chart_points = chart.forward;
                continue;
            }
            let mut value = data(row);
            if kind == "plan" {
                present_forward_risk(&mut value["ironCondorTradePlanSnapshot"]);
            }
            if kind == "realtime" && value["plan"].is_object() {
                present_forward_risk(&mut value["plan"]["ironCondorTradePlanSnapshot"]);
                if value["lastAvailablePlan"].is_object() {
                    present_forward_risk(
                        &mut value["lastAvailablePlan"]["ironCondorTradePlanSnapshot"],
                    );
                }
            }
            if kind == "plan" {
                if let Some(display) = row.fields.get("actionDateTimeEastern") {
                    value["actionDateTimeEastern"] = if display.is_empty() {
                        Value::Null
                    } else {
                        json!(display)
                    };
                }
                let index = model.groups.get("plan").map_or(0, Vec::len);
                // First match preserves the historical lookup's ordering.
                model
                    .plan_dates
                    .entry(value["valueDate"].to_string())
                    .or_insert(index);
                let mut plan = if value["ironCondorTradePlanSnapshot"].is_object() {
                    value["ironCondorTradePlanSnapshot"].clone()
                } else {
                    json!({"actionDateTime":value["calculatedAtUtc"], "actionType":value["action"],
                    "actionState":value["state"], "actionReason":value["explanation"], "dailyPnl":value["totalPnl"]})
                };
                if let Some(display) = value.get("actionDateTimeEastern") {
                    plan["actionDateTime"] = display.clone();
                }
                model.has_action_reason |= !plan["actionReason"].is_null();
                model.plans.push(plan);
            }
            model.groups.entry(kind.clone()).or_default().push(value);
        }
        model
    }

    /// Borrow all decoded rows for a requested trade group; an absent group yields an empty slice.
    pub fn values(&self, kind: &str) -> &[Value] {
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
            row.fields
                .get("kind")
                .is_some_and(|kind| kind == "established")
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
    /// Build a small deterministic trade publication for the display-cache regression tests.
    fn publication(value: i32) -> ContractPublication {
        serde_json::from_value(json!({"schema":1,"generation":3,"status":"Completed","busy":false,"outcomes":[],
            "result":{"view":"trade-orders","choices":{},"hasMore":false,"rows":[{"id":"plan:1","fields":{"kind":"plan"},
                "data":crate::wire::encode_value(&json!({"totalPnl":value})).unwrap()}]}})).unwrap()
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
        assert_eq!(first.plans[0]["dailyPnl"], 1);
        assert_eq!(next.plans[0]["dailyPnl"], 2);
    }
    /// Regression check: explicit invalidation covers in place updates and generation restart.
    #[test]
    fn explicit_invalidation_covers_in_place_updates_and_generation_restart() {
        let mut view = TradeWorkspace::default();
        let mut s = publication(1);
        let first = view.prepare_trade_display(&s);
        s.result.rows[0].data = Some(
            crate::wire::encode_value(&json!({"totalPnl":9}))
                .unwrap()
                .into(),
        );
        s.invalidate_render_cache();
        assert_eq!(view.prepare_trade_display(&s).plans[0]["dailyPnl"], 9);
        s.generation = 0;
        assert!(!Arc::ptr_eq(&first, &view.prepare_trade_display(&s)));
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
        assert_eq!(view.prepare_trade_display(&current).plans[0]["dailyPnl"], 2);
    }
}

/// Binds backend-computed signed risk to existing display fields without financial arithmetic.
/// Historical rows without this versioned payload retain their original meanings.
fn present_forward_risk(detail: &mut Value) {
    let Some(risk) = detail
        .get("ironCondorForwardRisk")
        .filter(|risk| risk.is_object())
        .cloned()
    else {
        return;
    };
    for (display, source) in [
        ("forwardLoss", "forwardLoss"),
        ("forwardLossRatio", "forwardLossRatio"),
        ("lossHeadroom", "forwardLossHeadroom"),
    ] {
        detail[display] = risk[source].clone();
    }
}
