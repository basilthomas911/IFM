//! Futures reference editor. Provider definitions stay as exact wire models;
//! ordinary edits and convention reviews have separate field whitelists.
pub use crate::backend::contracts::reference_offset as wire_offset;
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{actor_requests, reference},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared.";
const MODEL: &str = "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel";
const REVIEW: [(&str, &str); 10] = [
    ("reviewState", "ReviewState"),
    ("mappingVersion", "MappingVersion"),
    ("evidenceId", "EvidenceId"),
    ("effectiveFromUtc", "EffectiveFromUtc"),
    ("effectiveUntilUtc", "EffectiveUntilUtc"),
    ("exchangeTimeZoneId", "ExchangeTimeZoneId"),
    ("calendarVersion", "CalendarVersion"),
    ("lastTradingUtc", "LastTradingUtc"),
    ("settlementStyle", "SettlementStyle"),
    ("description", "Description"),
];
fn string(value: &Json) -> String {
    value.as_str().unwrap_or_default().into()
}
fn identity(row: &Json) -> Result<String, String> {
    let date = chrono::NaiveDate::parse_from_str(
        row["LastTradeDate"]
            .as_str()
            .ok_or("Missing contract maturity")?,
        "%Y-%m-%d",
    )
    .map_err(|_| "Invalid contract maturity")?;
    Ok(format!(
        "{}.{}.{}",
        row["ContractId"].as_str().ok_or("Missing ContractId")?,
        row["Symbol"].as_str().ok_or("Missing Symbol")?,
        date.format("%Y%m%d")
    ))
}
fn id_wire(row: &Json) -> Result<Value, String> {
    let date = chrono::NaiveDate::parse_from_str(
        row["LastTradeDate"].as_str().ok_or("Missing maturity")?,
        "%Y-%m-%d",
    )
    .map_err(|_| "Invalid maturity")?;
    Ok(Value::Array(vec![
        string(&row["ContractId"]).into(),
        string(&row["Symbol"]).into(),
        date_only_wire(date),
    ]))
}
fn display(value: &Json) -> String {
    match value {
        Json::Null => String::new(),
        Json::String(s) => s.clone(),
        Json::Bool(v) => if *v { "True" } else { "False" }.into(),
        _ => value.to_string(),
    }
}
fn display_offset(value: &Json) -> String {
    value
        .as_str()
        .and_then(|s| chrono::DateTime::parse_from_rfc3339(s).ok())
        .map(|d| {
            format!(
                "{}.{:07}{}",
                d.format("%Y-%m-%dT%H:%M:%S"),
                d.timestamp_subsec_nanos() / 100,
                d.format("%:z")
            )
        })
        .unwrap_or_default()
}
/// Shared convention-form fields; option editors replace the option defaults
/// with their typed values. These strings are never used as provider evidence.
pub fn review_fields(row: &Json) -> BTreeMap<String, String> {
    let mut fields = BTreeMap::new();
    for (key, field) in REVIEW
        .into_iter()
        .filter(|(_, field)| *field != "Description")
    {
        fields.insert(
            key.into(),
            if field.ends_with("Utc") {
                display_offset(&row[field])
            } else {
                display(&row[field])
            },
        );
    }
    for (key, value) in [
        ("exerciseCutoffUtc", ""),
        ("exerciseStyle", "Unknown"),
        ("premiumStyle", "Unknown"),
        ("premiumTickRule", "Unknown"),
        ("tickRuleVersion", ""),
        ("dayCount", "Unknown"),
        ("underlyingContractId", ""),
        ("exerciseResultContractId", ""),
    ] {
        fields.insert(key.into(), value.into());
    }
    fields
}
pub fn display_row(row: &Json) -> Result<ContractRow, String> {
    let mut fields = BTreeMap::new();
    for (key, field) in [
        ("contractId", "ContractId"),
        ("description", "Description"),
        ("symbol", "Symbol"),
        ("localSymbol", "LocalSymbol"),
        ("securityType", "SecurityType"),
        ("currency", "Currency"),
        ("exchange", "Exchange"),
        ("multiplier", "Multiplier"),
        ("lastTradeDate", "LastTradeDate"),
        ("onTheRun", "OnTheRun"),
        ("rollover", "Rollover"),
    ] {
        fields.insert(key.into(), display(&row[field]));
    }
    fields.insert(
        "provider".into(),
        if row["SchemaVersion"].as_i64().unwrap_or(0) > 0 {
            "true"
        } else {
            "false"
        }
        .into(),
    );
    fields.extend(review_fields(row));
    Ok(ContractRow {
        id: identity(row)?,
        fields,
        data: None,
    })
}
pub fn display_contract(registry: &Registry, wire: &Value) -> Result<ContractRow, String> {
    let mut row = display_row(&registry.project(MODEL, wire)?)?;
    // These are the explicit C# keys. Preserve the original offset when showing
    // a draft convention; the generic projection normalizes instants to UTC.
    for (key, index) in [
        ("lastTradingUtc", 21),
        ("effectiveFromUtc", 30),
        ("effectiveUntilUtc", 31),
    ] {
        if let Some(value) = wire
            .as_array()
            .and_then(|values| values.get(index))
            .filter(|value| !value.is_nil())
        {
            let parts = value.as_array().ok_or("Invalid reference timestamp")?;
            let minutes = parts
                .get(1)
                .and_then(Value::as_i64)
                .ok_or("Invalid reference timestamp offset")?;
            let offset = chrono::FixedOffset::east_opt(
                i32::try_from(minutes * 60).map_err(|_| "Invalid reference timezone")?,
            )
            .ok_or("Invalid reference timezone")?;
            let time = datetime_offset(value)?.with_timezone(&offset);
            row.fields.insert(
                key.into(),
                format!(
                    "{}.{:07}{}",
                    time.format("%Y-%m-%dT%H:%M:%S"),
                    time.timestamp_subsec_nanos() / 100,
                    time.format("%:z")
                ),
            );
        }
    }
    Ok(row)
}
#[derive(Default)]
pub struct FuturesContractController {
    generation: u64,
    loaded: bool,
    models: BTreeMap<String, Value>,
    result: ContractResult,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
impl FuturesContractController {
    async fn load(&mut self, backend: &Backend, registry: &Registry) -> Result<(), String> {
        let query_kind = format!("{ROOT}Queries.GetFuturesContractsQuery");
        let (models, lookups) = tokio::try_join!(
            actor_requests::query(backend, registry, &query_kind, "none", BTreeMap::new()),
            reference::load_lookups(backend, registry)
        )?;
        let values = models
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid or oversized futures contract result")?;
        let mut rows = Vec::new();
        let mut saved = BTreeMap::new();
        for wire in values {
            let row = display_contract(registry, wire)?;
            if saved.insert(row.id.clone(), wire.clone()).is_some() {
                return Err("Backend returned duplicate futures identities".into());
            }
            rows.push(row);
        }
        let mut choices = BTreeMap::new();
        for (key, kind) in [
            ("symbol", "Symbol"),
            ("securityType", "SecurityType"),
            ("currency", "Currency"),
            ("exchange", "Exchange"),
            ("multiplier", "Multiplier"),
        ] {
            choices.insert(
                key.into(),
                lookups
                    .iter()
                    .filter(|row| row.lookup_type_name == kind)
                    .map(|row| row.short_code.clone())
                    .collect(),
            );
        }
        for (key, kind) in [
            ("reviewState", "ReferenceReviewState"),
            ("exerciseStyle", "ReferenceExerciseStyle"),
            ("settlementStyle", "ReferenceSettlementStyle"),
            ("premiumStyle", "ReferencePremiumStyle"),
            ("premiumTickRule", "ReferencePremiumTickRule"),
            ("dayCount", "ReferenceDayCount"),
        ] {
            choices.insert(
                key.into(),
                registry.enum_names(&format!("{ROOT}ViewModels.{kind}"))?,
            );
        }
        self.models = saved;
        self.loaded = true;
        self.result = ContractResult {
            view: "futures-contract".into(),
            rows,
            choices,
            ..Default::default()
        };
        Ok(())
    }
    async fn mutate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if !self.loaded || intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("Contracts changed; reload before editing".into());
        }
        let action = intent["intent"].as_str().unwrap_or_default();
        let original_id = intent["originalId"].as_str().unwrap_or_default();
        let original = if action == "add" {
            None
        } else {
            Some(
                self.models
                    .get(original_id)
                    .ok_or("Contract is no longer loaded")?
                    .clone(),
            )
        };
        let old = original
            .as_ref()
            .map(|wire| registry.project(MODEL, wire))
            .transpose()?;
        let field = |name: &str| intent["fields"][name].as_str().unwrap_or_default().trim();
        let mut changes = BTreeMap::new();
        if action == "review" {
            if old.as_ref().and_then(|v| v["SchemaVersion"].as_i64()) != Some(1) {
                return Err(
                    "Import a provider definition before reviewing this legacy contract".into(),
                );
            }
            if !field("exchangeTimeZoneId").is_empty()
                && !crate::backend::time_zone::is_known(field("exchangeTimeZoneId"))
            {
                return Err("Select a known exchange time zone".into());
            }
            for key in [
                "effectiveFromUtc",
                "effectiveUntilUtc",
                "lastTradingUtc",
                "exerciseCutoffUtc",
            ] {
                if !field(key).is_empty() {
                    let time = chrono::DateTime::parse_from_rfc3339(field(key))
                        .map_err(|_| format!("Enter {key} as a UTC timestamp"))?;
                    if time.offset().local_minus_utc() != 0 {
                        return Err(format!("Enter {key} as a UTC timestamp"));
                    }
                }
            }
            for (key, member) in REVIEW
                .into_iter()
                .filter(|(_, field)| *field != "Description")
            {
                changes.insert(
                    member.into(),
                    if member.ends_with("Utc") && field(key).is_empty() {
                        Json::Null
                    } else {
                        json!(field(key))
                    },
                );
            }
        } else if action != "remove" {
            changes.insert("Description".into(), json!(field("description")));
            let on_the_run = match field("onTheRun").to_ascii_lowercase().as_str() {
                "true" => true,
                "false" => false,
                _ => return Err("Select a valid on-the-run flag".into()),
            };
            changes.insert("OnTheRun".into(), json!(on_the_run));
            if old
                .as_ref()
                .and_then(|v| v["SchemaVersion"].as_i64())
                .unwrap_or(0)
                == 0
            {
                let date = chrono::NaiveDate::parse_from_str(field("lastTradeDate"), "%Y-%m-%d")
                    .map_err(|_| "Select a contract maturity")?;
                changes.insert(
                    "ContractId".into(),
                    json!(format!("{}{}", field("symbol"), date.format("%Y%m%d"))),
                );
                for (key, member) in [
                    ("symbol", "Symbol"),
                    ("localSymbol", "LocalSymbol"),
                    ("securityType", "SecurityType"),
                    ("currency", "Currency"),
                    ("exchange", "Exchange"),
                    ("multiplier", "Multiplier"),
                    ("lastTradeDate", "LastTradeDate"),
                ] {
                    changes.insert(member.into(), json!(field(key)));
                }
                changes.insert(
                    "Rollover".into(),
                    json!(match field("rollover").to_ascii_lowercase().as_str() {
                        "true" => true,
                        "false" => false,
                        _ => return Err("Select a valid rollover flag".into()),
                    }),
                );
            }
        }
        let original = original.unwrap_or(registry.encode(MODEL, &BTreeMap::new())?);
        let next = registry.patch(MODEL, &original, &changes)?;
        let value = registry.project(MODEL, &next)?;
        if action != "remove"
            && (string(&value["ContractId"]).is_empty()
                || string(&value["Symbol"]).is_empty()
                || value["LastTradeDate"]
                    .as_str()
                    .is_none_or(|d| d <= "0001-01-01")
                || value["OnTheRun"] == true && value["Rollover"] != true)
        {
            return Err(
                "Invalid futures contract; on-the-run contracts require rollover enabled".into(),
            );
        }
        let stem = match action {
            "add" => "Added",
            "remove" => "Removed",
            "change" | "review" => "Changed",
            _ => return Err("Unsupported futures action".into()),
        };
        let command_name = match action {
            "add" => "AddFuturesContractCommand",
            "remove" => "RemoveFuturesContractCommand",
            _ => "ChangeFuturesContractCommand",
        };
        let kind = format!("{ROOT}Commands.{command_name}");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: identity(&value)?,
        };
        let mut fields = BTreeMap::from([
            ("CommandId".into(), id.into()),
            ("Subject".into(), subject.wire()),
            ("PostEvents".into(), false.into()),
            ("EntityId".into(), id_wire(&value)?),
            ("ErrorCode".into(), registry.error_id(&kind)?.into()),
            (
                "RouteTo".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                        "FuturesContractBoundedContext",
                    )?
                    .into(),
            ),
            ("Overwrite".into(), true.into()),
        ]);
        if action != "add" {
            fields.insert(
                "ContractId".into(),
                id_wire(old.as_ref().ok_or("Missing original contract")?)?,
            );
        }
        if action != "remove" {
            fields.insert("Contract".into(), next);
        }
        let body = registry.encode(&kind, &fields)?;
        actor_requests::command(
            backend,
            registry,
            &subject,
            &body,
            &format!("{ROOT}Events.FuturesContract{stem}CompleteEvent"),
            &format!("{ROOT}Events.FuturesContract{stem}FailEvent"),
            id,
            deadline,
        )
        .await?;
        // A failed refresh after a confirmed terminal event is not an unknown
        // mutation and must not invite an automatic duplicate submission.
        match tokio::time::timeout_at(deadline, self.load(backend, registry)).await {
            Ok(Ok(())) => Ok("Completed".into()),
            result => {
                self.loaded = false;
                Ok(format!(
                    "Saved; reload contracts: {}",
                    match result {
                        Ok(Err(error)) => error,
                        _ => "Refresh deadline expired".into(),
                    }
                ))
            }
        }
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "futures-contract" {
            return Err("Invalid futures editor intent".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|v| uuid::Uuid::parse_str(v).ok())
            .filter(|v| !v.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let outcome = if let Some((request, outcome)) = self.history.get(&id) {
            if request != intent {
                return Err("RequestId belongs to another contract operation".into());
            }
            outcome.clone()
        } else {
            if self.history.len() >= 256 && self.history.values().all(|(_, v)| v.state == "Unknown")
            {
                return Err("Reconcile uncertain contract operations before continuing".into());
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let result = match intent["intent"].as_str() {
                Some("load") => tokio::time::timeout_at(deadline, self.load(backend, registry))
                    .await
                    .map_err(|_| "Contract load deadline expired".to_string())
                    .and_then(|r| r)
                    .map(|_| "Completed".into()),
                Some("add" | "change" | "remove" | "review") => {
                    self.mutate(backend, registry, intent, &id, deadline).await
                }
                _ => Err("Unsupported futures action".into()),
            };
            let outcome = match result {
                Ok(message) => EditorOutcome {
                    request_id: id.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(error) => EditorOutcome {
                    request_id: id.clone(),
                    state: if error.starts_with("OutcomeUnknown:") {
                        self.loaded = false;
                        "Unknown"
                    } else {
                        "Failed"
                    }
                    .into(),
                    message: error,
                },
            };
            if self.history.len() >= 256 {
                if let Some(key) = self
                    .history
                    .iter()
                    .find(|(_, (_, v))| v.state != "Unknown")
                    .map(|(k, _)| k.clone())
                {
                    self.history.remove(&key);
                }
            }
            self.history.insert(id, (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        self.result.view = "futures-contract".into();
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            result: self.result.clone(),
            outcomes: vec![outcome],
            ..Default::default()
        })
    }
}
