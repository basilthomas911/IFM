//! Bounded, independently cancellable native NATS socket ownership.
//!
//! A completed flush is a broker protocol round trip, never a durable consumer
//! or storage acknowledgement. Dataset recovery can stop this publisher without
//! waiting for a broker flush, managed callback or downstream database operation.

use super::wire::Frame;
use crate::runtime::Deadline;
use futures_util::{FutureExt, StreamExt};
use std::panic::AssertUnwindSafe;
use std::sync::Arc;
use tokio::sync::{OwnedSemaphorePermit, Semaphore, mpsc, watch};
use tokio_util::sync::CancellationToken;

/// Public, bounded publication status; authentication material is redacted.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct Status {
    /// Native publisher has connected and has not stopped or failed.
    pub connected: bool,
    /// Successfully broker-flushed frames, not durable writes.
    pub flushed: u64,
    /// Start of the current unflushed native batch. The dataset supervisor uses
    /// this original instant even if no later market message fills the queue.
    pub pending_since: Option<tokio::time::Instant>,
    /// A failure requires the owning native supervisor to decide recovery.
    pub failure: Option<&'static str>,
    /// Full bounded broker/protocol error chain after authentication redaction.
    pub detail: Option<String>,
    /// Worker has exited; joined shutdown additionally awaits the task handle.
    pub stopped: bool,
}

/// Immutable broker configuration imported at native startup. DNS servers are
/// explicit numeric endpoints, shared with the configured native resolver policy.
/// Credentials may be carried by the URL and are never formatted into status.
#[derive(Clone)]
pub struct BrokerSettings {
    endpoints: Vec<String>,
    authentication: Authentication,
    resolver: Arc<crate::dns::Resolver>,
    tls: rustls::ClientConfig,
}
#[derive(Clone, PartialEq, Eq)]
enum Authentication {
    None,
    Token(String),
    UserPassword(String, String),
}
impl BrokerSettings {
    /// Whether cached raw metadata belongs to the same authenticated broker
    /// namespace. DNS/TLS transports are rebuilt independently; no old socket or
    /// resolver task is retained by this value comparison.
    pub(crate) fn same_data_source(&self, other: &Self) -> bool {
        self.endpoints == other.endpoints && self.authentication == other.authentication
    }

    /// Validates broker address and constructs a cancellable DNS resolver.
    /// TLS uses explicit preloaded trust roots rather than blocking OS stores.
    ///
    /// # Errors
    /// Rejects malformed URLs, non-NATS schemes, absent DNS policy or TLS setup.
    pub fn new(endpoint: String, dns: Vec<std::net::SocketAddr>) -> Result<Self, &'static str> {
        if endpoint.len() > 65536 {
            return Err("Native NATS address capacity exceeded");
        }
        let endpoints: Vec<_> = endpoint
            .split(',')
            .map(str::trim)
            .map(str::to_owned)
            .collect();
        if endpoints.is_empty() || endpoints.len() > 16 {
            return Err("Invalid native NATS seed count");
        }
        let mut authentication = Authentication::None;
        for (index, endpoint) in endpoints.iter().enumerate() {
            let address: async_nats::ServerAddr = endpoint
                .parse()
                .map_err(|_| "Invalid native NATS address")?;
            if !matches!(address.scheme(), "nats" | "tls") {
                return Err("Unsupported native NATS transport");
            }
            // Same URL policy as NATS.Client.Core: first seed URL supplies auth;
            // username-only is a token, user:password supplies both credentials.
            if index == 0
                && let Some(user) = address.username()
            {
                let decode = |value: &str| {
                    percent_encoding::percent_decode_str(value)
                        .decode_utf8()
                        .map(|s| s.into_owned())
                        .map_err(|_| "Invalid NATS credential encoding")
                };
                authentication = if let Some(password) = address.password() {
                    Authentication::UserPassword(decode(user)?, decode(password)?)
                } else {
                    Authentication::Token(decode(user)?)
                };
            }
        }
        let resolver =
            Arc::new(crate::dns::Resolver::new(dns).map_err(|_| "Invalid native NATS DNS policy")?);
        let roots =
            rustls::RootCertStore::from_iter(webpki_roots::TLS_SERVER_ROOTS.iter().cloned());
        let tls = rustls::ClientConfig::builder_with_provider(Arc::new(
            rustls::crypto::ring::default_provider(),
        ))
        .with_safe_default_protocol_versions()
        .map_err(|_| "Native NATS TLS configuration failed")?
        .with_root_certificates(roots)
        .with_no_client_auth();
        Ok(Self {
            endpoints,
            authentication,
            resolver,
            tls,
        })
    }
    /// Installs caller-imported trust/client authentication material already
    /// decoded in memory. No certificate file I/O occurs during dataset reset.
    pub fn with_tls(mut self, config: rustls::ClientConfig) -> Self {
        self.tls = config;
        self
    }
}

fn redact(mut value: String, secret: &Option<String>) -> String {
    if let Some(secret) = secret.as_ref().filter(|s| !s.is_empty()) {
        value = value.replace(secret, "[REDACTED]");
    }
    if value.len() > 131072 {
        let mut end = 131072;
        while !value.is_char_boundary(end) {
            end -= 1;
        }
        value.truncate(end);
        value.push_str(" [native error detail capacity exceeded]");
    }
    value
}
fn detail(error: &(dyn std::error::Error + 'static), secret: &Option<String>) -> String {
    let mut value = error.to_string();
    let mut source = error.source();
    for _ in 0..8 {
        let Some(error) = source else {
            break;
        };
        value.push_str(": ");
        value.push_str(&error.to_string());
        source = error.source();
    }
    redact(value, secret)
}

struct Packet {
    subject: String,
    bytes: Vec<u8>,
    reply: Option<String>,
    _charge: OwnedSemaphorePermit,
}

/// One bounded publication lane with explicit worker/socket lifetime.
pub struct Publisher {
    incoming: mpsc::Sender<Packet>,
    bytes: Arc<Semaphore>,
    maximum_bytes: usize,
    stopping: CancellationToken,
    task: Option<tokio::task::JoinHandle<()>>,
    status: watch::Receiver<Status>,
    scope: String,
    reply: Option<String>,
}

/// Cloneable bounded native publication port. It owns no task handle, socket,
/// dataset child or lease. Retained ports cannot prevent explicit owner shutdown.
#[derive(Clone)]
pub struct Port {
    incoming: mpsc::Sender<Packet>,
    bytes: Arc<Semaphore>,
    maximum_bytes: usize,
    stopping: CancellationToken,
    status: watch::Receiver<Status>,
    scope: String,
}
impl Port {
    /// Copies a detached frame into the one ordered native broker queue.
    ///
    /// # Errors
    /// Cancelled/stopped ports and capacity exhaustion fail immediately.
    pub fn try_publish(&self, frame: &Frame) -> Result<(), &'static str> {
        if self.stopping.is_cancelled() || self.status.borrow().stopped {
            return Err("Native publisher stopped");
        }
        let subject = frame.subject(&self.scope)?;
        let bytes = frame.encode()?;
        if bytes.len() > self.maximum_bytes {
            return Err("Native publication byte capacity exceeded");
        }
        let charge = self
            .bytes
            .clone()
            .try_acquire_many_owned(bytes.len() as u32)
            .map_err(|_| "Native publication byte capacity exceeded")?;
        self.incoming
            .try_send(Packet {
                subject,
                bytes,
                reply: None,
                _charge: charge,
            })
            .map_err(|_| "Native publication queue unavailable")
    }
    /// Copied broker state, independent of any caller holding this port.
    pub fn status(&self) -> Status {
        self.status.borrow().clone()
    }
    /// Event-driven protocol progress for original-deadline confirmation.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
}

struct Exchange {
    inbox: String,
    received: mpsc::Sender<Vec<u8>>,
}

struct Exit(watch::Sender<Status>);
impl Drop for Exit {
    fn drop(&mut self) {
        self.0.send_modify(|s| {
            s.connected = false;
            s.stopped = true;
        });
    }
}

impl Publisher {
    /// Starts a lazy broker connection using the original setup deadline.
    /// Queue and byte capacities cover queued AND in-flight packets. No caller
    /// waits for queue admission; exhausted capacity is explicit failure.
    ///
    /// # Errors
    /// Rejects unbounded/zero capacities, invalid scope and absent endpoint.
    pub fn start(
        broker: BrokerSettings,
        scope: String,
        capacity: usize,
        maximum_bytes: usize,
        deadline: Deadline,
    ) -> Result<Self, &'static str> {
        Self::start_inner(broker, scope, capacity, maximum_bytes, deadline, None, None)
    }
    /// Starts inside the dataset ownership tree. Parent cancellation joins the
    /// actual broker workers before releasing its tracked child registration.
    /// Dropping the returned control handle only signals cancellation.
    ///
    /// # Errors
    /// Rejects invalid settings or stopped/exhausted parent ownership.
    pub fn start_owned(
        broker: BrokerSettings,
        scope: String,
        capacity: usize,
        maximum_bytes: usize,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<Self, &'static str> {
        Self::start_inner(
            broker,
            scope,
            capacity,
            maximum_bytes,
            deadline,
            Some(parent),
            None,
        )
    }
    /// Starts a copied-evidence request/reply connection under the native owner.
    /// The bounded reply receiver carries bytes only and cannot delay shutdown.
    ///
    /// # Errors
    /// Invalid capacity or exhausted owner admission is explicit.
    pub fn start_evidence_exchange(
        broker: BrokerSettings,
        scope: String,
        capacity: usize,
        maximum_bytes: usize,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<(Self, mpsc::Receiver<Vec<u8>>), &'static str> {
        let (received, replies) = mpsc::channel(64);
        let identity = super::events::Identity::fresh(0).event;
        let inbox = format!(
            "_INBOX.ifm.{}",
            super::wire::guid(identity).replace('-', "")
        );
        let publisher = Self::start_inner(
            broker,
            scope,
            capacity,
            maximum_bytes,
            deadline,
            Some(parent),
            Some(Exchange { inbox, received }),
        )?;
        Ok((publisher, replies))
    }
    /// Starts copied diagnostic request/reply IO. The reply confirms external
    /// log acceptance only; it cannot acknowledge trade evidence or authorize an actor event.
    ///
    /// # Errors
    /// Invalid settings or stopped/exhausted native parent admission.
    pub fn start_diagnostic_exchange(
        broker: BrokerSettings,
        scope: String,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<(Self, mpsc::Receiver<Vec<u8>>), &'static str> {
        Self::start_evidence_exchange(broker, scope, 1, 16 * 1024, deadline, parent)
    }
    #[allow(clippy::too_many_arguments)]
    fn start_inner(
        broker: BrokerSettings,
        scope: String,
        capacity: usize,
        maximum_bytes: usize,
        deadline: Deadline,
        parent: Option<&crate::runtime::owner::Child>,
        exchange: Option<Exchange>,
    ) -> Result<Self, &'static str> {
        if capacity == 0
            || capacity > 4096
            || maximum_bytes == 0
            || maximum_bytes > 64 * 1024 * 1024
            || scope.is_empty()
            || scope.len() > 128
            || !scope
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || c == b'-')
        {
            return Err("Invalid native publisher configuration");
        }
        let (incoming, receiver) = mpsc::channel(capacity);
        let (status, observed) = watch::channel(Status::default());
        let stopping = parent.map_or_else(CancellationToken::new, |parent| {
            parent.cancellation().child_token()
        });
        let stop = stopping.clone();
        let runtime = tokio::runtime::Handle::try_current()
            .map_err(|_| "Native publisher requires Tokio runtime")?;
        let reply = exchange.as_ref().map(|exchange| exchange.inbox.clone());
        let task = if let Some(parent) = parent {
            parent
                .spawn_joined(move |_| {
                    Self::run(broker, receiver, stop, deadline, status, exchange)
                })
                .map_err(|_| "Native publisher parent admission failed")?
        } else {
            runtime.spawn(Self::run(
                broker, receiver, stop, deadline, status, exchange,
            ))
        };
        Ok(Self {
            incoming,
            bytes: Arc::new(Semaphore::new(maximum_bytes)),
            maximum_bytes,
            stopping,
            task: Some(task),
            status: observed,
            scope,
            reply,
        })
    }

    /// Attempts immediate admission of a copied native frame. This cannot pin or
    /// await a dataset lease, the broker, a managed consumer or a storage writer.
    ///
    /// # Errors
    /// Returns a classified stopped/invalid/capacity failure; never silently
    /// discards authoritative trades or pretends an overflow was delivered.
    pub fn try_publish(&self, frame: &Frame) -> Result<(), &'static str> {
        if self.stopping.is_cancelled() || self.status.borrow().stopped {
            return Err("Native publisher stopped");
        }
        let subject = frame.subject(&self.scope)?;
        let bytes = frame.encode()?;
        self.try_packet(subject, bytes, None)
    }

    /// Publishes one copied prepared-chain chunk on the existing cache subject.
    /// Use a dedicated cache writer: its flush count must not share actor IDs.
    ///
    /// # Errors
    /// Invalid chunks, stopped writers and bounded admission failure are explicit.
    pub fn try_option_cache(
        &self,
        publication: &super::option_cache::Publication,
        index: usize,
    ) -> Result<(), &'static str> {
        self.try_packet(
            format!("ifm.{}.marketdata.v1.option-cache", self.scope),
            publication.frame(index)?,
            None,
        )
    }

    /// Submits copied evidence to the external IO endpoint. The reply is a
    /// durable-writer receipt, distinct from a broker PONG. No FFI stream is used.
    ///
    /// # Errors
    /// A non-exchange publisher, invalid record or exhausted queue is explicit.
    pub fn try_evidence(
        &self,
        record: &super::evidence_outbox::Record,
    ) -> Result<(), &'static str> {
        let reply = self
            .reply
            .clone()
            .ok_or("Publisher has no evidence receipt subscription")?;
        let bytes = super::evidence_protocol::request(record)?;
        self.try_packet(
            format!("ifm.{}.marketdata.v1.evidence.write", self.scope),
            bytes,
            Some(reply),
        )
    }
    /// Requests one copied raw-reference page. This independent IO socket owns
    /// no provider or managed lease; external storage never controls its teardown.
    ///
    /// # Errors
    /// Missing reply subscription, stopped transport or bounded capacity.
    pub fn try_raw_reference(&self, bytes: Vec<u8>) -> Result<(), &'static str> {
        if bytes.len() > 16 * 1024 + 64 {
            return Err("Native raw-reference request capacity exceeded");
        }
        let reply = self
            .reply
            .clone()
            .ok_or("Publisher has no raw-reference reply subscription")?;
        self.try_packet(
            format!("ifm.{}.marketdata.v1.reference.read", self.scope),
            bytes,
            Some(reply),
        )
    }
    /// Copies a complete provider diagnostic onto the dedicated logging stream.
    /// Use an independently bounded publisher so logging cannot block prices or
    /// trade evidence. This does not cross a managed streaming FFI.
    ///
    /// # Errors
    /// Invalid event identity, payload or exhausted logging admission is explicit.
    pub fn try_diagnostic(
        &self,
        value: &crate::dataset::live::Diagnostic,
        worker: [u8; 16],
    ) -> Result<(), &'static str> {
        let role = if value.lane == crate::dataset::transport::Lane::OptionsGlbx {
            "Options"
        } else {
            "Futures"
        };
        self.try_packet(
            format!("ifm.{}.marketdata.v1.diagnostics.{role}", self.scope),
            super::diagnostic::encode(value, worker)?,
            self.reply.clone(),
        )
    }

    /// Publishes the existing terminal UI event directly. Its stable identity
    /// survives retries. PONG confirms broker I/O only, never journal consumption.
    ///
    /// # Errors
    /// Stopped transport, encoding failure or bounded queue exhaustion.
    pub fn try_recovery_notice(
        &self,
        notice: &super::recovery_notice::Notice,
    ) -> Result<(), &'static str> {
        self.try_packet(
            notice.subject.clone(),
            super::codec::compress(notice.bytes.clone())?,
            None,
        )
    }

    /// Publishes a versioned copied JSON outcome directly from Rust. The retained
    /// native journal and UI RecordId deduplication make reconnect retries safe.
    /// # Errors
    /// Invalid date, oversized body or unavailable bounded publication queue.
    pub fn try_recovery_outcome(&self, date: time::Date, body: &str) -> Result<(), &'static str> {
        if body.len() > 16 * 1024 || !(1..=9999).contains(&date.year()) {
            return Err("Invalid recovery outcome");
        }
        self.try_packet(
            format!("Notify.MarketDataRecoveryOutcome.{date}"),
            body.as_bytes().to_vec(),
            None,
        )
    }

    /// Sends a bounded existing Function request using this independent reply subscription.
    /// # Errors
    /// Missing inbox, stopped connection or full copied-value capacity.
    pub fn try_recovery_record(&self, subject: String, bytes: Vec<u8>) -> Result<(), &'static str> {
        let reply = self
            .reply
            .clone()
            .ok_or("Audit reply subscription unavailable")?;
        self.try_packet(subject, bytes, Some(reply))
    }

    fn try_packet(
        &self,
        subject: String,
        bytes: Vec<u8>,
        reply: Option<String>,
    ) -> Result<(), &'static str> {
        if self.stopping.is_cancelled() || self.status.borrow().stopped {
            return Err("Native publisher stopped");
        }
        if bytes.len() > self.maximum_bytes {
            return Err("Native publication byte capacity exceeded");
        }
        let charge = self
            .bytes
            .clone()
            .try_acquire_many_owned(bytes.len() as u32)
            .map_err(|_| "Native publication byte capacity exceeded")?;
        self.incoming
            .try_send(Packet {
                subject,
                bytes,
                reply,
                _charge: charge,
            })
            .map_err(|_| "Native publication queue unavailable")
    }

    /// Borrows only a copied queue port; explicit shutdown retains sole task
    /// ownership. Use a dedicated writer for a shared ordered dataset stream.
    pub fn port(&self) -> Port {
        Port {
            incoming: self.incoming.clone(),
            bytes: self.bytes.clone(),
            maximum_bytes: self.maximum_bytes,
            stopping: self.stopping.clone(),
            status: self.status.clone(),
            scope: self.scope.clone(),
        }
    }

    /// Returns a copied status projection, independent of broker responsiveness.
    pub fn status(&self) -> Status {
        self.status.borrow().clone()
    }

    /// Returns the exact currently charged queue/in-flight bytes.
    pub fn retained_bytes(&self) -> usize {
        self.maximum_bytes - self.bytes.available_permits()
    }

    /// Subscribes to native lifecycle progress without timer polling.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }

    async fn run(
        broker: BrokerSettings,
        mut receiver: mpsc::Receiver<Packet>,
        stop: CancellationToken,
        deadline: Deadline,
        status: watch::Sender<Status>,
        exchange: Option<Exchange>,
    ) {
        let _exit = Exit(status.clone());
        let secret = match &broker.authentication {
            Authentication::None => None,
            Authentication::Token(s) | Authentication::UserPassword(_, s) => Some(s.clone()),
        };
        let fault = CancellationToken::new();
        let event_fault = fault.clone();
        let event_status = status.clone();
        let event_stop = stop.clone();
        let event_secret = secret.clone();
        let resolver = broker.resolver;
        let resolve_stop = stop.clone();
        let options = async_nats::ConnectOptions::new()
            .client_capacity(1)
            .subscription_capacity(64)
            .inbound_payload_limit(
                std::num::NonZeroUsize::new(8192).expect("positive receipt bound"),
            )
            .connection_timeout(deadline.remaining())
            .max_reconnects(0)
            .event_callback(move |event| {
                let failed = matches!(
                    event,
                    async_nats::Event::ServerError(_)
                        | async_nats::Event::ClientError(_)
                        | async_nats::Event::SlowConsumer(_)
                        | async_nats::Event::Disconnected
                        | async_nats::Event::Closed
                );
                let status = event_status.clone();
                let fault = event_fault.clone();
                let stop = event_stop.clone();
                let message = redact(event.to_string(), &event_secret);
                async move {
                    if failed && !stop.is_cancelled() {
                        status.send_modify(|s| {
                            s.failure
                                .get_or_insert("Native NATS transport reported an error");
                            s.detail.get_or_insert(message);
                        });
                        fault.cancel();
                    }
                }
            })
            .tls_client_config(broker.tls)
            .socket_resolver(move |host, port| {
                let resolver = resolver.clone();
                let stop = resolve_stop.clone();
                async move {
                    resolver
                        .resolve(&host, port, &stop, deadline)
                        .await
                        .map_err(|_| std::io::Error::other("Native NATS DNS failed"))
                }
            });
        let options = match broker.authentication {
            Authentication::None => options,
            Authentication::Token(token) => options.token(token),
            Authentication::UserPassword(user, password) => {
                options.user_and_password(user, password)
            }
        };
        let connection = match deadline
            .run(&stop, async_nats::connect_owned(broker.endpoints, options))
            .await
        {
            Ok(Ok(connection)) => connection,
            failure => {
                if !stop.is_cancelled() {
                    let message = match failure {
                        Ok(Err(error)) => detail(&error, &secret),
                        Err(error) => error.to_string(),
                        Ok(Ok(_)) => unreachable!(),
                    };
                    status.send_modify(|s| {
                        s.failure = Some("Native NATS connection failed");
                        s.detail = Some(message);
                    });
                }
                return;
            }
        };
        let pumping = AssertUnwindSafe(async {
            let mut replies = if let Some(exchange) = &exchange {
                let subscribed = tokio::select! {biased; ()=stop.cancelled()=>return, ()=fault.cancelled()=>return,
                    result=deadline.run(&stop, async {
                        let subscriber=connection.client().subscribe(exchange.inbox.clone()).await.map_err(|e|detail(&e,&secret))?.with_connection_owner();
                        connection.client().roundtrip().await.map_err(|e|detail(&e,&secret))?;
                        Ok::<_,String>(subscriber)
                    })=>result};
                match subscribed {Ok(Ok(subscriber))=>Some(subscriber), _=>{status.send_modify(|s|s.failure=Some("Native evidence receipt subscription failed"));return;}}
            } else {None};
            status.send_modify(|s| s.connected = true);
            let mut batch = Vec::with_capacity(32);
            let mut charges = Vec::with_capacity(32);
            loop {
                let packet = tokio::select! { biased; () = stop.cancelled() => break,()=fault.cancelled()=>break,
                received=async{match &mut replies {Some(replies)=>replies.next().await,None=>std::future::pending().await}}=>{
                    match (received,&exchange) {
                        (Some(message),Some(exchange)) if message.payload.len()<=8192 && message.status.is_none()=>{
                            if exchange.received.try_send(message.payload.to_vec()).is_err(){status.send_modify(|s|s.failure=Some("Native evidence receipt queue exhausted"));break;}
                        }
                        (Some(message),_)=>{
                            let reason=redact(format!("Native evidence reply status={:?}; description={}; payload_bytes={}",message.status,message.description.as_deref().unwrap_or(""),message.payload.len()),&secret);
                            status.send_modify(|s|{s.failure=Some("Native evidence receipt stream failed");s.detail.get_or_insert(reason);});break;
                        }
                        _=>{status.send_modify(|s|s.failure=Some("Native evidence receipt stream failed"));break;}
                    }
                    continue;
                },
                next = receiver.recv() => match next { Some(packet) => packet, None => break } };
                batch.push(packet);
                // Only already-ready work is grouped: the first frame never waits
                // for a batching timer. One broker round trip acknowledges up to 32
                // ordered frames instead of imposing a round trip on every quote.
                while batch.len() < 32 {
                    match receiver.try_recv() {
                        Ok(packet) => batch.push(packet),
                        Err(_) => break,
                    }
                }
                let count = batch.len() as u64;
                status.send_modify(|s| s.pending_since = Some(tokio::time::Instant::now()));
                let sent = tokio::select! { biased; () = stop.cancelled() => None,()=fault.cancelled()=>None,
                result = async {
                    for packet in batch.drain(..){
                        let Packet{subject,bytes,reply,_charge}=packet;
                        charges.push(_charge);
                        if let Some(reply)=reply {connection.client().publish_with_reply(subject,reply,bytes.into()).await.map_err(|error|detail(&error,&secret))?;}
                        else {connection.client().publish(subject,bytes.into()).await.map_err(|error|detail(&error,&secret))?;}
                    }
                    connection.client().roundtrip().await.map_err(|error|detail(&error,&secret))
                } => Some(result) };
                // Reservations cover the entire queued/in-flight batch until its
                // flush result or cancellation, not merely client queue admission.
                charges.clear();
                batch.clear();
                match sent {
                    Some(Ok(())) => {
                        status.send_modify(|s| {
                            s.flushed = s.flushed.saturating_add(count);
                            s.pending_since = None;
                        });
                    }
                    Some(Err(error)) => {
                        status.send_modify(|s| {s.failure.get_or_insert("Native NATS publication failed");s.detail.get_or_insert(error);});
                        break;
                    }
                    None => break,
                }
            }
        })
        .catch_unwind()
        .await;
        if pumping.is_err() {
            status.send_modify(|s| s.failure = Some("Native NATS publisher panicked"));
        }
        // This aborts/joins socket workers; it does not send a broker drain that
        // could depend on a blackholed network. Dropped queued packets release
        // byte reservations without waiting for managed code.
        connection.shutdown().await;
    }

    /// Cancels immediately and joins under the caller's remaining original budget.
    ///
    /// # Errors
    /// An expired deadline or panicked worker is not claimed as cleanup proof.
    /// The retained owner can be joined again without extending that deadline.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        self.stopping.cancel();
        if let Some(task) = self.task.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native publisher join deadline expired")?;
            self.task.take();
            joined.map_err(|_| "Native publisher worker failed")?;
        }
        Ok(())
    }
}

impl Drop for Publisher {
    fn drop(&mut self) {
        self.stopping.cancel();
        // Signal-only fallback. Explicit shutdown is required as cleanup proof;
        // dropping the owner must never synchronously block a Tokio worker.
        // The run loop observes this signal while waiting on connect, queue,
        // publish or flush and explicitly joins both core broker workers. A
        // tracked parent's wait remains occupied through that cleanup.
    }
}
