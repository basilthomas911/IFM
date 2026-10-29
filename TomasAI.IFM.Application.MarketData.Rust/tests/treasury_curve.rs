//! Differential official-source XML qualification against unchanged C# output.
use ifm_market_data_native::reference::treasury_curve::parse;
use ifm_market_data_native::{
    dns::Resolver,
    historical::Failure,
    reference::treasury_curve::Treasury,
    runtime::{Deadline, Interrupted, clock::Clock},
};
use std::{
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
    sync::mpsc,
    task::JoinSet,
};
use tokio_util::sync::CancellationToken;

struct Server {
    endpoint: String,
    mode: Arc<AtomicUsize>,
    requests: Arc<AtomicUsize>,
    stop: CancellationToken,
    seen: mpsc::Receiver<()>,
    closed: mpsc::Receiver<()>,
    task: tokio::task::JoinHandle<()>,
}
impl Server {
    async fn new() -> Self {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("http://{}/treasury.xml", listener.local_addr().unwrap());
        let mode = Arc::new(AtomicUsize::new(0));
        let requests = Arc::new(AtomicUsize::new(0));
        let stop = CancellationToken::new();
        let (seen_tx, seen) = mpsc::channel(32);
        let (closed_tx, closed) = mpsc::channel(32);
        let child = stop.clone();
        let modes = mode.clone();
        let counts = requests.clone();
        let task = tokio::spawn(async move {
            let mut peers = JoinSet::new();
            loop {
                tokio::select! {
                    _=child.cancelled()=>break,
                    peer=listener.accept()=>{
                        let(mut stream,_)=peer.unwrap();let mode=modes.load(Ordering::Acquire);let seen=seen_tx.clone();let closed=closed_tx.clone();let stop=child.clone();let counts=counts.clone();
                        peers.spawn(async move{
                            let operation=async{
                                let mut head=Vec::new();let mut byte=[0u8;1];
                                while !head.ends_with(b"\r\n\r\n"){if stream.read(&mut byte).await.unwrap()==0{return;}head.push(byte[0]);assert!(head.len()<4096);}
                                let head=String::from_utf8(head).unwrap();assert!(!head.to_ascii_lowercase().contains("authorization:"));
                                assert!(head.contains("Accept: application/xml")||head.contains("accept: application/xml"));
                                let target=head.split_whitespace().nth(1).unwrap();let url=reqwest::Url::parse(&format!("http://fixture{target}")).unwrap();
                                let query=url.query_pairs().collect::<std::collections::BTreeMap<_,_>>();
                                assert_eq!(query.get("data").unwrap(),"daily_treasury_yield_curve");let month=query.get("field_tdr_date_value_month").unwrap();
                                counts.fetch_add(1,Ordering::AcqRel);seen.send(()).await.unwrap();
                                if mode==1{let _=stream.read(&mut byte).await;return;}
                                if mode==2{stream.write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 1000\r\nConnection: close\r\n\r\n<feed>").await.unwrap();let _=stream.read(&mut byte).await;return;}
                                if mode==3{let _=stream.write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 1048577\r\nConnection: close\r\n\r\n").await;let _=stream.read(&mut byte).await;return;}
                                if mode==4{let _=stream.write_all(b"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/leak\r\nContent-Length: 0\r\nConnection: close\r\n\r\n").await;let _=stream.read(&mut byte).await;return;}
                                let body=if mode==5{"invalid XML".into()}else{format!("<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'><entry><content><m:properties><d:NEW_DATE>{}-{}-01T00:00:00</d:NEW_DATE><d:BC_1MONTH>5.00</d:BC_1MONTH></m:properties></content></entry></feed>",&month[..4],&month[4..])};
                                let head=format!("HTTP/1.1 200 OK\r\nContent-Length: {}\r\nX-Warning: complete reference warning\r\nX-Request-Id: treasury-fixture\r\nConnection: close\r\n\r\n",body.len());
                                stream.write_all(head.as_bytes()).await.unwrap();stream.write_all(body.as_bytes()).await.unwrap();let _=stream.read(&mut byte).await;
                            };
                            tokio::select!{_=operation=>{},_=stop.cancelled()=>{}}
                            drop(stream);let _=closed.send(()).await;
                        });
                    }
                }
            }
            while let Some(result) = peers.join_next().await {
                result.unwrap();
            }
        });
        Self {
            endpoint,
            mode,
            requests,
            stop,
            seen,
            closed,
            task,
        }
    }
    async fn stop(self) {
        self.stop.cancel();
        self.task.await.unwrap();
    }
}
fn resolver() -> Arc<Resolver> {
    Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap())
}
fn date(month: u8, day: u8) -> time::Date {
    time::Date::from_calendar_date(2026, time::Month::try_from(month).unwrap(), day).unwrap()
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn http_cache_dates_expiry_eviction_and_warning_identity_are_preserved() {
    let mut server = Server::new().await;
    let clock = Arc::new(Clock::new(12300, false).unwrap());
    let source = Treasury::new(&server.endpoint, resolver(), clock.clone()).unwrap();
    let token = CancellationToken::new();
    let first = source
        .range(date(9, 1), date(9, 30), &token, Deadline::recovery())
        .await
        .unwrap();
    server.closed.recv().await.unwrap();
    assert_eq!(first.len(), 1);
    assert_eq!(first[0].retrieved, 12300);
    assert_eq!(first[0].rates[0].percentage.bits, [500, 0, 0, 2 << 16]);
    clock.update(12301, false).unwrap();
    assert_eq!(
        source
            .range(date(9, 1), date(9, 30), &token, Deadline::recovery())
            .await
            .unwrap(),
        first
    );
    assert_eq!(server.requests.load(Ordering::Acquire), 1);
    let warnings = source.drain_warnings().unwrap();
    assert_eq!(warnings.len(), 1);
    assert_eq!(warnings[0].text, "complete reference warning");
    assert_eq!(warnings[0].request_id, "treasury-fixture");
    clock.update(600_012_300, false).unwrap();
    let refreshed = source
        .range(date(9, 1), date(9, 30), &token, Deadline::recovery())
        .await
        .unwrap();
    server.closed.recv().await.unwrap();
    assert_eq!(refreshed[0].retrieved, 600_012_300);
    assert_eq!(server.requests.load(Ordering::Acquire), 2);
    for month in [8, 10] {
        source
            .range(
                date(month, 1),
                date(month, 28),
                &token,
                Deadline::recovery(),
            )
            .await
            .unwrap();
        server.closed.recv().await.unwrap();
    }
    assert_eq!(server.requests.load(Ordering::Acquire), 4);
    // Equal expiration: baseline's first cache slot (September) was evicted.
    source
        .range(date(9, 1), date(9, 30), &token, Deadline::recovery())
        .await
        .unwrap();
    server.closed.recv().await.unwrap();
    assert_eq!(server.requests.load(Ordering::Acquire), 5);
    let latest = source
        .latest(date(9, 8), &token, Deadline::recovery())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(latest.date, date(9, 1));
    assert!(latest.date <= date(9, 8));
    assert!(
        source
            .range(date(9, 2), date(9, 1), &token, Deadline::recovery())
            .await
            .is_err()
    );
    assert!(
        source
            .range(
                date(1, 1),
                date(1, 1) + time::Duration::days(366),
                &token,
                Deadline::recovery()
            )
            .await
            .is_err()
    );
    server.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn stalled_headers_body_and_cache_gate_cancel_and_fresh_retry_succeeds() {
    let mut server = Server::new().await;
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let source = Arc::new(Treasury::new(&server.endpoint, resolver(), clock).unwrap());
    for mode in [1, 2] {
        server.mode.store(mode, Ordering::Release);
        let token = CancellationToken::new();
        let child = token.clone();
        let client = source.clone();
        let task = tokio::spawn(async move {
            client
                .range(date(9, 1), date(9, 30), &child, Deadline::recovery())
                .await
        });
        tokio::time::timeout(Duration::from_secs(2), server.seen.recv())
            .await
            .unwrap()
            .unwrap();
        let count = server.requests.load(Ordering::Acquire);
        assert!(matches!(
            source
                .range(
                    date(9, 1),
                    date(9, 30),
                    &CancellationToken::new(),
                    Deadline::after(Duration::from_millis(40))
                )
                .await,
            Err(Failure::Interrupted(Interrupted::DeadlineExpired))
        ));
        assert_eq!(
            server.requests.load(Ordering::Acquire),
            count,
            "Cache gate opened a second stalled socket"
        );
        token.cancel();
        assert!(matches!(
            task.await.unwrap(),
            Err(Failure::Interrupted(Interrupted::Cancelled))
        ));
        tokio::time::timeout(Duration::from_secs(2), server.closed.recv())
            .await
            .unwrap()
            .unwrap();
    }
    for mode in [3, 4, 5] {
        server.mode.store(mode, Ordering::Release);
        assert!(
            source
                .range(
                    date(9, 1),
                    date(9, 30),
                    &CancellationToken::new(),
                    Deadline::after(Duration::from_secs(2))
                )
                .await
                .is_err()
        );
        server.closed.recv().await.unwrap();
    }
    server.mode.store(0, Ordering::Release);
    assert_eq!(
        source
            .range(
                date(9, 1),
                date(9, 30),
                &CancellationToken::new(),
                Deadline::recovery()
            )
            .await
            .unwrap()
            .len(),
        1
    );
    server.closed.recv().await.unwrap();
    server.stop().await;
}
#[test]
fn official_source_parser_matches_every_csharp_decimal_date_and_failure() {
    let oracle: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/TreasuryCurveOracle.json")).unwrap();
    for case in oracle.as_array().unwrap() {
        let result = parse(
            case["xml"].as_str().unwrap().as_bytes(),
            2026,
            time::Month::September,
            17888832000000000,
        );
        assert_eq!(
            result.is_ok(),
            case["valid"].as_bool().unwrap(),
            "{}: {:?}",
            case["name"],
            result
        );
        let Ok(rows) = result else { continue };
        let actual:Vec<_>=rows.iter().map(|row|serde_json::json!({"date":format!("{:04}-{:02}-{:02}",row.date.year(),row.date.month() as u8,row.date.day()),
            "retrieved":row.retrieved,"rates":row.rates.iter().map(|rate|serde_json::json!({"months":rate.months,"bits":rate.percentage.bits})).collect::<Vec<_>>()})).collect();
        assert_eq!(serde_json::json!(actual), case["rows"], "{}", case["name"]);
    }
}

/// Real HTTP integration for the new native blueprint path. A held source-cache
/// admission must not retain the dataset after cancellation or renew its deadline.
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_context_preparation_owns_http_and_cancels_cache_waiters() {
    use base64::Engine;
    use ifm_market_data_native::{
        messaging::codec,
        option_chain::{
            convention::Convention,
            preparation::{Blueprint, Error, References},
        },
        reference::{
            calendar::Calendar,
            treasury_policy::{Conversion, Publication, Publications},
        },
    };
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let row = &rows[0];
    let at = row["at"].as_i64().unwrap() - 25 * 864_000_000_000;
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(row["contract"].as_str().unwrap())
        .unwrap();
    let mut value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
    let rmpv::Value::Array(ref mut fields) = value else {
        panic!("convention array")
    };
    fields[12] = codec::utc_offset(at + 8 * 864_000_000_000).unwrap();
    fields[13] = fields[12].clone();
    let blueprint = Blueprint {
        contract: Convention::new(value).unwrap(),
        calendar: Calendar::new(
            "fixture/v1".into(),
            "Eastern Standard Time",
            date(10, 1),
            date(11, 29),
            648_000_000_000,
            (1..=31)
                .map(|day| date(10, day))
                .chain((1..=29).map(|day| date(11, day)))
                .collect(),
        )
        .unwrap(),
        publications: Publications::new(
            "publication/v1".into(),
            at - 864_000_000_000,
            at + 864_000_000_000,
            vec![Publication {
                date: date(10, 1),
                available: at - 864_000_000_000,
            }],
            "Eastern Standard Time",
        )
        .unwrap(),
        conversion: Conversion::standard(),
        maximum_lead: 0,
    };
    let mut server = Server::new().await;
    let clock = Arc::new(Clock::new(at, false).unwrap());
    let source = Arc::new(Treasury::new(&server.endpoint, resolver(), clock.clone()).unwrap());
    let references = References::new(source, clock.clone());
    let context = references
        .prepare(
            &blueprint,
            [1; 16],
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    context.validate_at(at).unwrap();
    assert!(
        context
            .bind(
                at,
                123,
                7,
                ifm_market_data_native::reference::decimal::parse("6000").unwrap(),
                true
            )
            .is_ok()
    );
    server.closed.recv().await.unwrap();
    // A distinct source has no warmed cache, so this blocks in real socket I/O.
    while server.seen.try_recv().is_ok() {}
    server.mode.store(1, Ordering::Release);
    let references = Arc::new(References::new(
        Arc::new(Treasury::new(&server.endpoint, resolver(), clock).unwrap()),
        Arc::new(Clock::new(at, false).unwrap()),
    ));
    let stop = CancellationToken::new();
    let first = {
        let references = references.clone();
        let b = blueprint.clone();
        let token = stop.clone();
        tokio::spawn(async move {
            references
                .prepare(&b, [2; 16], &token, Deadline::recovery())
                .await
        })
    };
    server.seen.recv().await.unwrap();
    let second = {
        let references = references.clone();
        let b = blueprint.clone();
        let token = stop.clone();
        tokio::spawn(async move {
            references
                .prepare(&b, [3; 16], &token, Deadline::recovery())
                .await
        })
    };
    tokio::task::yield_now().await;
    stop.cancel();
    for task in [first, second] {
        let error = tokio::time::timeout(Duration::from_secs(1), task)
            .await
            .unwrap()
            .unwrap()
            .err()
            .unwrap();
        assert!(matches!(
            error,
            Error::Acquisition(Failure::Interrupted(Interrupted::Cancelled))
        ));
    }
    tokio::time::timeout(Duration::from_secs(1), server.closed.recv())
        .await
        .unwrap()
        .unwrap();
    // The original absolute deadline also covers cache admission and HTTP reads.
    let deadline = Deadline::after(Duration::from_millis(50));
    let result = references
        .prepare(&blueprint, [4; 16], &CancellationToken::new(), deadline)
        .await;
    assert!(matches!(
        result,
        Err(Error::Acquisition(Failure::Interrupted(
            Interrupted::DeadlineExpired
        )))
    ));
    tokio::time::timeout(Duration::from_secs(1), server.closed.recv())
        .await
        .unwrap()
        .unwrap();
    let before = server.requests.load(Ordering::Acquire);
    assert!(
        references
            .prepare(&blueprint, [5; 16], &CancellationToken::new(), deadline)
            .await
            .is_err()
    );
    assert_eq!(
        server.requests.load(Ordering::Acquire),
        before,
        "expired deadline must not start another HTTP request"
    );
    server.stop().await;
}
