//! Existing HTTP/JSON health transport, with one caller-owned deadline and
//! streamed response bounds. Business time is supplied by the API session.
use futures_util::StreamExt;
use serde_json::Value;

#[derive(Clone)]
pub struct HealthClient {
    client: reqwest::Client,
}
impl HealthClient {
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
    pub async fn get(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<Value, String> {
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
            Ok(value)
        })
        .await
        .map_err(|_| "Health request deadline expired".to_string())?
    }
}
