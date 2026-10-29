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
                    if !matches!(&event, async_nats::Event::Connected) {
                        crate::diagnostics::errors::record("nats.connection_event",None,None,&format!("{event:?}"));
                    }
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
            .map_err(|_| crate::diagnostics::errors::request_failure("NATS.connect",&uuid::Uuid::new_v4().to_string(),"NATS connection deadline expired".into()))?
            .map_err(|e| crate::diagnostics::errors::request_failure("NATS.connect",&uuid::Uuid::new_v4().to_string(),format!("NATS connection failed: {e}; details={e:?}")))?;
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
        let operation = uuid::Uuid::new_v4().to_string();
        let result = self.request_until_inner(subject, body, deadline, trace, &operation).await;
        result.map_err(|error| crate::diagnostics::errors::request_failure(
            &subject.route().unwrap_or_else(|_| "invalid-actor-subject".into()), &operation, error))
    }
    async fn request_until_inner(
        &self, subject: &ActorSubject, body: &Value, deadline: tokio::time::Instant,
        trace: Option<(&str, &str)>, operation: &str,
    ) -> Result<Value, String> {
        let route = subject.route()?;
        let bytes = codec::encode(body)?;
        let mut headers = async_nats::HeaderMap::new();
        headers.insert("IFM-UI-Operation-Id", operation);
        if let Some((parent, state)) = trace {
            headers.insert("traceparent", parent);
            if !state.is_empty() {
                headers.insert("tracestate", state);
            }
        }
        let request = self
            .client
            .request_with_headers(route.clone(), headers, bytes.into());
        let reply = tokio::time::timeout_at(deadline, request)
            .await
            .map_err(|_| {
                "Backend request deadline expired; a submitted write may have an unknown outcome"
                    .to_string()
            })?
            .map_err(|e| format!("Backend request failed: {e}; details={e:?}"))?;
        let decoded=codec::decode(&reply.payload)?;
        // A valid reply can still reject a command/query. Preserve the wire
        // response for its controller while retaining the backend's full error.
        if decoded.as_array().is_some_and(|parts|parts.len()>=4&&parts[0].as_bool()==Some(false)) {
            if let Err(error)=super::contracts::service_result(&decoded) {
                crate::diagnostics::errors::record("backend.request_rejected",Some(&route),Some(operation),&error);
            }
        }
        Ok(decoded)
    }
    /// The only non-actor RPC used by this frontend is the existing versioned
    /// MarketData Host control subject. Scope is one validated deployment token.
    pub async fn marketdata_control_until(&self,scope:&str,body:&Value,deadline:tokio::time::Instant)->Result<Value,String>{
        let operation=uuid::Uuid::new_v4().to_string();
        let valid=!scope.is_empty()&&scope.bytes().all(|b|b.is_ascii_alphanumeric()||b==b'-');
        let subject=if valid {format!("ifm.{scope}.marketdata.v1.control")}else{"invalid-marketdata-scope".into()};
        let result=async {
            if !valid{return Err("Invalid MarketData Host scope".into())}
            let bytes=codec::encode(body)?;
            let mut headers=async_nats::HeaderMap::new();headers.insert("IFM-UI-Operation-Id",operation.as_str());
            let reply=tokio::time::timeout_at(deadline,self.client.request_with_headers(subject.clone(),headers,bytes.into())).await.map_err(|_|"MarketData Host request deadline expired; reconcile saved live-feed intent".to_string())?.map_err(|e|format!("MarketData Host request failed: {e}; details={e:?}"))?;
            let decoded=codec::decode(&reply.payload)?;
            if let Some(error)=decoded.as_array().filter(|parts|parts.len()==2).and_then(|parts|parts[1].as_str()) {
                crate::diagnostics::errors::record("backend.request_rejected",Some(&subject),Some(&operation),error);
            }
            Ok(decoded)
        }.await;
        result.map_err(|error|crate::diagnostics::errors::request_failure(&subject,&operation,error))
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
            .map_err(|e| crate::diagnostics::errors::request_failure(&format!("{actor}.{name}.>"), &uuid::Uuid::new_v4().to_string(), format!("NATS subscription failed: {e}; details={e:?}")))
    }
    /// Bind the exact durable push delivery subject so JetStream sees active UI interest.
    pub async fn subscribe_console(&self) -> Result<async_nats::Subscriber, String> {
        self.client.subscribe("Notify.StatusConsoleEvent.>").await.map_err(|error|crate::diagnostics::errors::request_failure("Notify.StatusConsoleEvent.Logged",&uuid::Uuid::new_v4().to_string(),format!("NATS console subscription failed: {error}; details={error:?}")))
    }
    pub async fn flush(&self) -> Result<(), String> {
        self.flush_until(tokio::time::Instant::now() + self.timeout)
            .await
    }
    pub async fn flush_until(&self, deadline: tokio::time::Instant) -> Result<(), String> {
        let result = tokio::time::timeout_at(deadline, self.client.flush())
            .await
            .map_err(|_| "NATS flush deadline expired".to_string())
            .and_then(|result|result.map_err(|e|format!("NATS flush failed: {e}; details={e:?}")));
        result.map_err(|error|crate::diagnostics::errors::request_failure("NATS.flush",&uuid::Uuid::new_v4().to_string(),error))
    }
}
