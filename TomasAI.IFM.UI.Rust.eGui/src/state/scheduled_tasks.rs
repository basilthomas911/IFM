//! Scheduler UI ownership. Cron evaluation, execution and installation remain
//! backend operations. Run/output cursors are scoped to their selected schedule.
use crate::backend::contracts::ActorSubject;
use crate::models::{
    cells::{Cell, Cells},
    labels::PublishedEnum,
    scheduled::{Preview, Project, Run},
    trade_contracts::*,
    wire_types::FromBackend,
};
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{actor_requests, business_context::BusinessContext},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.";
fn model(name: &str) -> String {
    format!("{ROOT}Contracts.{name}")
}
fn field<'a>(intent: &'a Json, name: &str) -> &'a str {
    intent["fields"][name].as_str().unwrap_or_default().trim()
}
fn text(row: &impl Cells, name: &str) -> String {
    row.cell(name).text()
}
fn display(v: Cell<'_>) -> String {
    match v {
        Cell::Boolean(v) => if v { "True" } else { "False" }.into(),
        _ => v.text(),
    }
}
fn id_wire(id: &str) -> Result<Value, String> {
    let id = uuid::Uuid::parse_str(id).map_err(|_| "Invalid scheduled task identity")?;
    Ok(Value::Array(vec![id.to_string().into()]))
}
fn id(identity: &Option<ScheduledTaskId>) -> Result<String, String> {
    identity
        .as_ref()
        .and_then(|id| id.value.as_deref())
        .filter(|s| uuid::Uuid::parse_str(s).is_ok_and(|id| !id.is_nil()))
        .map(str::to_string)
        .ok_or("Missing scheduled task identity".into())
}
fn member<'a>(wire: &'a Value, index: usize) -> Result<&'a Value, String> {
    wire.as_array()
        .and_then(|v| v.get(index))
        .ok_or("Incomplete scheduled-task payload".into())
}
fn offset(wire: &Value, index: usize) -> Result<Option<String>, String> {
    let value = member(wire, index)?;
    Ok(if value.is_nil() {
        None
    } else {
        Some(crate::backend::contracts::reference_offset(value)?)
    })
}
fn document<T: serde::Serialize>(value: &T) -> Result<serde_bytes::ByteBuf, String> {
    crate::wire::encode(value).map(Into::into)
}
/// Map one persisted run exactly as the hybrid service; no business outcomes or
/// EOD receipts are recomputed on the client. Raw timestamps retain their zones.
pub fn run_document(_registry: &Registry, wire: &Value) -> Result<Run, String> {
    let row = ScheduledTaskRun::from_backend(wire)?;
    Ok(Run {
        id: id(&row.id)?,
        status: row.status.as_ref().map(PublishedEnum::published_label),
        stage: row.stage,
        detail: row.detail,
        exit_code: row.exit_code,
        value_date: row.value_date,
        revision: row.revision,
        operation_command_id: row.operation_command_id,
        process_id: row.process_id,
        standard_output_tail: row.standard_output_tail,
        standard_error_tail: row.standard_error_tail,
        output_directory: row.output_directory,
        intended_fire_time_utc: offset(wire, 7)?,
        started_at_utc: offset(wire, 10)?,
        finished_at_utc: offset(wire, 11)?,
    })
}
pub fn schedule_input(registry: &Registry, intent: &Json) -> Result<Value, String> {
    let boolean = match field(intent, "oneTime").to_ascii_lowercase().as_str() {
        "true" => true,
        "false" => false,
        _ => return Err("Select valid schedule timing".into()),
    };
    let mut input = BTreeMap::new();
    for (key, name) in [
        ("taskKey", "TaskKey"),
        ("name", "Name"),
        ("environment", "Environment"),
        ("hostId", "HostId"),
        ("expression", "Expression"),
        ("timeZoneId", "TimeZoneId"),
        ("description", "Description"),
    ] {
        input.insert(name.into(), json!(field(intent, key)));
    }
    for (key, name) in [
        ("maximumRuntimeSeconds", "MaximumRuntimeSeconds"),
        ("dispatchToleranceSeconds", "DispatchToleranceSeconds"),
    ] {
        input.insert(
            name.into(),
            json!(
                field(intent, key)
                    .parse::<i32>()
                    .map_err(|_| format!("Invalid {key}"))?
            ),
        );
    }
    for (key, name) in [("startsAtUtc", "StartsAtUtc"), ("endsAtUtc", "EndsAtUtc")] {
        input.insert(
            name.into(),
            if field(intent, key).is_empty() {
                Json::Null
            } else {
                json!(field(intent, key))
            },
        );
    }
    input.insert(
        "Timing".into(),
        json!(if boolean { "OneTime" } else { "Cron" }),
    );
    let kind = model("ScheduledTaskSchedule");
    registry.patch(&kind, &registry.encode(&kind, &BTreeMap::new())?, &input)
}
#[derive(Default)]
pub struct ScheduledController {
    generation: u64,
    loaded: bool,
    environment: String,
    host: String,
    result: ContractResult,
    definitions: BTreeMap<String, Value>,
    histories: BTreeMap<String, Vec<Value>>,
    cursors: BTreeMap<String, Option<Vec<u8>>>,
    next_fires: BTreeMap<String, String>,
    history_schedule: Option<String>,
    preview: Option<Preview>,
    output: String,
    output_run: Option<String>,
    output_offset: i64,
    output_end: bool,
    history: BTreeMap<String, (Json, EditorOutcome)>,
    subscription: Option<async_nats::Subscriber>,
    subscribed: bool,
    observer: Option<tokio::task::JoinHandle<()>>,
}
impl Drop for ScheduledController {
    fn drop(&mut self) {
        if let Some(task) = self.observer.take() {
            task.abort();
        }
    }
}
impl ScheduledController {
    pub fn take_subscription(&mut self) -> Option<async_nats::Subscriber> {
        self.subscription.take()
    }
    pub fn set_observer(&mut self, task: tokio::task::JoinHandle<()>) {
        if let Some(previous) = self.observer.replace(task) {
            previous.abort();
        }
    }
    pub fn observer_unavailable(&mut self) {
        self.subscribed = false;
    }
    async fn query(
        &self,
        backend: &Backend,
        registry: &Registry,
        name: &str,
        mut fields: BTreeMap<String, Value>,
    ) -> Result<Value, String> {
        // These concrete C# query types initialize PageSize to 50. A CLR
        // primitive zero default is not the same contract as that initializer.
        fields.entry("PageSize".into()).or_insert(50.into());
        let query_id = uuid::Uuid::new_v4();
        fields
            .entry("EntityId".into())
            .or_insert(Value::Array(vec![query_id.to_string().into()]));
        let identity = fields["EntityId"]
            .as_array()
            .and_then(|v| v.first())
            .and_then(Value::as_str)
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .ok_or("Invalid scheduler query identity")?;
        fields.insert("Environment".into(), self.environment.clone().into());
        fields.insert("HostId".into(), self.host.clone().into());
        actor_requests::query(
            backend,
            registry,
            &format!("{ROOT}Queries.{name}"),
            &identity.simple().to_string(),
            fields,
        )
        .await
    }
    async fn preview(
        &self,
        backend: &Backend,
        registry: &Registry,
        schedule: Value,
    ) -> Result<Preview, String> {
        let wire = self
            .query(
                backend,
                registry,
                "PreviewScheduledTaskScheduleQuery",
                BTreeMap::from([("Schedule".into(), schedule)]),
            )
            .await?;
        let row = ScheduledTaskSchedulePreview::from_backend(&wire)?;
        let times = member(&wire, 2)?
            .as_array()
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid schedule preview occurrences")?
            .iter()
            .map(crate::backend::contracts::reference_offset)
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Preview {
            valid: row.valid,
            errors: row.errors,
            next_fire_times_utc: times,
        })
    }
    async fn history_page(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        schedule: &str,
        next: bool,
        merge: bool,
    ) -> Result<(), String> {
        let cursor = if next {
            self.cursors
                .get(schedule)
                .and_then(|v| v.clone())
                .ok_or("No continuation for this schedule")?
        } else {
            Vec::new()
        };
        let wire = self
            .query(
                backend,
                registry,
                "GetScheduledTaskRunHistoryQuery",
                BTreeMap::from([
                    ("ScheduleId".into(), id_wire(schedule)?),
                    ("PageSize".into(), 100.into()),
                    (
                        "PagingState".into(),
                        if next {
                            Value::Binary(cursor.clone())
                        } else {
                            Value::Nil
                        },
                    ),
                ]),
            )
            .await?;
        let rows = member(&wire, 0)?
            .as_array()
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid or oversized run history")?;
        let continuation = match member(&wire, 1)? {
            Value::Nil => None,
            Value::Binary(v) if v.len() <= 16384 => Some(v.clone()),
            _ => return Err("Invalid run history cursor".into()),
        };
        if next && continuation.as_ref() == Some(&cursor) {
            return Err("Run history page did not advance".into());
        }
        let mut all = if next || merge {
            self.histories.get(schedule).cloned().unwrap_or_default()
        } else {
            Vec::new()
        };
        // New observations replace an older receipt of the same occurrence. The
        // retained history is bounded; dropped display rows never relaunch a job.
        let mut indexed = BTreeMap::new();
        for row in all.drain(..).chain(rows.iter().cloned()) {
            let projected = ScheduledTaskRun::from_backend(&row)?;
            if projected
                .schedule_id
                .as_ref()
                .and_then(|id| id.value.as_deref())
                != Some(schedule)
                || projected.environment.as_deref() != Some(self.environment.as_str())
                || projected.host_id.as_deref() != Some(self.host.as_str())
            {
                return Err("Run history crossed the selected schedule or host".into());
            }
            indexed.insert(id(&projected.id)?, row);
        }
        if indexed.len() > 100_000 {
            return Err("Run history display bound reached".into());
        }
        all = indexed.into_values().collect();
        all.sort_by_key(|v| member(v, 7).ok().and_then(|v| datetime_offset(v).ok()));
        self.histories.insert(schedule.into(), all);
        // A notification refresh merges observations, not the user's paging
        // position. Replacing it here would resurrect an exhausted More button.
        if !merge {
            self.cursors.insert(schedule.into(), continuation);
        }

        Ok(())
    }
    async fn load_dashboard(
        &mut self,
        backend: &Backend,
        registry: &Registry,
    ) -> Result<(), String> {
        let wire = self
            .query(
                backend,
                registry,
                "GetScheduledTasksDashboardQuery",
                BTreeMap::from([
                    ("PageSize".into(), 100.into()),
                    (
                        "SelectedScheduleId".into(),
                        id_wire(
                            self.history_schedule
                                .as_deref()
                                .unwrap_or("00000000-0000-0000-0000-000000000000"),
                        )?,
                    ),
                ]),
            )
            .await?;
        let row = ScheduledTasksDashboard::from_backend(&wire)?;
        let definitions = member(&wire, 1)?
            .as_array()
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid or oversized schedules")?;
        let mut result = ContractResult {
            view: "scheduled-tasks".into(),
            ..Default::default()
        };
        let mut models = BTreeMap::new();
        for wire in definitions {
            let row = ScheduledTaskDefinition::from_backend(wire)?;
            if row.schedule.as_ref().and_then(|s| s.environment.as_deref())
                != Some(self.environment.as_str())
                || row.schedule.as_ref().and_then(|s| s.host_id.as_deref())
                    != Some(self.host.as_str())
            {
                return Err("Schedule dashboard crossed its requested host".into());
            }
            if row.removed == Some(true) {
                continue;
            }
            let identity = id(&row.id)?;
            let schedule = row.schedule.as_ref().ok_or("Missing task schedule")?;
            let schedule_wire = member(wire, 3)?;
            let mut fields = BTreeMap::new();
            for (key, name) in [
                ("taskKey", "TaskKey"),
                ("name", "Name"),
                ("environment", "Environment"),
                ("hostId", "HostId"),
                ("expression", "Expression"),
                ("timeZoneId", "TimeZoneId"),
                ("maximumRuntimeSeconds", "MaximumRuntimeSeconds"),
                ("dispatchToleranceSeconds", "DispatchToleranceSeconds"),
                ("description", "Description"),
            ] {
                fields.insert(key.into(), display(schedule.cell(name)));
            }
            fields.insert(
                "oneTime".into(),
                if schedule.timing == Some(ScheduledTaskTiming::OneTime) {
                    "True"
                } else {
                    "False"
                }
                .into(),
            );
            for (key, index) in [("startsAtUtc", 7), ("endsAtUtc", 8)] {
                fields.insert(
                    key.into(),
                    offset(schedule_wire, index)?.unwrap_or_default(),
                );
            }
            for (key, name) in [
                ("enabled", "Enabled"),
                ("installationStatus", "InstallationStatus"),
                ("installationDetail", "InstallationDetail"),
                ("desiredRevision", "DesiredRevision"),
                ("appliedRevision", "AppliedRevision"),
                ("activeRunId", "ActiveRunId"),
                ("revision", "Revision"),
            ] {
                fields.insert(key.into(), display(row.cell(name)));
            }
            if models.insert(identity.clone(), wire.clone()).is_some() {
                return Err("Duplicate schedule identity".into());
            }
            result.rows.push(ContractRow {
                id: identity,
                fields,
                data: None,
            });
        }
        let catalog = row.catalog.as_ref().ok_or("Missing task catalog")?;
        let projects = catalog.projects.as_deref().unwrap_or_default();
        if projects.len() > 10_000 {
            return Err("Oversized task catalog".into());
        }
        result.choices.insert(
            "taskKey".into(),
            projects
                .iter()
                .filter(|p| p.available == Some(true))
                .map(|p| p.task_key.clone().unwrap_or_default())
                .collect(),
        );
        let project_docs: Vec<_> = projects
            .iter()
            .map(|p| Project {
                task_key: p.task_key.clone(),
                name: p.display_name.clone(),
                available: p.available,
                platform: p.platform.clone(),
                manifest_version: p.manifest_version.clone(),
            })
            .collect();
        result
            .documents
            .insert("projects".into(), document(&project_docs)?);
        let health = catalog.host_capability.as_ref();
        let context = BusinessContext::load(backend).await?;
        result
            .context
            .insert("apiAsOfUtc".into(), utc_roundtrip(context.as_of_utc));
        result.context.insert(
            "hostStatus".into(),
            if health.is_none() {
                "Host has not reported capability.".into()
            } else {
                format!(
                    "{}: {} - {}",
                    text(&health, "Platform"),
                    if health.is_some_and(|h| h.ready == Some(true)) {
                        "Ready"
                    } else {
                        "Unavailable"
                    },
                    text(&health, "Detail")
                )
            },
        );
        self.definitions = models;
        self.result = result;
        self.loaded = true;
        Ok(())
    }
    async fn enrich(&mut self, backend: &Backend, registry: &Registry) -> Result<(), String> {
        let definitions = self
            .definitions
            .iter()
            .map(|(id, wire)| (id.clone(), wire.clone()))
            .collect::<Vec<_>>();
        for (id, wire) in definitions {
            self.history_page(backend, registry, &id, false, false)
                .await?;
            let row = ScheduledTaskDefinition::from_backend(&wire)?;
            let preview = if row.enabled == Some(true) {
                Some(
                    self.preview(backend, registry, member(&wire, 3)?.clone())
                        .await?,
                )
            } else {
                None
            };
            self.next_fires.insert(
                id,
                preview
                    .filter(|p| p.valid == Some(true))
                    .and_then(|p| p.next_fire_times_utc.first().cloned())
                    .unwrap_or_default(),
            );
        }
        Ok(())
    }
    fn publication_result(&self, registry: &Registry) -> Result<ContractResult, String> {
        let mut result = self.result.clone();
        result.has_more = self
            .history_schedule
            .as_ref()
            .and_then(|id| self.cursors.get(id))
            .is_some_and(Option::is_some);
        for (key, value) in [
            ("output", self.output.clone()),
            ("outputRun", self.output_run.clone().unwrap_or_default()),
            (
                "outputEnd",
                if self.output_end { "True" } else { "False" }.into(),
            ),
            (
                "historySchedule",
                self.history_schedule.clone().unwrap_or_default(),
            ),
        ] {
            result.context.insert(key.into(), value);
        }
        let mut histories = BTreeMap::new();
        for (key, rows) in &self.histories {
            histories.insert(
                key.clone(),
                rows.iter()
                    .map(|v| run_document(registry, v))
                    .collect::<Result<Vec<_>, _>>()?,
            );
        }
        result
            .documents
            .insert("histories".into(), document(&histories)?);
        result.documents.insert(
            "historyMore".into(),
            document(
                &self
                    .cursors
                    .iter()
                    .map(|(id, cursor)| (id.clone(), cursor.is_some()))
                    .collect::<BTreeMap<_, _>>(),
            )?,
        );
        result
            .documents
            .insert("nextFires".into(), document(&self.next_fires)?);
        result.documents.insert(
            "runs".into(),
            document(
                &self
                    .history_schedule
                    .as_ref()
                    .and_then(|id| histories.get(id))
                    .cloned()
                    .unwrap_or_default(),
            )?,
        );
        result
            .documents
            .insert("preview".into(), document(&self.preview)?);
        Ok(result)
    }
    fn selected(&self, intent: &Json) -> Result<(String, Value), String> {
        let id = intent["originalId"]
            .as_str()
            .ok_or("Select a current schedule")?;
        self.definitions
            .get(id)
            .map(|v| (id.into(), v.clone()))
            .ok_or("Select a current schedule".into())
    }
    async fn read_output(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        more: bool,
    ) -> Result<(), String> {
        let (schedule, _) = self.selected(intent)?;
        let run_id = field(intent, "runId");
        let run = self
            .histories
            .get(&schedule)
            .and_then(|rows| {
                rows.iter().find(|v| {
                    ScheduledTaskRun::from_backend(v).ok().is_some_and(|p| {
                        p.id.as_ref().and_then(|id| id.value.as_deref()) == Some(run_id)
                    })
                })
            })
            .ok_or("Select a run from this schedule history")?;
        if more && (self.output_run.as_deref() != Some(run_id) || self.output_end) {
            return Err("No output continuation for this run".into());
        }
        let wire = self
            .query(
                backend,
                registry,
                "GetScheduledTaskOutputQuery",
                BTreeMap::from([
                    ("EntityId".into(), id_wire(run_id)?),
                    ("ScheduleId".into(), id_wire(&schedule)?),
                    ("IntendedFireTimeUtc".into(), member(run, 7)?.clone()),
                    (
                        "Offset".into(),
                        if more { self.output_offset } else { 0 }.into(),
                    ),
                ]),
            )
            .await?;
        let page = ScheduledTaskOutputPage::from_backend(&wire)?;
        let next = page.next_offset.ok_or("Invalid output offset")?;
        let available = page.available == Some(true);
        let end = page.end_of_output == Some(true) || !available;
        if next < 0
            || more && next < self.output_offset
            || more && !end && next == self.output_offset
        {
            return Err("Task output did not advance".into());
        }
        let output = if available {
            text(&page, "Text")
        } else {
            let row = ScheduledTaskRun::from_backend(run)?;
            format!(
                "{}\n{}",
                text(&row, "StandardOutputTail"),
                text(&row, "StandardErrorTail")
            )
        };
        let output = if more {
            format!("{}{output}", self.output)
        } else {
            output
        };
        if output.encode_utf16().count() > 2_000_000 {
            return Err("Output view reached 2 MB; reopen a retained output page".into());
        }
        self.output = output;
        self.output_run = Some(run_id.into());
        self.output_offset = next;
        self.output_end = end;
        self.history_schedule = Some(schedule);
        Ok(())
    }
    async fn mutate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        request_id: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let action = intent["intent"].as_str().unwrap_or_default();
        let (selected, wire) = if action == "add" {
            (uuid::Uuid::new_v4().to_string(), None)
        } else {
            let (id, wire) = self.selected(intent)?;
            (id, Some(wire))
        };
        let row = wire
            .as_ref()
            .map(|v| ScheduledTaskDefinition::from_backend(v))
            .transpose()?;
        let command = match action {
            "add" => "CreateScheduledTaskCommand",
            "change" => "ChangeScheduledTaskScheduleCommand",
            "enable" => "EnableScheduledTaskCommand",
            "disable" => "DisableScheduledTaskCommand",
            "remove" => "RemoveScheduledTaskCommand",
            "run" => "RequestScheduledTaskRunCommand",
            "resolve" => "FailScheduledTaskRunCommand",
            _ => return Err("Unsupported scheduler mutation".into()),
        };
        let mut entity = selected.clone();
        let operator = std::env::var("IFM_UI_OPERATOR")
            .or_else(|_| std::env::var("USERNAME"))
            .map_err(|_| "Configure IFM_UI_OPERATOR for task operations")?;
        let mut fields = BTreeMap::from([
            ("CommandId".into(), request_id.into()),
            (
                "ExpectedRevision".into(),
                row.as_ref().and_then(|r| r.revision).unwrap_or(0).into(),
            ),
            ("Operator".into(), operator.into()),
            ("Reason".into(), "".into()),
            (
                "OperationCommandId".into(),
                uuid::Uuid::nil().to_string().into(),
            ),
        ]);
        match action {
            "add" | "change" => {
                fields.insert("Schedule".into(), schedule_input(registry, intent)?);
            }
            "remove" if row.as_ref().is_some_and(|r| r.enabled == Some(true)) => {
                return Err("Disable the schedule before removal".into());
            }
            "run" => {
                fields.insert("ExpectedRevision".into(), 0.into());
                let row = row.as_ref().unwrap();
                let context = BusinessContext::load(backend).await?;
                entity = uuid::Uuid::new_v4().to_string();
                fields.insert("ScheduleId".into(), id_wire(&selected)?);
                fields.insert(
                    "DefinitionRevision".into(),
                    row.desired_revision
                        .ok_or("Missing desired revision")?
                        .into(),
                );
                fields.insert(
                    "IntendedFireTimeUtc".into(),
                    Value::Array(vec![timestamp_wire(context.as_of_utc), 0.into()]),
                );
                fields.insert("Manual".into(), true.into());
                fields.insert("TaskKey".into(), row.schedule.cell("TaskKey").text().into());
                fields.insert("Environment".into(), self.environment.clone().into());
                fields.insert("HostId".into(), self.host.clone().into());
                fields.insert("Reason".into(), "Manual System Admin request.".into());
            }
            "resolve" => {
                let reason = field(intent, "reason");
                if reason.encode_utf16().count() < 3 {
                    return Err("Select an uncertain run and enter a reason".into());
                }
                let run_id = field(intent, "runId");
                let run = self
                    .histories
                    .get(&selected)
                    .and_then(|v| {
                        v.iter().find(|v| {
                            ScheduledTaskRun::from_backend(v).ok().is_some_and(|p| {
                                p.id.as_ref().and_then(|id| id.value.as_deref()) == Some(run_id)
                            })
                        })
                    })
                    .ok_or("Select an uncertain run")?;
                let run = ScheduledTaskRun::from_backend(run)?;
                if run.status != Some(ScheduledTaskRunStatus::Uncertain) {
                    return Err("Select an uncertain run".into());
                }
                let context = BusinessContext::load(backend).await?;
                entity = run_id.into();
                fields.insert(
                    "ExpectedRevision".into(),
                    run.revision.ok_or("Missing run revision")?.into(),
                );
                fields.insert(
                    "OperationCommandId".into(),
                    text(&run, "OperationCommandId").into(),
                );
                fields.insert("Reason".into(), reason.into());
                fields.insert("Stage".into(), "OperatorResolved".into());
                fields.insert(
                    "Detail".into(),
                    "Operator reviewed the interrupted run; no automatic relaunch.".into(),
                );
                for member in ["StandardOutputTail", "StandardErrorTail", "OutputDirectory"] {
                    fields.insert(member.into(), "".into());
                }
                fields.insert(
                    "FinishedAtUtc".into(),
                    Value::Array(vec![timestamp_wire(context.as_of_utc), 0.into()]),
                );
            }
            _ => {}
        }
        let kind = format!("{ROOT}Commands.{command}");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: uuid::Uuid::parse_str(&entity)
                .map_err(|_| "Invalid scheduler route")?
                .simple()
                .to_string(),
        };
        fields.insert("EntityId".into(), id_wire(&entity)?);
        fields.insert("Subject".into(), subject.wire());
        let body = registry.encode(&kind, &fields)?;
        let accepted = backend
            .request_until(&subject, &body, deadline, None)
            .await
            .map_err(|e| format!("OutcomeUnknown: {e}"))?;
        let receipt = match service_result(&accepted) {
            Ok(value) => value,
            Err(error) => {
                return Err(
                    if accepted
                        .as_array()
                        .and_then(|p| p.first())
                        .and_then(Value::as_bool)
                        == Some(false)
                    {
                        error
                    } else {
                        format!("OutcomeUnknown: invalid scheduler acknowledgement: {error}")
                    },
                );
            }
        };
        if receipt
            .as_array()
            .and_then(|v| v.first())
            .and_then(Value::as_str)
            .and_then(|v| uuid::Uuid::parse_str(v).ok())
            .is_none_or(|v| v.is_nil())
        {
            return Err(
                "OutcomeUnknown: scheduler acknowledgement did not contain a valid receipt".into(),
            );
        }

        // Acceptance is authoritative independently of a later read timeout. The
        // background consumer refreshes installation/run progress, never repeats work.
        match tokio::time::timeout_at(deadline, async {
            self.load_dashboard(backend, registry).await?;
            if matches!(action, "add" | "change" | "enable" | "disable") {
                self.enrich(backend, registry).await?;
            }
            Ok::<(), String>(())
        })
        .await
        {
            Ok(Ok(())) => Ok("Completed".into()),
            result => Ok(format!(
                "Saved; reload scheduled tasks: {}",
                match result {
                    Ok(Err(e)) => e,
                    _ => "Refresh deadline expired".into(),
                }
            )),
        }
    }
    async fn apply(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let action = intent["intent"].as_str().unwrap_or_default();
        if action == "load" {
            if !self.subscribed {
                self.subscription = Some(backend.subscribe("Event", "ScheduledTaskEvent").await?);
                backend.flush_until(deadline).await?;
                self.subscribed = true;
            }
            let environment = field(intent, "environment");
            let host = field(intent, "hostId");
            if environment.is_empty() || host.is_empty() {
                return Err("Environment and host are required".into());
            }
            self.environment = environment.into();
            self.host = host.into();
            self.loaded = false;
            self.history_schedule = None;
            self.histories.clear();
            self.cursors.clear();
            self.next_fires.clear();
            self.output.clear();
            self.preview = None;
            self.output_run = None;
            self.output_offset = 0;
            self.output_end = true;
            self.load_dashboard(backend, registry).await?;
            self.enrich(backend, registry).await?;
            self.history_schedule = self.result.rows.first().map(|r| r.id.clone());
            return Ok("Completed".into());
        }
        if !self.loaded
            || field(intent, "environment") != self.environment
            || field(intent, "hostId") != self.host
        {
            return Err("Reload after changing environment or host".into());
        }
        if intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("Schedules changed; reload before continuing".into());
        }
        match action {
            "preview" => {
                self.preview = Some(
                    self.preview(backend, registry, schedule_input(registry, intent)?)
                        .await?,
                )
            }
            "history" | "next" => {
                let (selected, _) = self.selected(intent)?;
                self.history_page(backend, registry, &selected, action == "next", false)
                    .await?;
                self.history_schedule = Some(selected);
                self.output.clear();
                self.output_run = None;
                self.output_offset = 0;
                self.output_end = true;
            }
            "output" | "more-output" => {
                self.read_output(backend, registry, intent, action == "more-output")
                    .await?
            }
            "add" | "change" | "enable" | "disable" | "remove" | "run" | "resolve" => {
                return self.mutate(backend, registry, intent, id, deadline).await;
            }
            _ => return Err("Unsupported scheduled-task action".into()),
        }
        Ok("Completed".into())
    }
    pub async fn refresh_if_loaded(
        &mut self,
        backend: &Backend,
        registry: &Registry,
    ) -> Option<ContractPublication> {
        if !self.loaded {
            return None;
        }
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let result = tokio::time::timeout_at(deadline, async {
            self.load_dashboard(backend, registry).await?;
            if let Some(schedule) = self.history_schedule.clone() {
                if self.definitions.contains_key(&schedule) {
                    self.history_page(backend, registry, &schedule, false, true)
                        .await?;
                }
            }
            Ok::<(), String>(())
        })
        .await;
        let status = match result {
            Ok(Ok(())) => "Refreshed".into(),
            Ok(Err(e)) => format!("Task refresh unavailable: {e}"),
            _ => "Task refresh deadline expired".into(),
        };
        let (publication, status) = match self.publication_result(registry) {
            Ok(result) => (result, status),
            Err(error) => (
                self.result.clone(),
                format!("{status}; Task display unavailable: {error}"),
            ),
        };
        self.generation += 1;
        Some(ContractPublication {
            schema: 1,
            generation: self.generation,
            status,
            result: publication,
            ..Default::default()
        })
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "scheduled-tasks" {
            return Err("Invalid scheduled-task intent".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|v| uuid::Uuid::parse_str(v).ok())
            .filter(|v| !v.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let outcome = if let Some((request, outcome)) = self.history.get(&id) {
            if request != intent {
                return Err("RequestId belongs to another scheduler operation".into());
            }
            outcome.clone()
        } else {
            if self.history.len() >= 256 && self.history.values().all(|(_, v)| v.state == "Unknown")
            {
                return Err("Reconcile uncertain scheduler operations before continuing".into());
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let mutation = matches!(
                intent["intent"].as_str(),
                Some("add" | "change" | "enable" | "disable" | "remove" | "run" | "resolve")
            );
            let result = if mutation {
                self.apply(backend, registry, intent, &id, deadline).await
            } else {
                tokio::time::timeout_at(
                    deadline,
                    self.apply(backend, registry, intent, &id, deadline),
                )
                .await
                .map_err(|_| "Scheduler read deadline expired".to_string())
                .and_then(|v| v)
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
                        // A new operation must not be submitted against stale
                        // state after an acknowledgement was lost or malformed.
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
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            result: self.publication_result(registry)?,
            outcomes: vec![outcome],
            ..Default::default()
        })
    }
}
