//! Isolated HTTP + NATS acceptance for the actual background console consumer.
use ifm_ui_egui::{
    backend::{
        schema::Registry,
        transport::{Backend, Settings},
    },
    services::notifications::monitor_with_console_history,
};
use serde_json::json;
use std::{
    io::{Read, Write},
    net::TcpListener,
    sync::Arc,
    time::Duration,
};

#[tokio::test]
#[ignore = "Requires IFM_RECOVERY_TEST_NATS_URL pointing to an isolated broker"]
async fn replay_catches_up_without_blocking_live_notifications_and_deduplicates_the_overlap() {
    let broker = std::env::var("IFM_STANDALONE_TEST_NATS_URL")
        .or_else(|_| std::env::var("IFM_RECOVERY_TEST_NATS_URL"))
        .unwrap();
    assert_ne!(
        broker.rsplit(':').next(),
        Some("4222"),
        "Never publish fixtures to Development NATS"
    );
    let backend = Backend::connect(Settings {
        url: broker.clone(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let endpoint = format!(
        "http://{}/api/status-console",
        listener.local_addr().unwrap()
    );
    let (entered, ready) = tokio::sync::oneshot::channel();
    let (release, held) = std::sync::mpsc::channel();
    let http = std::thread::spawn(move || {
        let mut entered = Some(entered);
        for page in 0..2 {
            let (mut stream, _) = listener.accept().unwrap();
            let mut bytes = [0; 4096];
            let count = stream.read(&mut bytes).unwrap();
            let request = String::from_utf8_lossy(&bytes[..count]);
            let body=if page==0 {
                assert!(request.contains("after=0"));entered.take().unwrap().send(()).unwrap();held.recv_timeout(Duration::from_secs(8)).unwrap();
                json!({"nextSequence":2,"throughSequence":3,"hasMore":true,"messages":[
                    {"sequence":1,"id":"11111111-2222-3333-4444-555555555555","statusDate":"2026-10-26T12:00:00Z","message":"startup before UI"},
                    {"sequence":2,"id":"00112233-4455-6677-8899-aabbccddeeff","statusDate":"2026-10-26T12:30:00Z","message":"Option subscriptions restored"}]})
            }else{
                assert!(request.contains("after=2"));assert!(request.contains("through=3"));
                json!({"nextSequence":3,"throughSequence":3,"hasMore":false,"messages":[
                    {"sequence":3,"id":"22222222-2222-3333-4444-555555555555","statusDate":"2026-10-26T12:10:00Z","message":"second retained startup message"}]})
            }.to_string();
            write!(stream,"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",body.len()).unwrap();
        }
    });
    let (output, mut updates) = tokio::sync::mpsc::channel(32);
    let (_actions, actions) = tokio::sync::mpsc::channel(32);
    let task = tokio::spawn(monitor_with_console_history(
        backend,
        Arc::new(Registry::compiled().unwrap()),
        output,
        actions,
        Some(endpoint),
    ));
    tokio::time::timeout(Duration::from_secs(5), ready)
        .await
        .unwrap()
        .unwrap();
    let client = async_nats::connect(broker).await.unwrap();
    client
        .publish(
            "Notify.StatusConsoleEvent.Logged.none",
            include_bytes!("fixtures/backend/status-notification.wire.msgpack")
                .to_vec()
                .into(),
        )
        .await
        .unwrap();
    client.flush().await.unwrap();
    // The first HTTP page is deliberately held: live delivery must remain responsive.
    tokio::time::timeout(Duration::from_secs(3), async {
        while let Some(state) = updates.recv().await {
            if state
                .status_rows
                .iter()
                .any(|row| row.message == "Option subscriptions restored")
            {
                return;
            }
        }
        panic!("Live notification was blocked by replay")
    })
    .await
    .unwrap();
    release.send(()).unwrap();
    let state = tokio::time::timeout(Duration::from_secs(5), async {
        while let Some(state) = updates.recv().await {
            if state
                .status_rows
                .iter()
                .any(|row| row.message == "second retained startup message")
            {
                return state;
            }
        }
        panic!("Replay stopped before catch-up")
    })
    .await
    .unwrap();
    assert_eq!(
        state
            .status_rows
            .iter()
            .filter(|row| row.message == "Option subscriptions restored")
            .count(),
        1
    );
    let console: Vec<_> = state
        .status_rows
        .iter()
        .filter(|row| row.id.as_bytes().get(4) == Some(&b'-'))
        .map(|row| row.message.as_str())
        .collect();
    assert_eq!(
        console,
        [
            "startup before UI",
            "second retained startup message",
            "Option subscriptions restored"
        ]
    );
    task.abort();
    let _ = task.await;
    http.join().unwrap();
}
