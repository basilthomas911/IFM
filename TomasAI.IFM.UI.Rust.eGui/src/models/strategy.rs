//! Compact typed workflow state. Full immutable backend evidence remains encoded
//! until the operator opens its schema-driven inspector; no JSON tree is retained.
use super::{
    cells::{Cell, Cells},
    trade_contracts::FuturesItiSignalV2ReadModel,
    wire_types::FromBackend,
};
use crate::backend::{
    codec,
    contracts::{timestamp, utc_roundtrip},
    schema::Registry,
};
use rmpv::Value;
use std::sync::Arc;
pub const VIEW: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView";
pub const STAGE: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStageState";
pub const STAGES: [(&str, &str, &str); 5] = [
    ("RegimeDiscovery", "regime-discovery", "Regime Discovery"),
    ("MarketCondition", "market-condition", "Market Condition"),
    ("TradeSelection", "trade-selection", "Trade Selection"),
    ("OrderComposition", "order-composition", "Order Composition"),
    ("RiskManagement", "risk-management", "Risk Management"),
];
#[derive(Clone, Debug)]
pub struct StageHeader {
    pub processing: String,
    pub continuation: String,
    pub completed: Option<chrono::DateTime<chrono::Utc>>,
    pub failed: Option<chrono::DateTime<chrono::Utc>>,
    pub failure: Option<StageFailure>,
}
#[derive(Clone, Debug)]
pub struct StageFailure {
    pub error_type: String,
    pub error_code: String,
    pub error_message: String,
}
#[derive(Clone, Debug)]
pub struct WorkflowHeader {
    pub id: String,
    pub definition_id: String,
    pub entity_contract: String,
    pub entity_date: String,
    pub frame: String,
    pub trigger_id: String,
    pub correlation_id: String,
    pub causation_id: String,
    pub definition_version: String,
    pub revision: u64,
    pub status: String,
    pub outcome: String,
    pub current_stage: String,
    pub stop_reason: String,
    pub started: chrono::DateTime<chrono::Utc>,
    pub updated: chrono::DateTime<chrono::Utc>,
    pub expires: chrono::DateTime<chrono::Utc>,
    pub terminal: Option<chrono::DateTime<chrono::Utc>>,
    pub trigger_created: chrono::DateTime<chrono::Utc>,
    pub signal: FuturesItiSignalV2ReadModel,
    pub stages: Vec<StageHeader>,
}
#[derive(Clone)]
pub struct WorkflowView {
    pub header: WorkflowHeader,
    pub evidence: Arc<[u8]>,
}
fn value<'a>(r: &Registry, kind: &str, wire: &'a Value, name: &str) -> Result<&'a Value, String> {
    if wire.is_nil() {
        Ok(&Value::Nil)
    } else if wire.is_array() {
        Ok(r.wire_field(kind, name, wire).unwrap_or(&Value::Nil))
    } else {
        r.wire_field(kind, name, wire)
    }
}
fn text(r: &Registry, kind: &str, wire: &Value, name: &str) -> Result<String, String> {
    let v = value(r, kind, wire, name)?;
    if v.is_nil() {
        return Ok(String::new());
    }
    if let Some(text) = v.as_str() {
        return Ok(text.into());
    }
    let field = r.field_kind(kind, name)?;
    if let Some(number) = v.as_i64() {
        return r
            .enum_name(field, number)
            .or_else(|_| Ok(number.to_string()));
    }
    Err(format!("Invalid workflow text {kind}.{name}"))
}
fn instant(
    r: &Registry,
    kind: &str,
    wire: &Value,
    name: &str,
) -> Result<chrono::DateTime<chrono::Utc>, String> {
    timestamp(value(r, kind, wire, name)?)
}
fn optional_instant(
    r: &Registry,
    kind: &str,
    wire: &Value,
    name: &str,
) -> Result<Option<chrono::DateTime<chrono::Utc>>, String> {
    let v = value(r, kind, wire, name)?;
    if v.is_nil() {
        Ok(None)
    } else {
        timestamp(v).map(Some)
    }
}
impl WorkflowView {
    pub fn from_backend(r: &Registry, wire: &Value) -> Result<Self, String> {
        Ok(Self {
            header: WorkflowHeader::from_backend(r, wire)?,
            evidence: codec::raw(wire)?.into(),
        })
    }
}
impl WorkflowHeader {
    pub fn from_backend(r: &Registry, wire: &Value) -> Result<Self, String> {
        let entity = value(r, VIEW, wire, "EntityId")?;
        let entity_kind = r.field_kind(VIEW, "EntityId")?;
        let iti = value(r, entity_kind, entity, "ItiSignalEntityId")?;
        let iti_kind = r.field_kind(entity_kind, "ItiSignalEntityId")?;
        let id = value(r, VIEW, wire, "WorkflowId")?;
        let id_kind = r.field_kind(VIEW, "WorkflowId")?;
        let id = text(r, id_kind, id, "Value")?;
        let id = uuid::Uuid::parse_str(&id)
            .ok()
            .filter(|id| !id.is_nil())
            .ok_or("Missing workflow identity")?
            .simple()
            .to_string();
        let trigger = value(r, VIEW, wire, "TriggerEvent")?;
        let trigger_kind = r.field_kind(VIEW, "TriggerEvent")?;
        let revision = value(r, VIEW, wire, "WorkflowRevision")?
            .as_i64()
            .filter(|value| *value > 0)
            .ok_or("Invalid workflow revision")? as u64;
        let mut stages = Vec::with_capacity(5);
        for (name, _, _) in STAGES {
            let stage = value(r, VIEW, wire, name)?;
            let failure = value(r, STAGE, stage, "Failure")?;
            let failure = if failure.is_nil() {
                None
            } else {
                let kind = r.field_kind(STAGE, "Failure")?;
                Some(StageFailure {
                    error_type: text(r, kind, failure, "ErrorType")?,
                    error_code: text(r, kind, failure, "ErrorCode")?,
                    error_message: text(r, kind, failure, "ErrorMessage")?,
                })
            };
            stages.push(StageHeader {
                processing: text(r, STAGE, stage, "ProcessingStatus")?,
                continuation: text(r, STAGE, stage, "ContinuationDecision")?,
                completed: optional_instant(r, STAGE, stage, "CompletedAtUtc")?,
                failed: optional_instant(r, STAGE, stage, "FailedAtUtc")?,
                failure,
            });
        }
        Ok(Self {
            id,
            definition_id: text(r, entity_kind, entity, "WorkflowDefinitionId")?,
            entity_contract: text(r, iti_kind, iti, "ContractId")?,
            entity_date: crate::backend::contracts::date_only(value(
                r,
                iti_kind,
                iti,
                "ValueDate",
            )?)?
            .to_string(),
            frame: text(r, iti_kind, iti, "TimePeriod")?,
            trigger_id: text(r, VIEW, wire, "TriggerEventId")?,
            correlation_id: text(r, VIEW, wire, "CorrelationId")?,
            causation_id: text(r, VIEW, wire, "CausationId")?,
            definition_version: text(r, VIEW, wire, "WorkflowDefinitionVersion")?,
            revision,
            status: text(r, VIEW, wire, "Status")?,
            outcome: text(r, VIEW, wire, "Outcome")?,
            current_stage: text(r, VIEW, wire, "CurrentStage")?,
            stop_reason: text(r, VIEW, wire, "StopReasonCode")?,
            started: instant(r, VIEW, wire, "StartedAtUtc")?,
            updated: instant(r, VIEW, wire, "UpdatedAtUtc")?,
            expires: instant(r, VIEW, wire, "ExpiresAtUtc")?,
            terminal: optional_instant(r, VIEW, wire, "TerminalAtUtc")?,
            trigger_created: instant(r, trigger_kind, trigger, "CreatedOn")?,
            signal: FuturesItiSignalV2ReadModel::from_backend(value(
                r,
                trigger_kind,
                trigger,
                "FuturesItiSignal",
            )?)?,
            stages,
        })
    }
    pub fn occurred(&self) -> chrono::DateTime<chrono::Utc> {
        if self.trigger_created.timestamp() <= -62_135_596_800 {
            self.started
        } else {
            self.trigger_created
        }
    }
    pub fn header(&self) -> String {
        let mut out = "=== WORKFLOW ===\r\n".to_owned();
        let id = uuid::Uuid::parse_str(&self.id)
            .map(|id| id.to_string())
            .unwrap_or_default();
        let entity = format!(
            "{}.{}.{}.{}",
            self.definition_id,
            self.entity_contract,
            self.entity_date.replace('-', ""),
            self.frame
        );
        for (label, value) in [
            ("Workflow ID", id),
            ("Workflow entity", entity),
            ("Trigger event ID", self.trigger_id.clone()),
            ("Correlation ID", self.correlation_id.clone()),
            ("Causation ID", self.causation_id.clone()),
            ("Definition", self.definition_id.clone()),
            ("Definition version", self.definition_version.clone()),
            ("Revision", self.revision.to_string()),
            ("Machine status", self.status.clone()),
            ("Business outcome", self.outcome.clone()),
            ("Current stage", self.current_stage.clone()),
            ("Stop reason", self.stop_reason.clone()),
            ("Started UTC", utc_roundtrip(self.started)),
            ("Updated UTC", utc_roundtrip(self.updated)),
            ("Expires UTC", utc_roundtrip(self.expires)),
            (
                "Terminal UTC",
                self.terminal.map(utc_roundtrip).unwrap_or_default(),
            ),
        ] {
            out.push_str(&format!(
                "{label}: {}\r\n",
                if value.trim().is_empty() {
                    "None"
                } else {
                    &value
                }
            ));
        }
        out.push_str("\r\n");
        out
    }
}
pub fn signal_time(
    signal: &FuturesItiSignalV2ReadModel,
) -> Result<chrono::DateTime<chrono::Utc>, String> {
    chrono::DateTime::parse_from_rfc3339(
        signal
            .intrinsic_time
            .as_ref()
            .map(|v| v.as_str())
            .ok_or("Missing strategy timestamp")?,
    )
    .map(|v| v.to_utc())
    .map_err(|_| "Invalid strategy timestamp".into())
}
pub fn signal_identity(signal: &FuturesItiSignalV2ReadModel) -> Result<String, String> {
    let at = signal_time(signal)?;
    let ticks = at.timestamp() as i128 * 10_000_000
        + at.timestamp_subsec_nanos() as i128 / 100
        + 621_355_968_000_000_000i128;
    let mut fields = vec![
        signal.cell("ContractId").text(),
        signal.cell("ValueDate").text(),
        signal.cell("TimePeriod").text(),
        ticks.to_string(),
        signal.cell("IntrinsicTimeMode").text(),
        signal.cell("IntrinsicTimeTrend").text(),
    ];
    for name in [
        "IntrinsicPrice",
        "IntrinsicTimeGroupId",
        "IntrinsicTimeLength",
        "TrendPrice",
        "TrendExtreme",
        "TrendReversal",
        "TrendDelta",
        "TargetDelta",
        "Threshold",
        "UpTrendTrigger",
        "DownTrendTrigger",
        "BandLevel",
        "ReversalLevel",
    ] {
        let value = signal.cell(name);
        if value.number().is_none() {
            return Err("Invalid workflow display number".into());
        }
        fields.push(match value {
            Cell::Number(value) => value.to_string(),
            value => value.text(),
        });
    }
    fields.push(signal.cell("TradeState").text());
    Ok(fields.join("|"))
}

impl WorkflowHeader {
    pub fn row(&self, action: u32) -> Result<crate::strategy_workspace::WorkflowRow, String> {
        use crate::strategy_workspace::{WorkflowActor, WorkflowRow};
        let local = self.occurred().with_timezone(&chrono_tz::America::New_York);
        let occurred_on = if self.frame == "Daily" {
            local.format("%I:%M:%S%.3f %p")
        } else {
            local.format("%d-%b-%Y %I:%M:%S%.3f %p")
        }
        .to_string();
        let resolved = if self.outcome != "None" {
            self.outcome.as_str()
        } else {
            match self.status.as_str() {
                "Failed" => "PipelineFailed",
                "TimedOut" => "TimedOut",
                "Cancelled" => "Cancelled",
                _ => "None",
            }
        };
        let end_state = if self.status == "Started" {
            "In Progress"
        } else {
            match resolved {
                "Completed" => "Approved",
                "NoTrade" => "No Trade",
                "PipelineFailed" => "Pipeline Failed",
                "InvalidResult" => "Invalid Result",
                "TimedOut" => "Timed Out",
                "Cancelled" => "Cancelled",
                "ConsistencyFault" => "Consistency Fault",
                _ => "Unknown",
            }
        }
        .into();
        let mut actors = Vec::new();
        for ((stage, (_, _, title)), short) in self
            .stages
            .iter()
            .zip(STAGES)
            .zip(["RD", "MC", "TS", "OC", "RM"])
        {
            if stage.processing == "NotStarted" {
                continue;
            }
            let state = match (stage.processing.as_str(), stage.continuation.as_str()) {
                ("Processing", _) => "Processing",
                ("Completed", "Proceed") => "Continued",
                ("Completed", "Stop") => "Stopped",
                ("Completed", _) => "Processing",
                _ => "Stopped",
            }
            .into();
            let mut value = format!(
                "{title}: {}; continuation {}",
                stage.processing, stage.continuation
            );
            for (label, at) in [("completed", stage.completed), ("failed", stage.failed)] {
                if let Some(at) = at {
                    value.push_str(&format!("; {label} {}", utc_roundtrip(at)));
                }
            }
            if let Some(failure) = &stage.failure {
                value.push_str(&format!(
                    "; {} {}: {}",
                    failure.error_type, failure.error_code, failure.error_message
                ));
            }
            actors.push(WorkflowActor {
                name: short.into(),
                value,
                state,
            });
        }
        Ok(WorkflowRow {
            id: self.id.clone(),
            action,
            trigger_identity: signal_identity(&self.signal)?,
            occurred_on,
            signal: self.signal.cell("IntrinsicTimeMode").text(),
            trend: self.signal.cell("IntrinsicTimeTrend").text(),
            futures_price: format!(
                "{:.2}",
                self.signal
                    .cell("IntrinsicPrice")
                    .number()
                    .ok_or("Invalid workflow display number")?
            ),
            outcome: self.outcome.clone(),
            end_state,
            actors,
        })
    }
}
