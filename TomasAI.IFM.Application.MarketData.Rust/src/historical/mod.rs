//! Cancellable, per-operation historical clients. DNS uses only the qualified
//! numeric resolver. Redirects, proxies and OS DNS fallback are disabled. TLS uses
//! explicit WebPKI roots and the Rust verifier, not blocking platform trust calls.
//! Each client lives inside its original-deadline future, including stream reads.
use crate::{
    dns::{ResolveError, Resolver},
    runtime::{Deadline, Interrupted},
};
use databento::{
    dbn::{Metadata, RecordRef},
    historical::{
        metadata::{GetBillableSizeParams, GetCostParams},
        timeseries::GetRangeParams,
    },
};
use reqwest::Url;
use std::{
    collections::VecDeque,
    sync::{Arc, Mutex},
};
use tokio_util::sync::CancellationToken;
pub mod batch;
pub mod file;
pub mod record;
/// Historical ownership/transport failures.
#[derive(Debug)]
pub enum Failure {
    /// Endpoint, credential or request identity is invalid.
    Input(&'static str),
    /// Qualified resolver failure.
    Resolution(ResolveError),
    /// Caller cancellation or original deadline expiry.
    Interrupted(Interrupted),
    /// Provider/decoder error, with the supplied credential redacted.
    Provider(String),
    /// Consumer failed to preserve authoritative history processing.
    Processing(String),
}
/// Immutable endpoint policy; no client, background connection or lease is cached.
/// HTTPS is mandatory except numeric loopback HTTP used by integration fixtures.
pub struct History {
    base: Url,
    key: String,
    reference_key: Option<String>,
    reference_timeout: Option<std::time::Duration>,
    resolver: Arc<Resolver>,
    warnings: Mutex<VecDeque<Warning>>,
    requests: tokio::sync::Semaphore,
    notification: Option<Arc<crate::runtime::owner::Owner>>,
}
/// Complete bounded provider warning, including correlation with its HTTP request.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Warning {
    /// Provider API method, without credential/query data.
    pub endpoint: String,
    /// HTTP response status code.
    pub status: u16,
    /// Provider request identifier when supplied.
    pub request_id: String,
    /// Original warning header text, with the configured credential redacted.
    pub text: String,
}
/// Result of a fully consumed history stream; records are never accumulated in a Vec.
pub struct Summary {
    /// Full metadata retained under the shared decoder's separate budget.
    pub metadata: Metadata,
    /// Number of successfully processed records.
    pub records: u64,
}
impl History {
    /// Validates fixed endpoint and credential format without performing I/O.
    pub fn new(base: &str, key: String, resolver: Arc<Resolver>) -> Result<Self, Failure> {
        if key.len() != 32 || !key.is_ascii() {
            return Err(Failure::Input("Unsupported historical credential"));
        }
        Self::transport(base, key, resolver)
    }
    pub(crate) fn public_reference(base: &str, resolver: Arc<Resolver>) -> Result<Self, Failure> {
        Self::transport(base, String::new(), resolver)
    }
    fn transport(base: &str, key: String, resolver: Arc<Resolver>) -> Result<Self, Failure> {
        let base = Url::parse(base).map_err(|_| Failure::Input("Invalid historical endpoint"))?;
        let host = base
            .host_str()
            .ok_or(Failure::Input("Missing historical hostname"))?;
        let loopback = host
            .parse::<std::net::IpAddr>()
            .is_ok_and(|ip| ip.is_loopback());
        if (base.scheme() != "https" && !(base.scheme() == "http" && loopback))
            || !base.username().is_empty()
            || base.password().is_some()
            || base.query().is_some()
            || base.fragment().is_some()
        {
            return Err(Failure::Input(
                "Unsupported historical endpoint or credential",
            ));
        }
        Ok(Self {
            base,
            key,
            reference_key: None,
            reference_timeout: None,
            resolver,
            warnings: Mutex::new(VecDeque::new()),
            requests: tokio::sync::Semaphore::new(4),
            notification: None,
        })
    }
    /// Attaches the runtime's independent delivery signal. The client retains
    /// only the cancellation/delivery owner, never a runtime or FFI instance.
    pub fn with_notifications(mut self, owner: Arc<crate::runtime::owner::Owner>) -> Self {
        self.notification = Some(owner);
        self
    }
    /// Copies the oldest warning before consuming it. If conversion or caller
    /// capacity fails, the warning remains available for a subsequent delivery.
    pub fn consume_warning<T>(
        &self,
        copy: impl FnOnce(&Warning) -> Result<T, Failure>,
    ) -> Result<Option<T>, Failure> {
        let mut warnings = self
            .warnings
            .lock()
            .map_err(|_| Failure::Input("Warning owner interrupted"))?;
        let Some(front) = warnings.front() else {
            return Ok(None);
        };
        let value = copy(front)?;
        warnings.pop_front();
        Ok(Some(value))
    }
    /// Removes accumulated warnings for application logging. A stalled receiver
    /// cannot cause unbounded growth: subsequent requests fail explicitly at 256
    /// retained warnings rather than silently discarding provider information.
    pub fn drain_warnings(&self) -> Result<Vec<Warning>, Failure> {
        let mut warnings = self
            .warnings
            .lock()
            .map_err(|_| Failure::Input("Warning owner interrupted"))?;
        Ok(warnings.drain(..).collect())
    }
    /// In-flight HTTP operations, capped at four per shared backend client. Queued
    /// callers consume their original deadline without opening another socket or
    /// multiplying the decoder/body retained-memory budget by operation capacity.
    pub fn active_requests(&self) -> usize {
        4 - self.requests.available_permits()
    }
    /// Retains live-replay control diagnostics in the same bounded source warning
    /// stream as historical HTTP notices. Capacity exhaustion fails the operation
    /// rather than silently dropping provider information.
    pub(crate) fn record_notice(
        &self,
        endpoint: &str,
        status: u16,
        text: &str,
    ) -> Result<(), Failure> {
        if endpoint.len() > 128 || text.len() > 8192 {
            return Err(Failure::Input(
                "Provider notice exceeds bounded delivery policy",
            ));
        }
        let mut warnings = self
            .warnings
            .lock()
            .map_err(|_| Failure::Input("Warning owner interrupted"))?;
        if warnings.len() >= 256 {
            return Err(Failure::Processing(
                "Provider notice delivery capacity exceeded".into(),
            ));
        }
        warnings.push_back(Warning {
            endpoint: endpoint.to_owned(),
            status,
            request_id: String::new(),
            text: self.redact(text),
        });
        drop(warnings);
        if let Some(owner) = &self.notification {
            owner
                .signal(1 << 4)
                .map_err(|_| Failure::Processing("Provider notice delivery interrupted".into()))?;
        }
        Ok(())
    }
    fn record_warnings(
        &self,
        endpoint: &str,
        response: &hyper::Response<hyper::body::Incoming>,
    ) -> Result<(), Failure> {
        let headers = response.headers().get_all("x-warning");
        let mut warnings = self
            .warnings
            .lock()
            .map_err(|_| Failure::Input("Warning owner interrupted"))?;
        for header in headers {
            if header.as_bytes().len() > 8192
                || response
                    .headers()
                    .get("x-request-id")
                    .is_some_and(|v| v.as_bytes().len() > 1024)
            {
                return Err(self.provider("Historical warning exceeds bounded delivery policy"));
            }
            if warnings.len() >= 256 {
                return Err(self.provider("Historical warning delivery capacity exceeded"));
            }
            let text = self.redact(String::from_utf8_lossy(header.as_bytes()));
            let request_id = response
                .headers()
                .get("x-request-id")
                .map(|v| String::from_utf8_lossy(v.as_bytes()).into_owned())
                .unwrap_or_default();
            let request_id = self.redact(request_id);
            warnings.push_back(Warning {
                endpoint: endpoint.to_owned(),
                status: response.status().as_u16(),
                request_id,
                text,
            });
        }
        let pending = !warnings.is_empty();
        drop(warnings);
        if pending && let Some(owner) = &self.notification {
            owner.signal(1 << 4).map_err(|_| {
                Failure::Processing("Historical warning delivery interrupted".into())
            })?;
        }
        Ok(())
    }
    fn provider(&self, error: impl ToString) -> Failure {
        Failure::Provider(self.redact(error))
    }
    fn redact(&self, value: impl ToString) -> String {
        let text = value.to_string();
        let text = if self.key.is_empty() {
            text
        } else {
            text.replace(&self.key, "[REDACTED]")
        };
        match &self.reference_key {
            Some(key) => text.replace(key, "[REDACTED]"),
            None => text,
        }
    }
    /// Cancellable reference transport using the provider's apikey header,
    /// never Databento Basic auth. Secrets are also redacted from diagnostics.
    pub(crate) fn keyed_reference(
        base: &str,
        key: String,
        resolver: Arc<Resolver>,
        timeout: std::time::Duration,
    ) -> Result<Self, Failure> {
        if key.is_empty()
            || key.len() > 4096
            || !key.is_ascii()
            || key.bytes().any(|b| b < 0x21 || b == 0x7f)
        {
            return Err(Failure::Input("Invalid reference credential"));
        }
        let mut transport = Self::public_reference(base, resolver)?;
        transport.reference_key = Some(key);
        transport.reference_timeout = Some(timeout);
        Ok(transport)
    }
    /// Reads a bounded JSON response, retaining status and Retry-After for the
    /// reference owner's explicit retry/circuit policy. No background task or
    /// connection survives cancellation or the original caller deadline.
    pub(crate) async fn reference_json(
        &self,
        target: Url,
        maximum: usize,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(u16, Option<String>, Vec<u8>), Failure> {
        if target.origin() != self.base.origin()
            || !target.username().is_empty()
            || target.password().is_some()
            || target.fragment().is_some()
            || maximum == 0
            || maximum > 8 * 1024 * 1024
        {
            return Err(Failure::Input("Invalid reference JSON destination/budget"));
        }
        self.request_at(
            hyper::Method::GET,
            "reference.json",
            target,
            Vec::new(),
            token,
            deadline,
            |response| async move {
                let status = response.status().as_u16();
                let retry = response
                    .headers()
                    .get("retry-after")
                    .and_then(|h| h.to_str().ok())
                    .filter(|h| h.len() <= 1024)
                    .map(str::to_owned);
                let body = if (200..300).contains(&status) {
                    self.body(response.into_body(), maximum).await?
                } else {
                    Vec::new()
                };
                Ok((status, retry, body))
            },
        )
        .await
    }
    pub(crate) async fn reference_xml(
        &self,
        target: Url,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<u8>, Failure> {
        if !self.key.is_empty()
            || target.origin() != self.base.origin()
            || !target.username().is_empty()
            || target.password().is_some()
            || target.fragment().is_some()
        {
            return Err(Failure::Input("Invalid public reference destination"));
        }
        self.request_at(
            hyper::Method::GET,
            "reference.xml",
            target,
            Vec::new(),
            token,
            deadline,
            |response| self.body(response.into_body(), 1_048_576),
        )
        .await
    }
    async fn response<T, F, Fut>(
        &self,
        endpoint: &str,
        form: Vec<(&str, String)>,
        token: &CancellationToken,
        deadline: Deadline,
        consume: F,
    ) -> Result<T, Failure>
    where
        F: FnOnce(hyper::Response<hyper::body::Incoming>) -> Fut,
        Fut: std::future::Future<Output = Result<T, Failure>>,
    {
        self.request(
            hyper::Method::POST,
            endpoint,
            form,
            token,
            deadline,
            consume,
        )
        .await
    }
    async fn request<T, F, Fut>(
        &self,
        method: hyper::Method,
        endpoint: &str,
        form: Vec<(&str, String)>,
        token: &CancellationToken,
        deadline: Deadline,
        consume: F,
    ) -> Result<T, Failure>
    where
        F: FnOnce(hyper::Response<hyper::body::Incoming>) -> Fut,
        Fut: std::future::Future<Output = Result<T, Failure>>,
    {
        let target = self
            .base
            .join(&format!("v0/{endpoint}"))
            .map_err(|e| self.provider(e))?;
        self.request_at(method, endpoint, target, form, token, deadline, consume)
            .await
    }
    #[expect(
        clippy::too_many_arguments,
        reason = "Explicit destination and original deadline remain within one transport owner"
    )]
    async fn request_at<T, F, Fut>(
        &self,
        method: hyper::Method,
        endpoint: &str,
        target: Url,
        form: Vec<(&str, String)>,
        token: &CancellationToken,
        deadline: Deadline,
        consume: F,
    ) -> Result<T, Failure>
    where
        F: FnOnce(hyper::Response<hyper::body::Incoming>) -> Fut,
        Fut: std::future::Future<Output = Result<T, Failure>>,
    {
        use base64::Engine;
        use tokio::io::{AsyncRead, AsyncWrite};
        trait Socket: AsyncRead + AsyncWrite + Unpin + Send {}
        impl<T: AsyncRead + AsyncWrite + Unpin + Send> Socket for T {}
        let operation = async {
            let _permit = self
                .requests
                .acquire()
                .await
                .map_err(|_| Failure::Input("Historical owner closed"))?;
            let headers_received = std::sync::atomic::AtomicBool::new(false);
            let perform = async {
                let host = target
                    .host_str()
                    .ok_or(Failure::Input("Missing endpoint host"))?;
                let port = target
                    .port_or_known_default()
                    .ok_or(Failure::Input("Invalid endpoint port"))?;
                let addresses = self
                    .resolver
                    .resolve(host, port, token, deadline)
                    .await
                    .map_err(|e| match e {
                        ResolveError::Interrupted(reason) => Failure::Interrupted(reason),
                        other => Failure::Resolution(other),
                    })?;
                let socket = tokio::net::TcpStream::connect(addresses.as_slice())
                    .await
                    .map_err(|e| self.provider(e))?;
                socket.set_nodelay(true).map_err(|e| self.provider(e))?;
                let socket: Box<dyn Socket> = if target.scheme() == "https" {
                    let mut roots = rustls::RootCertStore::empty();
                    roots.extend(webpki_roots::TLS_SERVER_ROOTS.iter().cloned());
                    let tls = rustls::ClientConfig::builder_with_provider(Arc::new(
                        rustls::crypto::ring::default_provider(),
                    ))
                    .with_safe_default_protocol_versions()
                    .map_err(|e| self.provider(e))?
                    .with_root_certificates(roots)
                    .with_no_client_auth();
                    let name = rustls::pki_types::ServerName::try_from(host.to_owned())
                        .map_err(|_| Failure::Input("Invalid TLS server name"))?;
                    Box::new(
                        tokio_rustls::TlsConnector::from(Arc::new(tls))
                            .connect(name, socket)
                            .await
                            .map_err(|e| self.provider(e))?,
                    )
                } else {
                    Box::new(socket)
                };
                let (mut sender, connection) = hyper::client::conn::http1::Builder::new()
                    .max_buf_size(65536)
                    .handshake(hyper_util::rt::TokioIo::new(socket))
                    .await
                    .map_err(|e| self.provider(e))?;
                let form = serde_urlencoded::to_string(form).map_err(|e| self.provider(e))?;
                if form.len() > 8 * 1024 * 1024 {
                    return Err(Failure::Input("Historical form exceeds capacity"));
                }
                let mut uri = target.clone();
                let host = match target.port() {
                    Some(port) => format!(
                        "{}:{port}",
                        target.host().ok_or(Failure::Input("Missing host"))?
                    ),
                    None => target
                        .host()
                        .ok_or(Failure::Input("Missing host"))?
                        .to_string(),
                };
                let (path, body) = if method == hyper::Method::GET {
                    if !form.is_empty() {
                        uri.set_query(Some(&form));
                    }
                    (
                        uri.query().map_or_else(
                            || uri.path().to_owned(),
                            |query| format!("{}?{query}", uri.path()),
                        ),
                        String::new(),
                    )
                } else {
                    (uri.path().to_owned(), form)
                };
                let authorization =
                    base64::engine::general_purpose::STANDARD.encode(format!("{}:", self.key));
                let request = hyper::Request::builder()
                    .method(method)
                    .uri(path)
                    .header("Host", host)
                    .header("Content-Type", "application/x-www-form-urlencoded")
                    .header(
                        "Accept",
                        if endpoint == "timeseries.get_range" {
                            "application/octet-stream"
                        } else if endpoint == "reference.xml" {
                            "application/xml"
                        } else {
                            "application/json"
                        },
                    )
                    .header("Connection", "close");
                // Provider-returned download URLs may use another origin. Never
                // forward the API credential to that origin or follow redirects.
                let request = if !self.key.is_empty() && target.origin() == self.base.origin() {
                    request.header("Authorization", format!("Basic {authorization}"))
                } else {
                    request
                };
                let request = if let Some(key) = &self.reference_key {
                    if target.origin() != self.base.origin() {
                        return Err(Failure::Input("Reference credential origin mismatch"));
                    }
                    request.header("apikey", key)
                } else {
                    request
                };
                let request = request
                    .body(http_body_util::Full::new(bytes::Bytes::from(body)))
                    .map_err(|e| self.provider(e))?;
                let work = async {
                    let response = sender
                        .send_request(request)
                        .await
                        .map_err(|e| self.provider(e))?;
                    headers_received.store(true, std::sync::atomic::Ordering::Relaxed);
                    drop(sender);
                    self.record_warnings(endpoint, &response)?;
                    if !response.status().is_success() && endpoint != "reference.json" {
                        let status = response.status().as_u16();
                        let body = self.body(response.into_body(), 65_536).await?;
                        return Err(self.provider(format!(
                            "Historical HTTP {status} error: {}",
                            String::from_utf8_lossy(&body)
                        )));
                    }
                    consume(response).await
                };
                tokio::pin!(work, connection);
                // The connection driver is polled here, never spawned. Dropping this
                // future drops TLS/TCP/HTTP together before reporting completion.
                tokio::select! {
                    result=&mut work=>result,
                    result=&mut connection=>{result.map_err(|e|self.provider(e))?;work.await},
                }
            };
            tokio::pin!(perform);
            if let Some(timeout) = self.reference_timeout {
                tokio::select! {
                    result=&mut perform=>result,
                    ()=tokio::time::sleep(timeout)=>{
                        if headers_received.load(std::sync::atomic::Ordering::Relaxed){perform.await}
                        else {Err(self.provider("FMP request headers timed out"))}
                    }
                }
            } else {
                perform.await
            }
        };
        deadline
            .run(token, operation)
            .await
            .map_err(Failure::Interrupted)?
    }
    async fn body(
        &self,
        mut body: hyper::body::Incoming,
        limit: usize,
    ) -> Result<Vec<u8>, Failure> {
        use http_body_util::BodyExt;
        use hyper::body::Body;
        if body
            .size_hint()
            .upper()
            .is_some_and(|size| size > limit as u64)
        {
            return Err(self.provider("Historical response exceeds bounded body policy"));
        }
        let mut result = Vec::new();
        while let Some(frame) = body.frame().await {
            let frame = frame.map_err(|e| self.provider(e))?;
            if let Ok(data) = frame.into_data() {
                let length = result
                    .len()
                    .checked_add(data.len())
                    .ok_or(Failure::Input("Body size overflow"))?;
                if length > limit {
                    return Err(self.provider("Historical response exceeds bounded body policy"));
                }
                if length > result.capacity() {
                    let next = length.next_power_of_two().min(limit);
                    result
                        .try_reserve_exact(next - result.len())
                        .map_err(|_| Failure::Input("Historical allocation failed"))?;
                }
                result.extend_from_slice(&data);
            }
            tokio::task::yield_now().await;
        }
        Ok(result)
    }
    fn form(
        params: &databento::historical::metadata::GetQueryParams,
    ) -> Result<Vec<(&'static str, String)>, Failure> {
        let bounded = match &params.symbols {
            databento::Symbols::All => true,
            databento::Symbols::Ids(ids) => !ids.is_empty() && ids.len() <= 65_536,
            databento::Symbols::Symbols(symbols) => {
                !symbols.is_empty()
                    && symbols.len() <= 65_536
                    && symbols
                        .iter()
                        .all(|s| !s.is_empty() && s.len() <= 70 && s.is_ascii() && !s.contains(','))
            }
        };
        if !bounded || params.dataset.is_empty() || params.dataset.len() > 64 {
            return Err(Failure::Input(
                "Historical request exceeds bounded symbol/dataset policy",
            ));
        }
        let mut form = vec![
            ("dataset", params.dataset.clone()),
            ("schema", params.schema.to_string()),
            ("stype_in", params.stype_in.to_string()),
            ("symbols", params.symbols.to_api_string()),
            (
                "start",
                params
                    .date_time_range
                    .start
                    .unix_timestamp_nanos()
                    .to_string(),
            ),
            (
                "end",
                params
                    .date_time_range
                    .end
                    .unix_timestamp_nanos()
                    .to_string(),
            ),
        ];
        if let Some(limit) = params.limit {
            form.push(("limit", limit.to_string()));
        }
        Ok(form)
    }
    /// Streams all records into a bounded-work consumer under one original deadline.
    /// Dropping or failing the consumer retires the complete per-operation client.
    pub async fn for_each<P>(
        &self,
        params: GetRangeParams,
        token: &CancellationToken,
        deadline: Deadline,
        mut process: P,
    ) -> Result<Summary, Failure>
    where
        P: FnMut(RecordRef<'_>) -> Result<(), String>,
    {
        self.for_each_async(params, token, deadline, |record| {
            std::future::ready(process(record))
        })
        .await
    }

    /// Streams to an asynchronous bounded-work consumer under the same original
    /// network deadline. The consumer must copy required record fields before
    /// constructing its returned future: no borrowed decoder record may escape.
    /// This permits bounded channel backpressure without blocking a Tokio thread
    /// or dropping authoritative historical observations when a consumer is slow.
    pub async fn for_each_async<P, F>(
        &self,
        params: GetRangeParams,
        token: &CancellationToken,
        deadline: Deadline,
        mut process: P,
    ) -> Result<Summary, Failure>
    where
        P: FnMut(RecordRef<'_>) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        let query = databento::historical::metadata::GetQueryParams::builder()
            .dataset(params.dataset)
            .symbols(params.symbols)
            .schema(params.schema)
            .stype_in(params.stype_in)
            .date_time_range(params.date_time_range)
            .maybe_limit(params.limit)
            .build();
        let mut form = Self::form(&query)?;
        form.extend([
            ("stype_out", params.stype_out.to_string()),
            ("encoding", "dbn".into()),
            ("compression", "zstd".into()),
        ]);
        #[expect(deprecated)]
        let policy = params.upgrade_policy.unwrap_or_default();
        self.response(
            "timeseries.get_range",
            form,
            token,
            deadline,
            |response| async move {
                use futures_util::TryStreamExt;
                use http_body_util::BodyExt;
                let stream = response
                    .into_body()
                    .into_data_stream()
                    .map_err(std::io::Error::other);
                let reader = tokio_util::io::StreamReader::new(stream);
                let reader = databento::dbn::decode::AsyncDynReader::with_buffer(
                    tokio::io::BufReader::new(reader),
                    databento::dbn::Compression::Zstd,
                );
                let mut decoder =
                    databento::dbn::decode::dbn::AsyncDecoder::with_upgrade_policy(reader, policy)
                        .await
                        .map_err(|e| self.provider(e))?;
                let mut records = 0_u64;
                while let Some(record) = decoder
                    .decode_record_ref()
                    .await
                    .map_err(|e| self.provider(e))?
                {
                    process(record).await.map_err(Failure::Processing)?;
                    records = records
                        .checked_add(1)
                        .ok_or(Failure::Input("History record count overflow"))?;
                    tokio::task::yield_now().await;
                }
                let (metadata, reader) = decoder.into_parts();
                drop(reader);
                Ok(Summary { metadata, records })
            },
        )
        .await
    }

    /// Reads the exclusive published end for the exact schema, never the newer
    /// overall dataset range. No billable data request or live reset is started.
    pub async fn available_end(
        &self,
        dataset: &str,
        schema: databento::dbn::Schema,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<time::OffsetDateTime, Failure> {
        self.request(
            hyper::Method::GET,
            "metadata.get_dataset_range",
            vec![("dataset", dataset.to_owned())],
            token,
            deadline,
            |response| async move {
                let bytes = self.body(response.into_body(), 8 * 1024 * 1024).await?;
                let ranges: databento::historical::metadata::DatasetRange =
                    serde_json::from_slice(&bytes).map_err(|e| self.provider(e))?;
                let range = ranges
                    .range_by_schema
                    .get(&schema)
                    .ok_or(Failure::Input("Historical schema availability is absent"))?;
                if range.start >= range.end {
                    return Err(Failure::Input(
                        "Historical schema availability is empty or invalid",
                    ));
                }
                Ok(range.end)
            },
        )
        .await
    }

    /// Provider estimate, without downloading records or starting live feeds.
    pub async fn cost(
        &self,
        params: GetCostParams,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<f64, Failure> {
        self.response(
            "metadata.get_cost",
            Self::form(&params)?,
            token,
            deadline,
            |response| async move {
                let data = self.body(response.into_body(), 8 * 1024 * 1024).await?;
                serde_json::from_slice(&data).map_err(|e| self.provider(e))
            },
        )
        .await
    }

    /// Billable-size estimate, preserving provider integer units.
    pub async fn billable_size(
        &self,
        params: GetBillableSizeParams,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<u64, Failure> {
        self.response(
            "metadata.get_billable_size",
            Self::form(&params)?,
            token,
            deadline,
            |response| async move {
                let data = self.body(response.into_body(), 8 * 1024 * 1024).await?;
                serde_json::from_slice(&data).map_err(|e| self.provider(e))
            },
        )
        .await
    }
    /// Estimates provider record count without starting a live session or
    /// collecting records. Counts retain their original unsigned integer units.
    pub async fn record_count(
        &self,
        params: databento::historical::metadata::GetQueryParams,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<u64, Failure> {
        self.response(
            "metadata.get_record_count",
            Self::form(&params)?,
            token,
            deadline,
            |response| async move {
                let bytes = self.body(response.into_body(), 8 * 1024 * 1024).await?;
                serde_json::from_slice(&bytes).map_err(|e| self.provider(e))
            },
        )
        .await
    }
    /// Streams the provider's original full-definition JSON lines without
    /// re-encoding them. Raw bytes participate in existing definition digests.
    /// Parents are API-selected; no wall clock or generic underlying is substituted.
    pub async fn latest_definitions<P>(
        &self,
        dataset: &str,
        parents: &[String],
        token: &CancellationToken,
        deadline: Deadline,
        mut process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> Result<(), String>,
    {
        self.latest_definitions_async(dataset, parents, token, deadline, |line| {
            std::future::ready(process(line))
        })
        .await
    }
    /// Streams original source lines to a bounded asynchronous consumer under
    /// the same original deadline; no complete provider universe is retained.
    pub async fn latest_definitions_async<P, F>(
        &self,
        dataset: &str,
        parents: &[String],
        token: &CancellationToken,
        deadline: Deadline,
        process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        if dataset.is_empty()
            || dataset.len() > 64
            || parents.len() > 16
            || parents.iter().any(|s| {
                let root = s.strip_suffix(".FUT").or_else(|| s.strip_suffix(".OPT"));
                root.is_none_or(|r| {
                    r.is_empty()
                        || s.len() > 64
                        || !r
                            .bytes()
                            .all(|b| b.is_ascii_uppercase() || b.is_ascii_digit())
                })
            })
        {
            return Err(Failure::Input("Invalid definition dataset/parents"));
        }
        self.definition_lines(
            dataset,
            parents,
            if parents.is_empty() {
                "raw_symbol"
            } else {
                "parent"
            },
            token,
            deadline,
            false,
            process,
        )
        .await
    }
    /// Acquires the exact configured provider symbols. This shares the same
    /// fixed-size JSON-line decoder as parent discovery; it never substitutes
    /// an all-dataset query for a missing configured contract.
    pub async fn exact_definitions<P>(
        &self,
        dataset: &str,
        symbols: &[String],
        token: &CancellationToken,
        deadline: Deadline,
        mut process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> Result<(), String>,
    {
        self.exact_definitions_async(dataset, symbols, token, deadline, |line| {
            std::future::ready(process(line))
        })
        .await
    }
    /// Streams original source lines to a bounded asynchronous consumer under
    /// the same original deadline; no complete provider universe is retained.
    pub async fn exact_definitions_async<P, F>(
        &self,
        dataset: &str,
        symbols: &[String],
        token: &CancellationToken,
        deadline: Deadline,
        process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        if dataset.is_empty()
            || dataset.len() > 64
            || symbols.is_empty()
            || symbols.len() > 2048
            || symbols.iter().any(|symbol| {
                symbol.trim().is_empty()
                    || symbol.len() > 70
                    || !symbol.is_ascii()
                    || symbol
                        .bytes()
                        .any(|byte| byte.is_ascii_control() || byte == b',')
            })
        {
            return Err(Failure::Input("Invalid exact definition symbols"));
        }
        self.definition_lines(
            dataset,
            symbols,
            "raw_symbol",
            token,
            deadline,
            false,
            process,
        )
        .await
    }
    /// Streams the full instrument-definition schema with the existing managed
    /// provider's stricter schema-coverage requirement. Exact/parent bridge queries
    /// retain their separate SDK dataset-end fallback policy.
    pub async fn all_definitions_async<P, F>(
        &self,
        dataset: &str,
        token: &CancellationToken,
        deadline: Deadline,
        process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        if dataset.trim().is_empty() || dataset.len() > 64 {
            return Err(Failure::Input("Invalid definition dataset"));
        }
        self.definition_lines(dataset, &[], "raw_symbol", token, deadline, true, process)
            .await
    }
    #[expect(
        clippy::too_many_arguments,
        reason = "Explicit coverage policy keeps the two frozen provider entry points distinct"
    )]
    async fn definition_lines<P, F>(
        &self,
        dataset: &str,
        symbols: &[String],
        symbology: &str,
        token: &CancellationToken,
        deadline: Deadline,
        require_schema: bool,
        mut process: P,
    ) -> Result<u64, Failure>
    where
        P: FnMut(&str) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        let (start, end) = self
            .request(
                hyper::Method::GET,
                "metadata.get_dataset_range",
                vec![("dataset", dataset.to_owned())],
                token,
                deadline,
                |response| async move {
                    let data = self.body(response.into_body(), 8 * 1024 * 1024).await?;
                    let json: serde_json::Value =
                        serde_json::from_slice(&data).map_err(|e| self.provider(e))?;
                    // Match the pinned current provider bridge, including its
                    // date parser and dataset-end fallback. Parsing only the
                    // first ten characters would accept corrupt timestamps.
                    let range: databento::historical::metadata::DatasetRange =
                        serde_json::from_slice(&data).map_err(|e| self.provider(e))?;
                    let selected = range
                        .range_by_schema
                        .get(&databento::dbn::Schema::Definition);
                    if require_schema && selected.is_none() {
                        return Err(Failure::Input("Definition schema coverage is missing"));
                    }
                    let parsed_end = selected.map_or(range.end, |value| value.end);
                    let end = if selected.is_some() {
                        &json["schema"]["definition"]["end"]
                    } else {
                        &json["end"]
                    }
                    .as_str()
                    .ok_or(Failure::Input("Definition coverage unavailable"))?
                    .to_owned();
                    let start = parsed_end
                        .date()
                        .previous_day()
                        .ok_or(Failure::Input("Definition coverage date underflow"))?
                        .to_string();
                    Ok((start, end))
                },
            )
            .await?;
        let form = vec![
            ("dataset", dataset.to_owned()),
            ("schema", "definition".into()),
            (
                "symbols",
                if symbols.is_empty() {
                    "ALL_SYMBOLS".into()
                } else {
                    symbols.join(",")
                },
            ),
            ("stype_in", symbology.into()),
            ("start", start),
            ("end", end),
            ("encoding", "json".into()),
            ("compression", "none".into()),
            ("pretty_px", "false".into()),
            ("pretty_ts", "false".into()),
            ("map_symbols", "false".into()),
        ];
        self.response(
            "timeseries.get_range",
            form,
            token,
            deadline,
            |response| async move {
                use futures_util::TryStreamExt;
                use http_body_util::BodyExt;
                use tokio::io::{AsyncBufReadExt, AsyncReadExt};
                let stream = response
                    .into_body()
                    .into_data_stream()
                    .map_err(std::io::Error::other);
                let mut source = tokio_util::io::StreamReader::new(stream);
                let mut prefix = [0u8; 3];
                let mut filled = 0;
                while filled < 3 {
                    let n = source
                        .read(&mut prefix[filled..])
                        .await
                        .map_err(|e| self.provider(e))?;
                    if n == 0 {
                        break;
                    }
                    filled += n;
                }
                // StreamReader strips an initial UTF-8 BOM. Its bytes must not
                // enter a definition digest or consume the 65,536-byte line cap.
                let retained = if filled == 3 && prefix == [0xef, 0xbb, 0xbf] {
                    0
                } else {
                    filled
                };
                let mut reader = tokio::io::BufReader::new(
                    std::io::Cursor::new(prefix)
                        .take(retained as u64)
                        .chain(source),
                );
                let mut line = Vec::new();
                line.try_reserve_exact(65_536)
                    .map_err(|_| Failure::Input("Definition buffer allocation failed"))?;
                let mut count = 0_u64;
                let mut skip_lf = false;
                loop {
                    let buffer = reader.fill_buf().await.map_err(|e| self.provider(e))?;
                    if buffer.is_empty() {
                        self.emit_definition_line(&line, &mut count, &mut process)
                            .await?;
                        break;
                    }
                    let consumed = buffer.len();
                    for &byte in buffer {
                        // StreamReader.ReadLine compatibility: CR, LF and CRLF
                        // are transport delimiters, excluded from the raw digest.
                        if skip_lf && byte == b'\n' {
                            skip_lf = false;
                            continue;
                        }
                        skip_lf = false;
                        if byte == b'\r' || byte == b'\n' {
                            self.emit_definition_line(&line, &mut count, &mut process)
                                .await?;
                            line.clear();
                            skip_lf = byte == b'\r';
                        } else {
                            if line.len() == 65_536 {
                                return Err(
                                    self.provider("Definition JSON line exceeds bounded capacity")
                                );
                            }
                            line.push(byte);
                        }
                    }
                    reader.consume(consumed);
                    tokio::task::yield_now().await;
                }
                Ok(count)
            },
        )
        .await
    }
    async fn emit_definition_line<P, F>(
        &self,
        line: &[u8],
        count: &mut u64,
        process: &mut P,
    ) -> Result<(), Failure>
    where
        P: FnMut(&str) -> F,
        F: std::future::Future<Output = Result<(), String>>,
    {
        let text = std::str::from_utf8(line).map_err(|e| self.provider(e))?;
        if !text.trim().is_empty() {
            process(text)
                .await
                .map_err(|error| Failure::Processing(error.replace(&self.key, "[REDACTED]")))?;
            *count = count
                .checked_add(1)
                .ok_or(Failure::Input("Definition count overflow"))?;
        }
        Ok(())
    }
}
