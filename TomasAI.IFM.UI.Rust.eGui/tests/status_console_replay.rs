//! Push delivery remains immediate while displayed-batch acknowledgements retry independently.
use ifm_ui_egui::{backend::{codec,schema::Registry,transport::{Backend,Settings}},services::notifications::monitor_with_console_history};
use std::{io::{Read,Write},net::TcpListener,sync::{Arc,atomic::{AtomicUsize,Ordering}},time::Duration};

#[tokio::test]
#[ignore = "Requires IFM_RECOVERY_TEST_NATS_URL pointing to an isolated broker"]
async fn push_is_immediate_without_polling_and_displayed_batches_ack_independently() {
    let broker=std::env::var("IFM_STANDALONE_TEST_NATS_URL").or_else(|_|std::env::var("IFM_RECOVERY_TEST_NATS_URL")).unwrap();
    assert_ne!(broker.rsplit(':').next(),Some("4222"));
    let backend=Backend::connect(Settings{url:broker.clone(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();
    let listener=TcpListener::bind("127.0.0.1:0").unwrap();
    let endpoint=format!("http://{}/api/status-console",listener.local_addr().unwrap());
    let acknowledgements=Arc::new(AtomicUsize::new(0));let counted=acknowledgements.clone();
    let(ready,connected)=tokio::sync::oneshot::channel();
    let http=std::thread::spawn(move||{
        let mut ready=Some(ready);
        while counted.load(Ordering::SeqCst)<3 {
            let(mut stream,_)=listener.accept().unwrap();stream.set_read_timeout(Some(Duration::from_secs(8))).unwrap();
            let mut request=Vec::new();let mut buffer=[0;4096];
            loop {
                let count=stream.read(&mut buffer).unwrap();assert!(count>0);request.extend_from_slice(&buffer[..count]);assert!(request.len()<8192);
                if let Some(end)=request.windows(4).position(|bytes|bytes==b"\r\n\r\n") {
                    let header=String::from_utf8_lossy(&request[..end]);
                    let length=header.lines().find_map(|line|line.to_ascii_lowercase().strip_prefix("content-length:").map(|v|v.trim().parse::<usize>().unwrap())).unwrap_or(0);
                    if request.len()>=end+4+length{break;}
                }
            }
            let request=String::from_utf8(request).unwrap();
            assert!(!request.starts_with("GET "),"Console must never poll for messages");
            let status=if request.starts_with("POST /api/status-console/connect ") {
                if let Some(ready)=ready.take(){ready.send(()).unwrap();}"200 OK"
            }else{
                assert!(request.starts_with("POST /api/status-console/ack "));
                let payload:serde_json::Value=serde_json::from_str(request.split("\r\n\r\n").nth(1).unwrap()).unwrap();
                let attempt=counted.fetch_add(1,Ordering::SeqCst);
                assert_eq!(payload["acknowledgements"].as_array().unwrap().len(),if attempt==2{1}else{2});
                if attempt==0{"503 Unavailable"}else{"200 OK"}
            };
            write!(stream,"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}").unwrap();
        }
    });
    let(output,mut updates)=tokio::sync::mpsc::channel(32);let(_actions,actions)=tokio::sync::mpsc::channel(32);
    let task=tokio::spawn(monitor_with_console_history(backend,Arc::new(Registry::compiled().unwrap()),output,actions,Some(endpoint)));
    tokio::time::timeout(Duration::from_secs(5),connected).await.unwrap().unwrap();
    let client=async_nats::connect(broker).await.unwrap();
    // Use the broker management protocol so the native client needs only Core NATS.
    // The broker, rather than the test publisher, supplies genuine ACK receipts.
    for (subject,config) in [
        ("$JS.API.STREAM.CREATE.IFM_STATUS_CONSOLE_QUEUE",serde_json::json!({
            "name":"IFM_STATUS_CONSOLE_QUEUE","subjects":["Console.StatusConsoleEvent.Logged"],
            "retention":"workqueue","storage":"memory","max_age":300_000_000_000u64
        })),
        ("$JS.API.CONSUMER.DURABLE.CREATE.IFM_STATUS_CONSOLE_QUEUE.console-ui",serde_json::json!({
            "stream_name":"IFM_STATUS_CONSOLE_QUEUE","config":{
                "durable_name":"console-ui","deliver_subject":"ConsoleDelivery.SingleUI.messages",
                "filter_subject":"Console.StatusConsoleEvent.Logged","ack_policy":"explicit",
                "ack_wait":10_000_000_000u64,"max_ack_pending":100
            }
        }))
    ] {
        let reply=client.request(subject,serde_json::to_vec(&config).unwrap().into()).await.unwrap();
        let reply:serde_json::Value=serde_json::from_slice(&reply.payload).unwrap();
        assert!(reply.get("error").is_none(),"Broker configuration failed: {reply}");
    }
    let payload=|index:u64|{
        let mut value=codec::decode(include_bytes!("fixtures/backend/status-notification.wire.msgpack")).unwrap();
        let rmpv::Value::Array(fields)=&mut value else {panic!("Expected event fields")};fields[1]=uuid::Uuid::from_u128(index as u128).to_string().into();
        let rmpv::Value::Map(log)=&mut fields[8] else {panic!("Expected console log map")};
        log.iter_mut().find(|(key,_)|key.as_str()==Some("Message")).unwrap().1=format!("pushed message {index}").into();
        codec::encode(&value).unwrap()
    };
    for index in 1..=2 {
        client.request("Console.StatusConsoleEvent.Logged",payload(index).into()).await.unwrap();
    }
    client.flush().await.unwrap();
    if task.is_finished(){panic!("Monitor ended: {:?}",task.await);}
    let first=tokio::time::timeout(Duration::from_secs(1),async{
        while let Some(publication)=updates.recv().await {
            if publication.console_deliveries.len()==2{return publication;}
        }
        panic!("No pushed batch delivered")
    }).await.unwrap();
    tokio::time::sleep(Duration::from_millis(100)).await;
    assert_eq!(acknowledgements.load(Ordering::SeqCst),0,"No acknowledgement before render");
    for delivery in &first.console_deliveries {delivery.mark_displayed();}
    tokio::time::timeout(Duration::from_secs(2),async{while acknowledgements.load(Ordering::SeqCst)<1{tokio::time::sleep(Duration::from_millis(5)).await;}}).await.unwrap();

    // A failed ACK is backing off. Live messages must still reach the view immediately, not in a timed batch.
    let started=std::time::Instant::now();
    client.request("Console.StatusConsoleEvent.Logged",payload(3).into()).await.unwrap();
    client.flush().await.unwrap();
    let next=tokio::time::timeout(Duration::from_millis(500),async{
        while let Some(publication)=updates.recv().await {if publication.status_rows.iter().any(|row|row.message=="pushed message 3"){return publication;}}
        panic!("Live push was blocked by acknowledgement retry")
    }).await.unwrap();
    assert!(started.elapsed()<Duration::from_millis(500));
    tokio::time::timeout(Duration::from_secs(4),async{while acknowledgements.load(Ordering::SeqCst)<2{tokio::time::sleep(Duration::from_millis(10)).await;}}).await.unwrap();
    next.console_deliveries.last().unwrap().mark_displayed();
    tokio::time::timeout(Duration::from_secs(3),async{while acknowledgements.load(Ordering::SeqCst)<3{tokio::time::sleep(Duration::from_millis(10)).await;}}).await.unwrap();
    task.abort();let _=task.await;http.join().unwrap();
}
