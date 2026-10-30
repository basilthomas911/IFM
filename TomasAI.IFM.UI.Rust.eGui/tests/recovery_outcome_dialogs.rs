use ifm_ui_egui::{backend::schema::Registry,services::notifications::Notifications,status_dialog::{self,Severity}};

fn outcome(id:uuid::Uuid,severity:&str)->Vec<u8>{
    serde_json::to_vec(&serde_json::json!({"Version":1,"RecordId":id.to_string(),"OperationId":uuid::Uuid::from_u128(42).to_string(),"Role":"Options","Dataset":"GLBX.MDP3","ValueDate":"2026-10-29","OccurredAtUtc":"2026-10-29T12:00:00Z","Severity":severity,"MonitoringReady":false,"Message":"Options reset completed. Connection restored; monitoring is not yet verified."})).unwrap()
}
#[test]
fn completion_is_deduplicated_and_only_the_latest_outcome_is_retained(){
    let registry=Registry::compiled().unwrap();let mut n=Notifications::default();
    n.error("failed-operation".into(),"Recovery deadline expired; resetting dataset".into());
    let sequence=n.publication.error_sequence;let error=n.publication.error.clone();
    let body=outcome(uuid::Uuid::from_u128(1),"success");
    let subject="Notify.MarketDataRecoveryOutcome.2026-10-29";
    assert!(n.event(&registry,subject,&body).unwrap());
    assert!(!n.event(&registry,subject,&body).unwrap());
    assert_eq!(n.publication.error_sequence,sequence);assert_eq!(n.publication.error,error);
    assert_eq!(n.publication.outcome_dialogs.len(),1);
    assert_eq!(n.publication.outcome_dialogs[0].severity,Severity::Success);
    assert!(n.publication.outcome_dialogs[0].message.contains("not yet verified"));
    assert!(n.event(&registry,subject,&outcome(uuid::Uuid::from_u128(2),"warning")).unwrap());
    assert_eq!(n.publication.outcome_dialogs.len(),1);
    assert_eq!(n.publication.outcome_dialogs[0].severity,Severity::Warning);
}
#[test]
fn a_burst_retains_one_latest_dialog_and_retries_do_not_restore_older_messages(){
    let registry=Registry::compiled().unwrap();let mut n=Notifications::default();
    let subject="Notify.MarketDataRecoveryOutcome.2026-10-29";
    for id in 1..=100 {
        assert!(n.event(&registry,subject,&outcome(uuid::Uuid::from_u128(id),"success")).unwrap());
        assert_eq!(n.publication.outcome_dialogs.len(),1);
        assert_eq!(n.publication.outcome_dialogs[0].sequence,id as u64);
    }
    assert!(!n.event(&registry,subject,&outcome(uuid::Uuid::from_u128(1),"success")).unwrap());
    assert_eq!(n.publication.outcome_dialogs[0].sequence,100);
}
#[test]
fn invalid_outcomes_never_open_a_dialog(){
    let registry=Registry::compiled().unwrap();let mut n=Notifications::default();
    for body in [outcome(uuid::Uuid::from_u128(1),"unknown"),b"{bad".to_vec(),vec![b'x';16385]]{
        assert!(n.event(&registry,"Notify.MarketDataRecoveryOutcome.2026-10-29",&body).is_err());
    }
    assert!(n.event(&registry,"Notify.MarketDataRecoveryOutcome.2026-10-28",&outcome(uuid::Uuid::from_u128(1),"success")).is_err());
    assert!(n.publication.outcome_dialogs.is_empty());
}
#[test]
fn all_status_icons_have_equal_bounds_and_ok_is_centered(){
    for severity in [Severity::Error,Severity::Success,Severity::Warning]{
        let ctx=eframe::egui::Context::default();
        let input=eframe::egui::RawInput{screen_rect:Some(eframe::egui::Rect::from_min_size(eframe::egui::Pos2::ZERO,eframe::egui::vec2(1000.0,700.0))),..Default::default()};
        let _=ctx.run(input,|ctx|{eframe::egui::CentralPanel::default().show(ctx,|ui|{
            let layout=status_dialog::body(ui,"A long recovery message that wraps across several lines and retains its operation identity.",severity);
            assert_eq!(layout.icon.size(),eframe::egui::Vec2::splat(56.0));
            assert!((layout.ok.center().x-layout.content.center().x).abs()<1.0);
            assert!(layout.icon.center().x>layout.content.center().x);
        });});
    }
}

#[tokio::test]
#[ignore="Requires IFM_STANDALONE_TEST_NATS_URL pointing to an isolated NATS broker"]
async fn actual_nats_push_reaches_the_dialog_while_queries_are_unavailable(){
    use ifm_ui_egui::backend::transport::{Backend,Settings};
    use std::{sync::Arc,time::Duration};
    let url=std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    let backend=Backend::connect(Settings{url:url.clone(),credentials_file:None,token:None,request_timeout:Duration::from_secs(5)}).await.unwrap();
    let raw=async_nats::connect(url).await.unwrap();
    let (send,mut receive)=tokio::sync::mpsc::channel(32);let (_actions,actions)=tokio::sync::mpsc::channel(32);
    let task=tokio::spawn(ifm_ui_egui::services::notifications::monitor(backend,Arc::new(Registry::compiled().unwrap()),send,actions));
    // The first publication follows subscription setup and its broker flush.
    tokio::time::timeout(Duration::from_secs(5),receive.recv()).await.unwrap().unwrap();
    raw.publish("Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26",include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack").as_slice().to_vec().into()).await.unwrap();
    let failure=tokio::time::timeout(Duration::from_secs(5),async{loop{let state=receive.recv().await.unwrap();if state.error_sequence>0{break state;}}}).await.unwrap();
    let bytes=outcome(uuid::Uuid::from_u128(1),"success");
    raw.publish("Notify.MarketDataRecoveryOutcome.2026-10-29",bytes.clone().into()).await.unwrap();
    raw.publish("Notify.MarketDataRecoveryOutcome.2026-10-29",bytes.into()).await.unwrap();raw.flush().await.unwrap();
    let completed=tokio::time::timeout(Duration::from_secs(5),async{loop{let state=receive.recv().await.unwrap();if !state.outcome_dialogs.is_empty(){break state;}}}).await.unwrap();
    assert_eq!(completed.outcome_dialogs.len(),1);assert_eq!(completed.outcome_dialogs[0].severity,Severity::Success);
    assert_eq!(completed.error_sequence,failure.error_sequence);assert_eq!(completed.error,failure.error);
    task.abort();let _=task.await;
}
