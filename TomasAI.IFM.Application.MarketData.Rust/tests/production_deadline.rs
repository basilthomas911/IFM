//! Wall-clock qualification of the production two-minute cancellation budget.

use ifm_market_data_native::{
    dataset::databento::{SessionExit, qualify_owned_session},
    runtime::{Deadline, Interrupted, RECOVERY_BUDGET},
};
use std::time::Duration;
use tokio::{io::AsyncReadExt, net::TcpListener, sync::mpsc};
use tokio_util::sync::CancellationToken;

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Real 120-second SDK stall; run explicitly during lifecycle qualification"]
async fn production_deadline_expires_without_waiting_for_provider_data() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    let (connected, accepted) = tokio::sync::oneshot::channel();
    let server = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        connected.send(()).unwrap();
        let mut byte = [0u8];
        assert_eq!(
            tokio::time::timeout(
                RECOVERY_BUDGET + Duration::from_secs(5),
                socket.read(&mut byte)
            )
            .await
            .unwrap()
            .unwrap(),
            0
        );
    });
    let (stages, _received) = mpsc::channel(8);
    let cancellation = CancellationToken::new();
    let deadline = Deadline::recovery();
    let started = tokio::time::Instant::now();
    let owner = tokio::spawn(qualify_owned_session(
        addr,
        "x".repeat(32),
        "GLBX.MDP3".into(),
        cancellation.clone(),
        deadline,
        stages,
    ));
    accepted.await.unwrap();
    // An independent observer has the original deadline. Owner cleanup cannot
    // delay this result; a production UI publisher will consume this evidence.
    let observed = deadline
        .run(&cancellation, std::future::pending::<()>())
        .await;
    assert_eq!(observed, Err(Interrupted::DeadlineExpired));
    let notified_after = started.elapsed();
    assert!(notified_after >= RECOVERY_BUDGET);
    assert!(notified_after < RECOVERY_BUDGET + Duration::from_secs(2));
    assert!(matches!(
        tokio::time::timeout(Duration::from_secs(2), owner)
            .await
            .unwrap()
            .unwrap(),
        SessionExit::Interrupted(Interrupted::DeadlineExpired)
    ));
    server.await.unwrap();
    eprintln!(
        "production_budget_seconds=120; terminal_observed_seconds={:.6}; peer_socket_closed=true",
        notified_after.as_secs_f64()
    );
}
