//! Qualification of publisher queue capacity and independent connections.
use ifm_market_data_native::{
    dataset::transport::Lane,
    messaging::{
        publisher::{BrokerSettings, Publisher},
        wire::Frame,
    },
    runtime::Deadline,
};
use std::time::{Duration, Instant};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::{TcpListener, TcpStream},
    sync::{oneshot, watch},
};

const PRODUCTION_QUEUE: usize = 256;
const PRODUCTION_BYTES: usize = 4 * 1024 * 1024;

fn budget() -> Deadline {
    Deadline::after(Duration::from_secs(8))
}

fn frame(sequence: usize, generation: u8) -> Frame {
    Frame {
        lane: Lane::OptionsGlbx,
        value_date: 739915,
        worker: [1; 16],
        generation: [generation; 16],
        manifest_revision: 1,
        sequence: sequence as i64,
        kind: 7,
        payload: vec![0x92, 0xc0, 0xc3],
    }
}

async fn publisher(endpoint: &str, capacity: usize) -> Publisher {
    let publisher = Publisher::start(
        BrokerSettings::new(endpoint.to_owned(), vec!["127.0.0.1:1".parse().unwrap()]).unwrap(),
        "queue-qualification".into(),
        capacity,
        PRODUCTION_BYTES,
        budget(),
    )
    .unwrap();
    let mut changes = publisher.changes();
    tokio::time::timeout(Duration::from_secs(3), async {
        while !changes.borrow_and_update().connected {
            changes.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    publisher
}

// Models latency or a stall on a single broker connection. Broker flush is
// transport evidence; a downstream subscriber's processing is independent.
async fn peer(
    socket: TcpStream,
    delay: Option<Duration>,
    first: Option<oneshot::Sender<()>>,
) -> Vec<Vec<u8>> {
    socket.set_nodelay(true).unwrap();
    let (read, mut write) = socket.into_split();
    let mut read = BufReader::new(read);
    write.write_all(b"INFO {\"server_id\":\"fixture\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
    let mut line = String::new();
    let mut first = first;
    let mut packets = Vec::new();
    loop {
        line.clear();
        match read.read_line(&mut line).await {
            Ok(0) | Err(_) => break,
            Ok(_) => {}
        }
        if line.starts_with("PUB ") {
            let count = line
                .split_whitespace()
                .last()
                .unwrap()
                .parse::<usize>()
                .unwrap();
            assert!(count <= 1_048_576);
            let mut payload = vec![0; count + 2];
            if read.read_exact(&mut payload).await.is_err() {
                break;
            }
            payload.truncate(count);
            packets.push(payload);
            if let Some(first) = first.take() {
                first.send(()).unwrap();
            }
        } else if line.starts_with("PING") {
            if !packets.is_empty() {
                let Some(delay) = delay else { continue };
                tokio::time::sleep(delay).await;
            }
            if write.write_all(b"PONG\r\n").await.is_err() {
                break;
            }
        }
    }
    packets
}

async fn flushed(publisher: &Publisher, target: u64) {
    let mut changes = publisher.changes();
    tokio::time::timeout(Duration::from_secs(4), async {
        loop {
            let status = changes.borrow_and_update().clone();
            assert!(status.failure.is_none(), "{status:?}");
            if status.flushed >= target {
                break;
            }
            changes.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
}

#[tokio::test]
async fn a_stalled_socket_does_not_hold_another_queue_on_the_same_broker_address() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("nats://{}", listener.local_addr().unwrap());
    let (published, observed) = oneshot::channel();
    let broker = tokio::spawn(async move {
        let (stalled, _) = listener.accept().await.unwrap();
        let stalled = tokio::spawn(peer(stalled, None, Some(published)));
        let (independent, _) = listener.accept().await.unwrap();
        let independent = tokio::spawn(peer(independent, Some(Duration::ZERO), None));
        let (replacement, _) = listener.accept().await.unwrap();
        let replacement = tokio::spawn(peer(replacement, Some(Duration::ZERO), None));
        (
            stalled.await.unwrap(),
            independent.await.unwrap(),
            replacement.await.unwrap(),
        )
    });
    let mut blocked = publisher(&endpoint, 1).await;
    let mut healthy = publisher(&endpoint, 1).await;
    blocked.try_publish(&frame(1, 2)).unwrap();
    tokio::time::timeout(Duration::from_secs(2), observed)
        .await
        .unwrap()
        .unwrap();
    blocked.try_publish(&frame(2, 2)).unwrap();
    assert!(blocked.try_publish(&frame(3, 2)).is_err());
    healthy.try_publish(&frame(9, 2)).unwrap();
    flushed(&healthy, 1).await;
    assert_eq!(blocked.status().flushed, 0);
    let blocked_counters = blocked.port().counters();
    assert_eq!(blocked_counters["Publication frames flushed"], 0);
    assert_eq!(blocked_counters["Publication queue capacity"], 1);
    assert_eq!(blocked_counters["Publication queue pending"], 1);
    assert!(blocked_counters["Publication retained bytes"] > 0);
    assert_eq!(healthy.port().counters()["Publication frames flushed"], 1);
    assert!(blocked.retained_bytes() <= PRODUCTION_BYTES);
    blocked.shutdown(budget()).await.unwrap();
    assert_eq!(blocked.retained_bytes(), 0);

    // A new queue can flush authoritative records after the caller retains and
    // resubmits them with the replacement generation.
    let mut replacement = publisher(&endpoint, 2).await;
    for sequence in 1..=2 {
        replacement.try_publish(&frame(sequence, 3)).unwrap();
    }
    flushed(&replacement, 2).await;
    healthy.shutdown(budget()).await.unwrap();
    replacement.shutdown(budget()).await.unwrap();
    let (stalled, healthy, replacement) = broker.await.unwrap();
    assert_eq!(stalled, [frame(1, 2).encode().unwrap()]);
    assert_eq!(healthy, [frame(9, 2).encode().unwrap()]);
    assert_eq!(
        replacement,
        [frame(1, 3).encode().unwrap(), frame(2, 3).encode().unwrap()]
    );
}

struct Workload {
    name: &'static str,
    records: usize,
    wave: usize,
    between_waves: Duration,
    round_trip_delay: Duration,
}

#[tokio::test]
#[ignore = "Five paired timing runs; execute in isolation with --ignored --nocapture"]
async fn live_publication_queue_capacity_pairs() {
    let workloads = [
        Workload {
            name: "quiet",
            records: 24,
            wave: 1,
            between_waves: Duration::from_millis(3),
            round_trip_delay: Duration::from_millis(2),
        },
        Workload {
            name: "normal",
            records: 256,
            wave: 16,
            between_waves: Duration::from_millis(2),
            round_trip_delay: Duration::from_millis(2),
        },
        Workload {
            name: "burst_delayed_flush",
            records: 640,
            wave: 640,
            between_waves: Duration::ZERO,
            round_trip_delay: Duration::from_millis(10),
        },
    ];
    for workload in workloads {
        // Warm both capacities outside recorded pairs.
        for capacity in [PRODUCTION_QUEUE, 128] {
            tokio::time::timeout(Duration::from_secs(8), probe(&workload, 0, capacity))
                .await
                .unwrap();
        }
        for pair in 1..=5 {
            let capacities = if pair % 2 == 0 {
                [128, PRODUCTION_QUEUE]
            } else {
                [PRODUCTION_QUEUE, 128]
            };
            for capacity in capacities {
                tokio::time::timeout(Duration::from_secs(8), probe(&workload, pair, capacity))
                    .await
                    .unwrap();
            }
        }
    }
}

async fn probe(workload: &Workload, pair: usize, capacity: usize) {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("nats://{}", listener.local_addr().unwrap());
    let delay = workload.round_trip_delay;
    let receiver = tokio::spawn(async move {
        let (socket, _) = listener.accept().await.unwrap();
        peer(socket, Some(delay), None).await
    });
    let mut publisher = publisher(&endpoint, capacity).await;
    let mut changes = publisher.changes();
    let mut accepted = Vec::with_capacity(workload.records);
    let mut queue_lags = Vec::with_capacity(workload.records);
    let mut offered_lags = Vec::with_capacity(workload.records);
    let mut rejected = 0;
    let mut peak = 0;
    let start = Instant::now();
    let mut offered = start;
    for sequence in 1..=workload.records {
        if (sequence - 1) % workload.wave == 0 {
            offered = Instant::now();
        }
        let value = frame(sequence, 2);
        loop {
            observe(&mut changes, &accepted, &mut queue_lags, &mut offered_lags);
            match publisher.try_publish(&value) {
                Ok(()) => {
                    accepted.push((offered, Instant::now()));
                    peak = peak.max(publisher.retained_bytes());
                    break;
                }
                Err("Native publication queue unavailable") => {
                    rejected += 1;
                    changes.changed().await.unwrap();
                }
                Err(error) => panic!("{error}"),
            }
        }
        if sequence % workload.wave == 0 && !workload.between_waves.is_zero() {
            tokio::time::sleep(workload.between_waves).await;
        }
    }
    while queue_lags.len() < workload.records {
        observe(&mut changes, &accepted, &mut queue_lags, &mut offered_lags);
        if queue_lags.len() < workload.records {
            changes.changed().await.unwrap();
        }
    }
    let elapsed = start.elapsed().as_micros();
    publisher.shutdown(budget()).await.unwrap();
    let packets = receiver.await.unwrap();
    assert_eq!(packets.len(), workload.records);
    for (index, packet) in packets.iter().enumerate() {
        assert_eq!(*packet, frame(index + 1, 2).encode().unwrap());
    }
    assert_eq!(publisher.retained_bytes(), 0);
    if pair > 0 {
        queue_lags.sort_unstable();
        offered_lags.sort_unstable();
        let percentile =
            |lags: &[u128], percent: usize| lags[(workload.records * percent).div_ceil(100) - 1];
        println!(
            "{{\"scenario\":\"{}\",\"pair\":{pair},\"capacity\":{capacity},\"records\":{},\"elapsedUs\":{elapsed},\"offeredP50Us\":{},\"offeredP95Us\":{},\"offeredP99Us\":{},\"queuedP99Us\":{},\"admissionRetries\":{rejected},\"peakRetainedBytes\":{peak}}}",
            workload.name,
            workload.records,
            percentile(&offered_lags, 50),
            percentile(&offered_lags, 95),
            percentile(&offered_lags, 99),
            percentile(&queue_lags, 99)
        );
    }
}

fn observe(
    changes: &mut watch::Receiver<ifm_market_data_native::messaging::publisher::Status>,
    accepted: &[(Instant, Instant)],
    queue_lags: &mut Vec<u128>,
    offered_lags: &mut Vec<u128>,
) {
    let status = changes.borrow_and_update();
    assert!(status.failure.is_none(), "{:?}", status.detail);
    let now = Instant::now();
    while queue_lags.len() < status.flushed as usize {
        let (offered, queued) = accepted[queue_lags.len()];
        offered_lags.push(now.duration_since(offered).as_micros());
        queue_lags.push(now.duration_since(queued).as_micros());
    }
}
