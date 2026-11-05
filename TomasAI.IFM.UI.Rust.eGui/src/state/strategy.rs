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
use std::{
    collections::BTreeMap,
    hash::{DefaultHasher, Hash, Hasher},
};
const FRAMES: [&str; 3] = ["Daily", "Weekly", "Monthly"];

/// Strategy Controller used by the strategy component.
pub struct StrategyController {
    /// Symbol associated with this strategy controller.
    symbol: String,
    /// Contract associated with this strategy controller.
    contract: String,
    /// Context required to interpret the current operation or publication.
    context: Option<BusinessContext>,
    /// Frame associated with this strategy controller.
    frame: usize,
    /// Keyed views retained by StrategyController.
    views: BTreeMap<String, (usize, api::GridWorkflow)>,
    /// Hashes detect conflicting live publications at the same revision.
    live_hashes: BTreeMap<String, (u64, u64)>,
    /// The sole retained detail payload is the selected workflow.
    detail: Option<WorkflowView>,
    /// Whether the operator has opened the Details tab.
    details_open: bool,
    /// Keyed signals retained by StrategyController.
    signals: BTreeMap<String, FuturesItiSignalV2ReadModel>,
    /// Keyed tokens retained by StrategyController.
    tokens: BTreeMap<String, u32>,
    /// Next token associated with this strategy controller.
    next_token: u32,
    /// Current selection retained by this state or view.
    selected: Option<String>,
    /// Keyed pages retained by StrategyController.
    pages: BTreeMap<String, u32>,
    /// Keyed totals retained by StrategyController.
    totals: BTreeMap<String, usize>,
    /// Generation associated with this strategy controller.
    generation: u64,
    /// Epoch associated with this strategy controller.
    epoch: String,
    /// Current status text or state exposed by this component.
    status: String,
}
impl Default for StrategyController {
    /// Constructs the initial state used when no explicit value is supplied.
    fn default() -> Self {
        Self {
            symbol: std::env::var("IFM_RUST_SYMBOL").unwrap_or("ES".into()),
            contract: String::new(),
            context: None,
            frame: 0,
            views: BTreeMap::new(),
            live_hashes: BTreeMap::new(),
            detail: None,
            details_open: false,
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
    /// Checks accepts against the current strategy state.
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
        if historical {
            let (start, end) = api::graph_window(context, frame)?;
            return Ok(at >= start && at <= end);
        }
        Ok(true)
    }

    /// Applies signal for strategy controller.
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

    /// Applies view for strategy controller.
    pub fn apply_view(&mut self, registry: &Registry, wire: Value) -> Result<bool, String> {
        let row = WorkflowView::from_backend(registry, &wire)?;
        let header = &row.header;
        if self.context.is_none()
            || !FRAMES.contains(&header.frame.as_str())
            || !header.entity_contract.starts_with(&self.symbol)
        {
            return Ok(false);
        }
        let mut hash = DefaultHasher::new();
        row.evidence.hash(&mut hash);
        let revision = header.revision;
        let digest = hash.finish();
        if let Some((old_revision, old_digest)) = self.live_hashes.get(&header.id) {
            if *old_revision == revision && *old_digest != digest {
                return Err(format!("Conflicting workflow state at revision {revision}"));
            }
        }
        let frame = FRAMES
            .iter()
            .position(|frame| *frame == header.frame)
            .ok_or("Unknown workflow timeframe")?;
        let key = header.id.clone();
        let changed = self.apply_grid(
            frame,
            api::GridWorkflow {
                row: header.row(0)?,
                revision,
                occurred: header.occurred(),
            },
        )?;
        if changed
            || self
                .views
                .get(&key)
                .is_some_and(|(_, item)| item.revision == revision)
        {
            self.live_hashes.insert(key, (revision, digest));
        }
        Ok(changed)
    }

    /// Retains the newest grid revision and keeps its selection token stable.
    fn apply_grid(&mut self, frame: usize, row: api::GridWorkflow) -> Result<bool, String> {
        let key = row.row.id.clone();
        if self
            .views
            .get(&key)
            .is_some_and(|(_, previous)| previous.revision >= row.revision)
        {
            return Ok(false);
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
        self.views.insert(key, (frame, row));
        if self.views.len() > 10_000 {
            return Err("Workflow display bound reached; refresh the view".into());
        }
        Ok(true)
    }

    /// Refreshes strategy for strategy controller.
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
            self.live_hashes.clear();
            self.detail = None;
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
        // Each history lane gets a fresh budget after resolving the session and contract.
        // A slow workflow response must not delay starting any timeframe's signal request.
        let symbol = &self.symbol;
        let pages = &self.pages;
        let requests = frames.into_iter().map(|frame| {
            let context = &context;
            async move {
                let page = if page_only {
                    pages.get(frame).copied().unwrap_or(1) + 1
                } else {
                    1
                };
                let history = async {
                    let (start, end) = api::graph_window(context, frame)?;
                    api::workflow_page(
                        backend,
                        registry,
                        symbol,
                        frame,
                        start,
                        end,
                        page,
                        tokio::time::Instant::now() + backend.default_timeout(),
                    )
                    .await
                };
                let signals = async {
                    if page_only {
                        return None;
                    }
                    Some(
                        api::signal_history(
                            backend,
                            registry,
                            symbol,
                            context,
                            frame,
                            tokio::time::Instant::now() + backend.default_timeout(),
                        )
                        .await,
                    )
                };
                let (history, signals) = tokio::join!(history, signals);
                (frame, page, history, signals)
            }
        });
        let mut errors = vec![];
        for (frame, page, history, signals) in futures_util::future::join_all(requests).await {
            match history {
                Ok((rows, total)) => {
                    let frame_index = FRAMES.iter().position(|name| *name == frame).unwrap();
                    for row in rows {
                        if let Err(error) = self.apply_grid(frame_index, row) {
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
            if let Some(signals) = signals {
                match signals {
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

    /// Provides action for the strategy controller implementation.
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
            14 => self.details_open = true,
            15 => {
                self.details_open = false;
                self.detail = None;
            }
            2 | 13 => {}
            _ => return Err("Unknown strategy action".into()),
        }
        Ok(())
    }

    /// Provides snapshot for the strategy controller implementation.
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
        let (start, mut end) = api::graph_window(context, frame)?;
        let mut views = self
            .views
            .values()
            .filter(|(index, row)| *index == self.frame && row.occurred >= start)
            .map(|(_, row)| row)
            .collect::<Vec<_>>();
        views.sort_by(|left, right| {
            (right.occurred, &right.row.id).cmp(&(left.occurred, &left.row.id))
        });
        if self
            .selected
            .as_ref()
            .is_none_or(|id| !views.iter().any(|view| &view.row.id == id))
        {
            self.selected = views.first().map(|view| view.row.id.clone());
        }
        result.workflow_id = self.selected.clone().unwrap_or_default();
        for view in views {
            end = end.max(view.occurred);
            let mut row = view.row.clone();
            row.action = self.tokens[&row.id];
            result.workflows.push(WorkflowChoice {
                id: row.id.clone(),
                label: format!(
                    "{} {} {}",
                    view.occurred.format("%H:%M:%S"),
                    row.outcome,
                    row.id
                ),
                action: row.action,
            });
            result.rows.push(row);
        }
        if let Some(detail) = self.detail.as_ref().filter(|detail| {
            detail.header.id == result.workflow_id
                && self
                    .views
                    .get(&result.workflow_id)
                    .is_some_and(|(_, item)| detail.header.revision >= item.revision)
        }) {
            let details =
                display::details(registry, &crate::backend::codec::decode(&detail.evidence)?)?;
            result.revision = details.revision;
            result.roots = details.roots;
            result.header = details.header;
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
            {
                continue;
            }
            end = end.max(at);
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

    /// Fetches details for the selected execution only while Details is open.
    pub async fn load_detail(
        &mut self,
        backend: &Backend,
        registry: &Registry,
    ) -> Result<bool, String> {
        if !self.details_open {
            return Ok(false);
        }
        let Some((id, (_, item))) = self
            .selected
            .as_ref()
            .and_then(|id| self.views.get_key_value(id))
        else {
            return Ok(false);
        };
        if self.detail.as_ref().is_some_and(|detail| {
            detail.header.id == *id && detail.header.revision >= item.revision
        }) {
            return Ok(false);
        }
        self.detail = Some(api::workflow_detail(backend, registry, id, item.revision).await?);
        Ok(true)
    }

    /// Sets error for strategy controller.
    pub fn set_error(&mut self, error: &str) {
        self.status = error.into();
    }
}

#[cfg(test)]
mod typed_state_tests {
    use super::*;

    /// Verifies that workflow revisions and signal deduplication preserve typed publications.
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
        let signal_at = signal_time(&FuturesItiSignalV2ReadModel::from_backend(&signal).unwrap()).unwrap();
        let before_live = signal_at.min(header.occurred()) - chrono::Duration::seconds(1);
        state.context.as_mut().unwrap().as_of_utc = before_live;
        state.views.clear();
        state.live_hashes.clear();
        assert!(state.apply_view(&registry, wire).unwrap());
        assert!(!state.apply_signal(&registry, signal.clone(), true).unwrap());
        assert!(state.apply_signal(&registry, signal.clone(), false).unwrap());
        assert!(!state.apply_signal(&registry, signal, false).unwrap());
        let snapshot = state.snapshot(&registry).unwrap();
        assert_eq!(snapshot.rows.len(), 1);
        assert_eq!(snapshot.signals.len(), 1);
        assert_eq!(
            snapshot.rows[0].trigger_identity,
            snapshot.signals[0].identity
        );
        assert_eq!(snapshot.workflow_id, header.id);
        assert_eq!(
            snapshot.graph_end_utc,
            signal_at.max(header.occurred()).timestamp_millis() as f64 / 1000.0
        );
    }
}
