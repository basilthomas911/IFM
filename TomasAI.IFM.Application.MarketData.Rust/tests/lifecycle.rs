//! Real pinned SDK cancellation against local protocol servers, without live credentials.

use databento::dbn::{MetadataBuilder, encode::dbn::AsyncMetadataEncoder};
use ifm_market_data_native::{
    dataset::databento::{SessionExit, Stage, qualify_owned_session},
    runtime::{Deadline, Interrupted, RECOVERY_BUDGET},
};
use std::{
    sync::Arc,
    sync::atomic::{AtomicUsize, Ordering},
    time::Duration,
};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
    sync::mpsc,
};
use tokio_util::sync::CancellationToken;

#[derive(Clone, Copy, Debug)]
enum Stall {
    Authentication,
    Metadata,
    Read,
    PartialRecord,
}

async fn cancelled_sdk_session_closes_its_socket(stall: Stall, expire: bool) {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    let (reached, stalled) = tokio::sync::oneshot::channel();
    let server = tokio::spawn(async move {
        let (stream, _) = listener.accept().await.unwrap();
        let mut stream = BufReader::new(stream);
        if !matches!(stall, Stall::Authentication) {
            stream
                .get_mut()
                .write_all(b"lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n")
                .await
                .unwrap();
            let mut line = String::new();
            stream.read_line(&mut line).await.unwrap();
            assert!(line.contains("dataset=GLBX.MDP3"));
            stream
                .get_mut()
                .write_all(b"success=1|session_id=5\n")
                .await
                .unwrap();
            line.clear();
            stream.read_line(&mut line).await.unwrap();
            assert!(line.contains("schema=trades"));
            line.clear();
            stream.read_line(&mut line).await.unwrap();
            assert_eq!(line, "start_session\n");
            if matches!(stall, Stall::Read | Stall::PartialRecord) {
                AsyncMetadataEncoder::new(stream.get_mut())
                    .encode(
                        &MetadataBuilder::new()
                            .dataset("GLBX.MDP3")
                            .start(0)
                            .schema(None)
                            .stype_in(None)
                            .stype_out(databento::dbn::SType::InstrumentId)
                            .build(),
                    )
                    .await
                    .unwrap();
                if matches!(stall, Stall::PartialRecord) {
                    // Header advertises a 48-byte trade record; only half arrives.
                    stream
                        .get_mut()
                        .write_all(&[12, 0, 0, 0, 0, 0, 0, 0])
                        .await
                        .unwrap();
                }
            }
        }
        reached.send(()).unwrap();
        let mut byte = [0u8];
        let read = tokio::time::timeout(Duration::from_secs(2), stream.read(&mut byte)).await;
        assert_eq!(
            read.unwrap().unwrap(),
            0,
            "SDK socket remained open after owner completion: {stall:?}"
        );
    });
    let cancellation = CancellationToken::new();
    let (stages, mut observed) = mpsc::channel(8);
    let deadline = Deadline::after(if expire {
        Duration::from_millis(300)
    } else {
        Duration::from_secs(5)
    });
    let owner = tokio::spawn(qualify_owned_session(
        addr,
        "x".repeat(32),
        "GLBX.MDP3".into(),
        cancellation.clone(),
        deadline,
        stages,
    ));
    tokio::time::timeout(Duration::from_secs(2), stalled)
        .await
        .unwrap()
        .unwrap();
    if matches!(stall, Stall::Read | Stall::PartialRecord) {
        tokio::time::timeout(Duration::from_secs(2), async {
            while observed.recv().await != Some(Stage::Reading) {}
        })
        .await
        .unwrap();
    }
    if !expire {
        cancellation.cancel();
    }
    let exit = tokio::time::timeout(Duration::from_secs(2), owner)
        .await
        .unwrap()
        .unwrap();
    assert!(
        matches!(exit, SessionExit::Interrupted(reason) if reason == if expire { Interrupted::DeadlineExpired } else { Interrupted::Cancelled }),
        "{exit:?}"
    );
    server.await.unwrap();
}

#[tokio::test]
async fn cancellation_during_authentication_drops_both_socket_halves() {
    cancelled_sdk_session_closes_its_socket(Stall::Authentication, false).await;
}

#[tokio::test]
async fn cancellation_during_start_abandons_partial_protocol_session() {
    cancelled_sdk_session_closes_its_socket(Stall::Metadata, false).await;
}

#[tokio::test]
async fn blocked_record_read_wakes_on_cancellation_without_polling() {
    cancelled_sdk_session_closes_its_socket(Stall::Read, false).await;
}

#[tokio::test]
async fn partial_dbn_record_is_discarded_and_socket_closed() {
    cancelled_sdk_session_closes_its_socket(Stall::PartialRecord, false).await;
}

#[tokio::test]
async fn owner_deadline_also_cancels_an_unresponsive_gateway() {
    cancelled_sdk_session_closes_its_socket(Stall::Authentication, true).await;
}

#[tokio::test]
async fn retries_consume_the_original_budget_and_drop_owned_resources() {
    struct Resource(Arc<AtomicUsize>);
    impl Drop for Resource {
        fn drop(&mut self) {
            self.0.fetch_sub(1, Ordering::AcqRel);
        }
    }
    let outstanding = Arc::new(AtomicUsize::new(0));
    let deadline = Deadline::after(Duration::from_millis(80));
    let cancellation = CancellationToken::new();
    let start = tokio::time::Instant::now();
    let mut attempts = 0;
    loop {
        attempts += 1;
        let result = deadline
            .run(&cancellation, async {
                outstanding.fetch_add(1, Ordering::AcqRel);
                let _resource = Resource(outstanding.clone());
                tokio::time::sleep(Duration::from_millis(15)).await;
            })
            .await;
        assert_eq!(outstanding.load(Ordering::Acquire), 0);
        if result == Err(Interrupted::DeadlineExpired) {
            break;
        }
    }
    assert!(attempts > 1);
    assert!(start.elapsed() < Duration::from_secs(1));
    assert_eq!(RECOVERY_BUDGET, Duration::from_secs(120));
}

#[tokio::test]
async fn saturated_diagnostic_channel_does_not_delay_cancellation() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    let (stages, _blocked_receiver) = mpsc::channel(1);
    stages.send(Stage::Starting).await.unwrap();
    let token = CancellationToken::new();
    token.cancel();
    let result = qualify_owned_session(
        addr,
        "x".repeat(32),
        "GLBX.MDP3".into(),
        token,
        Deadline::recovery(),
        stages,
    )
    .await;
    assert!(matches!(
        result,
        SessionExit::Interrupted(Interrupted::Cancelled)
    ));
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn independent_deadline_does_not_claim_a_blocked_owner_has_stopped() {
    use std::sync::atomic::AtomicBool;
    struct ReleaseOnDrop(Arc<AtomicBool>);
    impl Drop for ReleaseOnDrop {
        fn drop(&mut self) {
            self.0.store(true, Ordering::Release);
        }
    }
    let release = Arc::new(AtomicBool::new(false));
    // Ensure even a failed assertion releases the deliberately blocked owner.
    let _release_guard = ReleaseOnDrop(release.clone());
    let (entered, observed) = tokio::sync::oneshot::channel();
    let cancellation = CancellationToken::new();
    let child_token = cancellation.clone();
    let deadline = Deadline::after(Duration::from_millis(60));
    let owner = tokio::spawn(async move {
        deadline
            .run(&child_token, async move {
                entered.send(()).unwrap();
                // This models a non-yielding third-party call. It is deliberately
                // forbidden on production owner tasks, and select! cannot preempt it.
                while !release.load(Ordering::Acquire) {
                    std::thread::yield_now();
                }
            })
            .await
    });
    observed.await.unwrap();
    let observer = deadline
        .run(&cancellation, std::future::pending::<()>())
        .await;
    assert_eq!(observer, Err(Interrupted::DeadlineExpired));
    cancellation.cancel();
    assert!(
        !owner.is_finished(),
        "A cancellation signal is not task-exit proof"
    );
    drop(_release_guard);
    tokio::time::timeout(Duration::from_secs(2), owner)
        .await
        .unwrap()
        .unwrap()
        .ok();
}

#[tokio::test]
async fn oversized_budget_cannot_extend_recovery_or_overflow_the_clock() {
    assert!(Deadline::after(Duration::MAX).remaining() <= RECOVERY_BUDGET);
    let result = Deadline::after(Duration::ZERO)
        .run(&CancellationToken::new(), std::future::pending::<()>())
        .await;
    assert_eq!(result, Err(Interrupted::DeadlineExpired));
}

#[tokio::test]
async fn malformed_credentials_are_rejected_without_echoing_or_connecting() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let (stages, mut events) = mpsc::channel(1);
    let result = qualify_owned_session(
        listener.local_addr().unwrap(),
        "do-not-log-this".into(),
        "GLBX.MDP3".into(),
        CancellationToken::new(),
        Deadline::recovery(),
        stages,
    )
    .await;
    assert!(
        matches!(result, SessionExit::ProviderError(message) if message == "Invalid provider credential format")
    );
    assert_eq!(events.recv().await, None);
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn started_blocking_work_cannot_be_reported_as_cleaned_by_async_cancellation() {
    use std::sync::atomic::AtomicBool;
    struct ReleaseOnDrop(Arc<AtomicBool>);
    impl Drop for ReleaseOnDrop {
        fn drop(&mut self) {
            self.0.store(true, Ordering::Release);
        }
    }
    let release = Arc::new(AtomicBool::new(false));
    let release_guard = ReleaseOnDrop(release.clone());
    let active = Arc::new(AtomicBool::new(false));
    let observed_active = active.clone();
    let (entered, started) = tokio::sync::oneshot::channel();
    let job = tokio::task::spawn_blocking(move || {
        active.store(true, Ordering::Release);
        entered.send(()).unwrap();
        // Models the primitive used by Tokio hostname resolution. This is not
        // an injected real OS DNS stall; the dependency source establishes that
        // connection, and the controlled exit keeps this test runner safe.
        while !release.load(Ordering::Acquire) {
            std::thread::yield_now();
        }
        active.store(false, Ordering::Release);
    });
    started.await.unwrap();
    job.abort();
    let token = CancellationToken::new();
    let result = Deadline::after(Duration::from_millis(40))
        .run(&token, std::future::pending::<()>())
        .await;
    assert_eq!(result, Err(Interrupted::DeadlineExpired));
    assert!(observed_active.load(Ordering::Acquire));
    assert!(
        !job.is_finished(),
        "Aborting a started blocking resolver cannot prove cleanup"
    );
    drop(release_guard);
    tokio::time::timeout(Duration::from_secs(2), job)
        .await
        .unwrap()
        .unwrap();
    assert!(!observed_active.load(Ordering::Acquire));
}
