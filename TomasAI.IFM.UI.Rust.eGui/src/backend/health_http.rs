//! Existing HTTP/JSON health transport, with one caller-owned deadline and
//! streamed response bounds. Operational time is supplied by the API response.
use futures_util::StreamExt;
use serde_json::Value;

#[derive(Clone)]
pub struct HealthClient {
    client: reqwest::Client,
}
/// API operational time is independent of market-session observation time.
pub struct HealthObservation { pub value:Value, pub server_utc:Option<chrono::DateTime<chrono::Utc>> }
impl HealthClient {
    /// One bounded acknowledgement request per rendered console batch. No backend I/O runs in paint.
    pub async fn post_json(&self, endpoint: &str, body: &Value, deadline: tokio::time::Instant) -> Result<(), String> {
        tokio::time::timeout_at(deadline, async {
            let response = self.client.post(endpoint).json(body).send().await
                .map_err(|_| "Console acknowledgement endpoint could not be reached")?;
            if !response.status().is_success() {
                return Err(format!("Console acknowledgement failed (HTTP {})", response.status().as_u16()));
            }
            Ok(())
        }).await.map_err(|_| "Console acknowledgement deadline expired".to_string())?
    }
    pub fn new() -> Result<Self, String> {
        if rustls::crypto::CryptoProvider::get_default().is_none() {
            let _ = rustls::crypto::ring::default_provider().install_default();
        }
        Ok(Self {
            client: reqwest::Client::builder()
                .build()
                .map_err(|_| "Could not initialize HTTP health client")?,
        })
    }
    pub async fn get(&self,endpoint:&str,maximum:usize,deadline:tokio::time::Instant)->Result<Value,String>{
        self.get_observation(endpoint,maximum,deadline).await.map(|read|read.value)
    }
    pub async fn get_observation(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<HealthObservation, String> {
        let url = reqwest::Url::parse(endpoint).map_err(|_| "Invalid health endpoint")?;
        if !matches!(url.scheme(), "http" | "https") {
            return Err("An absolute HTTP(S) health endpoint is required".into());
        }
        tokio::time::timeout_at(deadline, async {
            let response = self
                .client
                .get(url)
                .send()
                .await
                .map_err(|_| "Health endpoint could not be reached")?;
            if response.status() != reqwest::StatusCode::OK {
                return Err(format!(
                    "Health unavailable (HTTP {})",
                    response.status().as_u16()
                ));
            }
            // Prefer the API's TimeProvider clock. HTTP Date supports existing
            // deployed servers; never substitute the workstation or session clock.
            let server_utc=if let Some(value)=response.headers().get("x-ifm-server-utc") {
                Some(chrono::DateTime::parse_from_rfc3339(value.to_str().map_err(|_|"Invalid API clock header")?).map_err(|_|"Invalid API clock header")?.to_utc())
            }else if let Some(value)=response.headers().get(reqwest::header::DATE) {
                Some(chrono::DateTime::parse_from_rfc2822(value.to_str().map_err(|_|"Invalid API Date header")?).map_err(|_|"Invalid API Date header")?.to_utc())
            }else{None};
            if response
                .content_length()
                .is_some_and(|n| n > maximum as u64)
            {
                return Err("Health response exceeds its bounded size".into());
            }
            let mut body = Vec::new();
            let mut chunks = response.bytes_stream();
            while let Some(chunk) = chunks.next().await {
                let chunk = chunk.map_err(|_| "Health response stream failed")?;
                if chunk.len() > maximum - body.len() {
                    return Err("Health response exceeds its bounded size".into());
                }
                body.extend_from_slice(&chunk);
            }
            let value: Value =
                serde_json::from_slice(&body).map_err(|_| "Health response is invalid JSON")?;
            fn bounded(value: &Value, depth: usize) -> bool {
                depth <= 20
                    && match value {
                        Value::Array(values) => values.iter().all(|v| bounded(v, depth + 1)),
                        Value::Object(values) => values.values().all(|v| bounded(v, depth + 1)),
                        _ => true,
                    }
            }
            if !bounded(&value, 0) {
                return Err("Health response exceeds its nesting bound".into());
            }
            Ok(HealthObservation{value,server_utc})
        })
        .await
        .map_err(|_| "Health request deadline expired".to_string())?
    }
}
