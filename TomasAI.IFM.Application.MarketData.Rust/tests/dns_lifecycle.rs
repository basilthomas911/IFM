//! Real TCP DNS fixtures and pinned SDK sessions; no system DNS or credentials.

use hickory_proto::{
    op::{Message, MessageType, ResponseCode},
    rr::{
        Name, RData, Record, RecordType,
        rdata::{A, AAAA, CNAME},
    },
};
use ifm_market_data_native::{
    dataset::databento::{NamedGateway, SessionExit, Stage, qualify_named_session},
    dns::{ResolveError, Resolver},
    runtime::{Deadline, Interrupted},
};
use std::{
    net::{Ipv4Addr, Ipv6Addr, SocketAddr},
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::{Duration, Instant},
};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::{TcpListener, TcpStream},
    sync::mpsc,
};
use tokio_util::sync::CancellationToken;

#[derive(Clone, Copy)]
enum Reply {
    Good,
    Stall,
    PartialLength,
    PartialBody,
    WrongId,
    WrongQuestion,
    NxDomain,
    ServFail,
    Empty,
    Alias,
    AliasOnly,
    Malformed,
    Cycle,
    Truncated,
    OtherOwner,
    V6Error,
}

struct Server {
    address: SocketAddr,
    shutdown: CancellationToken,
    owner: tokio::task::JoinHandle<()>,
    requests: Arc<AtomicUsize>,
    eof: Arc<AtomicUsize>,
    address_octet: Arc<AtomicUsize>,
}

impl Server {
    async fn new(reply: Reply, delay: Duration) -> Self {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let shutdown = CancellationToken::new();
        let requests = Arc::new(AtomicUsize::new(0));
        let eof = Arc::new(AtomicUsize::new(0));
        let address_octet = Arc::new(AtomicUsize::new(1));
        let (stop, count, closed, octet) = (
            shutdown.clone(),
            requests.clone(),
            eof.clone(),
            address_octet.clone(),
        );
        let owner = tokio::spawn(async move {
            let mut clients = tokio::task::JoinSet::new();
            loop {
                tokio::select! {
                    biased;
                    () = stop.cancelled() => break,
                    accepted = listener.accept() => {
                        let (socket, _) = accepted.unwrap();
                        clients.spawn(serve(socket, reply, delay, count.clone(), closed.clone(), octet.clone()));
                    }
                    Some(result) = clients.join_next(), if !clients.is_empty() => { result.unwrap(); }
                }
            }
            clients.abort_all();
            while let Some(result) = clients.join_next().await {
                assert!(result.is_ok() || result.unwrap_err().is_cancelled());
            }
        });
        Self {
            address,
            shutdown,
            owner,
            requests,
            eof,
            address_octet,
        }
    }
    async fn stop(self) {
        self.shutdown.cancel();
        self.owner.await.unwrap();
    }
}

async fn serve(
    mut socket: TcpStream,
    reply: Reply,
    delay: Duration,
    requests: Arc<AtomicUsize>,
    eof: Arc<AtomicUsize>,
    octet: Arc<AtomicUsize>,
) {
    let Ok(size) = socket.read_u16().await else {
        return;
    };
    let mut bytes = vec![0; size as usize];
    if socket.read_exact(&mut bytes).await.is_err() {
        return;
    }
    let query = Message::from_vec(&bytes).unwrap();
    tokio::time::sleep(delay).await;
    let mut response = Message::new();
    response
        .set_id(query.id())
        .set_message_type(MessageType::Response)
        .add_query(query.queries()[0].clone());
    let q = &query.queries()[0];
    let alias = Name::from_ascii("alias.test.").unwrap();
    let owner = if matches!(reply, Reply::Alias | Reply::AliasOnly) && q.name() != &alias {
        response.add_answer(Record::from_rdata(
            q.name().clone(),
            0,
            RData::CNAME(CNAME(alias.clone())),
        ));
        alias.clone()
    } else {
        q.name().clone()
    };
    match reply {
        Reply::WrongId => {
            response.set_id(query.id().wrapping_add(1));
        }
        Reply::WrongQuestion => {
            response.queries_mut().clear();
        }
        Reply::NxDomain => {
            response.set_response_code(ResponseCode::NXDomain);
        }
        Reply::ServFail => {
            response.set_response_code(ResponseCode::ServFail);
        }
        Reply::V6Error if q.query_type() == RecordType::AAAA => {
            response.set_response_code(ResponseCode::ServFail);
        }
        Reply::Truncated => {
            response.set_truncated(true);
        }
        Reply::Cycle => {
            response.add_answer(Record::from_rdata(
                owner.clone(),
                1,
                RData::CNAME(CNAME(owner.clone())),
            ));
        }
        _ => {}
    }
    if (matches!(reply, Reply::AliasOnly) && q.name() == &alias)
        || matches!(
            reply,
            Reply::Good | Reply::Alias | Reply::OtherOwner | Reply::V6Error
        )
    {
        let owner = if matches!(reply, Reply::OtherOwner) {
            Name::from_ascii("unrelated.test.").unwrap()
        } else {
            owner
        };
        let data = if q.query_type() == RecordType::A {
            RData::A(A(Ipv4Addr::new(
                127,
                0,
                0,
                octet.load(Ordering::SeqCst) as u8,
            )))
        } else {
            RData::AAAA(AAAA(Ipv6Addr::LOCALHOST))
        };
        response.add_answer(Record::from_rdata(owner, 0, data));
    }
    let bytes = response.to_vec().unwrap();
    match reply {
        Reply::Stall => {}
        Reply::PartialLength => {
            socket.write_all(&[0]).await.unwrap();
        }
        Reply::PartialBody => {
            socket.write_u16(100).await.unwrap();
            socket.write_all(&[0, 1]).await.unwrap();
        }
        Reply::Malformed => {
            socket.write_u16(2).await.unwrap();
            socket.write_all(&[0, 1]).await.unwrap();
        }
        _ => {
            socket.write_u16(bytes.len() as u16).await.unwrap();
            socket.write_all(&bytes).await.unwrap();
        }
    }
    requests.fetch_add(1, Ordering::SeqCst);
    let mut byte = [0];
    let closed = match socket.read(&mut byte).await {
        Ok(0) => true,
        Err(error) => error.kind() == std::io::ErrorKind::ConnectionReset,
        _ => false,
    };
    if closed {
        eof.fetch_add(1, Ordering::SeqCst);
    }
}

async fn wait_count(count: &AtomicUsize, expected: usize) {
    tokio::time::timeout(Duration::from_secs(3), async {
        while count.load(Ordering::SeqCst) != expected {
            tokio::time::sleep(Duration::from_millis(1)).await;
        }
    })
    .await
    .unwrap();
}

#[tokio::test]
async fn cancellation_drops_both_family_sockets_at_every_dns_read_boundary() {
    for reply in [Reply::Stall, Reply::PartialLength, Reply::PartialBody] {
        let server = Server::new(reply, Duration::ZERO).await;
        let resolver = Resolver::new(vec![server.address]).unwrap();
        let token = CancellationToken::new();
        let task = async {
            wait_count(&server.requests, 2).await;
            token.cancel();
        };
        let (result, ()) = tokio::join!(
            resolver.resolve("gateway.test", 13000, &token, Deadline::recovery()),
            task
        );
        assert!(matches!(
            result,
            Err(ResolveError::Interrupted(Interrupted::Cancelled))
        ));
        assert_eq!(resolver.active_exchanges(), 0);
        wait_count(&server.eof, 2).await;
        server.stop().await;
    }
}

#[tokio::test]
async fn new_resolution_refreshes_both_families_without_a_stale_cache() {
    let server = Server::new(Reply::Good, Duration::ZERO).await;
    let resolver = Resolver::new(vec![server.address]).unwrap();
    let token = CancellationToken::new();
    let first = resolver
        .resolve("gateway.test", 13000, &token, Deadline::recovery())
        .await
        .unwrap();
    assert_eq!(
        first,
        vec![
            "127.0.0.1:13000".parse().unwrap(),
            "[::1]:13000".parse().unwrap()
        ]
    );
    server.address_octet.store(2, Ordering::SeqCst);
    let next = resolver
        .resolve("gateway.test", 13000, &token, Deadline::recovery())
        .await
        .unwrap();
    assert_eq!(next[0], "127.0.0.2:13000".parse().unwrap());
    wait_count(&server.eof, 4).await;
    assert_eq!(resolver.active_exchanges(), 0);
    server.stop().await;
}

#[tokio::test]
async fn failures_are_reported_and_never_fall_back_to_system_dns() {
    for reply in [
        Reply::WrongId,
        Reply::WrongQuestion,
        Reply::NxDomain,
        Reply::ServFail,
        Reply::Empty,
        Reply::Cycle,
        Reply::Truncated,
        Reply::OtherOwner,
        Reply::Malformed,
    ] {
        let server = Server::new(reply, Duration::ZERO).await;
        let resolver = Resolver::new(vec![server.address]).unwrap();
        assert!(matches!(
            resolver
                .resolve(
                    "gateway.test",
                    13000,
                    &CancellationToken::new(),
                    Deadline::recovery()
                )
                .await,
            Err(ResolveError::Unavailable(_))
        ));
        assert_eq!(resolver.active_exchanges(), 0);
        server.stop().await;
    }
}

#[tokio::test]
async fn aliases_and_partial_family_failure_preserve_usable_addresses() {
    for reply in [Reply::Alias, Reply::AliasOnly, Reply::V6Error] {
        let server = Server::new(reply, Duration::ZERO).await;
        let resolver = Resolver::new(vec![server.address]).unwrap();
        let result = resolver
            .resolve(
                "gateway.test",
                13000,
                &CancellationToken::new(),
                Deadline::recovery(),
            )
            .await
            .unwrap();
        assert_eq!(result[0], "127.0.0.1:13000".parse().unwrap());
        assert_eq!(
            result.len(),
            if matches!(reply, Reply::Alias | Reply::AliasOnly) {
                2
            } else {
                1
            }
        );
        server.stop().await;
    }
}

#[tokio::test]
async fn healthy_resolver_wins_and_closes_stalled_loser() {
    let stalled = Server::new(Reply::Stall, Duration::ZERO).await;
    let healthy = Server::new(Reply::Good, Duration::from_millis(50)).await;
    let resolver = Resolver::new(vec![stalled.address, healthy.address]).unwrap();
    let result = resolver
        .resolve(
            "gateway.test",
            13000,
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(result.len(), 2);
    assert_eq!(resolver.active_exchanges(), 0);
    wait_count(&stalled.eof, 2).await;
    stalled.stop().await;
    healthy.stop().await;
}

#[tokio::test]
async fn dns_and_real_sdk_auth_share_original_deadline() {
    let dns = Server::new(Reply::Good, Duration::from_millis(150)).await;
    let resolver = Resolver::new(vec![dns.address]).unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let port = listener.local_addr().unwrap().port();
    let peer = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let mut byte = [0];
        assert_eq!(socket.read(&mut byte).await.unwrap(), 0);
    });
    let (stages, mut observed) = mpsc::channel(8);
    let started = Instant::now();
    let result = qualify_named_session(
        NamedGateway {
            host: "gateway.test",
            port,
            resolver: &resolver,
        },
        "x".repeat(32),
        "GLBX.MDP3".into(),
        CancellationToken::new(),
        Deadline::after(Duration::from_millis(350)),
        stages,
    )
    .await;
    assert!(matches!(
        result,
        SessionExit::Interrupted(Interrupted::DeadlineExpired)
    ));
    assert!(
        started.elapsed() < Duration::from_millis(480),
        "DNS must not restart the SDK budget"
    );
    assert_eq!(observed.recv().await, Some(Stage::Resolving));
    assert_eq!(observed.recv().await, Some(Stage::Connecting));
    tokio::time::timeout(Duration::from_secs(2), peer)
        .await
        .unwrap()
        .unwrap();
    assert_eq!(resolver.active_exchanges(), 0);
    dns.stop().await;
}

#[tokio::test]
async fn repeated_resets_complete_without_retained_exchanges() {
    let server = Server::new(Reply::Stall, Duration::ZERO).await;
    let resolver = Resolver::new(vec![server.address]).unwrap();
    for _ in 0..1000 {
        let result = resolver
            .resolve(
                "gateway.test",
                13000,
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(2)),
            )
            .await;
        assert!(matches!(
            result,
            Err(ResolveError::Interrupted(Interrupted::DeadlineExpired))
        ));
        assert_eq!(resolver.active_exchanges(), 0);
    }
    server.stop().await;
}

#[tokio::test]
async fn sdk_falls_back_from_refused_ipv4_to_resolved_ipv6_and_cancels_cleanly() {
    let dns = Server::new(Reply::Good, Duration::ZERO).await;
    let resolver = Resolver::new(vec![dns.address]).unwrap();
    let listener = TcpListener::bind("[::1]:0").await.unwrap();
    let port = listener.local_addr().unwrap().port();
    let (connected, accepted) = tokio::sync::oneshot::channel();
    let peer = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        connected.send(()).unwrap();
        let mut byte = [0];
        assert_eq!(socket.read(&mut byte).await.unwrap(), 0);
    });
    let token = CancellationToken::new();
    let cancel = async {
        accepted.await.unwrap();
        token.cancel();
    };
    let (stages, _observed) = mpsc::channel(8);
    let session = qualify_named_session(
        NamedGateway {
            host: "gateway.test",
            port,
            resolver: &resolver,
        },
        "x".repeat(32),
        "GLBX.MDP3".into(),
        token.clone(),
        Deadline::after(Duration::from_secs(3)),
        stages,
    );
    let (result, ()) = tokio::join!(session, cancel);
    assert!(matches!(
        result,
        SessionExit::Interrupted(Interrupted::Cancelled)
    ));
    tokio::time::timeout(Duration::from_secs(2), peer)
        .await
        .unwrap()
        .unwrap();
    assert_eq!(resolver.active_exchanges(), 0);
    dns.stop().await;
}

#[tokio::test]
async fn refused_resolver_falls_back_and_invalid_credentials_do_not_start_dns() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let refused = listener.local_addr().unwrap();
    drop(listener);
    let dns = Server::new(Reply::Good, Duration::ZERO).await;
    let resolver = Resolver::new(vec![refused, dns.address]).unwrap();
    assert_eq!(
        resolver
            .resolve(
                "gateway.test",
                13000,
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2))
            )
            .await
            .unwrap()
            .len(),
        2
    );
    let requests = dns.requests.load(Ordering::SeqCst);
    let (stages, mut observed) = mpsc::channel(8);
    let result = qualify_named_session(
        NamedGateway {
            host: "gateway.test",
            port: 13000,
            resolver: &resolver,
        },
        "secret-invalid".into(),
        "GLBX.MDP3".into(),
        CancellationToken::new(),
        Deadline::recovery(),
        stages,
    )
    .await;
    match result {
        SessionExit::ProviderError(text) => assert!(!text.contains("secret")),
        _ => panic!("invalid key accepted"),
    }
    assert_eq!(observed.recv().await, None);
    assert_eq!(dns.requests.load(Ordering::SeqCst), requests);
    dns.stop().await;
}

#[tokio::test]
async fn configuration_and_literal_addresses_do_not_invoke_external_resolution() {
    assert!(Resolver::new(vec![]).is_err());
    assert!(Resolver::new(vec!["0.0.0.0:53".parse().unwrap()]).is_err());
    let resolver = Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap();
    let result = resolver
        .resolve(
            "::1",
            13000,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert_eq!(result, vec!["[::1]:13000".parse().unwrap()]);
    assert_eq!(resolver.active_exchanges(), 0);
    assert!(matches!(
        resolver
            .resolve("", 13000, &CancellationToken::new(), Deadline::recovery())
            .await,
        Err(ResolveError::InvalidInput)
    ));
}

#[tokio::test]
async fn capacity_waiters_are_cancellable_and_do_not_open_more_sockets() {
    let server = Server::new(Reply::Stall, Duration::ZERO).await;
    let resolver = Resolver::new(vec![server.address]).unwrap();
    let token = CancellationToken::new();
    let lookups = futures_util::future::join_all(
        (0..24).map(|_| resolver.resolve("gateway.test", 13000, &token, Deadline::recovery())),
    );
    let cancel = async {
        wait_count(&server.requests, 32).await;
        assert_eq!(resolver.active_exchanges(), 32);
        token.cancel();
    };
    let (results, ()) = tokio::join!(lookups, cancel);
    assert!(
        results
            .iter()
            .all(|r| matches!(r, Err(ResolveError::Interrupted(Interrupted::Cancelled))))
    );
    assert_eq!(resolver.active_exchanges(), 0);
    wait_count(&server.eof, 32).await;
    server.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Real 120-second DNS stall; run explicitly during lifecycle qualification"]
async fn production_dns_deadline_closes_sockets_and_never_enters_sdk() {
    let server = Server::new(Reply::PartialBody, Duration::ZERO).await;
    let resolver = Resolver::new(vec![server.address]).unwrap();
    let token = CancellationToken::new();
    let deadline = Deadline::recovery();
    let (stages, mut observed) = mpsc::channel(8);
    let started = Instant::now();
    let session = qualify_named_session(
        NamedGateway {
            host: "gateway.test",
            port: 13000,
            resolver: &resolver,
        },
        "x".repeat(32),
        "GLBX.MDP3".into(),
        token.clone(),
        deadline,
        stages,
    );
    let observer = deadline.run(&token, std::future::pending::<()>());
    let (result, notification) = tokio::join!(session, observer);
    let elapsed = started.elapsed();
    assert!(matches!(
        result,
        SessionExit::Interrupted(Interrupted::DeadlineExpired)
    ));
    assert_eq!(notification, Err(Interrupted::DeadlineExpired));
    assert!(elapsed >= Duration::from_secs(120));
    assert!(elapsed < Duration::from_secs(122));
    assert_eq!(resolver.active_exchanges(), 0);
    assert_eq!(observed.recv().await, Some(Stage::Resolving));
    assert_eq!(observed.recv().await, None);
    wait_count(&server.eof, 2).await;
    eprintln!(
        "dns_production_budget_seconds=120; terminal_observed_seconds={:.6}; active_exchanges=0; peer_sockets_closed=2; sdk_entered=false",
        elapsed.as_secs_f64()
    );
    server.stop().await;
}
