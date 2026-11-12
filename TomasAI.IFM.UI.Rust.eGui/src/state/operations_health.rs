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

/// Operations Health Controller used by the operations health component.
pub struct OperationsHealthController {
    /// Client associated with this operations health controller.
    client: HealthClient,
    /// Optional endpoint; `None` represents an absent value.
    endpoint: Option<String>,
    /// Generation associated with this operations health controller.
    generation: u64,
}
impl OperationsHealthController {
    /// Loads configuration from the supported environment settings.
    pub fn from_env() -> Result<Self, String> {
        Self::new(crate::backend::configuration::value(
            "IFM_UI_OPERATIONS_HEALTH_URL",
        )?)
    }


    /// Constructs OperationsHealthController from the supplied inputs.
    pub fn new(endpoint: Option<String>) -> Result<Self, String> {
        Ok(Self {
            client: HealthClient::new()?,
            endpoint,
            generation: 0,
        })
    }


    /// Handles a operations health intent and returns the resulting publication or error.
    pub async fn execute(
        &mut self,
        backend: &Backend,
        intent: &Value,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1
            || intent["view"] != "operations-health"
            || !matches!(intent["intent"].as_str(), Some("load" | "restart-dataset"))
        {
            return Err("Invalid Operations Health action".into());
        }
        let id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        let restart = if intent["intent"] == "restart-dataset" {
            Some(restart_dataset(backend, &id, &intent["fields"]).await)
        } else {
            None
        };
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let result = tokio::time::timeout_at(deadline, async {
            let endpoint = self
                .endpoint
                .as_deref()
                .ok_or("Operations health endpoint is not configured")?;
            let indicator_url = reqwest::Url::parse(endpoint).and_then(|url| url.join("/health/ready"))
                .map_err(|_| "Invalid configured API health origin")?;
            let indicator_deadline = deadline.min(tokio::time::Instant::now() + std::time::Duration::from_secs(3));
            let (read, indicators) = tokio::join!(self.client.get_typed_observation::<MarketDataOperationsHealthSnapshot>(
                    endpoint,
                    1024 * 1024,
                    deadline,
                ), self.client.get_readiness_observation::<super::indicator_health::IndicatorReadiness>(
                    indicator_url.as_str(), 1024 * 1024, indicator_deadline));
            let read = read?;
            let server_utc = read
                .server_utc
                .ok_or("Operations health response has no API operational clock")?;
            let mut result = project(&read.value, server_utc)?;
            let appended = match indicators {
                Ok(read) => match read.value.entries.market_data_indicators {
                    Some(check) => super::indicator_health::append(&mut result, check.data, read.server_utc.unwrap_or(server_utc)),
                    None => Err("The API has no indicator health audit".into()),
                },
                Err(error) => Err(error),
            };
            if let Err(error) = appended { super::indicator_health::unavailable(&mut result, error); }
            Ok(result)
        })
        .await
        .map_err(|_| "Health request deadline expired".to_string())
        .and_then(|r| r);
        self.generation += 1;
        let (mut result,mut state,mut message)=match result {
            Ok(result)=>(result,"Completed","Completed".to_string()),
            Err(error)=>(ContractResult{view:"operations-health".into(),context:BTreeMap::from([
                ("status".into(),"Unavailable".into()),("summary".into(),format!("Operations health: unavailable. {error}")),("observation".into(),"No current central observation. Previous green values are not retained.".into()),
            ]),..Default::default()},"Failed",error),
        };
        if let Some(restart) = restart {
            match restart {
                Ok(status) if state == "Completed" => message = status,
                Ok(status) => message = format!("{status} Worker health could not be refreshed. {message}"),
                Err(error) => { state = "Failed"; message = error; }
            }
            result.context.insert("restartStatus".into(), message.clone());
        }
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

/// Send a fenced restart through the native Host recovery RPC.
async fn restart_dataset(backend: &Backend, id: &str, fields: &Value) -> Result<String, String> {
    use crate::backend::contracts::{date_only_wire, timestamp_wire};
    use rmpv::Value as Wire;
    let target = fields["dataset"].as_str().ok_or("Select a failed dataset worker")?;
    let (role, dataset) = target.split_once('/').ok_or("Invalid dataset worker identity")?;
    if !matches!((role, dataset), ("Options" | "Futures", "GLBX.MDP3") | ("Futures", "XCBF.PITCH")) {
        return Err("This dataset worker does not support restart".into());
    }
    let identity = |key: &str| fields[key].as_str().and_then(|value| uuid::Uuid::parse_str(value).ok())
        .map(|value| value.to_string()).ok_or("Dataset worker identity is unavailable. Refresh worker health.");
    let date = fields["valueDate"].as_str().and_then(|date| date.parse().ok())
        .ok_or("Dataset value date is unavailable. Refresh worker health.")?;
    let scope = crate::backend::configuration::value("IFM_UI_MARKETDATA_SCOPE")?
        .or_else(|| (std::env::var("DOTNET_ENVIRONMENT").as_deref().unwrap_or("Development") == "Development")
            .then(|| "development".into()))
        .ok_or("Market Data Host scope is not configured")?;
    let raw = |value: &Wire| crate::backend::codec::raw(value).map(Wire::Binary);
    let request = Wire::Array(vec![id.into(),role.into(),dataset.into(),identity("worker")?.into(),
        identity("generation")?.into(),date_only_wire(date),"Manual dataset restart".into(),false.into(),Wire::Nil]);
    let call = Wire::Array(vec![
        "RecoverMarketDataAsync(TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryRequest,System.Threading.CancellationToken)".into(),
        Wire::Array(vec![raw(&request)?, Wire::Binary(Vec::new())]),
    ]);
    // The native recovery owns the business deadline. This envelope mirrors the Host's unbounded recovery deadline.
    let until = DateTime::<Utc>::from_timestamp(253_402_300_799, 999_999_900).expect("valid .NET maximum time");
    let reply = backend.marketdata_recovery_until(&scope,
        &Wire::Array(vec!["api/RecoverMarketDataAsync".into(),raw(&call)?,id.into(),timestamp_wire(until)]),
        tokio::time::Instant::now() + std::time::Duration::from_secs(245)).await?;
    let parts = reply.as_array().filter(|parts| parts.len() == 2).ok_or("Invalid dataset restart reply")?;
    if let Some(error) = parts[1].as_str() {
        return Err(format!("{target} restart failed. {error}"));
    }
    let payload = parts[0].as_slice().ok_or("Missing dataset restart reply")?;
    let decoded = crate::backend::codec::decode(payload)?;
    let result = decoded.as_array().filter(|result| result.len() == 9 && result[0].as_str() == Some(id)
        && result[1].as_str() == Some(role) && result[2].as_str() == Some(dataset))
        .ok_or("Dataset restart reply identity did not match the request")?;
    if result[3].as_bool() != Some(true) {
        return Err(format!("{target} restart failed. {}", result[6].as_str().unwrap_or("Refresh worker health for details.")));
    }
    Ok(if result[5].as_bool() == Some(true) {
        format!("{target} restarted.")
    } else {
        format!("{target} connection restored. Worker readiness is still being verified.")
    })
}


/// Provides scalar for the operations health implementation.
fn scalar(value: Cell<'_>) -> String {
    match value {
        Cell::Boolean(b) => if b { "True" } else { "False" }.into(),
        value => value.text(),
    }
}


/// Provides cell str for the operations health implementation.
fn cell_str(value: Cell<'_>) -> Option<&str> {
    if let Cell::Text(std::borrow::Cow::Borrowed(value)) = value {
        Some(value)
    } else {
        None
    }
}


/// Reads the UTC timestamp representation expected by this component.
fn utc(value: Cell<'_>) -> String {
    cell_str(value)
        .and_then(|s| DateTime::parse_from_rfc3339(s).ok())
        .map(|d| d.to_utc().format("%Y-%m-%d %H:%M:%S UTC").to_string())
        .unwrap_or_else(|| "Unknown".into())
}


/// Provides span for the operations health implementation.
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


/// Projects backend data into the UI's display representation.
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
            d.counters.as_ref().is_some_and(|counters| counters.len() > 64
                || counters.iter().any(|(key, value)| key.len() > 128 || *value < 0))
                ||
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
        if dataset.resource_kind.as_deref() == Some("RustDataset") {
            for (source, target) in [
                ("dataset", "Dataset"), ("status", "Status"), ("sessionState", "Session"),
                ("valueDate", "ValueDate"), ("processId", "Host process ID"),
                ("workerInstanceId", "WorkerInstance"), ("generationId", "Generation"),
                ("running", "Running"), ("healthy", "Healthy"), ("reason", "Reason"),
            ] {
                fields.insert(target.into(), scalar(dataset.cell(source)));
            }
            fields.insert("Counter scope".into(), "Current resource/component lifetime; resets on replacement".into());
            fields.insert("Observed UTC".into(), utc(dataset.cell("lastObservedUtc")));
            fields.insert("LastHealthyUtc".into(), utc(dataset.cell("lastHealthyUtc")));
            if let Some(counters) = &dataset.counters {
                fields.extend(counters.iter().map(|(name, value)| (format!("Counter/{name}"), value.to_string())));
            } else {
                fields.insert("Counters".into(), "No current resource snapshot".into());
            }
            rows.push(ContractRow { id: format!("dataset:{}", scalar(dataset.cell("dataset"))), fields, ..Default::default() });
            continue;
        }
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
