//! Bounded stored-definition browser and single-use mapping previews. This
//! controller never starts a market-data subscription. Every operation shares
//! one caller deadline, and a submitted write is never automatically repeated.
use crate::{
    backend::{contracts::*, schema::Registry, time_zone, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{
        actor_requests,
        business_context::BusinessContext,
        provider_mapping::{self, DEFINITION, FUTURE, OPTION},
    },
};
use chrono::{DateTime, Datelike};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared.";
const PAGE: &str = "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.InstrumentDefinitionPage";
const REQUEST: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.InstrumentDefinitionPageRequest";
struct Preview {
    token: String,
    selected: String,
    definition: Value,
    zone: String,
    mapped: Value,
    original: Option<Value>,
    underlying: Option<Value>,
    future: bool,
}
#[derive(Default)]
pub struct ProviderController {
    generation: u64,
    result: ContractResult,
    search: Option<Value>,
    snapshot: Option<String>,
    cursor: Option<String>,
    definitions: BTreeMap<String, Value>,
    preview: Option<Preview>,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
fn field<'a>(intent: &'a Json, name: &str) -> &'a str {
    intent["fields"][name].as_str().unwrap_or_default().trim()
}
fn bool_field(intent: &Json, name: &str) -> Result<bool, String> {
    match field(intent, name).to_ascii_lowercase().as_str() {
        "" | "false" => Ok(false),
        "true" => Ok(true),
        _ => Err(format!("Invalid {name}")),
    }
}
fn optional(intent: &Json, name: &str) -> Json {
    if field(intent, name).is_empty() {
        Json::Null
    } else {
        json!(field(intent, name))
    }
}
fn display(value: &Json) -> String {
    match value {
        Json::Null => String::new(),
        Json::String(v) => v.clone(),
        Json::Bool(v) => if *v { "True" } else { "False" }.into(),
        _ => value.to_string(),
    }
}
fn camel(name: &str) -> String {
    let mut chars = name.chars();
    format!(
        "{}{}",
        chars
            .next()
            .map(|c| c.to_ascii_lowercase())
            .unwrap_or_default(),
        chars.as_str()
    )
}
fn key(row: &Json) -> Result<String, String> {
    let id = uuid::Uuid::parse_str(
        row["SnapshotId"]
            .as_str()
            .ok_or("Missing definition snapshot")?,
    )
    .map_err(|_| "Invalid definition snapshot")?;
    Ok(format!(
        "{}:{}:{}:{}",
        id.simple(),
        row["Dataset"].as_str().ok_or("Missing dataset")?,
        row["PublisherId"].as_u64().ok_or("Missing publisher")?,
        row["InstrumentId"].as_u64().ok_or("Missing instrument")?
    ))
}
impl ProviderController {
    async fn search(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<(), String> {
        self.preview = None;
        let request = if intent["intent"] == "next" {
            let cursor = self
                .cursor
                .as_ref()
                .ok_or("There are no more stored definitions")?;
            registry.patch(
                REQUEST,
                self.search
                    .as_ref()
                    .ok_or("Search for stored definitions first")?,
                &BTreeMap::from([
                    ("SnapshotId".into(), json!(self.snapshot)),
                    ("ContinuationToken".into(), json!(cursor)),
                ]),
            )?
        } else {
            let mut fields = BTreeMap::from([
                ("Dataset".into(), json!(field(intent, "dataset"))),
                ("Root".into(), json!(field(intent, "root"))),
                ("Options".into(), json!(bool_field(intent, "options")?)),
                (
                    "IncludeExpiredOrDeleted".into(),
                    json!(bool_field(intent, "includeHistory")?),
                ),
                ("PageSize".into(), json!(100)),
                ("SnapshotId".into(), json!(uuid::Uuid::nil().to_string())),
                (
                    "Right".into(),
                    json!(if field(intent, "right").is_empty() {
                        "Unknown"
                    } else {
                        field(intent, "right")
                    }),
                ),
            ]);
            for (key, member) in [
                ("exchange", "Exchange"),
                ("expiry", "Expiry"),
                ("minimumStrike", "MinimumStrike"),
                ("maximumStrike", "MaximumStrike"),
            ] {
                fields.insert(member.into(), optional(intent, key));
            }
            fields.insert(
                "UnderlyingInstrumentId".into(),
                if field(intent, "underlyingInstrumentId").is_empty() {
                    Json::Null
                } else {
                    json!(
                        field(intent, "underlyingInstrumentId")
                            .parse::<u32>()
                            .map_err(|_| "Invalid underlying instrument ID")?
                    )
                },
            );
            for member in ["Dataset", "Root"] {
                let value = fields[member].as_str().unwrap();
                if value.is_empty() || value.encode_utf16().count() > 32 {
                    return Err("Invalid bounded definition search".into());
                }
            }
            if field(intent, "exchange").encode_utf16().count() > 32 {
                return Err("Exchange filter is too long".into());
            }
            // Decimal conversion checks range/precision. The backend validates strike
            // ordering and applies authoritative provider filters to the pinned snapshot.
            registry.patch(
                REQUEST,
                &registry.encode(REQUEST, &BTreeMap::new())?,
                &fields,
            )?
        };
        let page = actor_requests::query(
            backend,
            registry,
            &format!("{ROOT}Queries.GetInstrumentDefinitionsQuery"),
            "definitions",
            BTreeMap::from([("Request".into(), request.clone())]),
        )
        .await?;
        let projected = registry.project(PAGE, &page)?;
        let snapshot = projected["SnapshotId"]
            .as_str()
            .filter(|v| uuid::Uuid::parse_str(v).is_ok_and(|id| !id.is_nil()))
            .ok_or("Invalid definition snapshot")?;
        let request_value = registry.project(REQUEST, &request)?;
        if request_value["SnapshotId"] != uuid::Uuid::nil().to_string()
            && request_value["SnapshotId"] != snapshot
        {
            return Err("Definition page changed the pinned snapshot".into());
        }
        let values = page
            .as_array()
            .and_then(|v| v.get(2))
            .and_then(Value::as_array)
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid or oversized definition page")?;
        let cursor = projected["ContinuationToken"].as_str().map(str::to_string);
        if cursor
            .as_ref()
            .is_some_and(|v| v.encode_utf16().count() > 2048)
            || intent["intent"] == "next" && cursor.is_some() && cursor == self.cursor
        {
            return Err("Stored definition page did not advance".into());
        }
        let mut rows = Vec::new();
        let mut definitions = BTreeMap::new();
        for wire in values {
            let row = registry.project(DEFINITION, wire)?;
            if row["SnapshotId"] != snapshot || row["Dataset"] != request_value["Dataset"] {
                return Err("Definition page contains mismatched snapshot or dataset".into());
            }
            let id = key(&row)?;
            if definitions.insert(id.clone(), wire.clone()).is_some() {
                return Err("Definition page has duplicate identities".into());
            }
            let mut fields = row
                .as_object()
                .ok_or("Invalid definition row")?
                .iter()
                .map(|(key, value)| (camel(key), display(value)))
                .collect::<BTreeMap<_, _>>();
            for (name, index) in [
                ("expirationUtc", 13),
                ("activationUtc", 14),
                ("definitionTimestampUtc", 15),
            ] {
                fields.insert(
                    name.into(),
                    crate::backend::contracts::reference_offset(
                        &wire.as_array().ok_or("Invalid definition model")?[index],
                    )?,
                );
            }
            rows.push(ContractRow {
                id,
                fields,
                data: None,
            });
        }
        self.result = ContractResult {
            view: "provider-definitions".into(),
            rows,
            choices: BTreeMap::from([(
                "right".into(),
                registry.enum_names(&format!("{ROOT}ViewModels.ReferenceOptionRight"))?,
            )]),
            has_more: cursor.is_some(),
            context: BTreeMap::from([
                ("snapshotId".into(), snapshot.into()),
                (
                    "snapshotCompletedUtc".into(),
                    display(&projected["SnapshotCompletedUtc"]),
                ),
            ]),
            ..Default::default()
        };
        self.definitions = definitions;
        self.snapshot = Some(snapshot.into());
        self.cursor = cursor;
        self.search = Some(request);
        Ok(())
    }
    async fn read_contract(
        backend: &Backend,
        registry: &Registry,
        id: &str,
        future: bool,
    ) -> Result<Value, String> {
        if id.is_empty() {
            return Err("Select a contract ID".into());
        }
        let value = actor_requests::query(
            backend,
            registry,
            &format!(
                "{ROOT}Queries.Get{}ContractQuery",
                if future { "Futures" } else { "FuturesOption" }
            ),
            id,
            BTreeMap::from([("ContractId".into(), id.into())]),
        )
        .await?;
        let model = registry.project(if future { FUTURE } else { OPTION }, &value)?;
        if model["ContractId"] != id {
            return Err("The requested reference contract was not found".into());
        }
        Ok(value)
    }
    fn prepare(
        registry: &Registry,
        definition: &Value,
        zone: &str,
        at: chrono::DateTime<chrono::Utc>,
        underlying: Option<&Value>,
        original: Option<&Value>,
    ) -> Result<Value, String> {
        let projected = registry.project(DEFINITION, definition)?;
        let expiration = DateTime::parse_from_rfc3339(
            projected["ExpirationUtc"]
                .as_str()
                .ok_or("Definition expiry is missing")?,
        )
        .map_err(|_| "Invalid definition expiry")?
        .to_utc();
        let local = time_zone::local_date(zone, expiration)?;
        provider_mapping::prepare(registry, definition, zone, at, local, underlying, original)
    }
    async fn map(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("The definition selection changed; search and preview again".into());
        }
        let selected = intent["originalId"]
            .as_str()
            .ok_or("Select a stored definition")?;
        if intent["intent"] == "preview" {
            self.preview = None;
            self.result.documents.clear();
            self.result.context.retain(|k, _| !k.starts_with("preview"));
            let definition = self
                .definitions
                .get(selected)
                .ok_or("Select a definition from the current stored page")?
                .clone();
            let projected = registry.project(DEFINITION, &definition)?;
            let future = projected["InstrumentClass"] == "F";
            let zone = field(intent, "exchangeTimeZoneId");
            let original = if field(intent, "existingContractId").is_empty() {
                None
            } else {
                Some(
                    Self::read_contract(
                        backend,
                        registry,
                        field(intent, "existingContractId"),
                        future,
                    )
                    .await?,
                )
            };
            let underlying = if future {
                None
            } else {
                Some(
                    Self::read_contract(
                        backend,
                        registry,
                        field(intent, "underlyingContractId"),
                        true,
                    )
                    .await?,
                )
            };
            let context = BusinessContext::load(backend).await?;
            let mapped = Self::prepare(
                registry,
                &definition,
                zone,
                context.as_of_utc,
                underlying.as_ref(),
                original.as_ref(),
            )?;
            let token = uuid::Uuid::new_v4().simple().to_string();
            self.result
                .context
                .insert("previewToken".into(), token.clone());
            self.result
                .context
                .insert("previewSelectedId".into(), selected.into());
            self.result.context.insert(
                "previewMode".into(),
                if original.is_some() { "Change" } else { "Add" }.into(),
            );
            let document = provider_mapping::preview_document(
                registry,
                if future { FUTURE } else { OPTION },
                &mapped,
            )?;
            self.result.documents.insert(
                "previewContract".into(),
                crate::wire::encode_value(&document)?.into(),
            );
            self.preview = Some(Preview {
                token,
                selected: selected.into(),
                definition,
                zone: zone.into(),
                mapped,
                original,
                underlying,
                future,
            });
            Ok(())
        } else {
            let preview = self
                .preview
                .as_ref()
                .ok_or("Preview the provider mapping before saving")?;
            if preview.token != field(intent, "previewToken") || preview.selected != selected {
                return Err("The mapping preview no longer matches this selection".into());
            }
            // Consume before any write. Neither a lost response nor a timeout permits
            // replaying this preview; the caller must read/preview the actual state.
            let preview = self.preview.take().unwrap();
            self.result.context.retain(|k, _| !k.starts_with("preview"));
            self.result.documents.clear();
            let kind = if preview.future { FUTURE } else { OPTION };
            if let Some(original) = &preview.original {
                let original_id = registry.project(kind, original)?["ContractId"]
                    .as_str()
                    .ok_or("Missing original identity")?
                    .to_string();
                if Self::read_contract(backend, registry, &original_id, preview.future).await?
                    != *original
                {
                    return Err("The existing contract changed; preview again".into());
                }
            }
            if let Some(underlying) = &preview.underlying {
                let underlying_id = registry.project(FUTURE, underlying)?["ContractId"]
                    .as_str()
                    .ok_or("Missing underlying identity")?
                    .to_string();
                if Self::read_contract(backend, registry, &underlying_id, true).await?
                    != *underlying
                {
                    return Err("The underlying changed; preview again".into());
                }
            }
            let context = BusinessContext::load(backend).await?;
            Self::prepare(
                registry,
                &preview.definition,
                &preview.zone,
                context.as_of_utc,
                preview.underlying.as_ref(),
                preview.original.as_ref(),
            )?;
            let row = registry.project(kind, &preview.mapped)?;
            let contract = row["ContractId"]
                .as_str()
                .ok_or("Missing mapped identity")?;
            let date = chrono::NaiveDate::parse_from_str(
                row[if preview.future {
                    "LastTradeDate"
                } else {
                    "ContractMonth"
                }]
                .as_str()
                .ok_or("Missing mapped expiry")?,
                "%Y-%m-%d",
            )
            .map_err(|_| "Invalid mapped expiry")?;
            let entity = if preview.future {
                Value::Array(vec![
                    contract.into(),
                    row["Symbol"]
                        .as_str()
                        .ok_or("Missing mapped symbol")?
                        .into(),
                    date_only_wire(date),
                ])
            } else {
                Value::Array(vec![contract.into(), date.year().into()])
            };
            let prefix = if preview.future {
                "FuturesContract"
            } else {
                "FuturesOptionContract"
            };
            let action = if preview.original.is_some() {
                "Change"
            } else {
                "Add"
            };
            let stem = if preview.original.is_some() {
                "Changed"
            } else {
                "Added"
            };
            let command_type = format!("{ROOT}Commands.{action}{prefix}Command");
            let (actor, verb) = registry.route(&command_type)?;
            let subject = ActorSubject {
                kind: ActorKind::Command,
                name: actor.into(),
                verb: verb.into(),
                entity: if preview.future {
                    format!(
                        "{contract}.{}.{}",
                        row["Symbol"].as_str().unwrap(),
                        date.format("%Y%m%d")
                    )
                } else {
                    format!("{contract}.{}", date.year())
                },
            };
            let mut fields = BTreeMap::from([
                ("CommandId".into(), id.into()),
                ("Subject".into(), subject.wire()),
                ("EntityId".into(), entity.clone()),
                ("PostEvents".into(), false.into()),
                ("Overwrite".into(), true.into()),
                ("Contract".into(), preview.mapped),
                ("ErrorCode".into(), registry.error_id(&command_type)?.into()),
                (
                    "RouteTo".into(),
                    registry
                        .enum_value(
                            "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                            &format!("{prefix}BoundedContext"),
                        )?
                        .into(),
                ),
            ]);
            if preview.original.is_some() {
                fields.insert(
                    "ContractId".into(),
                    if preview.future {
                        entity
                    } else {
                        contract.into()
                    },
                );
            }
            let body = registry.encode(&command_type, &fields)?;
            actor_requests::command(
                backend,
                registry,
                &subject,
                &body,
                &format!("{ROOT}Events.{prefix}{stem}CompleteEvent"),
                &format!("{ROOT}Events.{prefix}{stem}FailEvent"),
                id,
                deadline,
            )
            .await?;
            Ok(())
        }
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "provider-definitions" {
            return Err("Invalid provider definition intent".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let outcome = if let Some((request, outcome)) = self.history.get(&id) {
            if request != intent {
                return Err("RequestId belongs to another provider operation".into());
            }
            outcome.clone()
        } else {
            if self.history.len() >= 256 && self.history.values().all(|(_, v)| v.state == "Unknown")
            {
                return Err("Reconcile uncertain provider imports before continuing".into());
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let work = async {
                match intent["intent"].as_str().unwrap_or_default() {
                    "load" | "next" => self.search(backend, registry, intent).await,
                    "preview" | "publish" => {
                        self.map(backend, registry, intent, &id, deadline).await
                    }
                    _ => Err("Unsupported provider action".into()),
                }
            };
            let outcome = match tokio::time::timeout_at(deadline, work).await {
                Ok(Ok(())) => EditorOutcome {
                    request_id: id.clone(),
                    state: "Completed".into(),
                    message: "Completed".into(),
                },
                result => {
                    let error = match result {
                        Ok(Err(e)) => e,
                        _ => {
                            if intent["intent"] == "publish" {
                                "OutcomeUnknown: provider publish deadline expired".into()
                            } else {
                                "Provider operation deadline expired".into()
                            }
                        }
                    };
                    EditorOutcome {
                        request_id: id.clone(),
                        state: if error.starts_with("OutcomeUnknown:") {
                            "Unknown"
                        } else {
                            "Failed"
                        }
                        .into(),
                        message: error,
                    }
                }
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
        self.result.view = "provider-definitions".into();
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
