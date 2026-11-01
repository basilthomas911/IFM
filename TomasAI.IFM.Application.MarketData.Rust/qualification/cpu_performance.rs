//! Conditional CPU executor experiment. Timings include work completion; all prototype threads are joined.
use ifm_market_data_native::{
    pricing::{Calculator, Request, Settings, Values},
    runtime::Deadline,
};
use std::{
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::{Duration, Instant},
};
use tokio::sync::{mpsc, oneshot};
struct Job {
    request: Request,
    sigma: f64,
    deadline: Deadline,
    reply: oneshot::Sender<Values>,
}
fn request() -> Request {
    Request {
        forward: 5000.,
        strike: 5000.,
        time: 30. / 365.,
        rate: 0.04,
        call: true,
        american: true,
        futures_style: false,
    }
}
async fn sample(mode: u8) -> (serde_json::Value, Vec<Values>) {
    let calculator = Arc::new(Calculator::new(Settings::default()).unwrap());
    let (sender, receiver) = mpsc::channel::<Job>(2);
    let receiver = Arc::new(Mutex::new(receiver));
    let (ready, mut readiness) = mpsc::channel(2);
    let mut threads = Vec::new();
    if mode == 1 {
        for _ in 0..2 {
            let receiver = receiver.clone();
            let ready = ready.clone();
            threads.push(std::thread::spawn(move || {
                let runtime = tokio::runtime::Builder::new_current_thread()
                    .enable_all()
                    .build()
                    .unwrap();
                let calculator = Calculator::new(Settings::default()).unwrap();
                runtime
                    .block_on(calculator.price(
                        request(),
                        0.2,
                        &Default::default(),
                        Deadline::recovery(),
                    ))
                    .unwrap();
                ready.blocking_send(()).unwrap();
                loop {
                    let Some(job) = receiver.lock().unwrap().blocking_recv() else {
                        break;
                    };
                    let result = runtime
                        .block_on(calculator.price(
                            job.request,
                            job.sigma,
                            &Default::default(),
                            job.deadline,
                        ))
                        .unwrap();
                    let _ = job.reply.send(result);
                }
            }));
        }
        for _ in 0..2 {
            readiness.recv().await.unwrap();
        }
    } else {
        calculator
            .price(request(), 0.2, &Default::default(), Deadline::recovery())
            .await
            .unwrap();
    }
    let running = Arc::new(AtomicBool::new(true));
    let alive = running.clone();
    let ticker = tokio::spawn(async move {
        let mut interval = tokio::time::interval_at(
            tokio::time::Instant::now() + Duration::from_millis(1),
            Duration::from_millis(1),
        );
        interval.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
        let mut delays = Vec::new();
        while alive.load(Ordering::Relaxed) {
            let due = interval.tick().await;
            delays.push(due.elapsed().as_nanos() as u64);
        }
        delays
    });
    tokio::task::yield_now().await;
    let start = Instant::now();
    let deadline = Deadline::recovery();
    let jobs: usize = if mode == 0 { 128 } else { 256 };
    let mut results = Vec::with_capacity(jobs);
    if mode == 2 {
        let slots = Arc::new(tokio::sync::Semaphore::new(2));
        let mut tasks = Vec::new();
        for index in 0..jobs {
            let permit = slots.clone().acquire_owned().await.unwrap();
            let calculator = calculator.clone();
            tasks.push(tokio::spawn(async move {
                let _permit = permit;
                calculator
                    .price(
                        request(),
                        0.2 + (index % 4) as f64 * 0.01,
                        &Default::default(),
                        deadline,
                    )
                    .await
                    .unwrap()
            }));
        }
        for task in tasks {
            results.push(task.await.unwrap());
        }
        drop(sender);
    } else if mode == 1 {
        let mut receipts = Vec::new();
        for index in 0..jobs {
            let (reply, receipt) = oneshot::channel();
            sender
                .send(Job {
                    request: request(),
                    sigma: 0.2 + (index % 4) as f64 * 0.01,
                    deadline,
                    reply,
                })
                .await
                .unwrap();
            receipts.push(receipt);
        }
        drop(sender);
        for receipt in receipts {
            results.push(receipt.await.unwrap());
        }
    } else {
        for index in 0..jobs {
            results.push(
                calculator
                    .price(
                        request(),
                        0.2 + (index % 4) as f64 * 0.01,
                        &Default::default(),
                        deadline,
                    )
                    .await
                    .unwrap(),
            );
        }
        drop(sender);
    }
    let elapsed = start.elapsed().as_nanos() as u64;
    running.store(false, Ordering::Relaxed);
    let mut delays = ticker.await.unwrap();
    for thread in threads {
        thread.join().unwrap();
    }
    delays.sort_unstable();
    let p99 = delays[(delays.len() * 99 / 100).min(delays.len() - 1)];
    (
        serde_json::json!({"mode":match mode {0=>"cooperative",1=>"two-bounded-workers",_=>"two-cooperative-tasks"},"jobs":jobs,"totalNs":elapsed,"timerP99Ns":p99,"timerMaxNs":delays.last().unwrap(),"timerSamples":delays.len()}),
        results,
    )
}
async fn run() {
    let mut output = Vec::new();
    let mut expected: Option<Vec<Values>> = None;
    let modes = if std::env::args().any(|argument| argument == "--cooperative-only") {
        vec![0, 2]
    } else {
        vec![0, 1, 2]
    };
    for pair in 0..5 {
        let mut order = modes.clone();
        if pair % 2 != 0 {
            order.reverse();
        }
        for mode in order {
            let (value, results) = sample(mode).await;
            if let Some(expected) = &expected {
                for (index, result) in results.iter().enumerate() {
                    assert_eq!(&expected[index % 4], result);
                }
            } else {
                expected = Some(results[..4].to_vec());
            }
            output.push(value);
        }
    }
    println!(
        "{}",
        serde_json::json!({"scenario":"conditional-american-cpu-workers","note":"128 serial versus 256 parallel full-Greek repricings for similar-duration timer windows; 1 ms control timer; identical four-case numerical cycle","runs":output})
    );
}
fn main() {
    let multi = std::env::args().any(|argument| argument == "--multi");
    if multi {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .worker_threads(2)
            .enable_all()
            .build()
            .unwrap();
        let task = runtime.spawn(run());
        runtime.block_on(task).unwrap();
    } else {
        tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap()
            .block_on(run());
    }
}
