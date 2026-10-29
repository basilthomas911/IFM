//! Core NATS console delivery requires no stream, HTTP setup, acknowledgement or replay.
use ifm_ui_egui::{backend::{codec,schema::Registry,transport::{Backend,Settings}},services::notifications::monitor};
use std::{sync::Arc,time::Duration};
#[tokio::test]
#[ignore="Requires an isolated IFM_STANDALONE_TEST_NATS_URL; JetStream may be disabled"]
async fn core_console_keeps_latest_bounded_rows_when_gui_stalls_without_replay() {
    let url=std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();assert_ne!(url.rsplit(':').next(),Some("4222"));
    let raw=async_nats::connect(url.clone()).await.unwrap();
    let payload=|index:u128| {
        let mut value=codec::decode(include_bytes!("fixtures/backend/status-notification.wire.msgpack")).unwrap();
        let rmpv::Value::Array(fields)=&mut value else {panic!("Invalid fixture")};fields[1]=uuid::Uuid::from_u128(index+1).to_string().into();
        let rmpv::Value::Map(log)=&mut fields[8] else {panic!("Invalid log")};
        log.iter_mut().find(|(k,_)|k.as_str()==Some("Message")).unwrap().1=format!("live console {index}").into();codec::encode(&value).unwrap()
    };
    let subject="Notify.StatusConsoleEvent.Logged.console-capacity-test";
    raw.publish(subject,payload(0).into()).await.unwrap();raw.flush().await.unwrap();
    let backend=Backend::connect(Settings{url,credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();
    let connection=backend.watch_connection();
    let (send,mut receive)=tokio::sync::mpsc::channel(1);let(_actions,actions)=tokio::sync::mpsc::channel(1);
    let task=tokio::spawn(monitor(backend,Arc::new(Registry::compiled().unwrap()),send,actions));
    let first=tokio::time::timeout(Duration::from_secs(5),receive.recv()).await.unwrap().unwrap();
    assert!(!first.status_rows.iter().any(|r|r.message=="live console 0"),"No pre-subscription replay");
    // GUI remains stalled throughout this burst. Intake must keep consuming,
    // coalescing bounded snapshots rather than waiting for painting or ACKs.
    for index in 1..=3000 {raw.publish(subject,payload(index).into()).await.unwrap();}
    raw.publish(subject,payload(3000).into()).await.unwrap();raw.flush().await.unwrap();
    tokio::time::sleep(Duration::from_millis(500)).await;
    let latest=tokio::time::timeout(Duration::from_secs(10),async{loop{let p=receive.recv().await.unwrap();if p.status_rows.iter().any(|r|r.message=="live console 3000"){break p;}}}).await.unwrap();
    assert!(latest.status_rows.len()<=ifm_ui_egui::console_capacity::current());
    assert_eq!(latest.status_rows.iter().filter(|r|r.message=="live console 3000").count(),1);
    assert!(connection.borrow().last_warning.is_none(),"Console intake became a slow NATS consumer");
    raw.publish(subject,vec![0xc1].into()).await.unwrap();
    raw.publish(subject,payload(3001).into()).await.unwrap();raw.flush().await.unwrap();
    let after_invalid=tokio::time::timeout(Duration::from_secs(5),async{loop{let p=receive.recv().await.unwrap();if p.status_rows.iter().any(|r|r.message=="live console 3001"){break p;}}}).await.unwrap();
    assert_eq!(after_invalid.error_sequence,0,"Malformed console input must not terminate intake or open a fatal dialog");
    assert_eq!(after_invalid.status_rows.iter().filter(|r|r.id.starts_with("current-warning|")).count(),1);
    task.abort();let _=task.await;
}
