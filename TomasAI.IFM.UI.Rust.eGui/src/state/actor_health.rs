//! Actor explorer and generation-bound Supervisor operations. HTTP owns health
//! observation; existing NATS commands own lifecycle changes. UI time is API time.
use crate::models::{
    actor_health::{ActorHealthSnapshot, ActorHealthThreadId},
    cells::{Cell, Cells},
    health_fields::HealthFields,
};
use crate::{
    backend::{contracts::*, health_http::HealthClient, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
};
use chrono::{DateTime, Datelike, Utc};
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
        let enabled =
            crate::backend::configuration::value("SupervisorControl__Enabled")?.unwrap_or_default();
        let timeout = crate::backend::configuration::value("SupervisorControl__Timeout")?
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
            requester: crate::backend::configuration::value("SupervisorControl__Requester")?
                .unwrap_or_default(),
            timeout: Duration::from_secs(seconds),
        })
    }
    fn authority(&self, snapshot: &ActorHealthSnapshot) -> bool {
        self.enabled
            && snapshot.cell("ManualMutationEnabled") == Cell::Boolean(true)
            && snapshot.cell("SupervisorAuthorityState").as_str() != Some("HostRecoveryRequired")
    }
}
#[derive(Clone)]
pub struct Target {
    pub thread: ActorHealthThreadId,
    pub generation: i64,
}
pub struct ActorProjection {
    pub result: ContractResult,
    pub targets: BTreeMap<String, Target>,
}
fn scalar(value: &Cell<'_>) -> String {
    match value {
        Cell::Boolean(value) => if *value { "True" } else { "False" }.into(),
        _ => value.text(),
    }
}
fn date_display(time: DateTime<Utc>) -> String {
    format!(
        "{}.{:07}Z",
        time.format("%Y-%m-%dT%H:%M:%S"),
        time.timestamp_subsec_nanos() / 100
    )
}
fn utc(v: &Cell<'_>) -> Result<DateTime<Utc>, String> {
    DateTime::parse_from_rfc3339(v.as_str().ok_or("Missing health timestamp")?)
        .map(|v| v.to_utc())
        .map_err(|_| "Invalid health timestamp".into())
}
fn thread_id(value: &(impl Cells + ?Sized)) -> Result<String, String> {
    let name = value.cell("Name");
    let name = name
        .as_str()
        .filter(|value| !value.is_empty())
        .ok_or("Missing actor name")?;
    let entity = value.cell("EntityId");
    let entity = entity.as_str().ok_or("Missing mailbox entity")?;
    let kind = value.cell("ActorType");
    let kind = kind.as_str().ok_or("ActorType must be a named enum")?;
    Ok(format!("{kind}:{name}:{entity}"))
}

pub fn project(
    snapshot: &ActorHealthSnapshot,
    from: DateTime<Utc>,
    options: &ControlOptions,
) -> Result<ActorProjection, String> {
    let observed = utc(&snapshot.cell("ObservedUtc"))?;
    if observed.year_ce().1 <= 1 {
        return Err("Actor health observation is not initialized".into());
    }
    let actors = snapshot.actors.as_ref().ok_or("Missing actors")?;
    if actors.len() > 4096 {
        return Err("Actor health exceeds its actor limit".into());
    }
    let mut rows = Vec::new();
    let mut targets = BTreeMap::new();
    let mut add = |id: String,
                   name: String,
                   parent: String,
                   value: &dyn HealthFields,
                   kind: &str,
                   status: String|
     -> Result<(), String> {
        if rows.len() >= 100_000 {
            return Err("Actor health exceeds its row limit".into());
        }
        let mut fields = BTreeMap::new();
        value.fields("", &mut fields);
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
    actors.sort_by_key(|a| (scalar(&a.cell("Domain")), scalar(&a.actor_id.cell("Name"))));
    for actor in actors {
        let name = scalar(&actor.actor_id.cell("Name"));
        let parent = format!("actor:{}:{name}", scalar(&actor.actor_id.cell("ActorType")));
        add(
            parent.clone(),
            name,
            scalar(&actor.cell("Domain")),
            actor,
            "actor",
            scalar(&actor.cell("Status")),
        )?;
        let mut mailboxes: Vec<_> = actor
            .mailboxes
            .as_ref()
            .ok_or("Missing mailboxes")?
            .iter()
            .collect();
        mailboxes.sort_by_key(|m| scalar(&m.thread_id.cell("EntityId")));
        for mailbox in mailboxes {
            let thread = mailbox
                .thread_id
                .as_ref()
                .ok_or("Missing mailbox thread identity")?
                .clone();
            let id = format!("mailbox:{}", thread_id(&thread)?);
            let status = if mailbox.cell("IsProcessing") == Cell::Boolean(true)
                || mailbox.cell("QueueDepth").as_i64().unwrap_or(0) > 0
            {
                "Yellow"
            } else if !mailbox.cell("LastFailedUtc").is_missing()
                && (mailbox.cell("LastCompletedUtc").is_missing()
                    || utc(&mailbox.cell("LastFailedUtc"))?
                        >= utc(&mailbox.cell("LastCompletedUtc"))?)
            {
                "Red"
            } else {
                "Green"
            };
            add(
                id.clone(),
                scalar(&thread.cell("EntityId")),
                parent.clone(),
                mailbox,
                "mailbox",
                status.into(),
            )?;
            targets.insert(
                id,
                Target {
                    thread,
                    generation: mailbox
                        .cell("Generation")
                        .as_i64()
                        .ok_or("Missing mailbox generation")?,
                },
            );
        }
    }
    for value in snapshot.failures.as_ref().map(Vec::as_slice).unwrap_or(&[]) {
        add(
            format!("failure:{}", scalar(&value.cell("FailureId"))),
            format!(
                "{}.{}",
                scalar(&value.actor_id.cell("Name")),
                scalar(&value.cell("Verb"))
            ),
            "Failure history".into(),
            value,
            "failure",
            "Red".into(),
        )?;
    }
    for value in snapshot
        .projectors
        .as_ref()
        .map(Vec::as_slice)
        .unwrap_or(&[])
    {
        let name = scalar(&value.cell("ActorName"));
        let projector = scalar(&value.cell("ProjectorName"));
        add(
            format!("projector:{name}:{projector}"),
            format!("{name} / {projector}"),
            "Event projectors".into(),
            value,
            "projector",
            if value.cell("IsReady") == Cell::Boolean(true) {
                "Green"
            } else {
                "Red"
            }
            .into(),
        )?;
    }
    for value in snapshot
        .incidents
        .as_ref()
        .map(Vec::as_slice)
        .unwrap_or(&[])
    {
        let thread = value
            .thread_id
            .as_ref()
            .ok_or("Missing incident thread identity")?
            .clone();
        let id = format!("incident:{}", thread_id(&thread)?);
        add(
            id.clone(),
            format!(
                "{}.{}",
                scalar(&thread.cell("Name")),
                scalar(&thread.cell("EntityId"))
            ),
            "Active incidents".into(),
            value,
            "incident",
            if value.cell("Health").as_str() == Some("Degraded") {
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
                generation: value
                    .cell("Generation")
                    .as_i64()
                    .ok_or("Missing incident generation")?,
            },
        );
    }
    for value in snapshot
        .operations
        .as_ref()
        .map(Vec::as_slice)
        .unwrap_or(&[])
    {
        add(
            format!("operation:{}", scalar(&value.cell("OperationId"))),
            format!(
                "{} {}",
                utc(&value.cell("StartedUtc"))?.format("%Y-%m-%d %H:%M:%SZ"),
                scalar(&value.thread_id.cell("Name"))
            ),
            "Lifecycle operations".into(),
            value,
            "operation",
            if value.cell("Outcome").as_str() == Some("Succeeded") {
                "Green"
            } else {
                "Yellow"
            }
            .into(),
        )?;
    }
    for value in snapshot.history.as_ref().map(Vec::as_slice).unwrap_or(&[]) {
        add(
            format!("history:{}", scalar(&value.cell("Revision"))),
            utc(&value.cell("ObservedUtc"))?
                .format("%Y-%m-%d %H:%M:%SZ")
                .to_string(),
            "Health history".into(),
            value,
            "history",
            if value.cell("CriticalActors").as_i64().unwrap_or(0) > 0 {
                "Red"
            } else if value.cell("DegradedActors").as_i64().unwrap_or(0) > 0 {
                "Yellow"
            } else {
                "Green"
            }
            .into(),
        )?;
    }
    for value in snapshot.workers.as_ref().map(Vec::as_slice).unwrap_or(&[]) {
        let id = scalar(&value.cell("WorkerId"));
        add(
            format!("worker:{id}"),
            format!("Worker {id}"),
            "Shared workers".into(),
            value,
            "worker",
            if value.cell("IsFaulted") == Cell::Boolean(true) {
                "Red"
            } else {
                "Green"
            }
            .into(),
        )?;
    }
    let status = scalar(&snapshot.cell("OverallStatus"));
    add(
        "collection".into(),
        "Collection".into(),
        "Health collection".into(),
        &snapshot.collection,
        "collection",
        status.clone(),
    )?;
    add(
        "polling".into(),
        "Polling".into(),
        "Health collection".into(),
        &snapshot.polling,
        "polling",
        if snapshot.polling.cell("IsAlive") == Cell::Boolean(true) {
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
                        scalar(&snapshot.cell("RunningActorCount")),
                        scalar(&snapshot.cell("ActorCount")),
                        scalar(&snapshot.cell("QueuedMessageCount")),
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
    snapshot: Option<ActorHealthSnapshot>,
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
            crate::backend::configuration::value("IFM_UI_ACTOR_HEALTH_URL")?,
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
            let snapshot = self
                .client
                .get_typed_observation::<ActorHealthSnapshot>(
                    url.as_str(),
                    8 * 1024 * 1024,
                    deadline,
                )
                .await?
                .value;
            let observed = utc(&snapshot.cell("ObservedUtc"))?;
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
                        self.from = Some(utc(&Cell::Text(from.into()))?);
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
            target
                .thread
                .cell("ActorType")
                .as_str()
                .ok_or("Invalid ActorType")?,
        )?;
        let name = target
            .thread
            .name
            .as_ref()
            .map(String::as_str)
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
                target
                    .thread
                    .cell("EntityId")
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
