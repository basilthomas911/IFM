//! Native parameter catalogue and detached working copies. A save is reconciled
//! against event-stream state rather than waiting for list projection visibility.
use crate::{
    backend::{schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{parameter_fields, parameters as api},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
fn f<'a>(intent: &'a Json, key: &str) -> &'a str {
    intent["fields"][key].as_str().unwrap_or_default()
}
fn member(wire: &Value, index: usize) -> Result<&Value, String> {
    wire.as_array()
        .and_then(|v| v.get(index))
        .ok_or("Incomplete parameter model".into())
}
fn model(registry: &Registry, name: &str, wire: &Value) -> Result<Json, String> {
    registry.project(&api::kind(name), wire)
}
fn id(row: &Json) -> Result<String, String> {
    let set = row["Reference"]["SetId"]
        .as_str()
        .ok_or("Missing parameter identity")?;
    let version = row["Reference"]["Version"]
        .as_i64()
        .ok_or("Missing parameter version")?;
    Ok(format!("{set}:{version}"))
}
fn root_value(wire: &Value) -> Result<&Value, String> {
    member(member(wire, 1)?, 0)
}
fn parameter_identity(registry: &Registry, wire: &Value) -> Result<(uuid::Uuid, i64, i64), String> {
    let row = model(registry, "ParameterSetValue", wire)?;
    let value = &row["Value"];
    Ok((
        uuid::Uuid::parse_str(
            value["ParameterSetId"]
                .as_str()
                .ok_or("Missing draft identity")?,
        )
        .map_err(|_| "Invalid draft identity")?,
        value["SchemaVersion"]
            .as_i64()
            .ok_or("Missing draft schema")?,
        value["TargetHorizon"]
            .as_str()
            .and_then(|v| {
                registry
                    .enum_value(
                        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                        v,
                    )
                    .ok()
            })
            .unwrap_or(1),
    ))
}
#[derive(Default)]
pub struct ParameterController {
    components: Vec<Json>,
    versions: Vec<Value>,
    component: String,
    selected: Option<Value>,
    original: Option<Value>,
    working: Option<Value>,
    historical: Option<String>,
    name: String,
    description: String,
    revision: i64,
    editing: bool,
    awaiting: bool,
    pending: Option<(uuid::Uuid, uuid::Uuid)>,
    // An ambiguous assignment must be checked by its audit operation ID before
    // another write. Matching the requested value alone cannot prove acceptance.
    pending_assignment: Option<(uuid::Uuid, uuid::Uuid)>,
    documents: BTreeMap<String, serde_bytes::ByteBuf>,
    extras: BTreeMap<String, String>,
    generation: u64,
    receipts: BTreeMap<String, (Json, EditorOutcome)>,
    assignment: Option<Value>,
    legacy: Vec<Value>,
    legacy_offset: i64,
    legacy_preview: Option<(String, Value)>,
}
impl ParameterController {
    fn replace_regime(
        &mut self,
        registry: &Registry,
        fields: BTreeMap<String, Value>,
    ) -> Result<(), String> {
        let payload = self.payload()?;
        if member(payload, 0)?.as_i64() != Some(0) {
            return Err("Open a Regime Discovery working copy first".into());
        }
        let value = api::overlay_type(
            registry,
            &format!(
                "{}.RegimeDiscoveryParameterSet",
                crate::services::parameter_metrics::ROOT
            ),
            root_value(payload)?,
            &fields,
        )?;
        self.working = Some(Value::Array(vec![0.into(), Value::Array(vec![value])]));
        Ok(())
    }
    fn capture_metrics(&mut self, registry: &Registry, intent: &Json) -> Result<(), String> {
        let docs = &intent["documents"];
        if docs.get("signals").is_none() && docs.get("observations").is_none() {
            return Ok(());
        }
        if !self.editing {
            return Err("Saved versions are read-only".into());
        }
        // The copied editor includes empty metric documents for other component
        // types too. Those documents are not Regime Discovery edits.
        if member(self.payload()?, 0)?.as_i64() != Some(0) {
            return Ok(());
        }
        let row = model(registry, "ParameterSetValue", self.payload()?)?;
        let value = &row["Value"];
        let set = value["ParameterSetId"]
            .as_str()
            .ok_or("Missing parameter identity")?;
        let old = value["SignalRequirements"]
            .as_array()
            .cloned()
            .unwrap_or_default();
        let (default_signals, default_observations) =
            crate::services::parameter_metrics::from_legacy(registry, set, &old)?;
        let mut fields = BTreeMap::new();
        let mut normalized = vec![];
        for (key, name, fallback) in [
            (
                "signals",
                "RegimeDiscoverySignalMetricConfiguration",
                if value["SignalMetrics"].is_array() {
                    value["SignalMetrics"].clone()
                } else {
                    json!(default_signals)
                },
            ),
            (
                "observations",
                "RegimeDiscoveryObservationMetricConfiguration",
                if value["ObservationMetrics"].is_array() {
                    value["ObservationMetrics"].clone()
                } else {
                    json!(default_observations)
                },
            ),
        ] {
            let kind = format!("{}.{}[]", crate::services::parameter_metrics::ROOT, name);
            let wire = if let Some(doc) = docs.get(key) {
                api::from_document(registry, &kind, doc)?
            } else {
                registry.value_from_json(&kind, &fallback)?
            };
            let row = registry.project(&kind, &wire)?;
            let rows = row
                .as_array()
                .filter(|v| v.len() <= 1000)
                .ok_or("Invalid metric selection")?;
            for row in rows {
                if row["Metric"] == "Unknown"
                    || row["MaximumAgeSeconds"].as_i64().is_none_or(|v| v <= 0)
                    || key == "signals"
                        && (row["PeriodLength"].as_i64().is_none_or(|v| v <= 0)
                            || !matches!(
                                row["TimeFrame"].as_str(),
                                Some(
                                    "FifteenSeconds"
                                        | "OneMinute"
                                        | "FiveMinutes"
                                        | "FifteenMinutes"
                                        | "OneHour"
                                        | "FourHours"
                                        | "Daily"
                                )
                            ))
                {
                    return Err("Metric, timeframe, period and maximum age must be valid".into());
                }
            }
            normalized.push(row);
            fields.insert(
                if key == "signals" {
                    "SignalMetrics"
                } else {
                    "ObservationMetrics"
                }
                .into(),
                wire,
            );
        }
        let legacy = crate::services::parameter_metrics::legacy(
            registry,
            set,
            normalized[0].as_array().unwrap(),
            normalized[1].as_array().unwrap(),
            &value["Horizon"],
        )?;
        fields.insert(
            "SignalRequirements".into(),
            registry.value_from_json(
                &format!(
                    "{}.RegimeDiscoverySignalConfiguration[]",
                    crate::services::parameter_metrics::ROOT
                ),
                &json!(legacy),
            )?,
        );
        self.replace_regime(registry, fields)
    }
    async fn assignment(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let payload = self.payload()?;
        if member(payload, 0)?.as_i64() != Some(0) {
            return Err("Only Regime Discovery supports this workflow assignment".into());
        }
        let (_, _, horizon) = parameter_identity(registry, payload)?;
        let wire = api::query(
            backend,
            registry,
            "GetParameterAssignmentQuery",
            BTreeMap::from([
                (
                    "WorkflowDefinitionId".into(),
                    "IntrinsicTimeStrategy".into(),
                ),
                ("TargetHorizon".into(), horizon.into()),
            ]),
            deadline,
        )
        .await?;
        self.document(
            "assignment",
            &api::document(
                registry,
                &api::kind("ParameterAssignmentSnapshot"),
                &wire,
                true,
            )?,
        )?;
        self.assignment = Some(wire);
        Ok(())
    }
    async fn legacy_page(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let wire = api::query(
            backend,
            registry,
            "ListLegacyParameterVersionsQuery",
            BTreeMap::from([("Offset".into(), self.legacy_offset.into())]),
            deadline,
        )
        .await?;
        self.legacy = wire
            .as_array()
            .filter(|v| v.len() <= 100)
            .ok_or("Invalid historical parameter page")?
            .clone();
        self.legacy_preview = None;
        self.documents.remove("legacyPreview");
        self.extras.remove("legacyPreviewId");
        let rows=self.legacy.iter().map(|v|{let row=model(registry,"ParameterLegacyVersion",v)?;Ok(json!({"Id":format!("{}:{}",row["Reference"]["SetId"].as_str().unwrap_or_default(),row["Reference"]["Version"]),"Reference":row["Reference"],"SchemaVersion":row["SchemaVersion"],"PayloadJson":row["PayloadJson"],"Status":row["Status"]}))}).collect::<Result<Vec<_>,String>>()?;
        self.document("legacyVersions", &json!(rows))?;
        self.extras
            .insert("legacyOffset".into(), self.legacy_offset.to_string());
        self.extras.insert(
            "legacyHasMore".into(),
            if self.legacy.len() == 100 {
                "True"
            } else {
                "False"
            }
            .into(),
        );
        Ok(())
    }
    fn clear(&mut self) {
        self.selected = None;
        self.original = None;
        self.working = None;
        self.historical = None;
        self.name.clear();
        self.description.clear();
        self.revision = 0;
        self.editing = false;
        self.awaiting = false;
        self.documents.clear();
        self.extras.clear();
        self.assignment = None;
    }
    fn select(&mut self, registry: &Registry, wire: Value, revision: i64) -> Result<(), String> {
        self.clear();
        let row = model(registry, "ParameterSetVersion", &wire)?;
        self.component = row["Reference"]["ComponentCode"]
            .as_str()
            .ok_or("Missing component")?
            .into();
        self.name = row["Name"].as_str().unwrap_or_default().into();
        self.description = row["Description"].as_str().unwrap_or_default().into();
        self.revision = revision;
        let parameters = member(&wire, 12)?;
        if parameters.is_nil() {
            self.historical = Some(row["PayloadJson"].as_str().unwrap_or_default().into());
        } else {
            parameter_fields::read(registry, parameters, false)?;
            self.original = Some(parameters.clone());
            self.working = Some(parameters.clone());
        }
        self.selected = Some(wire);
        Ok(())
    }
    async fn load_versions(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.clear();
        self.versions = if self.component.is_empty() {
            vec![]
        } else {
            api::versions(backend, registry, &self.component, None, deadline).await?
        };
        if let Some(first) = self.versions.first().cloned() {
            let selected = model(registry, "ParameterSetVersion", &first)?;
            let set = &selected["Reference"]["SetId"];
            let revision = self
                .versions
                .iter()
                .map(|v| model(registry, "ParameterSetVersion", v))
                .collect::<Result<Vec<_>, _>>()?
                .iter()
                .filter(|v| v["Reference"]["SetId"] == *set)
                .filter_map(|v| v["CatalogRevision"].as_i64())
                .max()
                .unwrap_or_default();
            self.select(registry, first, revision)?;
        }
        Ok(())
    }
    fn document(&mut self, key: &str, value: &Json) -> Result<(), String> {
        self.documents
            .insert(key.into(), crate::wire::encode_value(value)?.into());
        Ok(())
    }
    fn payload(&self) -> Result<&Value, String> {
        if self.editing {
            self.working.as_ref()
        } else {
            self.original.as_ref()
        }
        .ok_or("No supported parameter values".into())
    }
    fn capture(&mut self, registry: &Registry, intent: &Json) -> Result<(), String> {
        let docs = intent["documents"].as_object();
        if !self.editing {
            if docs.is_some_and(|v| {
                v.contains_key("changes")
                    || v.contains_key("signals")
                    || v.contains_key("observations")
            }) {
                return Err("Saved versions are read-only".into());
            }
            return Ok(());
        }
        if let Some(name) = intent["fields"]["name"].as_str() {
            self.name = name.into();
        }
        if let Some(description) = intent["fields"]["description"].as_str() {
            self.description = description.into();
        }
        if let Some(changes) = docs.and_then(|v| v.get("changes")) {
            let fields: Vec<parameter_fields::ParameterField> =
                serde_json::from_value(changes.clone()).map_err(|e| e.to_string())?;
            self.working = Some(parameter_fields::apply(registry, self.payload()?, &fields)?);
        }
        Ok(())
    }
    async fn preview(
        &self,
        backend: &Backend,
        registry: &Registry,
        set: uuid::Uuid,
        horizon: i64,
        source: Option<Value>,
        rebuild: bool,
        deadline: tokio::time::Instant,
    ) -> Result<Value, String> {
        api::query(
            backend,
            registry,
            "PreviewTypedParameterDraftQuery",
            BTreeMap::from([
                ("SetId".into(), set.to_string().into()),
                ("ComponentCode".into(), self.component.clone().into()),
                ("TargetHorizon".into(), horizon.into()),
                ("Source".into(), source.unwrap_or(Value::Nil)),
                ("RebuildIntervals".into(), rebuild.into()),
            ]),
            deadline,
        )
        .await
    }
    async fn committed(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        set: uuid::Uuid,
        operation: uuid::Uuid,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let state = api::query(
            backend,
            registry,
            "GetParameterSetStateQuery",
            BTreeMap::from([("SetId".into(), set.to_string().into())]),
            deadline,
        )
        .await
        .map_err(|e| {
            format!("OutcomeUnknown: commit accepted but authoritative state failed: {e}")
        })?;
        let row = model(registry, "ParameterSetSnapshot", &state)?;
        if row["SetId"] != set.to_string() {
            return Err("OutcomeUnknown: state returned a different parameter set".into());
        }
        let receipt = row["Operations"]
            .as_array()
            .and_then(|v| v.iter().find(|v| v["OperationId"] == operation.to_string()))
            .ok_or(
                "OutcomeUnknown: committed operation is not visible; reconcile before editing",
            )?;
        let versions = member(&state, 2)?
            .as_array()
            .ok_or("Invalid parameter snapshot versions")?;
        let selected = versions
            .iter()
            .find(|v| {
                model(registry, "ParameterSetVersion", v)
                    .is_ok_and(|v| v["Reference"] == receipt["Reference"])
            })
            .cloned()
            .ok_or("OutcomeUnknown: committed version not visible")?;
        self.versions.retain(|v| {
            model(registry, "ParameterSetVersion", v)
                .is_ok_and(|v| v["Reference"]["SetId"] != set.to_string())
        });
        self.versions.extend(versions.iter().cloned());
        self.select(
            registry,
            selected,
            row["Revision"]
                .as_i64()
                .ok_or("Invalid snapshot revision")?,
        )?;
        Ok(())
    }
    fn result(&mut self, registry: &Registry) -> Result<ContractResult, String> {
        self.document("components", &json!(self.components))?;
        let selected = self
            .selected
            .as_ref()
            .map(|v| model(registry, "ParameterSetVersion", v))
            .transpose()?;
        let mut context = self.extras.clone();
        for (key, value) in [
            ("component", self.component.clone()),
            (
                "editing",
                if self.editing { "True" } else { "False" }.into(),
            ),
            (
                "canEdit",
                if self.working.is_some() || self.selected.is_none() {
                    "True"
                } else {
                    "False"
                }
                .into(),
            ),
            ("name", self.name.clone()),
            ("description", self.description.clone()),
            (
                "selectedId",
                selected.as_ref().map(id).transpose()?.unwrap_or_default(),
            ),
            ("revision", self.revision.to_string()),
            (
                "hasPayload",
                if self.working.is_some() || self.historical.is_some() {
                    "True"
                } else {
                    "False"
                }
                .into(),
            ),
        ] {
            context.insert(key.into(), value);
        }
        if let Some(historical) = &self.historical {
            context.insert("payload".into(), historical.clone());
        } else if let Ok(payload) = self.payload().cloned() {
            self.document(
                "fields",
                &serde_json::to_value(parameter_fields::read(registry, &payload, false)?)
                    .map_err(|e| e.to_string())?,
            )?;
            context.insert(
                "payload".into(),
                parameter_fields::display(registry, &payload)?,
            );
            let regime = member(&payload, 0)?.as_i64() == Some(0);
            context.insert(
                "regime".into(),
                if regime { "True" } else { "False" }.into(),
            );
        }
        let mut choices = BTreeMap::from([(
            "horizons".into(),
            vec!["Daily".into(), "Weekly".into(), "Monthly".into()],
        )]);
        for (key, kind) in [
            (
                "signalMetric",
                "TomasAI.IFM.Domain.MarketData.Shared.SignalMetricsType",
            ),
            (
                "observationMetric",
                "TomasAI.IFM.Domain.MarketData.Shared.ObservationMetricsType",
            ),
        ] {
            let names = registry
                .enum_names(kind)?
                .into_iter()
                .filter(|s| s != "Unknown")
                .collect::<Vec<_>>();
            choices.insert(
                format!("{key}Ids"),
                names
                    .iter()
                    .map(|v| registry.enum_value(kind, v).map(|v| v.to_string()))
                    .collect::<Result<Vec<_>, _>>()?,
            );
            choices.insert(format!("{key}Names"), names);
        }
        let timeframes = [
            "FifteenSeconds",
            "OneMinute",
            "FiveMinutes",
            "FifteenMinutes",
            "OneHour",
            "FourHours",
            "Daily",
        ];
        choices.insert(
            "timeframeNames".into(),
            timeframes.iter().map(|s| s.to_string()).collect(),
        );
        choices.insert(
            "timeframes".into(),
            timeframes
                .iter()
                .map(|s| {
                    registry
                        .enum_value(
                            "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                            s,
                        )
                        .map(|v| v.to_string())
                })
                .collect::<Result<Vec<_>, _>>()?,
        );
        if let Ok(payload) = self.payload().cloned() {
            if member(&payload, 0)?.as_i64() == Some(0) {
                let row = model(registry, "ParameterSetValue", &payload)?;
                let value = &row["Value"];
                let set = value["ParameterSetId"]
                    .as_str()
                    .ok_or("Missing metric identity")?;
                let defaults = crate::services::parameter_metrics::from_legacy(
                    registry,
                    set,
                    &value["SignalRequirements"]
                        .as_array()
                        .cloned()
                        .unwrap_or_default(),
                )?;
                for (key, name, value) in [
                    (
                        "signals",
                        "RegimeDiscoverySignalMetricConfiguration",
                        if value["SignalMetrics"].is_array() {
                            value["SignalMetrics"].clone()
                        } else {
                            json!(defaults.0)
                        },
                    ),
                    (
                        "observations",
                        "RegimeDiscoveryObservationMetricConfiguration",
                        if value["ObservationMetrics"].is_array() {
                            value["ObservationMetrics"].clone()
                        } else {
                            json!(defaults.1)
                        },
                    ),
                ] {
                    let kind = format!("{}.{}[]", crate::services::parameter_metrics::ROOT, name);
                    let wire = registry.value_from_json(&kind, &value)?;
                    self.document(key, &api::document(registry, &kind, &wire, false)?)?;
                }
                let frames = member(member(root_value(&payload)?, 6)?, 1)?;
                self.document(
                    "intervals",
                    &api::document(
                        registry,
                        &format!(
                            "{}.RegimeDiscoveryTimeFrameConfiguration[]",
                            crate::services::parameter_metrics::ROOT
                        ),
                        frames,
                        false,
                    )?,
                )?;
            } else {
                for key in ["signals", "observations", "intervals"] {
                    self.document(key, &json!([]))?;
                }
            }
        }
        let rows = self
            .versions
            .iter()
            .map(|wire| {
                let row = model(registry, "ParameterSetVersion", wire)?;
                Ok(ContractRow {
                    id: id(&row)?,
                    fields: BTreeMap::from([
                        (
                            "name".into(),
                            row["Name"].as_str().unwrap_or_default().into(),
                        ),
                        (
                            "status".into(),
                            row["Status"].as_str().unwrap_or_default().into(),
                        ),
                        ("version".into(), row["Reference"]["Version"].to_string()),
                        (
                            "setId".into(),
                            row["Reference"]["SetId"]
                                .as_str()
                                .unwrap_or_default()
                                .into(),
                        ),
                    ]),
                    ..Default::default()
                })
            })
            .collect::<Result<_, String>>()?;
        Ok(ContractResult {
            view: "parameter-sets".into(),
            rows,
            choices,
            context,
            documents: self.documents.clone(),
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
        let action = intent["intent"]
            .as_str()
            .ok_or("Missing parameter action")?;
        if !matches!(action, "load" | "resume-parameter")
            && intent["expectedGeneration"].as_u64() != Some(self.generation)
        {
            return Err(
                "Parameter view changed; review the current selection before continuing".into(),
            );
        }
        if let Some((entity, operation)) = self.pending_assignment {
            if !matches!(action, "load" | "resume-parameter") {
                return Err("Reconcile the uncertain assignment before another action".into());
            }
            self.assignment(backend, registry, deadline).await?;
            let row = model(
                registry,
                "ParameterAssignmentSnapshot",
                self.assignment.as_ref().ok_or("Missing assignment")?,
            )?;
            if row["EntityId"]["AssignmentId"] != entity.to_string()
                || !row["Audit"].as_array().is_some_and(|items| {
                    items
                        .iter()
                        .any(|item| item["OperationId"] == operation.to_string())
                })
            {
                return Err(
                    "OutcomeUnknown: assignment operation is not visible in authoritative audit"
                        .into(),
                );
            }
            self.pending_assignment = None;
            self.awaiting = false;
            for (_, outcome) in self.receipts.values_mut() {
                if outcome.request_id == operation.to_string() {
                    outcome.state = "Completed".into();
                    outcome.message = "Assignment commit reconciled".into();
                }
            }
            return Ok("Assignment commit reconciled".into());
        }
        if let Some((set, operation)) = self.pending {
            if matches!(action, "load" | "resume-parameter") {
                self.committed(backend, registry, set, operation, deadline)
                    .await?;
                self.pending = None;
                // Only one uncertain mutation is permitted by this owner. Its
                // verified commit also resolves the original retained receipt.
                for (_, outcome) in self.receipts.values_mut() {
                    if outcome.state == "Unknown" {
                        outcome.state = "Completed".into();
                        outcome.message = "Parameter commit reconciled".into();
                    }
                }
                return Ok("Committed parameter operation reconciled".into());
            }
            return Err("Reconcile the uncertain parameter operation before another action".into());
        }
        if action != "load" && self.components.is_empty() {
            return Err("Load the parameter catalogue first".into());
        }
        if matches!(action, "component" | "select" | "new-draft") && self.editing {
            return Err("Save or discard the working copy first".into());
        }
        if matches!(
            action,
            "publish-parameter"
                | "retire-parameter"
                | "rename-parameter"
                | "assignment"
                | "assign-parameter"
                | "disable-assignment"
        ) && (self.editing || self.selected.is_none())
        {
            return Err("Select a saved parameter version first".into());
        }
        if matches!(
            action,
            "stage-fields"
                | "stage-metrics"
                | "intervals"
                | "save-draft"
                | "validate-parameter"
                | "payload-parameter"
        ) {
            self.capture(registry, intent)?;
            self.capture_metrics(registry, intent)?;
        }
        match action {
            "resume-parameter" => {}
            "load" => {
                if self.editing {
                    return Err("Discard the working copy before refreshing".into());
                }
                let wire = api::query(
                    backend,
                    registry,
                    "ListParameterComponentsQuery",
                    BTreeMap::new(),
                    deadline,
                )
                .await?;
                let rows = wire
                    .as_array()
                    .filter(|v| v.len() <= 1000)
                    .ok_or("Invalid parameter catalogue")?;
                self.components = rows
                    .iter()
                    .map(|v| model(registry, "ParameterComponentSummary", v))
                    .collect::<Result<_, _>>()?;
                self.component = self
                    .components
                    .first()
                    .and_then(|v| v["ComponentCode"].as_str())
                    .unwrap_or_default()
                    .into();
                self.load_versions(backend, registry, deadline).await?;
            }
            "component" => {
                if !self
                    .components
                    .iter()
                    .any(|v| v["ComponentCode"] == f(intent, "component"))
                {
                    return Err("Unknown parameter component".into());
                }
                self.component = f(intent, "component").into();
                self.load_versions(backend, registry, deadline).await?;
            }
            "select" => {
                let selected = intent["originalId"].as_str().unwrap_or_default();
                let wire = self
                    .versions
                    .iter()
                    .find(|v| {
                        model(registry, "ParameterSetVersion", v)
                            .and_then(|r| id(&r))
                            .is_ok_and(|v| v == selected)
                    })
                    .cloned()
                    .ok_or("Unknown parameter version")?;
                let row = model(registry, "ParameterSetVersion", &wire)?;
                let revision = self
                    .versions
                    .iter()
                    .map(|v| model(registry, "ParameterSetVersion", v))
                    .collect::<Result<Vec<_>, _>>()?
                    .iter()
                    .filter(|v| v["Reference"]["SetId"] == row["Reference"]["SetId"])
                    .filter_map(|v| v["CatalogRevision"].as_i64())
                    .max()
                    .unwrap_or_default();
                self.select(registry, wire, revision)?;
            }
            "new-draft" => {
                let component = self
                    .components
                    .iter()
                    .find(|v| v["ComponentCode"] == self.component && v["CanEdit"] == true)
                    .ok_or("This component is read-only")?;
                let name = component["Name"].as_str().unwrap_or_default().to_string();
                let horizon = f(intent, "horizon");
                if !matches!(horizon, "Daily" | "Weekly" | "Monthly") {
                    return Err("Unsupported horizon".into());
                }
                let h = registry.enum_value(
                    "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                    horizon,
                )?;
                let value = self
                    .preview(
                        backend,
                        registry,
                        uuid::Uuid::new_v4(),
                        h,
                        None,
                        false,
                        deadline,
                    )
                    .await?;
                parameter_fields::read(registry, &value, false)?;
                self.clear();
                self.original = Some(value.clone());
                self.working = Some(value.clone());
                self.name = if member(&value, 0)?.as_i64() == Some(0) {
                    format!("Regime Discovery {horizon}")
                } else {
                    name
                };
                self.editing = true;
            }
            "edit-draft" => {
                if self.awaiting {
                    return Err("Reconcile the committed version before editing".into());
                }
                if self.selected.is_none() || self.editing {
                    return Err("Select a saved version first".into());
                }
                let payload = self.payload()?.clone();
                let (set, _, horizon) = parameter_identity(registry, &payload)?;
                self.working = Some(
                    self.preview(
                        backend,
                        registry,
                        set,
                        horizon,
                        Some(payload),
                        false,
                        deadline,
                    )
                    .await?,
                );
                self.editing = true;
                self.documents.clear();
                self.extras.clear();
            }
            "discard" => {
                if let Some(selected) = self.selected.clone() {
                    self.select(registry, selected, self.revision)?;
                } else {
                    self.clear();
                }
            }
            "stage-fields" | "stage-metrics" => {}
            "intervals" => {
                if !self.editing {
                    return Err("Open a Regime Discovery working copy first".into());
                }
                let json = &intent["documents"]["intervals"];
                let kind = format!(
                    "{}.RegimeDiscoveryTimeFrameConfiguration[]",
                    crate::services::parameter_metrics::ROOT
                );
                let wire = api::from_document(registry, &kind, json)?;
                let rows = registry.project(&kind, &wire)?;
                let rows = rows
                    .as_array()
                    .filter(|v| v.len() <= 100)
                    .ok_or("Invalid intervals")?;
                if rows.iter().any(|v| {
                    v["Weight"]
                        .as_str()
                        .and_then(|v| v.parse::<f64>().ok())
                        .is_none_or(|v| v <= 0.0)
                        || v["MaximumAgeSeconds"].as_i64().is_none_or(|v| v <= 0)
                        || !matches!(
                            v["TimeFrame"].as_str(),
                            Some(
                                "FifteenSeconds"
                                    | "OneMinute"
                                    | "FiveMinutes"
                                    | "FifteenMinutes"
                                    | "OneHour"
                                    | "FourHours"
                                    | "Daily"
                            )
                        )
                }) {
                    return Err("Intervals require positive weights and maximum age".into());
                }
                let rows = rows
                    .iter()
                    .cloned()
                    .map(|mut row| {
                        row["IsRequired"] = json!(true);
                        row
                    })
                    .collect::<Vec<_>>();
                let frames = registry.value_from_json(&kind, &json!(rows))?;
                let horizon = api::overlay_type(
                    registry,
                    &format!(
                        "{}.RegimeDiscoveryHorizonConfiguration",
                        crate::services::parameter_metrics::ROOT
                    ),
                    member(root_value(self.payload()?)?, 6)?,
                    &BTreeMap::from([("TimeFrames".into(), frames)]),
                )?;
                self.replace_regime(registry, BTreeMap::from([("Horizon".into(), horizon)]))?;
                let payload = self.payload()?.clone();
                let (set, _, horizon) = parameter_identity(registry, &payload)?;
                self.working = Some(
                    self.preview(
                        backend,
                        registry,
                        set,
                        horizon,
                        Some(payload),
                        true,
                        deadline,
                    )
                    .await?,
                );
            }
            "schema-parameter" => {
                let payload = self.payload()?;
                let (_, schema, _) = parameter_identity(registry, payload)?;
                let fields = parameter_fields::read(registry, payload, true)?
                    .into_iter()
                    .map(|v| json!({"Path":v.path,"Kind":v.kind}))
                    .collect::<Vec<_>>();
                self.document("schema",&json!({"ComponentCode":self.component,"Version":schema,"Codec":"typed-messagepack-v1","Fields":fields}))?;
            }
            "compare-parameter" => {
                let payload = self.payload()?.clone();
                let (set, _, _) = parameter_identity(registry, &payload)?;
                let state = api::query(
                    backend,
                    registry,
                    "GetParameterSetStateQuery",
                    BTreeMap::from([("SetId".into(), set.to_string().into())]),
                    deadline,
                )
                .await?;
                let versions = member(&state, 2)?
                    .as_array()
                    .ok_or("Invalid comparison versions")?;
                let choices=versions.iter().map(|wire|{let row=model(registry,"ParameterSetVersion",wire)?;Ok(json!({"Id":id(&row)?,"Name":row["Name"],"Version":row["Reference"]["Version"]}))}).collect::<Result<Vec<_>,String>>()?;
                self.document("comparisonVersions", &json!(choices))?;
                let before = versions
                    .iter()
                    .find(|v| {
                        model(registry, "ParameterSetVersion", v)
                            .and_then(|v| id(&v))
                            .is_ok_and(|v| v == f(intent, "compareId"))
                    })
                    .or_else(|| versions.first())
                    .ok_or("No comparison version")?;
                let a = parameter_fields::read(registry, member(before, 12)?, true)?
                    .into_iter()
                    .map(|v| (v.path, v.value))
                    .collect::<BTreeMap<_, _>>();
                let b = parameter_fields::read(registry, &payload, true)?
                    .into_iter()
                    .map(|v| (v.path, v.value))
                    .collect::<BTreeMap<_, _>>();
                let keys = a
                    .keys()
                    .chain(b.keys())
                    .collect::<std::collections::BTreeSet<_>>();
                let rows = keys
                    .into_iter()
                    .filter_map(|key| {
                        let before = a.get(key).map(String::as_str).unwrap_or("(absent)");
                        let after = b.get(key).map(String::as_str).unwrap_or("(absent)");
                        (before != after).then(|| json!({"Path":key,"Before":before,"After":after}))
                    })
                    .collect::<Vec<_>>();
                self.document("differences", &json!(rows))?;
            }
            "assignment" => {
                if self
                    .selected
                    .as_ref()
                    .map(|v| model(registry, "ParameterSetVersion", v))
                    .transpose()?
                    .is_none_or(|v| v["Status"] != "Published")
                {
                    return Err("Select a published version first".into());
                }
                self.assignment(backend, registry, deadline).await?;
            }
            "assign-parameter" | "disable-assignment" => {
                let selected = self.selected.as_ref().ok_or("Select a published version")?;
                let row = model(registry, "ParameterSetVersion", selected)?;
                if row["Status"] != "Published" || member(self.payload()?, 0)?.as_i64() != Some(0) {
                    return Err("Load a published Regime Discovery assignment first".into());
                }
                let assignment = self.assignment.as_ref().ok_or("Load assignment first")?;
                let row = model(registry, "ParameterAssignmentSnapshot", assignment)?;
                let entity = uuid::Uuid::parse_str(
                    row["EntityId"]["AssignmentId"]
                        .as_str()
                        .ok_or("Missing assignment identity")?,
                )
                .map_err(|_| "Invalid assignment identity")?;
                let reference = if action == "assign-parameter" {
                    member(selected, 0)?.clone()
                } else {
                    if row["Assignment"]["Enabled"] != true {
                        return Err("Assignment is already disabled".into());
                    }
                    let value = member(assignment, 3)?;
                    let definition =
                        registry.describe(&api::kind("ParameterAssignmentRevision"))?;
                    let index = definition
                        .members
                        .iter()
                        .find(|(n, _, _)| n == "Reference")
                        .ok_or("Missing assignment reference")?
                        .1;
                    member(value, index)?.clone()
                };
                self.pending_assignment = Some((entity, request));
                let submitted = api::command(
                    backend,
                    registry,
                    if action == "assign-parameter" {
                        "AssignParameterVersionCommand"
                    } else {
                        "DisableParameterAssignmentCommand"
                    },
                    entity,
                    request,
                    BTreeMap::from([
                        ("Scope".into(), member(assignment, 1)?.clone()),
                        ("Reference".into(), reference),
                        (
                            "ExpectedRevision".into(),
                            row["Revision"]
                                .as_i64()
                                .ok_or("Missing assignment revision")?
                                .into(),
                        ),
                    ]),
                    deadline,
                )
                .await;
                if let Err(error) = submitted {
                    if !error.starts_with("OutcomeUnknown:") {
                        self.pending_assignment = None;
                    }
                    return Err(error);
                }
                self.assignment(backend, registry, deadline)
                    .await
                    .map_err(|e| {
                        format!("OutcomeUnknown: assignment accepted but refresh failed: {e}")
                    })?;
                self.pending_assignment = None;
            }
            "startup-preview" | "startup-report" | "signal-monitoring" => {
                let (name, result, key, field) = match action {
                    "startup-preview" => (
                        "PreviewSignalStartupPlanQuery",
                        "ParameterSignalStartupPlan",
                        "startupPreview",
                        "StartupRunId",
                    ),
                    "startup-report" => (
                        "GetParameterStartupReportQuery",
                        "ParameterSignalStartupReport",
                        "startupReport",
                        "RunId",
                    ),
                    _ => (
                        "GetParameterSignalMonitoringQuery",
                        "ParameterSignalMonitoringSnapshot",
                        "signalMonitoring",
                        "RunId",
                    ),
                };
                let id = if action == "startup-preview" {
                    uuid::Uuid::new_v4()
                } else {
                    uuid::Uuid::parse_str(f(intent, "runId"))
                        .map_err(|_| "Invalid startup run identity")?
                };
                let wire = api::query(
                    backend,
                    registry,
                    name,
                    BTreeMap::from([(field.into(), id.to_string().into())]),
                    deadline,
                )
                .await?;
                self.document(
                    key,
                    &api::document(registry, &api::kind(result), &wire, true)?,
                )?;
            }
            "startup-runs" => {
                let mut fields = BTreeMap::new();
                let mut all = vec![];
                let mut ids = std::collections::BTreeSet::new();
                for page in 0..100 {
                    let wire = api::query(
                        backend,
                        registry,
                        "GetParameterStartupRunsQuery",
                        fields.clone(),
                        deadline,
                    )
                    .await?;
                    let rows = wire
                        .as_array()
                        .filter(|v| v.len() <= 20)
                        .ok_or("Invalid startup history page")?;
                    for wire in rows {
                        let row =
                            api::document(registry, &api::kind("ParameterStartupRun"), wire, true)?;
                        let id = row["RunId"]
                            .as_str()
                            .ok_or("Missing startup run identity")?;
                        if !ids.insert(id.to_string()) {
                            return Err("Startup history did not advance".into());
                        }
                        all.push(row);
                    }
                    if rows.len() < 20 {
                        break;
                    }
                    if page == 99 {
                        return Err("Parameter startup history exceeds 2000 runs".into());
                    }
                    let last = model(registry, "ParameterStartupRun", rows.last().unwrap())?;
                    fields.insert(
                        "AfterRunId".into(),
                        last["RunId"]
                            .as_str()
                            .ok_or("Missing startup cursor identity")?
                            .into(),
                    );
                    fields.insert(
                        "AfterCreatedAtUtc".into(),
                        registry.value_from_json("System.DateTime", &last["CreatedAtUtc"])?,
                    );
                }
                self.document("startupRuns", &json!(all))?;
            }
            "legacy-parameters" | "legacy-next" | "legacy-prev" => {
                match action {
                    "legacy-parameters" => self.legacy_offset = 0,
                    "legacy-next" => {
                        if self.legacy.len() < 100 {
                            return Err("No more historical versions".into());
                        }
                        self.legacy_offset = self
                            .legacy_offset
                            .checked_add(100)
                            .ok_or("Legacy offset overflow")?;
                    }
                    _ => self.legacy_offset = (self.legacy_offset - 100).max(0),
                }
                self.legacy_page(backend, registry, deadline).await?;
            }
            "legacy-preview" => {
                let row = self
                    .legacy
                    .iter()
                    .map(|wire| model(registry, "ParameterLegacyVersion", wire))
                    .collect::<Result<Vec<_>, _>>()?
                    .into_iter()
                    .find(|row| {
                        format!(
                            "{}:{}",
                            row["Reference"]["SetId"].as_str().unwrap_or_default(),
                            row["Reference"]["Version"]
                        ) == f(intent, "legacyId")
                    })
                    .ok_or("Select a historical version")?;
                let wire = api::query(
                    backend,
                    registry,
                    "PreviewLegacyParameterMigrationQuery",
                    BTreeMap::from([
                        (
                            "SetId".into(),
                            row["Reference"]["SetId"]
                                .as_str()
                                .ok_or("Invalid legacy identity")?
                                .into(),
                        ),
                        (
                            "Version".into(),
                            row["Reference"]["Version"]
                                .as_i64()
                                .ok_or("Invalid legacy version")?
                                .into(),
                        ),
                    ]),
                    deadline,
                )
                .await?;
                self.document(
                    "legacyPreview",
                    &api::document(
                        registry,
                        &api::kind("CreateParameterSetCommand"),
                        &wire,
                        false,
                    )?,
                )?;
                self.legacy_preview = Some((f(intent, "legacyId").into(), wire));
                self.extras
                    .insert("legacyPreviewId".into(), f(intent, "legacyId").into());
            }
            "legacy-create" => {
                if self.editing {
                    return Err("Save or discard the working copy first".into());
                }
                let (id, wire) = self
                    .legacy_preview
                    .as_ref()
                    .filter(|(id, _)| id == f(intent, "legacyId"))
                    .cloned()
                    .ok_or("Preview an exact historical draft first")?;
                let row = model(registry, "CreateParameterSetCommand", &wire)?;
                let set = uuid::Uuid::parse_str(
                    row["EntityId"]["SetId"]
                        .as_str()
                        .ok_or("Invalid migrated identity")?,
                )
                .map_err(|_| "Invalid migrated identity")?;
                let command = uuid::Uuid::parse_str(
                    row["CommandId"]
                        .as_str()
                        .ok_or("Invalid migration command")?,
                )
                .map_err(|_| "Invalid migration command")?;
                let shape = registry.describe(&api::kind("CreateParameterSetCommand"))?;
                let fields = shape
                    .members
                    .into_iter()
                    .map(|(name, index, _)| Ok((name, member(&wire, index)?.clone())))
                    .collect::<Result<BTreeMap<_, _>, String>>()?;
                self.legacy_preview = None;
                self.extras.remove("legacyPreviewId");
                self.documents.remove("legacyPreview");
                self.pending = Some((set, command));
                let result = api::command(
                    backend,
                    registry,
                    "CreateParameterSetCommand",
                    set,
                    command,
                    fields,
                    deadline,
                )
                .await;
                if let Err(error) = result {
                    if !error.starts_with("OutcomeUnknown:") {
                        self.pending = None;
                    }
                    return Err(error);
                }
                self.component = row["ComponentCode"]
                    .as_str()
                    .ok_or("Missing migrated component")?
                    .into();
                self.committed(backend, registry, set, command, deadline)
                    .await?;
                self.pending = None;
                let _ = id;
            }
            "validate-parameter" => {
                let payload = self.payload()?;
                let (_, schema, _) = parameter_identity(registry, payload)?;
                let wire = api::query(
                    backend,
                    registry,
                    "ValidateTypedParameterCandidateQuery",
                    BTreeMap::from([
                        ("Parameters".into(), payload.clone()),
                        ("ComponentCode".into(), self.component.clone().into()),
                        ("SchemaVersion".into(), schema.into()),
                    ]),
                    deadline,
                )
                .await?;
                self.document(
                    "validation",
                    &api::document(
                        registry,
                        &api::kind("ParameterValidationReport"),
                        &wire,
                        false,
                    )?,
                )?;
            }
            "payload-parameter" => {
                self.extras.insert(
                    "payload".into(),
                    parameter_fields::display(registry, self.payload()?)?,
                );
            }
            "save-draft" | "publish-parameter" | "retire-parameter" | "rename-parameter" => {
                let payload = self.payload()?.clone();
                let (set, schema, _) = parameter_identity(registry, &payload)?;
                let selected = self
                    .selected
                    .as_ref()
                    .map(|v| model(registry, "ParameterSetVersion", v))
                    .transpose()?;
                let (name, fields) = match action {
                    "save-draft" => {
                        if !self.editing {
                            return Err("Open a working copy first".into());
                        }
                        (
                            if self.selected.is_none() {
                                "CreateParameterSetCommand"
                            } else {
                                "SaveParameterDraftCommand"
                            },
                            BTreeMap::from([
                                ("Parameters".into(), payload),
                                ("Name".into(), self.name.clone().into()),
                                ("Description".into(), self.description.clone().into()),
                                ("SchemaVersion".into(), schema.into()),
                            ]),
                        )
                    }
                    "publish-parameter" => {
                        if selected.as_ref().is_none_or(|v| v["Status"] != "Draft") {
                            return Err("Select a saved draft first".into());
                        }
                        ("PublishParameterVersionCommand", BTreeMap::new())
                    }
                    "retire-parameter" => {
                        if selected.as_ref().is_none_or(|v| v["Status"] != "Published") {
                            return Err("Select a published version first".into());
                        }
                        ("RetireParameterVersionCommand", BTreeMap::new())
                    }
                    _ => (
                        "RenameParameterSetCommand",
                        BTreeMap::from([
                            ("Name".into(), f(intent, "name").into()),
                            ("Description".into(), f(intent, "description").into()),
                        ]),
                    ),
                };
                let mut fields = fields;
                fields.insert("ComponentCode".into(), self.component.clone().into());
                fields.insert("ExpectedRevision".into(), self.revision.into());
                if action != "save-draft" {
                    fields.insert(
                        "Version".into(),
                        selected
                            .as_ref()
                            .and_then(|v| v["Reference"]["Version"].as_i64())
                            .ok_or("Missing selected version")?
                            .into(),
                    );
                }
                self.pending = Some((set, request));
                if let Err(error) =
                    api::command(backend, registry, name, set, request, fields, deadline).await
                {
                    if !error.starts_with("OutcomeUnknown:") {
                        self.pending = None;
                    }
                    return Err(error);
                }
                self.editing = false;
                self.awaiting = true;
                self.committed(backend, registry, set, request, deadline)
                    .await?;
                self.pending = None;
            }
            "audit-parameter" => {
                let (set, _, _) = parameter_identity(registry, self.payload()?)?;
                let wire = api::query(
                    backend,
                    registry,
                    "GetParameterSetStateQuery",
                    BTreeMap::from([("SetId".into(), set.to_string().into())]),
                    deadline,
                )
                .await?;
                self.document("audit", &model(registry, "ParameterSetSnapshot", &wire)?)?;
            }
            _ => return Err(format!("Parameter action {action} is not yet wired")),
        }
        Ok("Parameter action completed".into())
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "parameter-sets" {
            return Err("Invalid parameter intent".into());
        }
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid parameter request identity")?;
        if request.is_nil() {
            return Err("Missing parameter request identity".into());
        }
        let key = request.to_string();
        let outcome = if let Some((original, outcome)) = self.receipts.get(&key) {
            if original != intent {
                return Err("RequestId belongs to another parameter action".into());
            }
            outcome.clone()
        } else {
            // Reserve receipt space before any mutation can leave this process.
            // Unknown receipts are retained until their authoritative outcome is known.
            if self.receipts.len() >= 256 {
                let old = self
                    .receipts
                    .iter()
                    .find(|(_, (_, o))| o.state != "Unknown")
                    .map(|(k, _)| k.clone())
                    .ok_or("Reconcile uncertain parameter requests before continuing")?;
                self.receipts.remove(&old);
            }
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let result = tokio::time::timeout_at(
                deadline,
                self.apply(backend, registry, intent, request, deadline),
            )
            .await
            .map_err(|_| {
                "OutcomeUnknown: parameter action deadline expired; reconcile before retrying"
                    .to_string()
            })
            .and_then(|v| v);
            let outcome = match result {
                Ok(message) => EditorOutcome {
                    request_id: key.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(error) => EditorOutcome {
                    request_id: key.clone(),
                    state: if error.starts_with("OutcomeUnknown:") {
                        self.awaiting = true;
                        "Unknown"
                    } else {
                        "Failed"
                    }
                    .into(),
                    message: error,
                },
            };
            self.receipts.insert(key, (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            // Match the existing editor's completed state label; retain the
            // specific diagnostic for failed or uncertain operations.
            status: if outcome.state == "Completed" { "Completed".into() } else { outcome.message.clone() },
            result: self.result(registry)?,
            outcomes: vec![outcome],
            ..Default::default()
        })
    }
}
