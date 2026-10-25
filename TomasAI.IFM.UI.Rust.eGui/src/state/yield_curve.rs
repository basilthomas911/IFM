use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{actor_requests, business_context::BusinessContext},
};
use chrono::{Datelike, NaiveDate};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;

const QUERY: &str = "TomasAI.IFM.Domain.MarketData.Shared.Queries.";
const COMMAND: &str = "TomasAI.IFM.Domain.MarketData.Shared.Commands.";
const EVENT: &str = "TomasAI.IFM.Domain.MarketData.Shared.Events.";
const RATES: [&str; 12] = [
    "oneMonth",
    "twoMonth",
    "threeMonth",
    "sixMonth",
    "oneYear",
    "twoYear",
    "threeYear",
    "fiveYear",
    "sevenYear",
    "tenYear",
    "twentyYear",
    "thirtyYear",
];
#[derive(Default)]
pub struct YieldController {
    rows: Vec<ContractRow>,
    periods: Vec<String>,
    selected: String,
    range: Option<(NaiveDate, NaiveDate)>,
    generation: u64,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
fn date(text: &str) -> Result<NaiveDate, String> {
    NaiveDate::parse_from_str(text, "%Y-%m-%d").map_err(|_| "Enter dates as yyyy-MM-dd".into())
}
pub fn range(period: &str, now: NaiveDate) -> Result<(NaiveDate, NaiveDate), String> {
    let (start, end) = if period == "Current Month" {
        let start =
            NaiveDate::from_ymd_opt(now.year(), now.month(), 1).ok_or("Invalid current month")?;
        let end = if now.month() == 12 {
            NaiveDate::from_ymd_opt(now.year(), 12, 31)
        } else {
            NaiveDate::from_ymd_opt(now.year(), now.month() + 1, 1).and_then(|d| d.pred_opt())
        }
        .ok_or("Invalid month end")?;
        (start, end)
    } else {
        let year = period
            .parse::<i32>()
            .ok()
            .filter(|y| (1..=9999).contains(y))
            .ok_or("Invalid yield-curve period")?;
        (
            NaiveDate::from_ymd_opt(year, 1, 1).unwrap(),
            NaiveDate::from_ymd_opt(year, 12, 31).unwrap(),
        )
    };
    Ok((start, end))
}
impl YieldController {
    async fn load(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        now: NaiveDate,
        preferred: Option<String>,
    ) -> Result<(), String> {
        let value = actor_requests::query(
            backend,
            registry,
            &format!("{QUERY}GetYieldCurveRateYearsQuery"),
            "YieldCurveRateYears",
            BTreeMap::new(),
        )
        .await?;
        let years = value
            .as_array()
            .and_then(|a| a.first())
            .and_then(Value::as_array)
            .ok_or("Invalid yield-curve years")?;
        let mut periods = vec!["Current Month".to_string()];
        for year in years {
            let year = year
                .as_i64()
                .filter(|y| (1..=9999).contains(y))
                .ok_or("Invalid stored curve year")?
                .to_string();
            if !periods.contains(&year) {
                periods.push(year);
            }
        }
        let selected = preferred
            .filter(|p| periods.contains(p))
            .or_else(|| {
                periods
                    .contains(&self.selected)
                    .then(|| self.selected.clone())
            })
            .unwrap_or_else(|| periods[0].clone());
        let window = range(&selected, now)?;
        let rows = self.read_rates(backend, registry, window).await?;
        self.periods = periods;
        self.selected = selected;
        self.range = Some(window);
        self.rows = rows;
        Ok(())
    }
    async fn read_rates(
        &self,
        backend: &Backend,
        registry: &Registry,
        window: (NaiveDate, NaiveDate),
    ) -> Result<Vec<ContractRow>, String> {
        let body = actor_requests::query(
            backend,
            registry,
            &format!("{QUERY}GetYieldCurveRatesQuery"),
            &format!("{}.{}", window.0, window.1),
            BTreeMap::from([
                ("StartDate".into(), date_only_wire(window.0)),
                ("EndDate".into(), date_only_wire(window.1)),
            ]),
        )
        .await?;
        body.as_array()
            .ok_or("Invalid yield-curve rows")?
            .iter()
            .map(|value| {
                let fields = value
                    .as_array()
                    .filter(|a| a.len() >= 13)
                    .ok_or("Incomplete yield curve")?;
                let day = date_only(&fields[0])?;
                let mut row = BTreeMap::from([("valueDate".into(), day.to_string())]);
                for (index, key) in RATES.iter().enumerate() {
                    let rate = fields[index + 1]
                        .as_f64()
                        .filter(|v| v.is_finite())
                        .ok_or("Invalid yield rate")?;
                    row.insert((*key).into(), rate.to_string());
                }
                Ok(ContractRow {
                    id: day.to_string(),
                    fields: row,
                    ..Default::default()
                })
            })
            .collect()
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        let id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        if intent["schema"] != 1 || intent["view"] != "yield-curve" {
            return Err("Invalid yield-curve intent".into());
        }
        let context = BusinessContext::load(backend).await?;
        if let Some((prior, outcome)) = self.history.get(&id) {
            if prior != intent {
                return Err("RequestId belongs to another yield request".into());
            }
            let outcome = outcome.clone();
            self.generation += 1;
            return Ok(self.snapshot(context.value_date, outcome));
        }
        let action = intent["intent"].as_str().ok_or("Missing yield action")?;
        if self.history.len() >= 256
            && self
                .history
                .values()
                .all(|(_, outcome)| outcome.state == "Unknown")
        {
            return Err(
                "Reconcile uncertain curve operations before submitting more requests".into(),
            );
        }
        if action != "load"
            && (self.range.is_none()
                || intent["expectedGeneration"].as_u64() != Some(self.generation))
        {
            return Err("Yield curves changed; reload before editing".into());
        }
        let field = |key: &str| intent["fields"][key].as_str().unwrap_or_default().trim();
        let result = match action {
            "load" => self.load(backend, registry, context.value_date, None).await,
            "range" => {
                let selected = field("timePeriod");
                if !self.periods.iter().any(|p| p == selected) {
                    return Err("Select a published time period".into());
                }
                let window = range(selected, context.value_date)?;
                match self.read_rates(backend, registry, window).await {
                    Ok(rows) => {
                        self.rows = rows;
                        self.selected = selected.into();
                        self.range = Some(window);
                        Ok(())
                    }
                    Err(e) => Err(e),
                }
            }
            "add" | "change" | "remove" | "import" => {
                self.mutate(backend, registry, intent, &id, context.value_date)
                    .await
            }
            _ => return Err("Unsupported yield action".into()),
        };
        let (state, message) = match result {
            Ok(()) => ("Completed", "Completed".to_string()),
            Err(e) => (
                if e.starts_with("OutcomeUnknown:") {
                    "Unknown"
                } else {
                    "Failed"
                },
                e,
            ),
        };
        let outcome = EditorOutcome {
            request_id: id.clone(),
            state: state.into(),
            message,
        };
        if self.history.len() >= 256 {
            if let Some(old) = self
                .history
                .iter()
                .find(|(_, (_, o))| o.state != "Unknown")
                .map(|(id, _)| id.clone())
            {
                self.history.remove(&old);
            }
        }
        self.history.insert(id, (intent.clone(), outcome.clone()));
        self.generation += 1;
        Ok(self.snapshot(context.value_date, outcome))
    }
    async fn mutate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        now: NaiveDate,
    ) -> Result<(), String> {
        let action = intent["intent"].as_str().unwrap();
        let field = |key: &str| intent["fields"][key].as_str().unwrap_or_default().trim();
        let day = if action == "remove" {
            date(
                intent["originalId"]
                    .as_str()
                    .ok_or("Select a stored curve")?,
            )?
        } else {
            date(field(if action == "import" {
                "importDate"
            } else {
                "valueDate"
            }))?
        };
        if matches!(action, "change" | "remove")
            && !self.rows.iter().any(|r| r.id == day.to_string())
        {
            return Err("The selected curve is no longer loaded".into());
        }
        if action == "change" && intent["originalId"].as_str() != Some(day.to_string().as_str()) {
            return Err("A saved curve's value date cannot change".into());
        }
        if action == "add" {
            let exists = actor_requests::query(
                backend,
                registry,
                &format!("{QUERY}GetYieldCurveRateExistsQuery"),
                &day.to_string(),
                BTreeMap::from([("ValueDate".into(), date_only_wire(day))]),
            )
            .await?;
            if exists
                .as_array()
                .and_then(|a| a.first())
                .and_then(Value::as_bool)
                != Some(false)
            {
                return Err("A curve already exists or its absence could not be verified".into());
            }
        }
        let (name, stem) = match action {
            "add" => ("AddYieldCurveRateCommand", "YieldCurveRateAdded"),
            "change" => ("ChangeYieldCurveRateCommand", "YieldCurveRateChanged"),
            "remove" => ("RemoveYieldCurveRateCommand", "YieldCurveRateRemoved"),
            _ => ("ImportYieldCurveRatesCommand", "YieldCurveRatesImported"),
        };
        let kind = format!("{COMMAND}{name}");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: day.year().to_string(),
        };
        let mut fields = BTreeMap::from([
            ("CommandId".into(), id.into()),
            ("Subject".into(), subject.wire()),
            ("PostEvents".into(), false.into()),
            ("EntityId".into(), Value::Array(vec![day.year().into()])),
            ("ErrorCode".into(), registry.error_id(&kind)?.into()),
            (
                "RouteTo".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                        "YieldCurveRateBoundedContext",
                    )?
                    .into(),
            ),
        ]);
        if action == "import" {
            fields.insert(
                "ImportDate".into(),
                timestamp_wire(day.and_hms_opt(0, 0, 0).unwrap().and_utc()),
            );
            fields.insert(
                "DuplicatePolicy".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Domain.MarketData.Shared.ImportDuplicatePolicy",
                        "Overwrite",
                    )?
                    .into(),
            );
        } else {
            fields.insert("Overwrite".into(), (action != "add").into());
            if action == "remove" {
                fields.insert("ValueDate".into(), date_only_wire(day));
            } else {
                let mut rates = vec![date_only_wire(day)];
                for key in RATES {
                    rates.push(Value::F64(
                        field(key)
                            .parse::<f64>()
                            .ok()
                            .filter(|v| v.is_finite())
                            .ok_or("Rates must be finite")?,
                    ));
                }
                fields.insert("YieldCurveRate".into(), Value::Array(rates));
            }
        }
        let body = registry.encode(&kind, &fields)?;
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        actor_requests::command(
            backend,
            registry,
            &subject,
            &body,
            &format!("{EVENT}{stem}CompleteEvent"),
            &format!("{EVENT}{stem}FailEvent"),
            id,
            deadline,
        )
        .await?;
        tokio::time::timeout_at(
            deadline,
            self.load(backend, registry, now, Some(day.year().to_string())),
        )
        .await
        .map_err(|_| {
            "OutcomeUnknown: curve command completed but refresh deadline expired".to_string()
        })?
        .map_err(|e| format!("OutcomeUnknown: curve command completed but refresh failed: {e}"))
    }
    fn snapshot(&self, now: NaiveDate, outcome: EditorOutcome) -> ContractPublication {
        let mut context = BTreeMap::from([
            ("timePeriod".into(), self.selected.clone()),
            ("valueDate".into(), now.to_string()),
        ]);
        if let Some((start, end)) = self.range {
            context.insert("rangeStart".into(), start.to_string());
            context.insert("rangeEnd".into(), end.to_string());
        }
        ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            outcomes: vec![outcome],
            result: ContractResult {
                view: "yield-curve".into(),
                rows: self.rows.clone(),
                choices: BTreeMap::from([("timePeriod".into(), self.periods.clone())]),
                context,
                ..Default::default()
            },
            ..Default::default()
        }
    }
}
