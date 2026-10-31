//! Read-only live strategy owner. Full snapshots replace older revisions, while
//! historical signal observations are deduplicated independently of workflow
//! selection. Its API clock and session are refreshed without a local-date fallback.
use crate::models::{
    cells::Cells,
    strategy::{WorkflowView, signal_identity, signal_time},
    trade_contracts::FuturesItiSignalV2ReadModel,
    wire_types::FromBackend,
};
use crate::{
    Snapshot,
    backend::{schema::Registry, transport::Backend},
    services::{
        business_context::BusinessContext, market_history, strategy as api,
        strategy_display as display,
    },
};
use crate::{WorkflowChoice, strategy_workspace::GraphPoint};
use rmpv::Value;
use std::collections::BTreeMap;
const FRAMES: [&str; 3] = ["Daily", "Weekly", "Monthly"];
pub struct StrategyController {
    symbol: String,
    contract: String,
    context: Option<BusinessContext>,
    frame: usize,
    views: BTreeMap<String, WorkflowView>,
    signals: BTreeMap<String, FuturesItiSignalV2ReadModel>,
    tokens: BTreeMap<String, u32>,
    next_token: u32,
    selected: Option<String>,
    pages: BTreeMap<String, u32>,
    totals: BTreeMap<String, usize>,
    generation: u64,
    epoch: String,
    status: String,
}
impl Default for StrategyController {
    fn default() -> Self {
        Self {
            symbol: std::env::var("IFM_RUST_SYMBOL").unwrap_or("ES".into()),
            contract: String::new(),
            context: None,
            frame: 0,
            views: BTreeMap::new(),
            signals: BTreeMap::new(),
            tokens: BTreeMap::new(),
            next_token: 1000,
            selected: None,
            pages: BTreeMap::new(),
            totals: BTreeMap::new(),
            generation: 0,
            epoch: uuid::Uuid::new_v4().to_string(),
            status: "Connecting and loading workflows".into(),
        }
    }
}
impl StrategyController {
    fn accepts(
        &self,
        contract: &str,
        frame: &str,
        at: chrono::DateTime<chrono::Utc>,
        historical: bool,
    ) -> Result<bool, String> {
        let Some(context) = &self.context else {
            return Ok(false);
        };
        if !FRAMES.contains(&frame)
            || if historical {
                !contract.starts_with(&self.symbol)
            } else {
                contract != self.contract
            }
        {
            return Ok(false);
        }
        let (start, end) = api::graph_window(context, frame)?;
        Ok(at >= start && at <= end)
    }
    pub fn apply_signal(
        &mut self,
        _registry: &Registry,
        wire: Value,
        historical: bool,
    ) -> Result<bool, String> {
        let row = FuturesItiSignalV2ReadModel::from_backend(&wire)?;
        if !self.accepts(
            row.contract_id.as_deref().unwrap_or_default(),
            row.time_period
                .as_ref()
                .map(|value| value.label())
                .as_deref()
                .unwrap_or_default(),
            signal_time(&row)?,
            historical,
        )? {
            return Ok(false);
        }
        let key = signal_identity(&row)?;
        if self.signals.contains_key(&key) {
            return Ok(false);
        }
        self.signals.insert(key, row);
        if self.signals.len() > 100_000 {
            let oldest = self
                .signals
                .iter()
                .filter_map(|(key, row)| signal_time(row).ok().map(|at| (at, key.clone())))
                .min()
                .map(|(_, key)| key);
            if let Some(key) = oldest {
                self.signals.remove(&key);
            }
            self.status =
                "Signal display bound reached; older display observations were omitted.".into();
        }
        Ok(true)
    }
    pub fn apply_view(&mut self, registry: &Registry, wire: Value) -> Result<bool, String> {
        let row = WorkflowView::from_backend(registry, &wire)?;
        let header = &row.header;
        if !self.accepts(
            &header.entity_contract,
            &header.frame,
            header.occurred(),
            true,
        )? {
            return Ok(false);
        }
        let key = header.id.clone();
        let revision = header.revision;
        if let Some(old) = self.views.get(&key) {
            if old.header.revision > revision {
                return Ok(false);
            }
            if old.header.revision == revision {
                if old.evidence != row.evidence {
                    return Err(format!("Conflicting workflow state at revision {revision}"));
                }
                return Ok(false);
            }
        }
        if !self.tokens.contains_key(&key) {
            let token = self.next_token;
            self.next_token = self
                .next_token
                .checked_add(1)
                .filter(|value| *value < 0x1000_0000)
                .ok_or("Workflow selection token bound reached")?;
            self.tokens.insert(key.clone(), token);
        }
        self.views.insert(key, row);
        if self.views.len() > 10_000 {
            return Err("Workflow display bound reached; refresh the view".into());
        }
        Ok(true)
    }
    pub async fn refresh(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        page_only: bool,
    ) -> Result<(), String> {
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let context = tokio::time::timeout_at(deadline, BusinessContext::load(backend))
            .await
            .map_err(|_| "Strategy API clock deadline expired")??;
        let contract = tokio::time::timeout_at(
            deadline,
            market_history::current_contract(backend, registry, &self.symbol),
        )
        .await
        .map_err(|_| "Strategy contract deadline expired")??;
        if contract.is_empty() {
            return Err("No current strategy futures contract".into());
        }
        if self
            .context
            .as_ref()
            .is_some_and(|v| v.value_date != context.value_date)
            || !self.contract.is_empty() && self.contract != contract
        {
            self.views.clear();
            self.signals.clear();
            self.pages.clear();
            self.totals.clear();
            self.tokens.clear();
            self.selected = None;
            self.epoch = uuid::Uuid::new_v4().to_string();
        }
        self.context = Some(context.clone());
        self.contract = contract;
        let frames = if page_only {
            vec![FRAMES[self.frame]]
        } else {
            FRAMES.to_vec()
        };
        // Hydration is read-only. A failing lane remains visibly stale while other
        // timeframes continue to refresh within the same absolute deadline.
        let mut errors = vec![];
        for frame in frames {
            let (start, end) = api::graph_window(&context, frame)?;
            let page = if page_only {
                self.pages.get(frame).copied().unwrap_or(1) + 1
            } else {
                1
            };
            let history = api::workflow_page(
                backend,
                registry,
                &self.symbol,
                frame,
                start,
                end,
                page,
                deadline,
            )
            .await;
            match history {
                Ok((rows, total)) => {
                    for row in rows {
                        if let Err(error) = self.apply_view(registry, row) {
                            errors.push(error);
                        }
                    }
                    self.pages
                        .entry(frame.into())
                        .and_modify(|v| *v = (*v).max(page))
                        .or_insert(page);
                    self.totals.insert(frame.into(), total);
                }
                Err(error) => errors.push(format!("{frame} workflow history: {error}")),
            }
            if !page_only {
                match api::signal_history(
                    backend,
                    registry,
                    &self.symbol,
                    &context,
                    frame,
                    deadline,
                )
                .await
                {
                    Ok(rows) => {
                        for row in rows {
                            if let Err(error) = self.apply_signal(registry, row, true) {
                                errors.push(error);
                            }
                        }
                    }
                    Err(error) => errors.push(format!("{frame} ITI history: {error}")),
                }
            }
        }
        self.status = if errors.is_empty() {
            "Listening for strategy and ITI updates".into()
        } else {
            errors.join("; ")
        };
        Ok(())
    }
    pub fn action(&mut self, action: u32) -> Result<(), String> {
        match action {
            10..=12 => {
                self.frame = (action - 10) as usize;
                self.selected = None;
            }
            1000.. => {
                let key = self
                    .tokens
                    .iter()
                    .find(|(_, v)| **v == action)
                    .map(|(k, _)| k.clone())
                    .ok_or("Workflow selection no longer exists")?;
                self.selected = Some(key);
            }
            2 | 13 => {}
            _ => return Err("Unknown strategy action".into()),
        }
        Ok(())
    }
    pub fn snapshot(&mut self, registry: &Registry) -> Result<Snapshot, String> {
        self.generation += 1;
        let frame = FRAMES[self.frame];
        let mut result = Snapshot {
            schema: 1,
            epoch: self.epoch.clone(),
            generation: self.generation,
            mode: "Live".into(),
            status: self.status.clone(),
            ..Default::default()
        };
        let Some(context) = self.context.as_ref() else {
            return Ok(result);
        };
        let (start, end) = api::graph_window(context, frame)?;
        let mut views = self
            .views
            .values()
            .filter(|view| {
                view.header.frame == frame
                    && view.header.occurred() >= start
                    && view.header.occurred() <= end
            })
            .collect::<Vec<_>>();
        views.sort_by_key(|view| {
            std::cmp::Reverse((view.header.occurred(), view.header.id.clone()))
        });
        if self
            .selected
            .as_ref()
            .is_none_or(|id| !views.iter().any(|view| &view.header.id == id))
        {
            self.selected = views.first().map(|view| view.header.id.clone());
        }
        for view in &views {
            let header = &view.header;
            let token = self.tokens[&header.id];
            result.rows.push(header.row(token)?);
            result.workflows.push(WorkflowChoice {
                id: header.id.clone(),
                label: format!(
                    "{} {} {}",
                    header.occurred().format("%H:%M:%S"),
                    header.outcome,
                    header.id
                ),
                action: token,
            });
            if self.selected.as_ref() == Some(&header.id) {
                let details =
                    display::details(registry, &crate::backend::codec::decode(&view.evidence)?)?;
                result.workflow_id = details.workflow_id;
                result.revision = details.revision;
                result.roots = details.roots;
                result.header = details.header;
            }
        }
        for signal in self.signals.values() {
            let at = signal_time(signal)?;
            if signal
                .time_period
                .as_ref()
                .map(|value| value.label())
                .as_deref()
                != Some(frame)
                || at < start
                || at > end
            {
                continue;
            }
            let mode = signal.cell("IntrinsicTimeMode").text();
            let price = signal
                .cell("IntrinsicPrice")
                .number()
                .ok_or("Invalid ITI plotted price")?;
            let direction = if mode == "TrendDirectionChanged" {
                if signal.cell("IntrinsicTimeTrend").text() == "UpTrend" {
                    "Up"
                } else {
                    "Down"
                }
            } else {
                "Other"
            };
            let local = at.with_timezone(&chrono_tz::America::New_York);
            result.signals.push(GraphPoint {
                identity: signal_identity(signal)?,
                utc_seconds: at.timestamp_millis() as f64 / 1000.,
                market_seconds: Some(market_history::market_seconds(at)),
                price,
                direction: direction.into(),
                tooltip: format!(
                    "{} ET | {mode} | {price:.2}",
                    local.format("%Y-%m-%d %-I:%M:%S %p")
                ),
            });
        }
        result.signals.sort_by(|a, b| {
            a.utc_seconds
                .partial_cmp(&b.utc_seconds)
                .unwrap_or(std::cmp::Ordering::Equal)
                .then_with(|| a.identity.cmp(&b.identity))
        });
        let page = self.pages.get(frame).copied().unwrap_or(1) as usize;
        let total = self.totals.get(frame).copied().unwrap_or(result.rows.len());
        result.page_number = page;
        result.page_count = total.div_ceil(50).max(1);
        result.total_rows = total;
        result.has_more = page * 50 < total;
        result.graph_start_utc = start.timestamp_millis() as f64 / 1000.;
        result.graph_end_utc = end.timestamp_millis() as f64 / 1000.;
        result.graph_start_market = Some(market_history::market_seconds(start));
        result.graph_end_market = Some(market_history::market_seconds(end));
        result.contract_id = self.contract.clone();
        result.time_frame = frame.into();
        Ok(result)
    }
    pub fn set_error(&mut self, error: &str) {
        self.status = error.into();
    }
}

#[cfg(test)]
mod typed_state_tests {
    use super::*;
    #[test]
    fn workflow_revisions_and_signal_deduplication_preserve_typed_publications() {
        let registry = Registry::compiled().unwrap();
        let wire = crate::backend::codec::decode(include_bytes!(
            "../../tests/fixtures/backend/strategy-view.msgpack"
        ))
        .unwrap();
        let header =
            crate::models::strategy::WorkflowHeader::from_backend(&registry, &wire).unwrap();
        let date = chrono::NaiveDate::parse_from_str(&header.entity_date, "%Y-%m-%d").unwrap();
        let mut state = StrategyController::default();
        state.contract = header.entity_contract.clone();
        state.context = Some(BusinessContext {
            value_date: date,
            active_value_date: Some(date),
            as_of_utc: header.occurred() + chrono::Duration::hours(1),
            revision: 1,
            market_state: 2,
            allow_development_emulator: false,
        });
        assert!(state.apply_view(&registry, wire.clone()).unwrap());
        assert!(!state.apply_view(&registry, wire.clone()).unwrap());
        let mut conflict = wire.clone();
        let shape = registry.describe(display::VIEW).unwrap();
        let slot = shape
            .members
            .iter()
            .find(|(name, _, _)| name == "StopReasonCode")
            .unwrap()
            .1;
        let Value::Array(values) = &mut conflict else {
            panic!("Expected numeric workflow contract")
        };
        values[slot] = "conflict at same revision".into();
        assert!(
            state
                .apply_view(&registry, conflict)
                .unwrap_err()
                .contains("Conflicting workflow state")
        );
        let trigger = registry
            .wire_field(display::VIEW, "TriggerEvent", &wire)
            .unwrap();
        let kind = registry.field_kind(display::VIEW, "TriggerEvent").unwrap();
        let signal = registry
            .wire_field(kind, "FuturesItiSignal", trigger)
            .unwrap()
            .clone();
        assert!(state.apply_signal(&registry, signal.clone(), true).unwrap());
        assert!(!state.apply_signal(&registry, signal, true).unwrap());
        let snapshot = state.snapshot(&registry).unwrap();
        assert_eq!(snapshot.rows.len(), 1);
        assert_eq!(snapshot.signals.len(), 1);
        assert_eq!(
            snapshot.rows[0].trigger_identity,
            snapshot.signals[0].identity
        );
        assert_eq!(snapshot.workflow_id, header.id);
    }
}
