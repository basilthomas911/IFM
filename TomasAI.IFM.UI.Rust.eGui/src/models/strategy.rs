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

/// Stage Header used by the strategy component.
#[derive(Clone, Debug)]
pub struct StageHeader {
    /// Processing associated with this stage header.
    pub processing: String,
    /// Continuation associated with this stage header.
    pub continuation: String,
    /// Optional completed; `None` represents an absent value.
    pub completed: Option<chrono::DateTime<chrono::Utc>>,
    /// Optional failed; `None` represents an absent value.
    pub failed: Option<chrono::DateTime<chrono::Utc>>,
    /// Optional failure; `None` represents an absent value.
    pub failure: Option<StageFailure>,
    /// Recorded reasons for a terminal continuation decision.
    pub decision_reasons: Vec<String>,
}

/// Stage Failure used by the strategy component.
#[derive(Clone, Debug)]
pub struct StageFailure {
    /// Error type associated with this stage failure.
    pub error_type: String,
    /// Error code associated with this stage failure.
    pub error_code: String,
    /// Error message associated with this stage failure.
    pub error_message: String,
}

/// Workflow Header used by the strategy component.
#[derive(Clone, Debug)]
pub struct WorkflowHeader {
    /// Id associated with this workflow header.
    pub id: String,
    /// Definition id associated with this workflow header.
    pub definition_id: String,
    /// Entity contract associated with this workflow header.
    pub entity_contract: String,
    /// Entity date associated with this workflow header.
    pub entity_date: String,
    /// Frame associated with this workflow header.
    pub frame: String,
    /// Trigger id associated with this workflow header.
    pub trigger_id: String,
    /// Correlation id associated with this workflow header.
    pub correlation_id: String,
    /// Causation id associated with this workflow header.
    pub causation_id: String,
    /// Definition version associated with this workflow header.
    pub definition_version: String,
    /// Revision associated with this workflow header.
    pub revision: u64,
    /// Current status text or state exposed by this component.
    pub status: String,
    /// Outcome associated with this workflow header.
    pub outcome: String,
    /// Current stage associated with this workflow header.
    pub current_stage: String,
    /// Stop reason associated with this workflow header.
    pub stop_reason: String,
    /// Started associated with this workflow header.
    pub started: chrono::DateTime<chrono::Utc>,
    /// Updated associated with this workflow header.
    pub updated: chrono::DateTime<chrono::Utc>,
    /// Expires associated with this workflow header.
    pub expires: chrono::DateTime<chrono::Utc>,
    /// Optional terminal; `None` represents an absent value.
    pub terminal: Option<chrono::DateTime<chrono::Utc>>,
    /// Trigger created associated with this workflow header.
    pub trigger_created: chrono::DateTime<chrono::Utc>,
    /// Signal associated with this workflow header.
    pub signal: FuturesItiSignalV2ReadModel,
    /// Ordered stages retained by WorkflowHeader.
    pub stages: Vec<StageHeader>,
}

/// Workflow View used by the strategy component.
#[derive(Clone)]
pub struct WorkflowView {
    /// Header associated with this workflow view.
    pub header: WorkflowHeader,
    /// Evidence associated with this workflow view.
    pub evidence: Arc<[u8]>,
}

/// Provides value for the strategy implementation.
fn value<'a>(r: &Registry, kind: &str, wire: &'a Value, name: &str) -> Result<&'a Value, String> {
    if wire.is_nil() {
        Ok(&Value::Nil)
    } else if wire.is_array() {
        Ok(r.wire_field(kind, name, wire).unwrap_or(&Value::Nil))
    } else {
        r.wire_field(kind, name, wire)
    }
}

/// Returns the display text for the requested value or field.
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

/// Reads a timestamp as an instant while retaining the supported precision.
fn instant(
    r: &Registry,
    kind: &str,
    wire: &Value,
    name: &str,
) -> Result<chrono::DateTime<chrono::Utc>, String> {
    timestamp(value(r, kind, wire, name)?)
}

/// Reads an optional timestamp without inventing a value for missing data.
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
    /// Reads the pinned backend representation and validates its scalar values.
    pub fn from_backend(r: &Registry, wire: &Value) -> Result<Self, String> {
        Ok(Self {
            header: WorkflowHeader::from_backend(r, wire)?,
            evidence: codec::raw(wire)?.into(),
        })
    }
}
impl WorkflowHeader {
    /// Reads the pinned backend representation and validates its scalar values.
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
        let outcome = text(r, VIEW, wire, "Outcome")?;
        let stop_reason = text(r, VIEW, wire, "StopReasonCode")?;
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
            let mut decision_reasons: Vec<String> = value(r, STAGE, stage, "ContinuationReasonCodes")?
                .as_array()
                .into_iter()
                .flatten()
                .filter_map(Value::as_str)
                .map(str::to_owned)
                .collect();
            let continuation = text(r, STAGE, stage, "ContinuationDecision")?;
            if name == "MarketCondition" && outcome == "NoTrade" && continuation == "Stop" {
                if decision_reasons.is_empty() && !stop_reason.is_empty() {
                    decision_reasons.push(stop_reason.clone());
                }
                if decision_reasons.iter().any(|code| code == "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE") {
                    decision_reasons.extend(regime_volatility_reasons(r, wire).unwrap_or_default());
                }
            }
            stages.push(StageHeader {
                processing: text(r, STAGE, stage, "ProcessingStatus")?,
                continuation,
                completed: optional_instant(r, STAGE, stage, "CompletedAtUtc")?,
                failed: optional_instant(r, STAGE, stage, "FailedAtUtc")?,
                failure,
                decision_reasons,
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
            outcome,
            current_stage: text(r, VIEW, wire, "CurrentStage")?,
            stop_reason,
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

    /// Reads the occurrence time used to order the supplied event.
    pub fn occurred(&self) -> chrono::DateTime<chrono::Utc> {
        if self.trigger_created.timestamp() <= -62_135_596_800 {
            self.started
        } else {
            self.trigger_created
        }
    }

    /// Provides header for the workflow header implementation.
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

/// Extracts the workflow signal's published event time.
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

/// Extracts the workflow signal identity used for correlation and deduplication.
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
    /// Provides row for the workflow header implementation.
    pub fn row(&self, action: u32) -> Result<crate::strategy_workspace::WorkflowRow, String> {
        workflow_grid_row(
            &self.id,
            action,
            self.occurred(),
            &self.frame,
            &self.status,
            &self.outcome,
            &self.signal,
            &self.stages,
        )
    }
}

/// Formats the same grid columns for historical summaries and live updates.
pub fn workflow_grid_row(
    id: &str,
    action: u32,
    occurred: chrono::DateTime<chrono::Utc>,
    frame: &str,
    status: &str,
    outcome: &str,
    signal: &FuturesItiSignalV2ReadModel,
    stages: &[StageHeader],
) -> Result<crate::strategy_workspace::WorkflowRow, String> {
    use crate::strategy_workspace::{WorkflowActor, WorkflowRow};
    let local = occurred.with_timezone(&chrono_tz::America::New_York);
    let occurred_on = if frame == "Daily" {
        local.format("%I:%M:%S%.3f %p")
    } else {
        local.format("%d-%b-%Y %I:%M:%S%.3f %p")
    }
    .to_string();
    let resolved = if outcome != "None" {
        outcome
    } else {
        match status {
            "Failed" => "PipelineFailed",
            "TimedOut" => "TimedOut",
            "Cancelled" => "Cancelled",
            _ => "None",
        }
    };
    let end_state = if status == "Started" {
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
    for ((stage, (_, _, title)), short) in stages
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
            ("Completed", "Stop") if resolved == "NoTrade" => "No Trade",
            ("Completed", "Stop") => "Stopped",
            ("Completed", _) => "Processing",
            _ => "Stopped",
        };
        let mut value = if state == "No Trade" {
            let heading = format!("{title}: No Trade");
            match no_trade_reason(&stage.decision_reasons) {
                Some(reason) => format!("{heading}\nReason: {reason}"),
                None => heading,
            }
        } else {
            format!("{title}: {}; continuation {}", stage.processing, stage.continuation)
        };
        for (label, at) in [("completed", stage.completed), ("failed", stage.failed)] {
            if let Some(at) = at {
                if state == "No Trade" && label == "completed" {
                    value.push_str(&format!("\nCompleted: {}", utc_roundtrip(at)));
                } else {
                    value.push_str(&format!("; {label} {}", utc_roundtrip(at)));
                }
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
            state: state.into(),
        });
    }
    Ok(WorkflowRow {
        id: id.into(),
        action,
        trigger_identity: signal_identity(signal)?,
        occurred_on,
        signal: signal.cell("IntrinsicTimeMode").text(),
        trend: signal.cell("IntrinsicTimeTrend").text(),
        futures_price: format!(
            "{:.2}",
            signal
                .cell("IntrinsicPrice")
                .number()
                .ok_or("Invalid workflow display number")?
        ),
        outcome: outcome.into(),
        end_state,
        actors,
    })
}

fn regime_volatility_reasons(r: &Registry, wire: &Value) -> Result<Vec<String>, String> {
    let stage = value(r, VIEW, wire, "RegimeDiscovery")?;
    let envelope = value(r, STAGE, stage, "Result")?;
    let envelope_kind = r.field_kind(STAGE, "Result")?;
    let regime = value(r, envelope_kind, envelope, "RegimeResult")?;
    let regime_kind = r.field_kind(envelope_kind, "RegimeResult")?;
    let volatility = value(r, regime_kind, regime, "Volatility")?;
    let volatility_kind = r.field_kind(regime_kind, "Volatility")?;
    let reasons = value(r, volatility_kind, volatility, "Reasons")?;
    let reason_kind = r.field_kind(volatility_kind, "Reasons")?.trim_end_matches("[]");
    reasons
        .as_array()
        .into_iter()
        .flatten()
        .map(|reason| text(r, reason_kind, reason, "Code"))
        .collect()
}

fn no_trade_reason(codes: &[String]) -> Option<String> {
    let first = codes.first()?;
    if first == "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE" {
        let mut reason = String::from("Regime Discovery blocked new trades.");
        let has = |code| codes.iter().any(|item| item == code);
        if has("RD.VOL.BACKWARDATION") {
            reason.push_str(" VX futures were in backwardation.");
        }
        if has("RD.VOL.EXTREME") {
            reason.push_str(" Volatility was extreme.");
        } else if has("RD.VOL.HIGH") {
            reason.push_str(" Volatility was high.");
        }
        return Some(reason);
    }
    Some(codes.join(", "))
}
