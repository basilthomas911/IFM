//! Native economic-calendar editor. Filter days are Eastern; event identities
//! and wire timestamps are UTC. Imports execute on the backend.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{actor_requests, business_context::BusinessContext},
};
use chrono::{DateTime, NaiveDate, TimeZone, Timelike, Utc};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared.";
const FIELDS: [&str; 12] = [
    "EventDate",
    "CountryCode",
    "EventName",
    "Actual",
    "Forecast",
    "Prior",
    "CreatedOn",
    "CreatedBy",
    "Impact",
    "Unit",
    "Change",
    "ChangePercentage",
];

pub fn eastern_midnight(day: NaiveDate) -> Result<DateTime<Utc>, String> {
    chrono_tz::America::New_York
        .from_local_datetime(&day.and_hms_opt(0, 0, 0).ok_or("Invalid date")?)
        .single()
        .map(|time| time.to_utc())
        .ok_or_else(|| "Eastern date has no unique midnight".into())
}
fn day(text: &str) -> Result<NaiveDate, String> {
    NaiveDate::parse_from_str(text, "%Y-%m-%d")
        .map_err(|_| "Enter the filter date as yyyy-MM-dd".into())
}
pub fn instant(text: &str) -> Result<DateTime<Utc>, String> {
    DateTime::parse_from_rfc3339(text)
        .ok()
        .filter(|t| t.offset().local_minus_utc() == 0 && t.timestamp_subsec_nanos() % 100 == 0)
        .map(|t| t.to_utc())
        .ok_or_else(|| "Enter an ISO UTC timestamp with at most seven fractional digits".into())
}
fn text(time: DateTime<Utc>) -> String {
    format!(
        "{}.{:07}Z",
        time.format("%Y-%m-%dT%H:%M:%S"),
        time.timestamp_subsec_nanos() / 100
    )
}
fn lower(key: &str) -> String {
    format!("{}{}", key[..1].to_ascii_lowercase(), &key[1..])
}
#[derive(Default)]
pub struct CalendarController {
    countries: Vec<String>,
    rows: Vec<ContractRow>,
    date: Option<NaiveDate>,
    country: String,
    generation: u64,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
impl CalendarController {
    async fn countries(&mut self, backend: &Backend, registry: &Registry) -> Result<(), String> {
        let reply = actor_requests::query(
            backend,
            registry,
            &format!("{ROOT}Queries.GetEconomicCalendarCountryCodesQuery"),
            "EconomicCalendarCountryCodes",
            BTreeMap::new(),
        )
        .await?;
        let countries = registry.project(
            &format!("{ROOT}ViewModels.EconomicCalendarCountryCodeReadModel[]"),
            &reply,
        )?;
        let mut values = Vec::new();
        for country in countries.as_array().ok_or("Invalid calendar countries")? {
            let code = country["CountryCode"]
                .as_str()
                .filter(|s| !s.is_empty())
                .ok_or("Missing country code")?
                .to_string();
            if !values.contains(&code) {
                values.push(code);
            }
        }
        if values.is_empty() {
            return Err("No economic-calendar countries are published".into());
        }
        self.countries = values;
        Ok(())
    }
    async fn read(
        &self,
        backend: &Backend,
        registry: &Registry,
        date: NaiveDate,
        country: &str,
    ) -> Result<Vec<ContractRow>, String> {
        if !self.countries.iter().any(|c| c == country) {
            return Err("Select a published country".into());
        }
        let time = eastern_midnight(date)?;
        let reply = actor_requests::query(
            backend,
            registry,
            &format!("{ROOT}Queries.GetEconomicCalendarQuery"),
            &format!("{}.Today.{country}", time.format("%Y-%m-%d:%I-%M")),
            BTreeMap::from([
                ("TodaysDate".into(), timestamp_wire(time)),
                ("CalendarViewType".into(), 0.into()),
                ("CountryCode".into(), country.into()),
            ]),
        )
        .await?;
        let result = registry.project(
            &format!("{ROOT}ViewModels.EconomicCalendarReadModel[]"),
            &reply,
        )?;
        let mut rows = Vec::new();
        for item in result.as_array().ok_or("Invalid calendar rows")? {
            let fields: BTreeMap<String, String> = FIELDS
                .iter()
                .map(|key| {
                    (
                        lower(key),
                        item[key].as_str().unwrap_or_default().to_owned(),
                    )
                })
                .collect();
            let time = instant(&fields["eventDate"])?;
            if time
                .with_timezone(&chrono_tz::America::New_York)
                .date_naive()
                != date
                || fields["countryCode"] != country
            {
                return Err("Calendar response is outside the requested date/country".into());
            }
            let id =
                serde_json::to_string(&(text(time), &fields["countryCode"], &fields["eventName"]))
                    .map_err(|e| e.to_string())?;
            rows.push(ContractRow {
                id,
                fields,
                ..Default::default()
            });
        }
        rows.sort_by(|a, b| a.fields["eventDate"].cmp(&b.fields["eventDate"]));
        Ok(rows)
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
            .filter(|v| !v.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        if intent["schema"] != 1 || intent["view"] != "economic-calendar" {
            return Err("Invalid calendar intent".into());
        }
        let session = BusinessContext::load(backend).await?;
        if let Some((prior, outcome)) = self.history.get(&id) {
            if prior != intent {
                return Err("RequestId belongs to another calendar request".into());
            }
            let outcome = outcome.clone();
            self.generation += 1;
            return Ok(self.snapshot(session.as_of_utc, outcome));
        }
        if self.history.len() >= 256 && self.history.values().all(|(_, o)| o.state == "Unknown") {
            return Err(
                "Reconcile uncertain calendar operations before submitting more requests".into(),
            );
        }
        let action = intent["intent"].as_str().ok_or("Missing calendar action")?;
        if action != "load"
            && (self.date.is_none()
                || intent["expectedGeneration"].as_u64() != Some(self.generation))
        {
            return Err("Calendars changed; reload before editing".into());
        }
        let field = |key: &str| intent["fields"][key].as_str().unwrap_or_default().trim();
        let result = async {
            let date = if field("filterDate").is_empty() {
                self.date.unwrap_or(session.value_date)
            } else {
                day(field("filterDate"))?
            };
            if action == "load" {
                self.countries(backend, registry).await?;
            }
            let country = if !field("filterCountry").is_empty() {
                field("filterCountry").to_owned()
            } else if !self.country.is_empty() {
                self.country.clone()
            } else if self.countries.iter().any(|s| s == "US") {
                "US".into()
            } else {
                self.countries
                    .first()
                    .cloned()
                    .ok_or("No published countries")?
            };
            if !self.countries.contains(&country) {
                return Err("Select a published country".into());
            }
            let (date, country) = match action {
                "load" | "range" => (date, country),
                "add" | "change" | "remove" | "import" => {
                    self.mutate(
                        backend,
                        registry,
                        intent,
                        &id,
                        date,
                        &country,
                        session.as_of_utc,
                    )
                    .await?
                }
                _ => return Err("Unsupported calendar action".into()),
            };
            // A command may already have committed even if its projection read fails.
            let rows = self
                .read(backend, registry, date, &country)
                .await
                .map_err(|e| {
                    if matches!(action, "add" | "change" | "remove" | "import") {
                        format!(
                            "OutcomeUnknown: command completed, but calendar refresh failed: {e}"
                        )
                    } else {
                        e
                    }
                })?;
            self.rows = rows;
            self.date = Some(date);
            self.country = country;
            Ok::<_, String>(())
        }
        .await;
        let (state, message) = match result {
            Ok(()) => ("Completed", "Completed".into()),
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
            if let Some(key) = self
                .history
                .iter()
                .find(|(_, (_, o))| o.state != "Unknown")
                .map(|(id, _)| id.clone())
            {
                self.history.remove(&key);
            }
        }
        self.history.insert(id, (intent.clone(), outcome.clone()));
        self.generation += 1;
        Ok(self.snapshot(session.as_of_utc, outcome))
    }
    async fn mutate(
        &self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        date: NaiveDate,
        country: &str,
        now: DateTime<Utc>,
    ) -> Result<(NaiveDate, String), String> {
        let action = intent["intent"].as_str().unwrap();
        let field = |key: &str| intent["fields"][key].as_str().unwrap_or_default().trim();
        let original = self
            .rows
            .iter()
            .find(|r| Some(r.id.as_str()) == intent["originalId"].as_str());
        if matches!(action, "change" | "remove") && original.is_none() {
            return Err("The selected calendar is no longer loaded".into());
        }
        let mut row = BTreeMap::new();
        let (time, code, name) = if action == "import" {
            (
                eastern_midnight(date)?,
                "ZZ".to_string(),
                "ImportEconomicCalendars".to_string(),
            )
        } else if action == "remove" {
            let row = &original.unwrap().fields;
            (
                instant(&row["eventDate"])?,
                row["countryCode"].clone(),
                row["eventName"].clone(),
            )
        } else {
            if !self.countries.iter().any(|s| s == field("countryCode"))
                || field("eventName").is_empty()
            {
                return Err("Select a published country and supply an event name/date".into());
            }
            let time = instant(field("eventDate"))?;
            for key in FIELDS {
                let value = match key {
                    "EventDate" => timestamp_wire(time),
                    "CreatedOn" => timestamp_wire(
                        original
                            .map(|r| instant(&r.fields["createdOn"]))
                            .transpose()?
                            .unwrap_or(now),
                    ),
                    "CreatedBy" => original
                        .map(|r| r.fields["createdBy"].clone())
                        .unwrap_or_else(|| "Rust UI".into())
                        .into(),
                    _ => {
                        let key = lower(key);
                        if field(&key).is_empty() {
                            Value::Nil
                        } else {
                            field(&key).into()
                        }
                    }
                };
                row.insert(key.into(), value);
            }
            (
                time,
                field("countryCode").to_owned(),
                field("eventName").to_owned(),
            )
        };
        let (identity_time, identity_code, identity_name) = if action == "change" {
            let f = &original.unwrap().fields;
            (
                instant(&f["eventDate"])?,
                f["countryCode"].clone(),
                f["eventName"].clone(),
            )
        } else {
            (time, code.clone(), name.clone())
        };
        let identity = Value::Array(vec![
            timestamp_wire(
                identity_time
                    .with_nanosecond(0)
                    .ok_or("Invalid event identity")?,
            ),
            identity_code.clone().into(),
            identity_name.clone().into(),
        ]);
        let (kind, stem) = match action {
            "add" => ("AddEconomicCalendarCommand", "EconomicCalendarAdded"),
            "change" => ("ChangeEconomicCalendarCommand", "EconomicCalendarChanged"),
            "remove" => ("RemoveEconomicCalendarCommand", "EconomicCalendarRemoved"),
            _ => (
                "ImportEconomicCalendarsCommand",
                "EconomicCalendarsImported",
            ),
        };
        let kind = format!("{ROOT}Commands.{kind}");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: format!(
                "{}.{}.{}",
                identity_time.format("%Y%m%d%H%M%S"),
                identity_code,
                identity_name.replace(' ', "-")
            ),
        };
        let mut fields = BTreeMap::from([
            ("CommandId".into(), id.into()),
            ("Subject".into(), subject.wire()),
            ("PostEvents".into(), false.into()),
            ("EntityId".into(), identity.clone()),
            ("ErrorCode".into(), registry.error_id(&kind)?.into()),
            (
                "RouteTo".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                        "EconomicCalendarBoundedContext",
                    )?
                    .into(),
            ),
        ]);
        if action == "import" {
            fields.insert("ImportedDate".into(), timestamp_wire(time));
            fields.insert("CountryCodes".into(), Value::Array(vec![country.into()]));
            fields.insert("DuplicatePolicy".into(), 0.into());
        } else {
            if action != "add" {
                fields.insert("Overwrite".into(), true.into());
                fields.insert("EconomicCalendarId".into(), identity);
            }
            if action != "remove" {
                fields.insert(
                    "EconomicCalendar".into(),
                    registry
                        .encode(&format!("{ROOT}ViewModels.EconomicCalendarReadModel"), &row)?,
                );
            }
        }
        let body = registry.encode(&kind, &fields)?;
        actor_requests::command(
            backend,
            registry,
            &subject,
            &body,
            &format!("{ROOT}Events.{stem}CompleteEvent"),
            &format!("{ROOT}Events.{stem}FailEvent"),
            id,
            tokio::time::Instant::now() + backend.default_timeout(),
        )
        .await?;
        Ok(if action == "import" {
            (date, country.into())
        } else {
            (
                time.with_timezone(&chrono_tz::America::New_York)
                    .date_naive(),
                code,
            )
        })
    }
    fn snapshot(&self, now: DateTime<Utc>, outcome: EditorOutcome) -> ContractPublication {
        ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            outcomes: vec![outcome],
            result: ContractResult {
                view: "economic-calendar".into(),
                rows: self.rows.clone(),
                choices: BTreeMap::from([("countryCode".into(), self.countries.clone())]),
                context: BTreeMap::from([
                    (
                        "filterDate".into(),
                        self.date.map(|d| d.to_string()).unwrap_or_default(),
                    ),
                    ("filterCountry".into(), self.country.clone()),
                    ("newEventUtc".into(), text(now)),
                ]),
                ..Default::default()
            },
            ..Default::default()
        }
    }
}
