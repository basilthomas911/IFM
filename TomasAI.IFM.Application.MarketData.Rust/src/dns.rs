//! Cancellable DNS using owned Tokio TCP sockets and Hickory's packet codec only.
//!
//! No OS resolver, blocking jobs, spawned tasks, cache, or hidden retry timer is
//! used. Explicit numeric recursive-server addresses are required. TCP DNS avoids
//! UDP retransmission/truncation state and is a deployment requirement, not a
//! silent fallback to system DNS. Each reconnect obtains fresh A and AAAA answers.

use crate::runtime::{Deadline, Interrupted};
use futures_util::{StreamExt, stream::FuturesUnordered};
use hickory_proto::{
    op::{Message, MessageType, OpCode, Query, ResponseCode},
    rr::{DNSClass, Name, RData, RecordType},
};
use std::{
    net::{IpAddr, SocketAddr},
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpStream,
    sync::Semaphore,
};
use tokio_util::sync::CancellationToken;

/// Resolution failure, retaining cancellation/deadline independently of DNS errors.
#[derive(Debug)]
pub enum ResolveError {
    /// The original owner budget or its cancellation token ended the lookup.
    Interrupted(Interrupted),
    /// Invalid hostname, port or resolver configuration.
    InvalidInput,
    /// All configured resolvers failed; includes protocol/transport diagnostics.
    Unavailable(Vec<String>),
}

/// Bounded resolver with no independently running work or retained DNS answers.
pub struct Resolver {
    servers: Vec<SocketAddr>,
    slots: Arc<Semaphore>,
    active: Arc<AtomicUsize>,
}

struct Active(Arc<AtomicUsize>);
impl Drop for Active {
    fn drop(&mut self) {
        self.0.fetch_sub(1, Ordering::SeqCst);
    }
}

impl Resolver {
    /// Requires 1–4 trusted recursive DNS endpoints supporting TCP, all numeric.
    /// At most 16 resolution calls perform socket work concurrently. Waiting for
    /// a slot consumes the same caller deadline and is itself cancellable.
    pub fn new(servers: Vec<SocketAddr>) -> Result<Self, ResolveError> {
        Self::with_slots(servers, Arc::new(Semaphore::new(16)))
    }

    /// Shares runtime admission across live scopes and historical acquisition.
    /// Server addresses remain request-specific; only the finite capacity is
    /// shared. No registry of configurations or DNS answers accumulates.
    pub(crate) fn with_slots(
        servers: Vec<SocketAddr>,
        slots: Arc<Semaphore>,
    ) -> Result<Self, ResolveError> {
        if servers.is_empty()
            || servers.len() > 4
            || servers
                .iter()
                .any(|s| s.port() == 0 || s.ip().is_unspecified())
        {
            return Err(ResolveError::InvalidInput);
        }
        Ok(Self {
            servers,
            slots,
            active: Arc::new(AtomicUsize::new(0)),
        })
    }

    /// Exact configured recursive endpoints, used to compare physical intents.
    pub(crate) fn servers(&self) -> &[SocketAddr] {
        &self.servers
    }

    /// Number of in-flight DNS exchanges, including connect/partial reads.
    /// After awaiting a cancelled lookup this owner's exchanges are already gone.
    pub fn active_exchanges(&self) -> usize {
        self.active.load(Ordering::SeqCst)
    }

    /// Resolves both address families under the original deadline. Configured
    /// resolvers race without spawning tasks; a successful result drops losers.
    /// Partial family failure preserves usable addresses from the other family.
    /// DNS negative answers are not cached, so retries can observe provider changes.
    pub async fn resolve(
        &self,
        host: &str,
        port: u16,
        cancellation: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<SocketAddr>, ResolveError> {
        let operation = async {
            if port == 0 || host.is_empty() {
                return Err(ResolveError::InvalidInput);
            }
            if let Ok(ip) = host.parse::<IpAddr>() {
                return Ok(vec![SocketAddr::new(ip, port)]);
            }
            let mut name = Name::from_ascii(host).map_err(|_| ResolveError::InvalidInput)?;
            if name.is_root() {
                return Err(ResolveError::InvalidInput);
            }
            // DNS wire names are absolute. Normalize before comparing the echoed
            // question, otherwise an input without a final dot fails equality.
            name.set_fqdn(true);
            let _permit = self
                .slots
                .acquire()
                .await
                .map_err(|_| ResolveError::InvalidInput)?;
            let mut candidates = FuturesUnordered::new();
            for server in &self.servers {
                candidates.push(self.at_server(*server, name.clone(), port));
            }
            let mut failures = Vec::new();
            while let Some(result) = candidates.next().await {
                match result {
                    Ok(addresses) => return Ok(addresses),
                    Err(error) => failures.push(error),
                }
            }
            Err(ResolveError::Unavailable(failures))
        };
        deadline
            .run(cancellation, operation)
            .await
            .map_err(ResolveError::Interrupted)?
    }

    async fn at_server(
        &self,
        server: SocketAddr,
        name: Name,
        port: u16,
    ) -> Result<Vec<SocketAddr>, String> {
        // join! owns both futures: cancellation drops both sockets synchronously.
        let (v4, v6) = tokio::join!(
            self.family(server, name.clone(), RecordType::A),
            self.family(server, name, RecordType::AAAA)
        );
        let mut addresses = Vec::new();
        for ips in [&v4, &v6].into_iter().flatten() {
            for ip in ips {
                let addr = SocketAddr::new(*ip, port);
                if !addresses.contains(&addr) {
                    addresses.push(addr);
                }
            }
        }
        if addresses.is_empty() {
            Err(format!("DNS {server}: A={v4:?}; AAAA={v6:?}"))
        } else {
            Ok(addresses)
        }
    }

    async fn family(
        &self,
        server: SocketAddr,
        mut name: Name,
        kind: RecordType,
    ) -> Result<Vec<IpAddr>, String> {
        let mut visited = Vec::new();
        for _ in 0..8 {
            if visited.contains(&name) {
                return Err("DNS CNAME cycle".into());
            }
            visited.push(name.clone());
            let response = self
                .exchange(server, Query::query(name.clone(), kind))
                .await?;
            // Only accept records for the queried owner or its validated CNAME
            // chain; unrelated answer/additional addresses must never redirect us.
            let mut owner = name.clone();
            for _ in 0..8 {
                let ips: Vec<_> = response
                    .answers()
                    .iter()
                    .filter(|r| r.name() == &owner && r.dns_class() == DNSClass::IN)
                    .filter_map(|r| match (kind, r.data()) {
                        (RecordType::A, RData::A(a)) => Some(IpAddr::V4(a.0)),
                        (RecordType::AAAA, RData::AAAA(a)) => Some(IpAddr::V6(a.0)),
                        _ => None,
                    })
                    .collect();
                if !ips.is_empty() {
                    return Ok(ips);
                }
                match response.answers().iter().find_map(|r| {
                    if r.name() == &owner
                        && r.dns_class() == DNSClass::IN
                        && let RData::CNAME(c) = r.data()
                    {
                        return Some(c.0.clone());
                    }
                    None
                }) {
                    Some(alias) => {
                        owner = alias;
                    }
                    None => break,
                }
            }
            if owner == name {
                return Ok(Vec::new());
            }
            name = owner;
        }
        Err("DNS CNAME hop limit exceeded".into())
    }

    async fn exchange(&self, server: SocketAddr, query: Query) -> Result<Message, String> {
        self.active.fetch_add(1, Ordering::SeqCst);
        let _active = Active(self.active.clone());
        let mut request = Message::new();
        request
            .set_id(rand::random())
            .set_recursion_desired(true)
            .add_query(query.clone());
        let bytes = request.to_vec().map_err(|e| e.to_string())?;
        let mut socket = TcpStream::connect(server)
            .await
            .map_err(|e| e.to_string())?;
        socket
            .write_u16(bytes.len() as u16)
            .await
            .map_err(|e| e.to_string())?;
        socket.write_all(&bytes).await.map_err(|e| e.to_string())?;
        let size = socket.read_u16().await.map_err(|e| e.to_string())?;
        let mut bytes = vec![0; usize::from(size)];
        socket
            .read_exact(&mut bytes)
            .await
            .map_err(|e| e.to_string())?;
        let response = Message::from_vec(&bytes).map_err(|e| e.to_string())?;
        if response.id() != request.id()
            || response.message_type() != MessageType::Response
            || response.op_code() != OpCode::Query
            || response.queries() != [query]
            || response.truncated()
        {
            return Err("DNS response validation failed".into());
        }
        if response.response_code() != ResponseCode::NoError {
            return Err(format!("DNS response {}", response.response_code()));
        }
        Ok(response)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::Duration;
    use tokio::{net::TcpListener, sync::mpsc, task::JoinSet};

    /// Distinct provider configurations must not multiply runtime DNS capacity.
    /// Real partial-response sockets prove that queued cancellation neither opens
    /// another connection nor leaves a permit held after its owner is dropped.
    #[tokio::test]
    async fn independent_resolvers_share_admission_and_release_on_cancellation() {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let stop = CancellationToken::new();
        let server_stop = stop.clone();
        let (accepted, mut peers) = mpsc::channel(8);
        let server = tokio::spawn(async move {
            let mut children = JoinSet::new();
            loop {
                tokio::select! {
                    biased;
                    _ = server_stop.cancelled() => break,
                    pair = listener.accept() => {
                        let (mut socket, _) = pair.unwrap();
                        let accepted = accepted.clone();
                        children.spawn(async move {
                            let length = socket.read_u16().await.unwrap();
                            let mut query = vec![0; length as usize];
                            socket.read_exact(&mut query).await.unwrap();
                            accepted.send(()).await.unwrap();
                            // No response: the caller must release this socket.
                            let mut byte = [0];
                            assert_eq!(socket.read(&mut byte).await.unwrap(), 0);
                        });
                    }
                }
            }
            while let Some(result) = children.join_next().await {
                result.unwrap();
            }
        });
        let slots = Arc::new(Semaphore::new(1));
        let first = Arc::new(Resolver::with_slots(vec![address], slots.clone()).unwrap());
        let second = Arc::new(Resolver::with_slots(vec![address], slots.clone()).unwrap());
        let running = CancellationToken::new();
        let queued = CancellationToken::new();
        let spawn = |resolver: Arc<Resolver>, cancellation: CancellationToken| {
            tokio::spawn(async move {
                resolver
                    .resolve(
                        "feed.example",
                        13000,
                        &cancellation,
                        Deadline::after(Duration::from_secs(5)),
                    )
                    .await
            })
        };
        let task = spawn(first.clone(), running.clone());
        for _ in 0..2 {
            tokio::time::timeout(Duration::from_secs(1), peers.recv())
                .await
                .unwrap()
                .unwrap();
        }
        assert_eq!(slots.available_permits(), 0);
        let waiting = spawn(second.clone(), queued.clone());
        assert!(
            tokio::time::timeout(Duration::from_millis(40), peers.recv())
                .await
                .is_err()
        );
        queued.cancel();
        assert!(matches!(
            waiting.await.unwrap(),
            Err(ResolveError::Interrupted(_))
        ));
        assert_eq!(second.active_exchanges(), 0);
        assert_eq!(first.active_exchanges(), 2);
        running.cancel();
        assert!(matches!(
            task.await.unwrap(),
            Err(ResolveError::Interrupted(_))
        ));
        assert_eq!(first.active_exchanges(), 0);
        assert_eq!(slots.available_permits(), 1);

        // A fresh configuration can immediately reuse the same finite slot.
        let fresh = CancellationToken::new();
        let reused = spawn(second.clone(), fresh.clone());
        for _ in 0..2 {
            tokio::time::timeout(Duration::from_secs(1), peers.recv())
                .await
                .unwrap()
                .unwrap();
        }
        fresh.cancel();
        assert!(matches!(
            reused.await.unwrap(),
            Err(ResolveError::Interrupted(_))
        ));
        assert_eq!(slots.available_permits(), 1);
        assert_eq!(second.active_exchanges(), 0);
        stop.cancel();
        tokio::time::timeout(Duration::from_secs(1), server)
            .await
            .unwrap()
            .unwrap();
    }
}
