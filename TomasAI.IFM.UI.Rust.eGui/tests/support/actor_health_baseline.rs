//! Actor explorer and generation-bound Supervisor operations. HTTP owns health
//! observation; existing NATS commands own lifecycle changes. UI time is API time.
use chrono::{DateTime, Datelike, Timelike, Utc};
use ifm_ui_egui::{
    backend::{contracts::*, health_http::HealthClient, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
};
use rmpv::Value;
use serde_json::Value as Json;
use std::{collections::BTreeMap, time::Duration};
pub const OPERATIONS: [&str; 8] = [
    "Pause",
    "Drain",
    "Resume",
    "Restart",
    "Quarantine",
    "Retire",
    "Recycle",
    "AcknowledgeIncident",
];
#[derive(Clone)]
pub struct ControlOptions {
    pub enabled: bool,
    pub requester: String,
    pub timeout: Duration,
}
impl Default for ControlOptions {
    fn default() -> Self {
        Self {
            enabled: false,
            requester: String::new(),
            timeout: Duration::from_secs(120),
        }
    }
}
impl ControlOptions {
    pub fn from_env() -> Result<Self, String> {
        let enabled = ifm_ui_egui::backend::configuration::value("SupervisorControl__Enabled")?
            .unwrap_or_default();
        let timeout = ifm_ui_egui::backend::configuration::value("SupervisorControl__Timeout")?
            .unwrap_or_else(|| "00:02:00".into());
        let parts = timeout
            .split(':')
            .map(str::parse::<u64>)
            .collect::<Result<Vec<_>, _>>()
            .map_err(|_| "Invalid Supervisor timeout")?;
        if parts.len() != 3 || parts[1] >= 60 || parts[2] >= 60 || parts[0] > 0 {
            return Err("Supervisor timeout must be hh:mm:ss within ten minutes".into());
        }
        let seconds = parts[0] * 3600 + parts[1] * 60 + parts[2];
        if seconds == 0 || seconds > 600 {
            return Err("Supervisor timeout must be positive and at most ten minutes".into());
        }
        if !enabled.is_empty()
            && !enabled.eq_ignore_ascii_case("true")
            && !enabled.eq_ignore_ascii_case("false")
        {
            return Err("Invalid SupervisorControl__Enabled".into());
        }
        Ok(Self {
            enabled: enabled.eq_ignore_ascii_case("true"),
            requester: ifm_ui_egui::backend::configuration::value("SupervisorControl__Requester")?
                .unwrap_or_default(),
            timeout: Duration::from_secs(seconds),
        })
    }
    fn authority(&self, snapshot: &Json) -> bool {
        self.enabled
            && snapshot["ManualMutationEnabled"] == true
            && snapshot["SupervisorAuthorityState"] != "HostRecoveryRequired"
    }
}
#[derive(Clone)]
pub struct Target {
    pub thread: Json,
    pub generation: i64,
}
pub struct ActorProjection {
    pub result: ContractResult,
    pub targets: BTreeMap<String, Target>,
}
pub fn pascal(value: &Json) -> Json {
    match value {
        Json::Object(map) => Json::Object(
            map.iter()
                .map(|(k, v)| {
                    let name = k
                        .chars()
                        .next()
                        .map(|c| format!("{}{}", c.to_uppercase(), &k[c.len_utf8()..]))
                        .unwrap_or_default();
                    (name, pascal(v))
                })
                .collect(),
        ),
        Json::Array(values) => Json::Array(values.iter().map(pascal).collect()),
        _ => value.clone(),
    }
}
fn scalar(v: &Json) -> String {
    match v {
        Json::Null => String::new(),
        Json::String(s) => s.clone(),
        Json::Bool(b) => if *b { "True" } else { "False" }.into(),
        _ => v.to_string(),
    }
}
fn date_display(time: DateTime<Utc>) -> String {
    format!(
        "{}.{:07}Z",
        time.format("%Y-%m-%dT%H:%M:%S"),
        time.timestamp_subsec_nanos() / 100
    )
}
fn display(value: &Json, key: &str) -> String {
    if key.ends_with("Utc") {
        if let Some(text) = value.as_str() {
            if let Ok(time) = DateTime::parse_from_rfc3339(text) {
                return date_display(time.to_utc());
            }
            if let Ok(time) = chrono::NaiveDateTime::parse_from_str(text, "%Y-%m-%dT%H:%M:%S%.f") {
                return format!(
                    "{}.{:07}",
                    time.format("%Y-%m-%dT%H:%M:%S"),
                    time.nanosecond() / 100
                );
            }
        }
    }
    scalar(value)
}
fn flatten(value: &Json, prefix: &str, fields: &mut BTreeMap<String, String>) {
    match value {
        Json::Object(map) => {
            for (k, v) in map {
                flatten(
                    v,
                    &if prefix.is_empty() {
                        k.clone()
                    } else {
                        format!("{prefix}.{k}")
                    },
                    fields,
                )
            }
        }
        Json::Array(_) => {}
        _ => {
            fields.insert(prefix.into(), display(value, prefix));
        }
    }
}
fn utc(v: &Json) -> Result<DateTime<Utc>, String> {
    DateTime::parse_from_rfc3339(v.as_str().ok_or("Missing health timestamp")?)
        .map(|v| v.to_utc())
        .map_err(|_| "Invalid health timestamp".into())
}
fn thread_id(value: &Json) -> Result<String, String> {
    let name = value["Name"]
        .as_str()
        .filter(|s| !s.is_empty())
        .ok_or("Missing actor name")?;
    let entity = value["EntityId"].as_str().ok_or("Missing mailbox entity")?;
    let kind = value["ActorType"]
        .as_str()
        .ok_or("ActorType must be a named enum")?;
    Ok(format!("{kind}:{name}:{entity}"))
}
pub fn project(
    snapshot: &Json,
    from: DateTime<Utc>,
    options: &ControlOptions,
) -> Result<ActorProjection, String> {
    let observed = utc(&snapshot["ObservedUtc"])?;
    if observed.year_ce().1 <= 1 {
        return Err("Actor health observation is not initialized".into());
    }
    let actors = snapshot["Actors"].as_array().ok_or("Missing actors")?;
    if actors.len() > 4096 {
        return Err("Actor health exceeds its actor limit".into());
    }
    let mut rows = Vec::new();
    let mut targets = BTreeMap::new();
    let mut add = |id: String,
                   name: String,
                   parent: String,
                   value: &Json,
                   kind: &str,
                   status: String|
     -> Result<(), String> {
        if rows.len() >= 100_000 {
            return Err("Actor health exceeds its row limit".into());
        }
        let mut fields = BTreeMap::new();
        flatten(value, "", &mut fields);
        fields.extend(BTreeMap::from([
            ("name".into(), name),
            ("parent".into(), parent),
            ("kind".into(), kind.into()),
            ("status".into(), status),
            (
                "canExecute".into(),
                matches!(kind, "mailbox" | "incident").to_string(),
            ),
        ]));
        rows.push(ContractRow {
            id,
            fields,
            ..Default::default()
        });
        Ok(())
    };
    let mut actors: Vec<_> = actors.iter().collect();
    actors.sort_by_key(|a| (scalar(&a["Domain"]), scalar(&a["ActorId"]["Name"])));
    for actor in actors {
        let name = scalar(&actor["ActorId"]["Name"]);
        let parent = format!("actor:{}:{name}", scalar(&actor["ActorId"]["ActorType"]));
        add(
            parent.clone(),
            name,
            scalar(&actor["Domain"]),
            actor,
            "actor",
            scalar(&actor["Status"]),
        )?;
        let mut mailboxes: Vec<_> = actor["Mailboxes"]
            .as_array()
            .ok_or("Missing mailboxes")?
            .iter()
            .collect();
        mailboxes.sort_by_key(|m| scalar(&m["ThreadId"]["EntityId"]));
        for mailbox in mailboxes {
            let thread = mailbox["ThreadId"].clone();
            let id = format!("mailbox:{}", thread_id(&thread)?);
            let status = if mailbox["IsProcessing"] == true
                || mailbox["QueueDepth"].as_i64().unwrap_or(0) > 0
            {
                "Yellow"
            } else if !mailbox["LastFailedUtc"].is_null()
                && (mailbox["LastCompletedUtc"].is_null()
                    || utc(&mailbox["LastFailedUtc"])? >= utc(&mailbox["LastCompletedUtc"])?)
            {
                "Red"
            } else {
                "Green"
            };
            add(
                id.clone(),
                scalar(&thread["EntityId"]),
                parent.clone(),
                mailbox,
                "mailbox",
                status.into(),
            )?;
            targets.insert(
                id,
                Target {
                    thread,
                    generation: mailbox["Generation"]
                        .as_i64()
                        .ok_or("Missing mailbox generation")?,
                },
            );
        }
    }
    let empty = Vec::new();
    for value in snapshot["Failures"].as_array().unwrap_or(&empty) {
        add(
            format!("failure:{}", scalar(&value["FailureId"])),
            format!(
                "{}.{}",
                scalar(&value["ActorId"]["Name"]),
                scalar(&value["Verb"])
            ),
            "Failure history".into(),
            value,
            "failure",
            "Red".into(),
        )?;
    }
    for value in snapshot["Projectors"].as_array().unwrap_or(&empty) {
        let name = scalar(&value["ActorName"]);
        let projector = scalar(&value["ProjectorName"]);
        add(
            format!("projector:{name}:{projector}"),
            format!("{name} / {projector}"),
            "Event projectors".into(),
            value,
            "projector",
            if value["IsReady"] == true {
                "Green"
            } else {
                "Red"
            }
            .into(),
        )?;
    }
    for value in snapshot["Incidents"].as_array().unwrap_or(&empty) {
        let thread = value["ThreadId"].clone();
        let id = format!("incident:{}", thread_id(&thread)?);
        add(
            id.clone(),
            format!(
                "{}.{}",
                scalar(&thread["Name"]),
                scalar(&thread["EntityId"])
            ),
            "Active incidents".into(),
            value,
            "incident",
            if value["Health"] == "Degraded" {
                "Yellow"
            } else {
                "Red"
            }
            .into(),
        )?;
        targets.insert(
            id,
            Target {
                thread,
                generation: value["Generation"]
                    .as_i64()
                    .ok_or("Missing incident generation")?,
            },
        );
    }
    for value in snapshot["Operations"].as_array().unwrap_or(&empty) {
        add(
            format!("operation:{}", scalar(&value["OperationId"])),
            format!(
                "{} {}",
                utc(&value["StartedUtc"])?.format("%Y-%m-%d %H:%M:%SZ"),
                scalar(&value["ThreadId"]["Name"])
            ),
            "Lifecycle operations".into(),
            value,
            "operation",
            if value["Outcome"] == "Succeeded" {
                "Green"
            } else {
                "Yellow"
            }
            .into(),
        )?;
    }
    for value in snapshot["History"].as_array().unwrap_or(&empty) {
        add(
            format!("history:{}", scalar(&value["Revision"])),
            utc(&value["ObservedUtc"])?
                .format("%Y-%m-%d %H:%M:%SZ")
                .to_string(),
            "Health history".into(),
            value,
            "history",
            if value["CriticalActors"].as_i64().unwrap_or(0) > 0 {
                "Red"
            } else if value["DegradedActors"].as_i64().unwrap_or(0) > 0 {
                "Yellow"
            } else {
                "Green"
            }
            .into(),
        )?;
    }
    for value in snapshot["Workers"].as_array().unwrap_or(&empty) {
        let id = scalar(&value["WorkerId"]);
        add(
            format!("worker:{id}"),
            format!("Worker {id}"),
            "Shared workers".into(),
            value,
            "worker",
            if value["IsFaulted"] == true {
                "Red"
            } else {
                "Green"
            }
            .into(),
        )?;
    }
    let status = scalar(&snapshot["OverallStatus"]);
    add(
        "collection".into(),
        "Collection".into(),
        "Health collection".into(),
        &snapshot["Collection"],
        "collection",
        status.clone(),
    )?;
    add(
        "polling".into(),
        "Polling".into(),
        "Health collection".into(),
        &snapshot["Polling"],
        "polling",
        if snapshot["Polling"]["IsAlive"] == true {
            "Green"
        } else {
            "Red"
        }
        .into(),
    )?;
    let controls = options.authority(snapshot);
    Ok(ActorProjection {
        targets,
        result: ContractResult {
            view: "actor-health".into(),
            rows,
            choices: BTreeMap::from([(
                "operations".into(),
                OPERATIONS.map(str::to_string).to_vec(),
            )]),
            context: BTreeMap::from([
                (
                    "summary".into(),
                    format!(
                        "{status} | Actors {}/{} running | Messages waiting {} | Observed {}",
                        scalar(&snapshot["RunningActorCount"]),
                        scalar(&snapshot["ActorCount"]),
                        scalar(&snapshot["QueuedMessageCount"]),
                        observed.format("%Y-%m-%d %H:%M:%SZ")
                    ),
                ),
                ("status".into(), status),
                ("controls".into(), controls.to_string()),
                ("fromUtc".into(), date_display(from)),
                (
                    "observation".into(),
                    if controls {
                        "Controlled; refreshes every 15 seconds."
                    } else {
                        "Observation only; refreshes every 15 seconds."
                    }
                    .into(),
                ),
            ]),
            ..Default::default()
        },
    })
}
pub struct ActorHealthController {
    client: HealthClient,
    endpoint: Option<String>,
    options: ControlOptions,
    from: Option<DateTime<Utc>>,
    snapshot: Option<Json>,
    targets: BTreeMap<String, Target>,
    generation: u64,
    history: BTreeMap<String, (Json, EditorOutcome)>,
    result: ContractResult,
}
impl ActorHealthController {
    pub fn new(endpoint: Option<String>, options: ControlOptions) -> Result<Self, String> {
        Ok(Self {
            client: HealthClient::new()?,
            endpoint,
            options,
            from: None,
            snapshot: None,
            targets: BTreeMap::new(),
            generation: 0,
            history: BTreeMap::new(),
            result: ContractResult {
                view: "actor-health".into(),
                ..Default::default()
            },
        })
    }
    pub fn from_env() -> Result<Self, String> {
        Self::new(
            ifm_ui_egui::backend::configuration::value("IFM_UI_ACTOR_HEALTH_URL")?,
            ControlOptions::from_env()?,
        )
    }
    async fn refresh(&mut self, backend: &Backend) -> Result<(), String> {
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let result = tokio::time::timeout_at(deadline, async {
            let mut url = reqwest::Url::parse(
                self.endpoint
                    .as_deref()
                    .ok_or("Actor health endpoint is not configured")?,
            )
            .map_err(|_| "Invalid actor health endpoint")?;
            url.set_query(None);
            // The endpoint supplies ToUtc and its default history window from
            // the supervisor's operational clock, including while markets close.
            if let Some(from) = self.from {
                url.query_pairs_mut()
                    .append_pair("fromUtc", &from.to_rfc3339());
            }
            let snapshot = pascal(
                &self
                    .client
                    .get(url.as_str(), 8 * 1024 * 1024, deadline)
                    .await?,
            );
            let observed = utc(&snapshot["ObservedUtc"])?;
            let from = self.from.unwrap_or(observed - chrono::Duration::hours(1));
            if from > observed {
                return Err("The From time must be before the API observation time".into());
            }
            let projection = project(&snapshot, from, &self.options)?;
            Ok::<_, String>((from, snapshot, projection))
        })
        .await
        .map_err(|_| "Actor health deadline expired".to_string())
        .and_then(|r| r);
        match result {
            Ok((from, snapshot, projection)) => {
                self.from = Some(from);
                self.snapshot = Some(snapshot);
                self.targets = projection.targets;
                self.result = projection.result;
                Ok(())
            }
            Err(error) => {
                self.snapshot = None;
                self.targets.clear();
                self.result = ContractResult {
                    view: "actor-health".into(),
                    context: BTreeMap::from([
                        (
                            "summary".into(),
                            format!("Actor health unavailable: {error}"),
                        ),
                        ("status".into(), "Red".into()),
                        ("controls".into(), "false".into()),
                    ]),
                    ..Default::default()
                };
                Err(error)
            }
        }
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
        if intent["schema"] != 1 || intent["view"] != "actor-health" {
            return Err("Invalid actor-health intent".into());
        }
        let result: EditorOutcome = if let Some((prior, outcome)) = self.history.get(&id) {
            if prior != intent {
                return Err("RequestId belongs to another actor operation".into());
            }
            outcome.clone()
        } else {
            if self.history.len() >= 256 && self.history.values().all(|(_, o)| o.state == "Unknown")
            {
                return Err("Reconcile uncertain Supervisor operations before continuing".into());
            }
            let action = intent["intent"].as_str().unwrap_or_default();
            let operation = match action {
                "load" => {
                    if let Some(from) = intent["fields"]["fromUtc"]
                        .as_str()
                        .filter(|s| !s.trim().is_empty())
                    {
                        self.from = Some(utc(&Json::String(from.into()))?);
                    }
                    self.refresh(backend).await.map(|_| "Completed".to_string())
                }
                "supervise" => self.supervise(backend, registry, intent, &id).await,
                _ => return Err("Unsupported actor health action".into()),
            };
            let outcome = match operation {
                Ok(message) => EditorOutcome {
                    request_id: id.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(error) => EditorOutcome {
                    request_id: id.clone(),
                    state: if error.starts_with("OutcomeUnknown:") {
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
                    .find(|(_, (_, o))| o.state != "Unknown")
                    .map(|(k, _)| k.clone())
                {
                    self.history.remove(&key);
                } else {
                    return Err(
                        "Reconcile uncertain Supervisor operations before continuing".into(),
                    );
                }
            }
            self.history
                .insert(id.clone(), (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: result.message.clone(),
            outcomes: vec![result],
            result: self.result.clone(),
            ..Default::default()
        })
    }
    async fn supervise(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
    ) -> Result<String, String> {
        if intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("Selected mailbox generation changed; refresh health".into());
        }
        if !self
            .snapshot
            .as_ref()
            .is_some_and(|s| self.options.authority(s))
            || self.options.requester.trim().is_empty()
            || self.options.timeout.is_zero()
            || self.options.timeout > Duration::from_secs(600)
        {
            return Err("Supervisor operation is unavailable".into());
        }
        let reason = intent["fields"]["reason"].as_str().unwrap_or_default();
        if reason.trim().is_empty() || reason.encode_utf16().count() > 1024 {
            return Err("An operational reason of at most 1024 characters is required".into());
        }
        let operation = intent["fields"]["operation"]
            .as_str()
            .filter(|v| OPERATIONS.contains(v))
            .ok_or("Unsupported Supervisor operation")?;
        let target = self
            .targets
            .get(
                intent["originalId"]
                    .as_str()
                    .ok_or("Select a mailbox or incident")?,
            )
            .ok_or("Selected mailbox is no longer loaded")?
            .clone();
        let kind = format!(
            "TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands.{}SupervisorActorCommand",
            operation
        );
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: "none".into(),
        };
        let actor_type = registry.enum_value(
            "TomasAI.IFM.Shared.EventModelActor.ActorType",
            target.thread["ActorType"]
                .as_str()
                .ok_or("Invalid ActorType")?,
        )?;
        let name = target.thread["Name"]
            .as_str()
            .ok_or("Missing target name")?;
        let mailbox = Value::Map(vec![
            ("ActorType".into(), actor_type.into()),
            ("Name".into(), name.into()),
        ]);
        let target_wire = Value::Map(vec![
            ("ActorType".into(), actor_type.into()),
            ("Name".into(), name.into()),
            (
                "EntityId".into(),
                target.thread["EntityId"]
                    .as_str()
                    .ok_or("Missing entity")?
                    .into(),
            ),
            ("MailboxId".into(), mailbox),
        ]);
        let body = registry.encode(
            &kind,
            &BTreeMap::from([
                ("CommandId".into(), id.into()),
                ("Subject".into(), subject.wire()),
                ("PostEvents".into(), true.into()),
                ("EntityId".into(), Value::Array(vec!["none".into()])),
                ("ErrorCode".into(), registry.error_id(&kind)?.into()),
                (
                    "RouteTo".into(),
                    registry
                        .enum_value(
                            "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                            "SupervisorBoundedContext",
                        )?
                        .into(),
                ),
                ("Target".into(), target_wire),
                ("ExpectedGeneration".into(), target.generation.into()),
                ("Requester".into(), self.options.requester.clone().into()),
                ("Reason".into(), reason.trim().into()),
                (
                    "TimeoutTicks".into(),
                    (self.options.timeout.as_nanos() as i64 / 100).into(),
                ),
            ]),
        )?;
        let response = backend
            .request(&subject, &body, Some(self.options.timeout), None)
            .await
            .map_err(|e| format!("OutcomeUnknown: {e}"))?;
        if let Err(error) = service_result(&response) {
            return Err(
                if response
                    .as_array()
                    .and_then(|v| v.first())
                    .and_then(Value::as_bool)
                    == Some(false)
                {
                    error
                } else {
                    format!("OutcomeUnknown: invalid Supervisor acknowledgement: {error}")
                },
            );
        }
        let message =
            format!("Supervisor command {id} accepted; refresh health for the projected outcome.");
        let _ = self.refresh(backend).await;
        self.result
            .context
            .insert("outcome".into(), message.clone());
        Ok(message)
    }
}
