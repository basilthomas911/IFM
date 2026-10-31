use crate::models::{
    cells::{Cell, Cells},
    operations_health::MarketDataOperationsHealthSnapshot,
};
use crate::{
    backend::{health_http::HealthClient, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
};
use chrono::{DateTime, Utc};
use serde_json::Value;
use std::collections::BTreeMap;

pub struct OperationsHealthController {
    client: HealthClient,
    endpoint: Option<String>,
    generation: u64,
}
impl OperationsHealthController {
    pub fn from_env() -> Result<Self, String> {
        Self::new(crate::backend::configuration::value(
            "IFM_UI_OPERATIONS_HEALTH_URL",
        )?)
    }
    pub fn new(endpoint: Option<String>) -> Result<Self, String> {
        Ok(Self {
            client: HealthClient::new()?,
            endpoint,
            generation: 0,
        })
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        intent: &Value,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1
            || intent["view"] != "operations-health"
            || intent["intent"] != "load"
        {
            return Err("Operations health is read-only".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let result = tokio::time::timeout_at(deadline, async {
            let endpoint = self
                .endpoint
                .as_deref()
                .ok_or("Operations health endpoint is not configured")?;
            let read = self
                .client
                .get_typed_observation::<MarketDataOperationsHealthSnapshot>(
                    endpoint,
                    1024 * 1024,
                    deadline,
                )
                .await?;
            let server_utc = read
                .server_utc
                .ok_or("Operations health response has no API operational clock")?;
            project(&read.value, server_utc)
        })
        .await
        .map_err(|_| "Health request deadline expired".to_string())
        .and_then(|r| r);
        self.generation += 1;
        let (result,state,message)=match result {
            Ok(result)=>(result,"Completed","Completed".to_string()),
            Err(error)=>(ContractResult{view:"operations-health".into(),context:BTreeMap::from([
                ("status".into(),"Unavailable".into()),("summary".into(),format!("Operations health: unavailable. {error}")),("observation".into(),"No current central observation. Previous green values are not retained.".into()),
            ]),..Default::default()},"Failed",error),
        };
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: message.clone(),
            result,
            outcomes: vec![EditorOutcome {
                request_id: id,
                state: state.into(),
                message,
            }],
            ..Default::default()
        })
    }
}
fn scalar(value: Cell<'_>) -> String {
    match value {
        Cell::Boolean(b) => if b { "True" } else { "False" }.into(),
        value => value.text(),
    }
}
fn cell_str(value: Cell<'_>) -> Option<&str> {
    if let Cell::Text(std::borrow::Cow::Borrowed(value)) = value {
        Some(value)
    } else {
        None
    }
}
fn utc(value: Cell<'_>) -> String {
    cell_str(value)
        .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
        .map(|d| d.to_utc().format("%Y-%m-%d %H:%M:%S UTC").to_string())
        .unwrap_or_else(|| "Unknown".into())
}
fn span(value: Cell<'_>) -> String {
    let Some(text) = cell_str(value) else {
        return "Unknown".into();
    };
    let negative = text.starts_with('-');
    let text = text.strip_prefix('-').unwrap_or(text);
    let parts: Vec<_> = text.split(':').collect();
    if parts.len() != 3 {
        return "Unknown".into();
    }
    let (days, hours) = parts[0]
        .split_once('.')
        .map(|(d, h)| (d.parse::<f64>().ok(), h.parse::<f64>().ok()))
        .unwrap_or((Some(0.), parts[0].parse::<f64>().ok()));
    let Some(seconds) = days
        .zip(hours)
        .zip(parts[1].parse::<f64>().ok())
        .zip(parts[2].parse::<f64>().ok())
        .map(|(((d, h), m), s)| d * 86400. + h * 3600. + m * 60. + s)
    else {
        return "Unknown".into();
    };
    let seconds = if negative { -seconds } else { seconds };
    if !seconds.is_finite() {
        return "Unknown".into();
    }
    if seconds >= 60. {
        format!("{:.1} min", seconds / 60.)
    } else if seconds >= 1. {
        format!("{seconds:.1} s")
    } else {
        format!("{:.1} ms", seconds * 1000.)
    }
}
pub fn project(
    value: &MarketDataOperationsHealthSnapshot,
    as_of: DateTime<Utc>,
) -> Result<ContractResult, String> {
    let valid_status = |v: Cell<'_>| {
        matches!(
            cell_str(v),
            Some("Inactive" | "Green" | "Yellow" | "Orange" | "Red")
        )
    };
    let stages = value.stages.as_ref().ok_or("Missing health stages")?;
    let datasets = value.datasets.as_ref().ok_or("Missing dataset health")?;
    let observed = cell_str(value.cell("observedOnUtc"))
        .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
        .ok_or("Missing central observation time")?
        .to_utc();
    let bounded =
        |v: Cell<'_>, max: usize| cell_str(v).is_some_and(|s| s.encode_utf16().count() <= max);
    if value.schema_version.unwrap_or(1) != 1
        || stages.len() > 512
        || datasets.len() > 16
        || !valid_status(value.cell("overallStatus"))
        || stages.iter().any(|s| {
            !valid_status(s.cell("status"))
                || !bounded(s.cell("stage"), 128)
                || !bounded(s.cell("reasonCode"), 128)
                || !bounded(s.cell("reason"), 4096)
        })
        || datasets.iter().any(|d| {
            !valid_status(d.cell("status"))
                || !bounded(d.cell("dataset"), 64)
                || !bounded(d.cell("reason"), 4096)
        })
    {
        return Err("Operations health is incomplete or outside its bounds".into());
    }
    if as_of
        .signed_duration_since(observed)
        .num_milliseconds()
        .unsigned_abs()
        > 30000
    {
        return Err("Central operations observation is stale or its clock is invalid; current health is unknown".into());
    }
    let mut rows = Vec::new();
    for stage in stages {
        let mut fields = BTreeMap::from([
            ("kind".into(), "stage".into()),
            (
                "Requirement".into(),
                if stage.required == Some(true) {
                    "Required"
                } else {
                    "Optional"
                }
                .into(),
            ),
        ]);
        for (source, target) in [
            ("stage", "Stage"),
            ("status", "Status"),
            ("pending", "Pending"),
            ("capacity", "Capacity"),
            ("highWater", "HighWater"),
            ("received", "Received"),
            ("completed", "Completed"),
            ("failed", "Failed"),
            ("coalesced", "Coalesced"),
            ("saturated", "Saturated"),
            ("reasonCode", "ReasonCode"),
            ("reason", "Reason"),
        ] {
            fields.insert(target.into(), scalar(stage.cell(source)));
        }
        for (source, target) in [
            ("marketDataAsOfUtc", "MarketDataAsOfUtc"),
            ("lastSucceededUtc", "LastSucceededUtc"),
        ] {
            fields.insert(target.into(), utc(stage.cell(source)));
        }
        for (source, target) in [
            ("oldestPendingAge", "OldestPending"),
            ("p50Latency", "P50Latency"),
            ("p95Latency", "P95Latency"),
            ("p99Latency", "P99Latency"),
        ] {
            fields.insert(target.into(), span(stage.cell(source)));
        }
        let age = cell_str(stage.cell("marketDataAsOfUtc"))
            .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
            .map(|d| {
                let delta = observed.signed_duration_since(d.to_utc());
                let ms = delta.num_milliseconds();
                if delta < chrono::Duration::zero() {
                    "Clock ahead".into()
                } else if ms >= 60000 {
                    format!("{:.1} min", ms as f64 / 60000.)
                } else if ms >= 1000 {
                    format!("{:.1} s", ms as f64 / 1000.)
                } else {
                    format!("{:.1} ms", ms as f64)
                }
            })
            .unwrap_or_else(|| "Unknown".into());
        fields.insert("SourceAge".into(), age);
        rows.push(ContractRow {
            id: format!("stage:{}", scalar(stage.cell("stage"))),
            fields,
            ..Default::default()
        });
    }
    for dataset in datasets {
        let mut fields = BTreeMap::from([("kind".into(), "dataset".into())]);
        for (source, target) in [
            ("dataset", "Dataset"),
            ("status", "Status"),
            ("sessionState", "Session"),
            ("processId", "ProcessId"),
            ("generationId", "Generation"),
            ("running", "Running"),
            ("healthy", "Healthy"),
            ("cooperativeAttempts", "CooperativeAttempts"),
            ("processReplacementCount", "ProcessReplacements"),
            ("processReplacementLatched", "ReplacementLatched"),
            ("reason", "Reason"),
            ("workerInstanceId", "WorkerInstance"),
            ("gracefulStopSucceeded", "GracefulStopSucceeded"),
            ("forcedTermination", "ForcedTermination"),
        ] {
            fields.insert(target.into(), scalar(dataset.cell(source)));
        }
        for (a, b, target) in [
            (
                "recordsConsumed",
                "recordsProduced",
                "NativeConsumedProduced",
            ),
            ("ringUsed", "ringCapacity", "RingUsedCapacity"),
            (
                "channelBatchCount",
                "channelBatchCapacity",
                "ChannelUsedCapacity",
            ),
            (
                "recordsCompleted",
                "recordsStarted",
                "AggregationCompletedStarted",
            ),
        ] {
            fields.insert(
                target.into(),
                format!("{}/{}", scalar(dataset.cell(a)), scalar(dataset.cell(b))),
            );
        }
        for (source, target) in [
            ("lastHealthyUtc", "LastHealthyUtc"),
            ("nextProbeUtc", "NextProbeUtc"),
            ("startedOnUtc", "StartedOnUtc"),
        ] {
            fields.insert(target.into(), utc(dataset.cell(source)));
        }
        fields.insert("IncidentAge".into(), span(dataset.cell("incidentAge")));
        rows.push(ContractRow {
            id: format!("dataset:{}", scalar(dataset.cell("dataset"))),
            fields,
            ..Default::default()
        });
    }
    let context = BTreeMap::from([
        ("status".into(), scalar(value.cell("overallStatus"))),
        (
            "summary".into(),
            format!(
                "Operations health: {} | Session: {} | Value date: {} | Revision: {}",
                scalar(value.cell("overallStatus")),
                scalar(value.cell("sessionState")),
                cell_str(value.cell("valueDate")).unwrap_or("none"),
                scalar(value.cell("revision"))
            ),
        ),
        (
            "observation".into(),
            format!(
                "Central observation: {} | Last probe: {} | Next probe: {} | Stale-generation publications rejected: {}",
                utc(value.cell("observedOnUtc")),
                utc(value.cell("lastProbeUtc")),
                utc(value.cell("nextProbeUtc")),
                scalar(value.cell("rejectedStaleGenerationPublications"))
            ),
        ),
    ]);
    Ok(ContractResult {
        view: "operations-health".into(),
        rows,
        context,
        ..Default::default()
    })
}
