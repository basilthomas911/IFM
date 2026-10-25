//! Core NATS request/reply. A request has one caller-supplied deadline.
//! Writes are never automatically retried following timeout or disconnection.
use super::{codec, contracts::ActorSubject};
use rmpv::Value;
use std::{path::PathBuf, time::Duration};

#[derive(Clone)]
pub struct Settings {
    pub url: String,
    pub credentials_file: Option<PathBuf>,
    pub token: Option<String>,
    pub request_timeout: Duration,
}
impl Settings {
    pub fn from_env() -> Result<Self, String> {
        let url = super::configuration::value("IFM_UI_NATS_URL")?
            .ok_or("Configure IFM_UI_NATS_URL or AppSettings:NatsServerUri in the client profile; no backend is selected implicitly")?;
        let seconds = super::configuration::value("IFM_UI_REQUEST_TIMEOUT_SECONDS")?
            .map(|s| s.parse::<u64>().map_err(|_| "Invalid request timeout"))
            .transpose()?
            .unwrap_or(120);
        if seconds == 0 {
            return Err("Request timeout must be positive".into());
        }
        Ok(Self {
            url,
            credentials_file: std::env::var_os("IFM_UI_NATS_CREDENTIALS").map(PathBuf::from)
                .or(super::configuration::value("IFM_UI_NATS_CREDENTIALS")?.map(PathBuf::from)),
            token: super::configuration::value("IFM_UI_NATS_TOKEN")?,
            request_timeout: Duration::from_secs(seconds),
        })
    }
}

#[derive(Clone)]
pub struct Backend {
    client: async_nats::Client,
    timeout: Duration,
    connection: tokio::sync::watch::Receiver<ConnectionState>,
}
#[derive(Clone, Debug, Default)]
pub struct ConnectionState {
    pub connected: bool,
    pub revision: u64,
    pub warning_sequence: u64,
    pub last_warning: Option<String>,
}
impl Backend {
    pub fn default_timeout(&self) -> Duration {
        self.timeout
    }
    pub fn watch_connection(&self) -> tokio::sync::watch::Receiver<ConnectionState> {
        self.connection.clone()
    }
    pub async fn connect(settings: Settings) -> Result<Self, String> {
        let (connection_sender, connection) =
            tokio::sync::watch::channel(ConnectionState::default());
        let mut options = async_nats::ConnectOptions::new()
            .name("TomasAI.IFM.UI.Rust.eGui")
            .request_timeout(None)
            .subscription_capacity(4096)
            .connection_timeout(settings.request_timeout)
            .event_callback(move |event| {
                let sender = connection_sender.clone();
                async move {
                    sender.send_modify(|state| {
                        state.revision += 1;
                        let warning = match event {
                            async_nats::Event::Connected => { state.connected = true; None },
                            async_nats::Event::Disconnected | async_nats::Event::Closed => { state.connected = false; Some("NATS disconnected; displayed values may be stale. Authoritative refresh is required.") },
                            async_nats::Event::SlowConsumer(_) => Some("NATS subscription capacity exceeded; notifications may have been missed. Authoritative refresh is required."),
                            async_nats::Event::ServerError(_) => Some("NATS reported a server error; authoritative refresh is required."),
                            async_nats::Event::ClientError(_) => Some("NATS reported a client transport error; authoritative refresh is required."),
                            _ => None,
                        };
                        if let Some(warning) = warning { state.warning_sequence += 1; state.last_warning = Some(warning.into()); }
                    });
                }
            });
        if let Some(path) = settings.credentials_file {
            let credentials = tokio::fs::read_to_string(path)
                .await
                .map_err(|_| "Cannot read configured NATS credentials file")?;
            options = options
                .credentials(&credentials)
                .map_err(|_| "Invalid NATS credentials file")?;
        }
        if let Some(token) = settings.token {
            options = options.token(token);
        }
        let client = tokio::time::timeout(settings.request_timeout, options.connect(settings.url))
            .await
            .map_err(|_| "NATS connection deadline expired".to_string())?
            .map_err(|e| format!("NATS connection failed: {}", e.kind()))?;
        Ok(Self {
            client,
            timeout: settings.request_timeout,
            connection,
        })
    }
    pub async fn request(
        &self,
        subject: &ActorSubject,
        body: &Value,
        timeout: Option<Duration>,
        trace: Option<(&str, &str)>,
    ) -> Result<Value, String> {
        self.request_until(
            subject,
            body,
            tokio::time::Instant::now() + timeout.unwrap_or(self.timeout),
            trace,
        )
        .await
    }
    pub async fn request_until(
        &self,
        subject: &ActorSubject,
        body: &Value,
        deadline: tokio::time::Instant,
        trace: Option<(&str, &str)>,
    ) -> Result<Value, String> {
        let route = subject.route()?;
        let bytes = codec::encode(body)?;
        let mut headers = async_nats::HeaderMap::new();
        if let Some((parent, state)) = trace {
            headers.insert("traceparent", parent);
            if !state.is_empty() {
                headers.insert("tracestate", state);
            }
        }
        let request = self
            .client
            .request_with_headers(route, headers, bytes.into());
        let reply = tokio::time::timeout_at(deadline, request)
            .await
            .map_err(|_| {
                "Backend request deadline expired; a submitted write may have an unknown outcome"
                    .to_string()
            })?
            .map_err(|e| format!("Backend request failed: {}", e.kind()))?;
        codec::decode(&reply.payload)
    }
    /// The only non-actor RPC used by this frontend is the existing versioned
    /// MarketData Host control subject. Scope is one validated deployment token.
    pub async fn marketdata_control_until(&self,scope:&str,body:&Value,deadline:tokio::time::Instant)->Result<Value,String>{
        if scope.is_empty()||!scope.bytes().all(|b|b.is_ascii_alphanumeric()||b==b'-'){return Err("Invalid MarketData Host scope".into())}
        let subject=format!("ifm.{scope}.marketdata.v1.control");let bytes=codec::encode(body)?;
        let reply=tokio::time::timeout_at(deadline,self.client.request(subject,bytes.into())).await.map_err(|_|"MarketData Host request deadline expired; reconcile saved live-feed intent".to_string())?.map_err(|e|format!("MarketData Host request failed: {}",e.kind()))?;
        codec::decode(&reply.payload)
    }
    /// Subscriber ownership is scoped: dropping the returned stream unsubscribes.
    pub async fn subscribe(
        &self,
        actor: &str,
        name: &str,
    ) -> Result<async_nats::Subscriber, String> {
        if [actor, name].iter().any(|s| {
            s.is_empty() || s.contains(['.', '*', '>']) || s.chars().any(char::is_whitespace)
        }) {
            return Err("Invalid subscription mailbox".into());
        }
        self.client
            .subscribe(format!("{actor}.{name}.>"))
            .await
            .map_err(|e| e.to_string())
    }
    /// Bind the exact durable push delivery subject so JetStream sees active UI interest.
    pub async fn subscribe_console(&self) -> Result<async_nats::Subscriber, String> {
        self.client.subscribe("ConsoleDelivery.SingleUI.messages").await.map_err(|error|error.to_string())
    }
    pub async fn flush(&self) -> Result<(), String> {
        self.flush_until(tokio::time::Instant::now() + self.timeout)
            .await
    }
    pub async fn flush_until(&self, deadline: tokio::time::Instant) -> Result<(), String> {
        tokio::time::timeout_at(deadline, self.client.flush())
            .await
            .map_err(|_| "NATS flush deadline expired".to_string())?
            .map_err(|e| e.to_string())
    }
}
