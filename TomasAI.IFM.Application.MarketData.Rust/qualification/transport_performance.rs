//! Offline actual TCP publisher probe with an explicit two-millisecond broker round-trip delay.
#[allow(dead_code)]
#[path = "cache_performance.rs"]
mod cache;
use ifm_market_data_native::{
    messaging::{
        option_cache::Publication,
        publisher::{BrokerSettings, Publisher},
    },
    runtime::Deadline,
};
use std::{
    sync::{
        Arc,
        atomic::{AtomicU64, Ordering},
    },
    time::{Duration, Instant},
};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
};
fn main() {
    tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .unwrap()
        .block_on(run());
}
async fn run() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let pings = Arc::new(AtomicU64::new(0));
    let records = Arc::new(AtomicU64::new(0));
    let observed = pings.clone();
    let copied = records.clone();
    let server = tokio::spawn(async move {
        let (socket, _) = listener.accept().await.unwrap();
        let (read, mut write) = socket.into_split();
        let mut read = BufReader::new(read);
        write.write_all(b"INFO {\"server_id\":\"fixture\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
        loop {
            let mut line = String::new();
            if read.read_line(&mut line).await.unwrap() == 0 {
                break;
            }
            if line.starts_with("PUB ") {
                let length: usize = line.split_whitespace().last().unwrap().parse().unwrap();
                assert!(length <= 1048576);
                let mut bytes = vec![0; length + 2];
                read.read_exact(&mut bytes).await.unwrap();
                copied.fetch_add(1, Ordering::Relaxed);
            } else if line.starts_with("PING") {
                observed.fetch_add(1, Ordering::Relaxed);
                tokio::time::sleep(Duration::from_millis(2)).await;
                write.write_all(b"PONG\r\n").await.unwrap();
            }
        }
    });
    let mut snapshot = cache::snapshot(1024);
    snapshot.seal().unwrap();
    let publication = Publication::new(&snapshot, 739914, "policy/v1", 1, "fixture").unwrap();
    let chunks = publication.chunks();
    let mut publisher = Publisher::start(
        BrokerSettings::new(
            format!("nats://{address}"),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "probe".into(),
        2,
        256 * 1024,
        Deadline::recovery(),
    )
    .unwrap();
    let mut changes = publisher.changes();
    while !changes.borrow_and_update().connected {
        changes.changed().await.unwrap();
    }
    let mut sequence = 0;
    let mut samples = Vec::new();
    let mut roundtrips = Vec::new();
    for _ in 0..9 {
        let before = pings.load(Ordering::Relaxed);
        let start = Instant::now();
        let mut index = 0;
        while index < chunks {
            #[cfg(not(optimized))]
            let count = {
                publisher.try_option_cache(&publication, index).unwrap();
                1
            };
            #[cfg(optimized)]
            let count = publisher
                .try_option_cache_pair(&publication, index)
                .unwrap();
            index += count;
            sequence += count as u64;
            loop {
                let status = changes.borrow_and_update().clone();
                assert!(status.failure.is_none(), "{:?}", status.detail);
                if status.flushed >= sequence {
                    break;
                }
                changes.changed().await.unwrap();
            }
        }
        samples.push(start.elapsed().as_nanos() as u64);
        roundtrips.push(pings.load(Ordering::Relaxed) - before);
    }
    assert_eq!(records.load(Ordering::Relaxed), chunks as u64 * 9);
    publisher.shutdown(Deadline::recovery()).await.unwrap();
    server.await.unwrap();
    samples.sort_unstable();
    println!(
        "{{\"scenario\":\"cache-delivery-local-2ms-rtt\",\"chunksPerSample\":{chunks},\"p50Ns\":{},\"p95Ns\":{},\"samplesNs\":{samples:?},\"roundtripsPerSample\":{roundtrips:?}}}",
        samples[4], samples[8]
    );
}
