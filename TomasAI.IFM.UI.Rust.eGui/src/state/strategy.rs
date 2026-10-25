//! Read-only live strategy owner. Full snapshots replace older revisions, while
//! historical signal observations are deduplicated independently of workflow
//! selection. Its API clock and session are refreshed without a local-date fallback.
use crate::{
    Snapshot,
    backend::{schema::Registry, transport::Backend},
    services::{
        business_context::BusinessContext, market_history, strategy as api,
        strategy_display as display,
    },
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const FRAMES: [&str; 3] = ["Daily", "Weekly", "Monthly"];
fn text<'a>(row: &'a Json, key: &str) -> &'a str {
    row[key].as_str().unwrap_or_default()
}
fn time(value: &Json) -> Result<chrono::DateTime<chrono::Utc>, String> {
    chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing strategy timestamp")?)
        .map(|v| v.to_utc())
        .map_err(|_| "Invalid strategy timestamp".into())
}
fn id(row: &Json) -> Result<String, String> {
    let id = uuid::Uuid::parse_str(
        row["WorkflowId"]["Value"]
            .as_str()
            .ok_or("Missing workflow identity")?,
    )
    .map_err(|_| "Invalid workflow identity")?;
    if id.is_nil() {
        return Err("Missing workflow identity".into());
    }
    Ok(id.simple().to_string())
}
pub struct StrategyController {
    symbol: String,
    contract: String,
    context: Option<BusinessContext>,
    frame: usize,
    views: BTreeMap<String, Value>,
    signals: BTreeMap<String, Value>,
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
        registry: &Registry,
        wire: Value,
        historical: bool,
    ) -> Result<bool, String> {
        let row = registry.project(display::SIGNAL, &wire)?;
        if !self.accepts(
            text(&row, "ContractId"),
            text(&row, "TimePeriod"),
            time(&row["IntrinsicTime"])?,
            historical,
        )? {
            return Ok(false);
        }
        let key = display::signal_identity(&row)?;
        if self.signals.contains_key(&key) {
            return Ok(false);
        }
        self.signals.insert(key, wire);
        if self.signals.len() > 100_000 {
            let oldest = self
                .signals
                .iter()
                .filter_map(|(k, v)| {
                    registry
                        .project(display::SIGNAL, v)
                        .ok()
                        .and_then(|v| time(&v["IntrinsicTime"]).ok())
                        .map(|time| (time, k.clone()))
                })
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
        let row = registry.project(display::VIEW, &wire)?;
        let iti = &row["EntityId"]["ItiSignalEntityId"];
        let at = if time(&row["TriggerEvent"]["CreatedOn"])?.timestamp() <= -62_135_596_800 {
            time(&row["StartedAtUtc"])?
        } else {
            time(&row["TriggerEvent"]["CreatedOn"])?
        };
        if !self.accepts(text(iti, "ContractId"), text(iti, "TimePeriod"), at, true)? {
            return Ok(false);
        }
        let key = id(&row)?;
        let revision = row["WorkflowRevision"]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or("Invalid workflow revision")?;
        if let Some(old) = self.views.get(&key) {
            let old = registry.project(display::VIEW, old)?;
            let existing = old["WorkflowRevision"]
                .as_i64()
                .ok_or("Invalid cached workflow revision")?;
            if existing > revision {
                return Ok(false);
            }
            if existing == revision {
                if self.views[&key] != wire {
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
                .filter(|v| *v < 0x1000_0000)
                .ok_or("Workflow selection token bound reached")?;
            self.tokens.insert(key.clone(), token);
        }
        self.views.insert(key, wire);
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
        let mut views = self
            .views
            .values()
            .map(|wire| {
                let row = registry.project(display::VIEW, wire)?;
                let at = if time(&row["TriggerEvent"]["CreatedOn"])?.timestamp() <= -62_135_596_800
                {
                    time(&row["StartedAtUtc"])?
                } else {
                    time(&row["TriggerEvent"]["CreatedOn"])?
                };
                Ok((at, row, wire))
            })
            .collect::<Result<Vec<_>, String>>()?;
        views.retain(|(at, v, _)| {
            v["EntityId"]["ItiSignalEntityId"]["TimePeriod"] == frame
                && self.context.as_ref().is_some_and(|context| {
                    api::graph_window(context, frame)
                        .is_ok_and(|(start, end)| *at >= start && *at <= end)
                })
        });
        views.sort_by_key(|(at, v, _)| std::cmp::Reverse((*at, id(v).unwrap_or_default())));
        let ids = views
            .iter()
            .map(|(_, row, _)| id(row))
            .collect::<Result<Vec<_>, _>>()?;
        if self.selected.as_ref().is_none_or(|id| !ids.contains(id)) {
            self.selected = ids.first().cloned();
        }
        let mut rows = vec![];
        let mut choices = vec![];
        let mut details = json!({"workflowId":"","revision":0,"roots":[],"header":""});
        for ((at, row, wire), key) in views.iter().zip(ids) {
            let token = self.tokens[&key];
            rows.push(display::row(registry, wire, token)?);
            choices.push(json!({"id":key,"label":format!("{} {} {}",at.format("%H:%M:%S"),text(row,"Outcome"),key),"action":token}));
            if self.selected.as_ref() == Some(&key) {
                details = display::details(registry, wire)?;
            }
        }
        let (start, end) = if let Some(context) = &self.context {
            api::graph_window(context, frame)?
        } else {
            return serde_json::from_value(json!({"schema":1,"epoch":self.epoch,"generation":self.generation,"mode":"Live","workflowId":"","revision":0,"roots":[],"status":self.status})).map_err(|e|e.to_string());
        };
        let mut signals = vec![];
        for wire in self.signals.values() {
            let row = registry.project(display::SIGNAL, wire)?;
            let at = time(&row["IntrinsicTime"])?;
            if row["TimePeriod"] != frame || at < start || at > end {
                continue;
            }
            let mode = text(&row, "IntrinsicTimeMode");
            let price = row["IntrinsicPrice"]
                .as_f64()
                .filter(|v| v.is_finite())
                .ok_or("Invalid ITI plotted price")?;
            let local = at.with_timezone(&chrono_tz::America::New_York);
            let direction = if mode == "TrendDirectionChanged" {
                if row["IntrinsicTimeTrend"] == "UpTrend" {
                    "Up"
                } else {
                    "Down"
                }
            } else {
                "Other"
            };
            signals.push(json!({"identity":display::signal_identity(&row)?,"utcSeconds":at.timestamp_millis()as f64/1000.0,"marketSeconds":market_history::market_seconds(at),"price":price,"direction":direction,"tooltip":format!("{} ET | {mode} | {price:.2}",local.format("%Y-%m-%d %-I:%M:%S %p"))}));
        }
        signals.sort_by(|a, b| {
            a["utcSeconds"]
                .as_f64()
                .partial_cmp(&b["utcSeconds"].as_f64())
                .unwrap_or(std::cmp::Ordering::Equal)
                .then_with(|| text(a, "identity").cmp(text(b, "identity")))
        });
        let page = self.pages.get(frame).copied().unwrap_or(1);
        let total = self.totals.get(frame).copied().unwrap_or(rows.len());
        let mut result = details;
        let fields = result
            .as_object_mut()
            .ok_or("Invalid strategy details projection")?;
        for (key, value) in [
            ("schema", json!(1)),
            ("epoch", json!(self.epoch)),
            ("generation", json!(self.generation)),
            ("mode", json!("Live")),
            ("status", json!(self.status)),
            ("rows", json!(rows)),
            ("workflows", json!(choices)),
            ("signals", json!(signals)),
            ("pageNumber", json!(page)),
            ("pageCount", json!(total.div_ceil(50).max(1))),
            ("totalRows", json!(total)),
            ("hasMore", json!(page as usize * 50 < total)),
            (
                "graphStartUtc",
                json!(start.timestamp_millis() as f64 / 1000.0),
            ),
            ("graphEndUtc", json!(end.timestamp_millis() as f64 / 1000.0)),
            (
                "graphStartMarket",
                json!(market_history::market_seconds(start)),
            ),
            ("graphEndMarket", json!(market_history::market_seconds(end))),
            ("contractId", json!(self.contract)),
            ("timeFrame", json!(frame)),
        ] {
            fields.insert(key.into(), value);
        }
        serde_json::from_value(result).map_err(|e| e.to_string())
    }
    pub fn set_error(&mut self, error: &str) {
        self.status = error.into();
    }
}
