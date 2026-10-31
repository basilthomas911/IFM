//! Backup screen state. Raw policy definitions are retained for revision-matched
//! edits; history and output paging never submit an operation. Each action has
//! one absolute deadline, and ambiguous writes cannot be automatically replayed.
use ifm_ui_egui::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult},
    services::{business_context::BusinessContext, database_backup as api},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
fn f<'a>(intent: &'a Json, key: &str) -> &'a str {
    intent["fields"][key].as_str().unwrap_or_default().trim()
}
fn s<'a>(row: &'a Json, key: &str) -> &'a str {
    row[key].as_str().unwrap_or_default()
}
fn created(row: &Json) -> Option<chrono::DateTime<chrono::FixedOffset>> {
    chrono::DateTime::parse_from_rfc3339(s(row, "createdUtc")).ok()
}
fn member(wire: &Value, index: usize) -> Result<&Value, String> {
    wire.as_array()
        .and_then(|v| v.get(index))
        .ok_or("Incomplete backup payload".into())
}
fn offset(wire: &Value, index: usize) -> Result<Json, String> {
    let v = member(wire, index)?;
    Ok(if v.is_nil() {
        Json::Null
    } else {
        json!(reference_offset(v)?)
    })
}
fn project(registry: &Registry, name: &str, wire: &Value) -> Result<Json, String> {
    registry.project(&api::kind("ReadModels", name), wire)
}
pub fn operation_document(registry: &Registry, wire: &Value) -> Result<Json, String> {
    let row = project(registry, "DatabaseBackupOperationReadModel", wire)?;
    let lineage = &row["BackupLineage"];
    let engine = match s(lineage, "NativeKind") {
        "PostgreSqlBase" | "PostgreSqlIncremental" => json!("PostgreSql"),
        "ScyllaManagerSnapshot" | "ScyllaManagerDeduplicatedSnapshot" => json!("ScyllaDb"),
        _ => row["Engine"].clone(),
    };
    Ok(
        json!({"operationId":row["OperationId"]["Value"],"protectionSet":row["ProtectionSetId"]["Value"],"source":row["Source"],"phase":row["Phase"],"outcome":row["Outcome"],"progressPercent":row["ProgressPercent"],"safeDiagnosticReference":row["SafeDiagnosticReference"],"requestedMode":if lineage.is_null(){json!("Full")}else{lineage["RequestedMode"].clone()},"resolvedMode":if lineage.is_null(){json!("None")}else{lineage["ResolvedMode"].clone()},"createdUtc":offset(wire,9)?,"completedUtc":offset(wire,10)?,"backupSetId":row["BackupSetId"].get("Value").cloned().unwrap_or(Json::Null),"kind":row["Kind"],"engine":engine}),
    )
}
fn protection_document(registry: &Registry, wire: &Value) -> Result<Json, String> {
    let row = project(registry, "DatabaseProtectionSetReadModel", wire)?;
    Ok(
        json!({"id":row["ProtectionSetId"]["Value"],"source":row["Source"],"engines":row["Engines"],"enabled":row["Enabled"],"policyRevision":row["PolicyRevision"]}),
    )
}
fn restore_document(registry: &Registry, wire: &Value) -> Result<Json, String> {
    if wire.is_nil() {
        return Ok(Json::Null);
    }
    let row = project(registry, "DatabaseRestorePointReadModel", wire)?;
    Ok(
        json!({"restorePointId":row["RestorePointId"]["Value"],"recoveryPointUtc":offset(wire,4)?,"verificationLevel":row["VerificationLevel"],"verifiedUtc":offset(wire,6)?,"restoreTestedUtc":offset(wire,7)?,"eligible":row["Eligible"]}),
    )
}
fn list(wire: &Value, max: usize) -> Result<&Vec<Value>, String> {
    wire.as_array()
        .filter(|v| v.len() <= max)
        .ok_or("Invalid or oversized backup result".into())
}
/// Build the same configured sets as the hybrid API service, including disabled
/// persisted sets which no longer occur in host configuration.
pub fn configured_sets(source: &str, persisted: Vec<Json>, settings: &Json) -> Vec<Json> {
    let flag = |key: &str| s(settings, key).eq_ignore_ascii_case("true");
    let enabled = flag("Enabled") && (source != "AwsCloud" || flag("AcceptBackupRequests"));
    let mut sets = vec![];
    for (key, engine, capability) in [
        (
            "PostgreSqlProtectionSets",
            "PostgreSql",
            "PostgreSqlEnabled",
        ),
        ("ScyllaProtectionSets", "ScyllaDb", "ScyllaEnabled"),
    ] {
        for id in s(settings, key)
            .split(',')
            .map(str::trim)
            .filter(|v| !v.is_empty())
        {
            let revision = persisted
                .iter()
                .find(|v| s(v, "id") == id)
                .map(|v| v["policyRevision"].clone())
                .unwrap_or(json!(0));
            sets.push(json!({"id":id,"source":source,"engines":[engine],"enabled":enabled&&(source=="AwsCloud"||flag(capability)),"policyRevision":revision}));
        }
    }
    for mut row in persisted {
        if !sets.iter().any(|v| v["id"] == row["id"]) {
            row["enabled"] = json!(false);
            sets.push(row);
        }
    }
    sets
}
#[derive(Default)]
pub struct BackupController {
    source: String,
    status: String,
    dashboard: Json,
    setup: Json,
    health: Vec<Json>,
    history: BTreeMap<String, BTreeMap<String, Json>>,
    cursors: BTreeMap<String, String>,
    policies: BTreeMap<String, Value>,
    selected: Option<(String, String)>,
    output: String,
    offset: i64,
    phase: i64,
    ended: bool,
    subscription: Option<async_nats::Subscriber>,
    observer: Option<tokio::task::JoinHandle<()>>,
    loaded: bool,
    acknowledged_action: bool,
    generation: u64,
    receipts: BTreeMap<String, (Json, ifm_ui_egui::lookup_editor::EditorOutcome)>,
}
impl Drop for BackupController {
    fn drop(&mut self) {
        if let Some(task) = self.observer.take() {
            task.abort();
        }
    }
}
impl BackupController {
    pub fn take_subscription(&mut self) -> Option<async_nats::Subscriber> {
        self.subscription.take()
    }
    pub fn set_observer(&mut self, task: tokio::task::JoinHandle<()>) {
        self.observer = Some(task);
    }
    async fn query(
        &self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        name: &str,
        source: &str,
        fields: BTreeMap<String, Value>,
        deadline: tokio::time::Instant,
    ) -> Result<Value, String> {
        api::query(backend, registry, context, name, source, fields, deadline).await
    }
    async fn dashboard(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        selected: &str,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let (sets, operations) = tokio::try_join!(
            self.query(
                backend,
                registry,
                context,
                "GetDatabaseProtectionSetsQuery",
                &self.source,
                BTreeMap::from([("PageSize".into(), 200.into())]),
                deadline
            ),
            self.query(
                backend,
                registry,
                context,
                "ListDatabaseBackupOperationsQuery",
                &self.source,
                BTreeMap::new(),
                deadline
            )
        )?;
        let mut sets = list(&sets, 200)?
            .iter()
            .map(|v| protection_document(registry, v))
            .collect::<Result<Vec<_>, _>>()?;
        sets.retain(|v| v["source"] == self.source);
        let setup = self
            .query(
                backend,
                registry,
                context,
                "GetDatabaseBackupSetupQuery",
                &self.source,
                BTreeMap::new(),
                deadline,
            )
            .await;
        match setup.and_then(|v| project(registry, "DatabaseBackupSetupReadModel", &v)) {
            Ok(v) => {
                self.setup = json!({"backupHostSettings":v["BackupHostSettings"],"available":v["Available"]});
                if v["Available"] == true {
                    sets = configured_sets(&self.source, sets, &v["BackupHostSettings"]);
                }
            }
            Err(e) => self.setup = json!({"backupHostSettings":{"Status":e},"available":false}),
        }
        let chosen = if selected.is_empty() {
            sets.iter().find(|v| v["enabled"] == true)
        } else {
            sets.iter().find(|v| s(v, "id") == selected)
        };
        let mut latest = vec![];
        for name in [
            "GetLatestVerifiedDatabaseBackupQuery",
            "GetLatestRestoreTestedDatabaseBackupQuery",
        ] {
            let value = if let Some(chosen) = chosen {
                match self
                    .query(
                        backend,
                        registry,
                        context,
                        name,
                        &self.source,
                        BTreeMap::from([("ProtectionSetId".into(), api::wrapper(s(chosen, "id")))]),
                        deadline,
                    )
                    .await
                {
                    Ok(v) => restore_document(registry, &v)?,
                    Err(_) => Json::Null,
                }
            } else {
                Json::Null
            };
            latest.push(value);
        }
        let mut operations = list(&operations, 50)?
            .iter()
            .map(|v| operation_document(registry, v))
            .collect::<Result<Vec<_>, _>>()?;
        operations.retain(|v| v["source"] == self.source);
        operations.sort_by_key(|v| std::cmp::Reverse(created(v)));
        self.dashboard = json!({"source":self.source,"protectionSets":sets,"recentOperations":operations,"latestVerified":latest[0],"latestRestoreTested":latest[1]});
        Ok(())
    }
    async fn history_page(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        source: &str,
        next: bool,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let cursor = if next {
            self.cursors
                .get(source)
                .filter(|s| !s.is_empty())
                .ok_or("No older backup operations")?
                .clone()
        } else {
            String::new()
        };
        let wire = self
            .query(
                backend,
                registry,
                context,
                "ListDatabaseBackupOperationsQuery",
                source,
                BTreeMap::from([("ContinuationIdentity".into(), cursor.clone().into())]),
                deadline,
            )
            .await?;
        let rows = list(&wire, 50)?;
        let mut ids = std::collections::BTreeSet::new();
        let mut page = vec![];
        for wire in rows {
            let mut row = operation_document(registry, wire)?;
            if row["source"] != source {
                return Err("Backup history crossed source".into());
            }
            let id = uuid::Uuid::parse_str(s(&row, "operationId"))
                .map_err(|_| "Invalid operation identity")?;
            if id.is_nil() || !ids.insert(id) {
                return Err("Duplicate or empty backup operation".into());
            }
            if row["engine"] == "None" {
                for (key, engine) in [
                    ("PostgreSqlProtectionSets", "PostgreSql"),
                    ("ScyllaProtectionSets", "ScyllaDb"),
                ] {
                    if s(&self.setup["backupHostSettings"], key)
                        .split(',')
                        .any(|v| v == s(&row, "protectionSet"))
                    {
                        row["engine"] = json!(engine);
                        break;
                    }
                }
            }
            page.push(row);
        }
        let continuation = if rows.len() == 50 {
            s(page.last().unwrap(), "operationId").to_string()
        } else {
            String::new()
        };
        if next && continuation == cursor {
            return Err("Backup history did not advance".into());
        }
        let history = self.history.entry(source.into()).or_default();
        if history.len()
            + page
                .iter()
                .filter(|v| !history.contains_key(s(v, "operationId")))
                .count()
            > 100_000
        {
            return Err("Backup history display bound reached".into());
        }
        for row in page {
            history.insert(s(&row, "operationId").into(), row);
        }
        if next || !self.cursors.contains_key(source) {
            self.cursors.insert(source.into(), continuation);
        }
        Ok(())
    }
    async fn read_output(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if self.ended {
            return Ok(());
        }
        let (source, id) = self.selected.clone().ok_or("Select an output operation")?;
        let wire = self
            .query(
                backend,
                registry,
                context,
                "GetDatabaseBackupLogQuery",
                &source,
                BTreeMap::from([
                    ("OperationId".into(), api::wrapper(id)),
                    ("PageSize".into(), 100.into()),
                    ("OutputOffset".into(), self.offset.into()),
                    ("AfterPhaseRevision".into(), self.phase.into()),
                ]),
                deadline,
            )
            .await?;
        let row = project(registry, "DatabaseBackupLogReadModel", &wire)?;
        let next = row["NextOutputOffset"]
            .as_i64()
            .filter(|v| *v >= self.offset)
            .ok_or("Backup output offset regressed")?;
        if row["EndOfOutput"] != true && next == self.offset {
            return Err("Backup output did not advance".into());
        }
        let mut text = String::new();
        let mut revision = self.phase;
        for phase in list(member(&wire, 0)?, 100)? {
            let row = project(registry, "DatabaseBackupPhaseReadModel", phase)?;
            let r = row["Revision"].as_i64().ok_or("Invalid phase revision")?;
            if r <= revision {
                return Err("Backup phases did not advance".into());
            }
            revision = r;
            text.push_str(&format!(
                "{} {} {}% {}\r\n",
                reference_offset(member(phase, 1)?)?,
                s(&row, "Phase"),
                row["ProgressPercent"],
                s(&row, "Outcome")
            ));
        }
        if text.is_empty() {
            text.push_str("\r\n");
        }
        text.push_str(s(&row, "Output"));
        self.output.push_str(&text);
        // Drop old display text on UTF-8 character boundaries, never a partial
        // codepoint; the server cursor, not retained text length, owns paging.
        let mut units = 0;
        let mut start = self.output.len();
        for (index, ch) in self.output.char_indices().rev() {
            units += ch.len_utf16();
            if units > 2_000_000 {
                break;
            }
            start = index;
        }
        if start > 0 {
            self.output.drain(..start);
        }
        self.offset = next;
        self.phase = revision;
        self.ended = row["EndOfOutput"] == true;
        Ok(())
    }
    async fn refresh(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.dashboard(backend, registry, context, "", deadline)
            .await?;
        match self
            .query(
                backend,
                registry,
                context,
                "GetDatabaseBackupServiceHealthQuery",
                &self.source,
                BTreeMap::new(),
                deadline,
            )
            .await
        {
            Ok(v) => {
                self.health=list(&v,200)?.iter().map(|wire|{let row=project(registry,"DatabaseBackupHealthReadModel",wire)?;Ok(json!({"hostId":row["HostId"]["Value"],"ready":row["Ready"],"capability":row["CapabilityState"],"observedUtc":offset(wire,5)?,"diagnostic":row["SafeDiagnosticReference"]}))}).collect::<Result<_,String>>()?;
            }
            Err(e) => {
                self.health.clear();
                self.status = e;
            }
        }
        for source in ["AwsCloud", "LocalWorkstation"] {
            if let Err(e) = self
                .history_page(backend, registry, context, source, false, deadline)
                .await
            {
                self.status = e;
            }
        }
        if self.selected.is_some() {
            self.ended = false;
            self.read_output(backend, registry, context, deadline)
                .await?;
        }
        self.loaded = true;
        Ok(())
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
            let context = BusinessContext::load(backend).await?;
            self.refresh(backend, registry, &context, deadline).await
        })
        .await
        .map_err(|_| "Backup refresh deadline expired".to_string())
        .and_then(|v| v);
        if let Err(error) = result {
            self.status = error;
        }
        self.generation += 1;
        self.publication(registry, "", "", "").ok()
    }
    fn find_operation(&self, intent: &Json) -> Result<Json, String> {
        let source = f(intent, "operationSource");
        let id = f(intent, "operationId");
        let mut matches = self
            .history
            .iter()
            .filter(|(key, _)| source.is_empty() || key.as_str() == source)
            .filter_map(|(_, rows)| rows.get(id));
        let row = matches
            .next()
            .cloned()
            .ok_or("Reload and select a known backup operation")?;
        if matches.next().is_some() {
            return Err("Ambiguous backup operation source".into());
        }
        Ok(row)
    }
    pub fn publication(
        &self,
        registry: &Registry,
        request: &str,
        outcome: &str,
        message: &str,
    ) -> Result<ContractPublication, String> {
        let mut result = ContractResult {
            view: "database-backup".into(),
            ..Default::default()
        };
        result.context = BTreeMap::from([
            ("source".into(), self.source.clone()),
            ("status".into(), self.status.clone()),
            ("output".into(), self.output.clone()),
            (
                "outputOperation".into(),
                self.selected
                    .as_ref()
                    .map(|v| v.1.clone())
                    .unwrap_or_default(),
            ),
        ]);
        let mut history = BTreeMap::new();
        for (source, rows) in &self.history {
            let mut rows: Vec<_> = rows.values().cloned().collect();
            rows.sort_by_key(|v| std::cmp::Reverse(created(v)));
            history.insert(source, rows);
        }
        let (policy, editor) = if let Some(wire) = self.policies.get(&self.source) {
            policy_document(registry, wire)?
        } else {
            (Json::Null, json!({}))
        };
        for (key, value) in [
            ("dashboard", self.dashboard.clone()),
            ("setup", self.setup.clone()),
            ("health", json!(self.health)),
            ("history", json!(history)),
            ("continuations", json!(self.cursors)),
            ("policy", policy),
            ("policyEditor", editor),
        ] {
            result
                .documents
                .insert(key.into(), ifm_ui_egui::wire::encode_value(&value)?.into());
        }
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: self.status.clone(),
            result,
            outcomes: if request.is_empty() {
                vec![]
            } else {
                vec![ifm_ui_egui::lookup_editor::EditorOutcome {
                    request_id: request.into(),
                    state: outcome.into(),
                    message: message.into(),
                }]
            },
            ..Default::default()
        })
    }
    async fn apply(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        request: uuid::Uuid,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let source = f(intent, "source");
        api::source(registry, source)?;
        self.source = source.into();
        if self.observer.as_ref().is_some_and(|v| v.is_finished()) {
            self.observer = None;
        }
        if self.subscription.is_none() && self.observer.is_none() {
            self.subscription = Some(backend.subscribe("Event", "DatabaseBackupEvent").await?);
            backend.flush_until(deadline).await?;
        }
        let action = intent["intent"].as_str().ok_or("Missing backup action")?;
        let mutation = matches!(
            action,
            "backup-run" | "backup-cancel" | "backup-policy-save" | "backup-restore"
        );
        if mutation && !self.loaded {
            return Err("Load authoritative backup state before submitting operations".into());
        }
        let context = BusinessContext::load(backend).await?;
        let mut writes: Vec<(String, uuid::Uuid, BTreeMap<String, Value>)> = vec![];
        match action {
            "load" | "backup-source" | "backup-refresh" => {
                self.refresh(backend, registry, &context, deadline).await?
            }
            "backup-close" => {
                self.loaded = false;
                self.subscription = None;
                if let Some(task) = self.observer.take() {
                    task.abort();
                }
            }
            "backup-clear-output" => {
                self.selected = None;
                self.output.clear();
                self.offset = 0;
                self.phase = 0;
                self.ended = false;
                self.status =
                    "No identified operation for this database in the selected run.".into();
            }
            "backup-select" => {
                self.dashboard(
                    backend,
                    registry,
                    &context,
                    f(intent, "protectionSet"),
                    deadline,
                )
                .await?
            }
            "backup-older" => {
                let source = f(intent, "historySource");
                api::source(registry, source)?;
                self.history_page(backend, registry, &context, source, true, deadline)
                    .await?;
            }
            "backup-output" | "backup-more-output" => {
                let operation = self.find_operation(intent)?;
                let selected = (
                    s(&operation, "source").to_string(),
                    s(&operation, "operationId").to_string(),
                );
                if action == "backup-output" {
                    self.selected = Some(selected);
                    self.output.clear();
                    self.offset = 0;
                    self.phase = 0;
                    self.ended = false;
                } else if self.selected.as_ref() != Some(&selected) {
                    return Err("Select the output operation again".into());
                }
                self.read_output(backend, registry, &context, deadline)
                    .await?;
            }
            "backup-policy-load" => {
                let wire = self
                    .query(
                        backend,
                        registry,
                        &context,
                        "GetDatabaseBackupPolicyQuery",
                        source,
                        BTreeMap::from([("PolicyId".into(), api::wrapper(f(intent, "policyId")))]),
                        deadline,
                    )
                    .await?;
                let row = project(registry, "DatabaseBackupPolicyReadModel", &wire)?;
                if row["PolicyId"]["Value"] != f(intent, "policyId") {
                    return Err("Policy query returned a different identity".into());
                }
                self.policies.insert(source.into(), wire);
                self.status = "Accepted policy loaded.".into();
            }
            "backup-run" => {
                let mode = f(intent, "mode");
                if !matches!(mode, "Full" | "Automatic" | "Incremental") {
                    return Err("Select a backup mode".into());
                }
                let sets = f(intent, "protectionSets")
                    .split('|')
                    .filter(|v| !v.is_empty())
                    .collect::<std::collections::BTreeSet<_>>();
                if sets.is_empty() || sets.len() > 32 {
                    return Err("Select between one and 32 protection sets".into());
                }
                for set in sets {
                    let row = self.dashboard["protectionSets"]
                        .as_array()
                        .and_then(|v| {
                            v.iter().find(|v| {
                                s(v, "id") == set && v["source"] == source && v["enabled"] == true
                            })
                        })
                        .ok_or("Select an enabled protection set")?;
                    writes.push((
                        "RequestDatabaseBackupCommand".into(),
                        if writes.is_empty() {
                            request
                        } else {
                            uuid::Uuid::new_v4()
                        },
                        BTreeMap::from([
                            ("ProtectionSetId".into(), api::wrapper(set)),
                            (
                                "ExpectedPolicyRevision".into(),
                                row["policyRevision"]
                                    .as_i64()
                                    .ok_or("Invalid policy revision")?
                                    .into(),
                            ),
                            (
                                "RequestedBackupMode".into(),
                                registry.value_from_json(
                                    &api::kind("Contracts", "DatabaseBackupMode"),
                                    &json!(mode),
                                )?,
                            ),
                            (
                                "ConsistencyMode".into(),
                                registry.value_from_json(
                                    &api::kind("Contracts", "DatabaseConsistencyMode"),
                                    &json!("CoordinatedProtectionSet"),
                                )?,
                            ),
                            (
                                "RequiredDestinations".into(),
                                Value::Array(vec![registry.value_from_json(
                                    &api::kind("Contracts", "DatabaseLogicalDestination"),
                                    &json!({"Name":"online-vault","Required":true}),
                                )?]),
                            ),
                        ]),
                    ));
                }
                self.status = "Backup accepted; awaiting completion.".into();
            }
            "backup-cancel" => {
                let row = self.find_operation(intent)?;
                if row["source"] != source || row["kind"] != "Backup" || row["outcome"] != "None" {
                    return Err("Select a running backup for this source".into());
                }
                let id = uuid::Uuid::parse_str(s(&row, "operationId"))
                    .map_err(|_| "Invalid operation identity")?;
                writes.push((
                    "CancelDatabaseBackupCommand".into(),
                    id,
                    BTreeMap::from([
                        (
                            "ProtectionSetId".into(),
                            api::wrapper(s(&row, "protectionSet")),
                        ),
                        (
                            "SafeReason".into(),
                            "Operator cancelled from Backup Logs.".into(),
                        ),
                    ]),
                ));
                self.status = "Cancellation accepted; awaiting terminal status.".into();
            }
            "backup-restore" => {
                let set = self.dashboard["protectionSets"]
                    .as_array()
                    .and_then(|v| {
                        v.iter().find(|v| {
                            s(v, "id") == f(intent, "protectionSet") && v["source"] == source
                        })
                    })
                    .ok_or("Select a known protection set")?;
                let target = f(intent, "targetProfile");
                let validation = f(intent, "validationProfile");
                writes.push(("RequestDatabaseRestoreDrillCommand".into(),request,BTreeMap::from([("ProtectionSetId".into(),api::wrapper(f(intent,"protectionSet"))),("RestorePointId".into(),api::wrapper(f(intent,"restorePoint"))),("ExpectedPolicyRevision".into(),set["policyRevision"].as_i64().ok_or("Invalid policy revision")?.into()),("DisposableTargetProfile".into(),target.into()),("ValidationProfile".into(),validation.into()),("FreshTarget".into(),registry.value_from_json(&api::kind("Contracts","DatabaseFreshTargetDescriptor"),&json!({"Profile":target,"LogicalTarget":validation,"RecoveryTargetUtc":null,"PreferredReplicaId":null}))?),("RestoreClass".into(),registry.value_from_json(&api::kind("Contracts","DatabaseRestoreClass"),&json!("Drill"))?)])));
                self.status = "Restore drill accepted; awaiting completion.".into();
            }
            "backup-policy-save" => {
                let wire = self
                    .policies
                    .get(source)
                    .ok_or("Load the policy before saving")?;
                let row = project(registry, "DatabaseBackupPolicyReadModel", wire)?;
                if row["PolicyId"]["Value"] != f(intent, "policyId") {
                    return Err("Reload the selected policy before saving".into());
                }
                let mut definition = row["Definition"].clone();
                for (key, member) in [
                    ("daily", "DailyCount"),
                    ("weekly", "WeeklyCount"),
                    ("monthly", "MonthlyCount"),
                ] {
                    let count = f(intent, key)
                        .parse::<i32>()
                        .ok()
                        .filter(|v| *v >= 0)
                        .ok_or("Retention must be a nonnegative integer")?;
                    definition["Retention"][member] = json!(count);
                }
                for (key, section, member, multiplier) in [
                    (
                        "rpoMinutes",
                        "RecoveryObjectives",
                        "RecoveryPointObjective",
                        600_000_000.0,
                    ),
                    (
                        "rtoMinutes",
                        "RecoveryObjectives",
                        "RecoveryTimeObjective",
                        600_000_000.0,
                    ),
                    (
                        "maximumVerificationHours",
                        "Verification",
                        "MaximumVerificationAge",
                        36_000_000_000.0,
                    ),
                ] {
                    let value = f(intent, key)
                        .parse::<f64>()
                        .map_err(|_| "Invalid duration")?;
                    let ticks = value * multiplier;
                    if !value.is_finite() || value <= 0.0 || ticks >= i64::MAX as f64 {
                        return Err(
                            "Recovery durations must be positive and within TimeSpan range".into(),
                        );
                    }
                    definition[section][member] = json!(ticks.trunc() as i64);
                }
                writes.push((
                    "UpdateDatabaseBackupPolicyCommand".into(),
                    request,
                    BTreeMap::from([
                        ("PolicyId".into(), api::wrapper(f(intent, "policyId"))),
                        (
                            "ExpectedPolicyRevision".into(),
                            row["Revision"]
                                .as_i64()
                                .ok_or("Invalid policy revision")?
                                .into(),
                        ),
                        (
                            "Policy".into(),
                            registry.value_from_json(
                                &api::kind("Contracts", "DatabaseBackupPolicyDefinition"),
                                &definition,
                            )?,
                        ),
                    ]),
                ));
                self.status = "Policy accepted; awaiting completion.".into();
            }
            _ => return Err("Unsupported backup action".into()),
        }
        // Prepare every payload before the first submission. A multi-set batch
        // can partially succeed; an interruption must not replay accepted sets.
        let mut prepared = vec![];
        for (name, entity, fields) in writes {
            let command_id = if name == "CancelDatabaseBackupCommand" {
                request
            } else {
                entity
            };
            prepared.push(api::command_body(
                registry, &context, &name, source, command_id, entity, fields,
            )?);
        }
        let mut accepted = 0;
        for (subject, body) in prepared {
            if let Err(error) = api::command(backend, &subject, &body, deadline).await {
                return Err(if accepted > 0 {
                    format!(
                        "OutcomeUnknown: {accepted} batch operations accepted; {error}. Reconcile before retrying."
                    )
                } else {
                    error
                });
            }
            accepted += 1;
        }
        if accepted > 0 {
            self.acknowledged_action = true;
            if let Err(error) = self.refresh(backend, registry, &context, deadline).await {
                self.status = format!("Accepted; reload backup status: {error}");
            }
        }
        Ok(self.status.clone())
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "database-backup" {
            return Err("Invalid backup intent".into());
        }
        let id = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Missing backup request identity")?;
        if id.is_nil() {
            return Err("Missing backup request identity".into());
        }
        let id_string = id.to_string();
        let outcome = if let Some((original, outcome)) = self.receipts.get(&id_string) {
            if original != intent {
                return Err("RequestId belongs to another backup action".into());
            }
            outcome.clone()
        } else {
            if self.receipts.len() >= 256 {
                let key = self
                    .receipts
                    .iter()
                    .find(|(_, (_, o))| o.state != "Unknown")
                    .map(|(k, _)| k.clone())
                    .ok_or("Reconcile uncertain backup requests before continuing")?;
                self.receipts.remove(&key);
            }
            if !intent["fields"].as_object().is_some_and(|v| {
                v.len() <= 64
                    && v.iter().all(|(k, v)| {
                        k.len() <= 64
                            && v.as_str().is_some_and(|s| s.encode_utf16().count() <= 2048)
                    })
            }) {
                return Err("Invalid backup intent fields".into());
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            self.acknowledged_action = false;
            let mutation = matches!(
                intent["intent"].as_str(),
                Some("backup-run" | "backup-cancel" | "backup-policy-save" | "backup-restore")
            );
            let result = tokio::time::timeout_at(
                deadline,
                self.apply(backend, registry, intent, id, deadline),
            )
            .await
            .map_err(|_| {
                if self.acknowledged_action {
                    "Accepted; reload backup status: refresh deadline expired".to_string()
                } else if mutation {
                    "OutcomeUnknown: backup action deadline expired; reconcile before retrying"
                        .to_string()
                } else {
                    "Backup read deadline expired".to_string()
                }
            })
            .and_then(|v| v);
            let (state, message) = match result {
                Ok(message) => ("Completed", message),
                Err(error) if self.acknowledged_action => {
                    self.status = error.clone();
                    ("Completed", error)
                }
                Err(error) => {
                    let state = if error.starts_with("OutcomeUnknown:") {
                        self.loaded = false;
                        "Unknown"
                    } else {
                        "Failed"
                    };
                    (state, error)
                }
            };
            let outcome = ifm_ui_egui::lookup_editor::EditorOutcome {
                request_id: id_string.clone(),
                state: state.into(),
                message,
            };
            self.receipts
                .insert(id_string.clone(), (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        self.publication(registry, &id_string, &outcome.state, &outcome.message)
    }
}
/// TimeSpan ticks remain exact on the wire. This display mirrors the hybrid
/// policy model while editable durations are converted only when saved.
pub fn policy_document(registry: &Registry, wire: &Value) -> Result<(Json, Json), String> {
    let row = project(registry, "DatabaseBackupPolicyReadModel", wire)?;
    let d = &row["Definition"];
    let rpo = d["RecoveryObjectives"]["RecoveryPointObjective"]
        .as_i64()
        .ok_or("Invalid RPO ticks")?;
    let rto = d["RecoveryObjectives"]["RecoveryTimeObjective"]
        .as_i64()
        .ok_or("Invalid RTO ticks")?;
    let age = d["Verification"]["MaximumVerificationAge"]
        .as_i64()
        .ok_or("Invalid verification ticks")?;
    Ok((
        json!({"id":row["PolicyId"]["Value"],"revision":row["Revision"],"enforced":row["Enforced"],"enabledSources":d["EnabledSources"],"protectionSets":d["ProtectedSets"].as_array().ok_or("Invalid policy sets")?.iter().map(|v|v["Value"].clone()).collect::<Vec<_>>(),"rpo":timespan(rpo),"rto":timespan(rto),"daily":d["Retention"]["DailyCount"],"weekly":d["Retention"]["WeeklyCount"],"monthly":d["Retention"]["MonthlyCount"],"verificationLevels":d["Verification"]["Levels"],"maximumVerificationAge":timespan(age)}),
        json!({"rpoMinutes":(rpo as f64/600_000_000.0).to_string(),"rtoMinutes":(rto as f64/600_000_000.0).to_string(),"maximumVerificationHours":(age as f64/36_000_000_000.0).to_string()}),
    ))
}
fn timespan(ticks: i64) -> String {
    let n = ticks.unsigned_abs();
    let days = n / 864_000_000_000;
    let seconds = n / 10_000_000;
    format!(
        "{}{}{:02}:{:02}:{:02}{}",
        if ticks < 0 { "-" } else { "" },
        if days > 0 {
            format!("{days}.")
        } else {
            String::new()
        },
        seconds / 3600 % 24,
        seconds / 60 % 60,
        seconds % 60,
        if n % 10_000_000 > 0 {
            format!(".{:07}", n % 10_000_000)
        } else {
            String::new()
        }
    )
}
