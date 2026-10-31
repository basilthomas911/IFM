//! Existing HTTP/JSON health transport, with one caller-owned deadline and
//! streamed response bounds. Operational time is supplied by the API response.
use futures_util::StreamExt;
use serde_json::Value;

#[derive(Clone)]
pub struct HealthClient {
    client: reqwest::Client,
}
/// API operational time is independent of market-session observation time.
pub struct HealthObservation<T = Value> {
    pub value: T,
    pub server_utc: Option<chrono::DateTime<chrono::Utc>>,
}
impl HealthClient {
    /// One bounded acknowledgement request per rendered console batch. No backend I/O runs in paint.
    pub async fn post_json(
        &self,
        endpoint: &str,
        body: &Value,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.post_json_inner(endpoint, body, deadline)
            .await
            .map_err(|error| audit_failure("POST", endpoint, error))
    }
    async fn post_json_inner(
        &self,
        endpoint: &str,
        body: &Value,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        tokio::time::timeout_at(deadline, async {
            let response = self
                .client
                .post(endpoint)
                .json(body)
                .send()
                .await
                .map_err(|error| {
                    format!(
                        "Console acknowledgement endpoint could not be reached: {:?}",
                        error.without_url()
                    )
                })?;
            if !response.status().is_success() {
                return Err(format!(
                    "Console acknowledgement failed (HTTP {})",
                    response.status().as_u16()
                ));
            }
            Ok(())
        })
        .await
        .map_err(|_| "Console acknowledgement deadline expired".to_string())?
    }
    pub fn new() -> Result<Self, String> {
        if rustls::crypto::CryptoProvider::get_default().is_none() {
            let _ = rustls::crypto::ring::default_provider().install_default();
        }
        Ok(Self {
            client: reqwest::Client::builder()
                .build()
                .map_err(|error| format!("Could not initialize HTTP health client: {error:?}"))?,
        })
    }
    pub async fn get(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<Value, String> {
        self.get_observation(endpoint, maximum, deadline)
            .await
            .map(|read| read.value)
    }
    pub async fn get_observation(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<HealthObservation, String> {
        self.get_observation_inner(endpoint, maximum, deadline)
            .await
            .map_err(|error| audit_failure("GET", endpoint, error))
    }
    pub async fn get_typed_observation<T: serde::de::DeserializeOwned>(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<HealthObservation<T>, String> {
        self.get_observation_inner(endpoint, maximum, deadline)
            .await
            .map_err(|error| audit_failure("GET", endpoint, error))
    }
    async fn get_observation_inner<T: serde::de::DeserializeOwned>(
        &self,
        endpoint: &str,
        maximum: usize,
        deadline: tokio::time::Instant,
    ) -> Result<HealthObservation<T>, String> {
        let url = reqwest::Url::parse(endpoint).map_err(|_| "Invalid health endpoint")?;
        if !matches!(url.scheme(), "http" | "https") {
            return Err("An absolute HTTP(S) health endpoint is required".into());
        }
        tokio::time::timeout_at(deadline, async {
            let response = self.client.get(url).send().await.map_err(|error| {
                format!(
                    "Health endpoint could not be reached: {:?}",
                    error.without_url()
                )
            })?;
            if response.status() != reqwest::StatusCode::OK {
                return Err(format!(
                    "Health unavailable (HTTP {})",
                    response.status().as_u16()
                ));
            }
            // Prefer the API's TimeProvider clock. HTTP Date supports existing
            // deployed servers; never substitute the workstation or session clock.
            let server_utc = if let Some(value) = response.headers().get("x-ifm-server-utc") {
                Some(
                    chrono::DateTime::parse_from_rfc3339(
                        value.to_str().map_err(|_| "Invalid API clock header")?,
                    )
                    .map_err(|_| "Invalid API clock header")?
                    .to_utc(),
                )
            } else if let Some(value) = response.headers().get(reqwest::header::DATE) {
                Some(
                    chrono::DateTime::parse_from_rfc2822(
                        value.to_str().map_err(|_| "Invalid API Date header")?,
                    )
                    .map_err(|_| "Invalid API Date header")?
                    .to_utc(),
                )
            } else {
                None
            };
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
            if !json_depth_within(&body, 20) {
                return Err("Health response exceeds its nesting bound".into());
            }
            let value: T = serde_json::from_slice(&body)
                .map_err(|error| format!("Health response is invalid JSON: {error:?}"))?;
            Ok(HealthObservation { value, server_utc })
        })
        .await
        .map_err(|_| "Health request deadline expired".to_string())?
    }
}

/// Keep the route useful without exposing URL credentials or query secrets.
fn audit_failure(method: &str, endpoint: &str, error: String) -> String {
    let route = reqwest::Url::parse(endpoint)
        .map(|mut url| {
            let _ = url.set_username("");
            let _ = url.set_password(None);
            url.set_query(None);
            url.set_fragment(None);
            format!("{method} {url}")
        })
        .unwrap_or_else(|_| format!("{method} invalid-http-endpoint"));
    crate::diagnostics::errors::request_failure(&route, &uuid::Uuid::new_v4().to_string(), error)
}

/// Bound nesting before deserialization without allocating a second document tree.
/// Serde validates JSON syntax and field types after this lexical depth check.
fn json_depth_within(bytes: &[u8], maximum: usize) -> bool {
    let (mut depth, mut index) = (0usize, 0usize);
    while index < bytes.len() {
        match bytes[index] {
            b'"' => {
                index += 1;
                while index < bytes.len() {
                    match bytes[index] {
                        b'\\' => index += 2,
                        b'"' => break,
                        _ => index += 1,
                    }
                }
                let mut next = index + 1;
                while next < bytes.len() && bytes[next].is_ascii_whitespace() {
                    next += 1;
                }
                if bytes.get(next) != Some(&b':') && depth > maximum {
                    return false;
                }
            }
            b'{' | b'[' => {
                if depth > maximum {
                    return false;
                }
                depth += 1;
            }
            b'}' | b']' => depth = depth.saturating_sub(1),
            b'-' | b'0'..=b'9' | b't' | b'f' | b'n' if depth > maximum => return false,
            _ => {}
        }
        index += 1;
    }
    true
}
