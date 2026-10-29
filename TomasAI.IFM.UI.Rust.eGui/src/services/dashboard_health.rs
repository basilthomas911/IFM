//! Read-only dashboard health. Pipeline freshness uses the API clock; the health
//! endpoint supplements feed runtime state and never controls feed recovery.
use crate::{
    backend::{health_http::HealthClient, transport::Backend},
    services::feed_control::FeedObservation,
};
use chrono::{DateTime, Utc};
use serde_json::Value;

#[derive(Clone, Debug)]
pub struct PipelineHealth {
    pub running: Option<bool>,
    pub health: String,
    pub reason: String,
    pub options_health: String,
    pub options_reason: String,
}
impl PipelineHealth {
    pub fn unknown(running: Option<bool>) -> Self {
        Self {
            running,
            options_health: "Yellow".into(),
            options_reason: "Options readiness has not been verified.".into(),
            health: if running == Some(false) {
                "Inactive"
            } else {
                "Unknown"
            }
            .into(),
            reason: "Current pipeline health has not been verified.".into(),
        }
    }
    pub fn unavailable(error: &str) -> Self {
        Self {
            running: None,
            options_health: "Yellow".into(),
            options_reason: "Options status unavailable.".into(),
            health: "Unknown".into(),
            reason: format!("Pipeline health unavailable: {error}"),
        }
    }
}
/// Match the existing dashboard's presentation classification. A stale response
/// cannot retain an earlier green status, even when its HTTP request succeeds.
pub fn project(
    snapshot: &Value,
    running: Option<bool>,
    off_hours: bool,
    now: DateTime<Utc>,
) -> Result<PipelineHealth, String> {
    let mut result = PipelineHealth::unknown(running);
    let observed = DateTime::parse_from_rfc3339(
        snapshot["observedUtc"]
            .as_str()
            .ok_or("Missing pipeline observation time")?,
    )
    .map_err(|_| "Invalid pipeline observation time")?
    .with_timezone(&Utc);
    if observed < now - chrono::Duration::minutes(2) {
        return Ok(result);
    }
    let checks = snapshot["checks"]
        .as_array()
        .ok_or("Missing pipeline health checks")?;
    if checks.len() > 10_000 {
        return Err("Pipeline health check count exceeds the display bound".into());
    }
    result.health = match snapshot["status"].as_str() {
        Some("Healthy") if running == Some(true) => {
            if off_hours {
                "OffHoursActive"
            } else {
                "Healthy"
            }
        }
        Some("Unhealthy") => "Critical",
        _ => "Intermittent",
    }
    .into();
    result.reason = checks
        .iter()
        .find(|check| {
            check["required"] == true
                && matches!(
                    check["status"].as_str(),
                    Some("Degraded" | "Unhealthy" | "Unknown")
                )
        })
        .and_then(|check| check["reason"].as_str())
        .unwrap_or("All required stages verified.")
        .into();
    Ok(result)
}
pub struct DashboardHealth {
    client: HealthClient,
    endpoint: Option<String>,
}
impl DashboardHealth {
    pub fn new(endpoint: Option<String>) -> Result<Self, String> {
        Ok(Self {
            client: HealthClient::new()?,
            endpoint,
        })
    }
    pub fn from_env() -> Result<Self, String> {
        let endpoint = crate::backend::configuration::value("IFM_UI_LIVE_PIPELINE_HEALTH_URL")?
            .or_else(|| {
                crate::backend::configuration::value("IFM_UI_OPERATIONS_HEALTH_URL").ok().flatten()
                    .and_then(|value| {
                        reqwest::Url::parse(&value)
                            .ok()?
                            .join("live-health")
                            .ok()
                            .map(Into::into)
                    })
            });
        Self::new(endpoint)
    }
    pub async fn load(&self, backend: &Backend) -> Result<PipelineHealth, String> {
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let observation = tokio::time::timeout_at(deadline, FeedObservation::load(backend))
            .await
            .map_err(|_| "Dashboard health refresh deadline expired".to_string())??;
        let running = Some(observation.status.running);
        let Some(endpoint) = self.endpoint.as_deref() else {
            return Ok(PipelineHealth::unknown(running));
        };
        let result = match self.client.get(endpoint, 1024 * 1024, deadline).await {
            Ok(snapshot) => project(
                &snapshot,
                running,
                observation.session.market_state == 1,
                observation.session.as_of_utc,
            ),
            Err(error) => Err(error),
        };
        // An HTTP diagnostic failure must not erase an independently confirmed
        // feed-running observation or keep a previous green classification.
        let mut health = result.unwrap_or_else(|error| {
            let mut value = PipelineHealth::unknown(running);
            value.reason = format!("Pipeline health unavailable: {error}");
            value
        });
        if let Ok(url) = reqwest::Url::parse(endpoint).and_then(|u| u.join("/api/market-data/options-health")) {
            match self.client.get(url.as_str(), 256 * 1024, deadline).await {
                Ok(value) => { let (state, reason) = options_health(&value); health.options_health=state.into(); health.options_reason=reason.into(); },
                Err(error) => health.options_reason=format!("Options status unavailable: {error}"),
            }
        }
        Ok(health)
    }
}
/// Only current-generation scope/resource proofs permit green. Quote silence
/// does not imply a broken connection and never initiates recovery from this UI.
pub fn options_health(value: &Value) -> (&'static str, &'static str) {
    let Some(datasets)=value["datasets"].as_array() else {return ("Yellow", "Options status unavailable");};
    if datasets.is_empty() {return ("Inactive", "Options inactive");}
    if datasets.iter().any(|d| matches!(d["lifecycle"].as_str(),Some("StoppedError"|"Failed"|"Down"))) {return ("Red", "Options dataset is down");}
    if datasets.iter().all(|d| !d["running"].as_bool().unwrap_or(false) && matches!(d["lifecycle"].as_str(),Some("Stopped"|"Inactive"|"Idle"))) {return ("Inactive", "Options inactive");}
    if datasets.iter().any(|d| matches!(d["lifecycle"].as_str(),Some("Starting"|"Recovering"|"Resetting")) || d["running"]!=true || d["healthy"]!=true) {return ("Yellow", "Options starting or recovering");}
    if datasets.iter().any(|d| matches!(d["lifecycle"].as_str(),Some("Idle"))) {return ("Inactive", "Options inactive");}
    let proof=&value["workload"];
    let scopes=proof["scopes"].as_array(); let ready=proof["readyScopes"].as_array();
    if datasets.len()==1 && proof["generationId"]==datasets[0]["generationId"] && proof["generationId"].as_str().is_some_and(|s|s!="00000000-0000-0000-0000-000000000000") && proof["replacementPending"]==false && proof["nativeResourceProofsComplete"]==true && scopes.zip(ready).is_some_and(|(s,r)| !s.is_empty() && s.iter().all(|x|r.contains(x))) {
        ("Green", "Current Options generation and monitoring scopes verified")
    } else { ("Yellow", "Options connected; waiting for verified live data") }
}
