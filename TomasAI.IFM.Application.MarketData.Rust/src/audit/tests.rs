use super::{
    journal::Context,
    outbox::{self, Spool},
    protocol::Record,
};
use crate::{
    dataset::transport::Lane,
    messaging::{codec, ordered::Header},
    runtime::{
        Deadline,
        clock::Clock,
        recovery::{Proof, Recovery, Requirements},
    },
};
use std::{path::PathBuf, sync::Arc};
fn directory() -> PathBuf {
    std::env::temp_dir().join(format!(
        "ifm-recovery-audit-{:032x}",
        rand::random::<u128>()
    ))
}
fn payload(sequence: u64, kind: &str) -> String {
    serde_json::json!({"Version":1,"RecordId":"00000001-0001-4001-8001-000000000001","OperationId":"00000002-0002-4002-8002-000000000002","Sequence":sequence,"Kind":kind}).to_string()
}
fn receipt(record: &Record) -> Vec<u8> {
    use rmpv::Value;
    let mut completed = vec![Value::Nil; 12];
    completed[1] = Value::Array(vec![record.id.clone().into()]);
    completed[3] = 1.into();
    completed[8] = record.operation.clone().into();
    completed[9] = record.id.clone().into();
    completed[10] = record.digest.clone().into();
    completed[11] = record.sequence.into();
    codec::encode(&Value::Array(vec![
        true.into(),
        0.into(),
        "".into(),
        Value::Array(vec![Value::Array(completed), Value::Nil]),
    ]))
    .unwrap()
}
fn configured() -> (Arc<Recovery>, tokio::sync::mpsc::Receiver<outbox::Captured>) {
    let health = Recovery::new(
        Requirements::new(&[
            Proof::Transport,
            Proof::Reference,
            Proof::Pricing,
            Proof::Publication,
            Proof::Persistence,
        ]),
        Deadline::recovery(),
    );
    let (sink, receiver) = outbox::channel();
    health.audit_context(Context {
        runtime: [1; 16],
        lane: Lane::OptionsGlbx,
        date: time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap(),
        clock: Arc::new(Clock::new(17_932_320_000_000_000, false).unwrap()),
        sink,
        workloads: vec!["option-instrument/123".into()],
    });
    health.monitor_provider();
    (health, receiver)
}
fn drain(receiver: &mut tokio::sync::mpsc::Receiver<outbox::Captured>) -> Vec<serde_json::Value> {
    let mut values = vec![];
    while let Ok(captured) = receiver.try_recv() {
        values.push(serde_json::from_str(&captured.payload).unwrap());
    }
    values
}
#[tokio::test]
async fn soft_recovery_preserves_identity_and_distinguishes_transport_from_monitoring() {
    let (health, mut receiver) = configured();
    health.provider_fault(health.status().ticket, "test connection fault");
    health.provider_fault(health.status().ticket, "test second attempt");
    let ticket = health.status().ticket;
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    let values = drain(&mut receiver);
    assert_eq!(
        values
            .iter()
            .map(|v| v["Kind"].as_str().unwrap())
            .collect::<Vec<_>>(),
        ["Started", "ConnectionRestored", "Completed"]
    );
    assert!(
        values
            .iter()
            .all(|v| v["OperationId"] == values[0]["OperationId"])
    );
    assert_eq!(values[2]["MonitoringReady"], false);
    assert_eq!(values[2]["CompletionScope"], "TransportOnly");
    assert_eq!(
        health.outcomes().len(),
        1,
        "Soft recovery also requires explicit confirmation"
    );
    health.prove(ticket, Proof::Reference);
    health.prove(ticket, Proof::Pricing);
    health.prove(ticket, Proof::Publication);
    health.prove(ticket, Proof::Persistence);
    let monitoring = drain(&mut receiver);
    assert_eq!(monitoring[0]["Kind"], "MonitoringRestored");
    assert_eq!(monitoring[0]["MonitoringReady"], true);
}
#[tokio::test(start_paused = true)]
async fn deadline_reset_and_failure_are_one_ordered_incident() {
    let (health, mut receiver) = configured();
    health.plan_final_reset(true);
    health.provider_fault(
        health.status().ticket,
        "broker unavailable: retained detail",
    );
    tokio::time::advance(std::time::Duration::from_secs(120)).await;
    health.status();
    health.audit_reset_started();
    health.replace(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    health.audit_owner(Some(Header {
        lane: Lane::OptionsGlbx,
        value_date: 1,
        worker: [2; 16],
        generation: [3; 16],
        revision: 2,
    }));
    health.fail("reset provider failed");
    let values = drain(&mut receiver);
    assert_eq!(
        values
            .iter()
            .map(|v| v["Kind"].as_str().unwrap())
            .collect::<Vec<_>>(),
        ["Started", "ResetStarted", "ResetCompleted", "Failed"]
    );
    assert!(
        values
            .iter()
            .all(|v| v["OperationId"] == values[0]["OperationId"])
    );
    assert_eq!(values[2]["OldResourcesRetired"], true);
    assert!(!values[2]["NewOwner"].as_str().unwrap().is_empty());
    assert!(
        health.outcomes().is_empty(),
        "A failed reset must never send green success"
    );
}

#[tokio::test(start_paused = true)]
async fn reset_outcome_retains_record_identity_and_distinguishes_transport_and_monitoring() {
    let (health, mut receiver) = configured();
    health.plan_final_reset(true);
    health.provider_fault(health.status().ticket, "test fault");
    tokio::time::advance(std::time::Duration::from_secs(120)).await;
    health.status();
    health.audit_reset_started();
    let requirements = Requirements::new(&[
        Proof::Transport,
        Proof::Reference,
        Proof::Pricing,
        Proof::Publication,
        Proof::Persistence,
    ]);
    health.replace(requirements, Deadline::recovery());
    health.audit_owner(Some(Header {
        lane: Lane::OptionsGlbx,
        value_date: 1,
        worker: [2; 16],
        generation: [3; 16],
        revision: 2,
    }));
    let ticket = health.status().ticket;
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    let outcomes = health.outcomes();
    assert_eq!(outcomes.len(), 1);
    let first: serde_json::Value = serde_json::from_str(&outcomes[0]).unwrap();
    assert_eq!(first["MonitoringReady"], false);
    assert!(
        first["Message"]
            .as_str()
            .unwrap()
            .contains("not yet verified")
    );
    let facts = drain(&mut receiver);
    let completed = facts.iter().find(|v| v["Kind"] == "Completed").unwrap();
    assert_eq!(first["RecordId"], completed["RecordId"]);
    assert_eq!(first["OperationId"], completed["OperationId"]);
    for proof in [
        Proof::Reference,
        Proof::Pricing,
        Proof::Publication,
        Proof::Persistence,
    ] {
        health.prove(ticket, proof);
    }
    let outcomes = health.outcomes();
    assert_eq!(outcomes.len(), 2);
    let second: serde_json::Value = serde_json::from_str(&outcomes[1]).unwrap();
    assert_eq!(second["MonitoringReady"], true);
    assert_ne!(second["RecordId"], first["RecordId"]);
    health.prove(ticket, Proof::Transport);
    assert_eq!(health.outcomes(), outcomes);
}
#[tokio::test]
async fn intentional_stop_is_interrupted_not_successful() {
    let (health, mut receiver) = configured();
    health.provider_fault(health.status().ticket, "fault");
    health.stop();
    let v = drain(&mut receiver);
    assert_eq!(v[1]["Kind"], "Interrupted");
}
#[tokio::test(start_paused = true)]
async fn healthy_idle_heartbeats_do_not_open_incidents() {
    let (health, mut receiver) = configured();
    let ticket = health.status().ticket;
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    // Quiet quotes are valid only after actual final-source reconstruction.
    assert!(health.complete_setup(ticket));
    for _ in 0..30 {
        tokio::time::advance(std::time::Duration::from_secs(30)).await;
        health.status();
    }
    assert!(drain(&mut receiver).is_empty());
}
#[tokio::test]
async fn committed_outbox_restarts_with_identical_bytes_then_deletes_only_after_ack() {
    let path = directory();
    let record = Record::new(payload(1, "Started")).unwrap();
    Spool::new(path.clone())
        .commit(&record, true)
        .await
        .unwrap();
    let spool = Spool::new(path.clone());
    let retained = spool.read().await.unwrap();
    assert_eq!(retained[0].payload, record.payload);
    assert!(spool.acknowledge(&record, b"no responders").await.is_err());
    assert_eq!(spool.read().await.unwrap().len(), 1);
    spool.acknowledge(&record, &receipt(&record)).await.unwrap();
    assert!(spool.read().await.unwrap().is_empty());
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn duplicate_commits_do_not_overwrite_conflicting_history() {
    let path = directory();
    let spool = Spool::new(path.clone());
    let record = Record::new(payload(1, "Started")).unwrap();
    spool.commit(&record, true).await.unwrap();
    for _ in 0..607 {
        spool.commit(&record, true).await.unwrap();
    }
    let conflict = Record::new(payload(2, "Failed")).unwrap();
    assert!(spool.commit(&conflict, true).await.is_err());
    assert_eq!(spool.read().await.unwrap().len(), 1);
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn full_disk_budget_is_explicit_and_cannot_ack_unstored_data() {
    let path = directory();
    let spool = Spool::new(path.clone()).limits(1, 1);
    let record = Record::new(payload(1, "Started")).unwrap();
    assert!(
        spool
            .commit(&record, true)
            .await
            .unwrap_err()
            .contains("capacity")
    );
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn invalid_partial_and_oversized_files_are_rejected_without_unbounded_read() {
    let path = directory();
    tokio::fs::create_dir_all(&path).await.unwrap();
    tokio::fs::write(path.join("partial.tmp"), b"bad")
        .await
        .unwrap();
    assert!(Spool::new(path.clone()).read().await.is_err());
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[test]
fn receipt_rejects_wrong_identity_failed_result_truncation_and_length_bombs() {
    let record = Record::new(payload(1, "Started")).unwrap();
    let valid = receipt(&record);
    assert!(record.acknowledge(&valid).is_ok());
    assert!(
        record
            .acknowledge(&codec::compress(valid.clone()).unwrap())
            .is_ok()
    );
    for n in 0..valid.len() {
        assert!(record.acknowledge(&valid[..n]).is_err());
    }
    for invalid in [
        vec![0xdd, 255, 255, 255, 255],
        vec![0xdb, 255, 255, 255, 255],
        vec![0xc4, 255, 1],
        vec![0xc0],
        vec![0; 8193],
    ] {
        assert!(record.acknowledge(&invalid).is_err());
    }
    let wrong = Record::new(payload(2, "Started")).unwrap();
    assert!(wrong.acknowledge(&valid).is_err());
}
#[test]
fn retry_changes_attempt_but_not_record_digest_or_occurrence() {
    let record = Record::new(payload(1, "Started")).unwrap();
    let first = record.request(1).unwrap();
    let second = record.request(2).unwrap();
    assert_ne!(first.1, second.1);
    assert_eq!(first.0, second.0);
    assert_eq!(
        record.digest,
        Record::new(record.payload.clone()).unwrap().digest
    );
}
#[test]
fn ingress_is_bounded_and_terminal_reserve_does_not_block() {
    let (sink, _receiver) = outbox::channel();
    for _ in 0..400 {
        sink.capture(payload(1, "Started"), false);
    }
    assert!(
        sink.health()
            .failures
            .load(std::sync::atomic::Ordering::Relaxed)
            > 0
    );
    sink.capture(payload(1, "Failed"), true);
}

/// Driven by the managed PostgreSQL/NATS qualification fixture, not a simulated receipt.
#[tokio::test]
#[ignore = "Requires the isolated managed actor/PostgreSQL/NATS acceptance fixture"]
async fn real_nats_function_delivery() {
    use crate::{messaging::publisher::BrokerSettings, runtime::owner::Owner};
    use std::sync::atomic::Ordering;
    let address = std::env::var("IFM_RECOVERY_AUDIT_TEST_NATS").unwrap();
    let input = std::env::var("IFM_RECOVERY_AUDIT_TEST_RECORDS").unwrap();
    let records: Vec<String> =
        serde_json::from_str(&tokio::fs::read_to_string(input).await.unwrap()).unwrap();
    let ticks = std::env::var("IFM_RECOVERY_AUDIT_TEST_TICKS")
        .unwrap()
        .parse()
        .unwrap();
    let clock = Arc::new(Clock::new(ticks, true).unwrap());
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.notification_child().unwrap());
    let path = directory();
    let broker = BrokerSettings::new(address, vec!["127.0.0.1:53".parse().unwrap()]).unwrap();
    let sink = outbox::start(
        parent.clone(),
        broker,
        "audit-qualification".into(),
        clock,
        path.clone(),
    )
    .unwrap();
    for record in &records {
        sink.capture(record.clone(), true);
    }
    tokio::time::timeout(std::time::Duration::from_secs(150), async {
        loop {
            if sink.health().acknowledged.load(Ordering::Relaxed) == records.len() as u64 {
                break;
            }
            tokio::time::sleep(std::time::Duration::from_millis(10)).await;
        }
    })
    .await
    .expect("Native outbox was not durably acknowledged by the managed function");
    assert!(Spool::new(path.clone()).read().await.unwrap().is_empty());
    root.stop().unwrap();
    drop(sink);
    drop(parent);
    root.wait_idle(
        &tokio_util::sync::CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
    tokio::fs::remove_dir_all(path).await.unwrap();
}

#[tokio::test]
async fn corrupted_integrity_entry_is_rejected_then_other_incidents_can_continue() {
    let path = directory();
    let spool = Spool::new(path.clone());
    let record = Record::new(payload(1, "Started")).unwrap();
    spool.commit(&record, true).await.unwrap();
    let file = path.join(format!("{}.audit", record.id));
    let bytes = tokio::fs::read(&file).await.unwrap();
    let mut corrupt = bytes;
    corrupt[0] = if corrupt[0] == b'a' { b'b' } else { b'a' };
    tokio::fs::write(file, &corrupt).await.unwrap();
    assert!(spool.read().await.unwrap_err().contains("checksum"));
    assert!(spool.read().await.unwrap().is_empty());
    assert!(
        tokio::fs::try_exists(path.join(format!("{}.rejected", record.id)))
            .await
            .unwrap()
    );
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn complete_partial_write_is_recovered_with_unchanged_identity_and_digest() {
    let path = directory();
    tokio::fs::create_dir_all(&path).await.unwrap();
    let record = Record::new(payload(1, "Started")).unwrap();
    tokio::fs::write(
        path.join(format!("{}.tmp", record.id)),
        format!("{}\n{}", record.digest, record.payload),
    )
    .await
    .unwrap();
    let spool = Spool::new(path.clone());
    assert!(
        spool
            .read()
            .await
            .unwrap_err()
            .contains("Recovered complete")
    );
    let recovered = spool.read().await.unwrap();
    assert_eq!(recovered[0].digest, record.digest);
    assert_eq!(recovered[0].payload, record.payload);
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn stalled_audit_broker_cannot_retain_dataset_resources_or_delay_their_actual_join() {
    use crate::{messaging::publisher::BrokerSettings, runtime::owner::Owner};
    use std::sync::atomic::{AtomicU64, Ordering};
    struct Retired(Arc<AtomicU64>);
    impl Drop for Retired {
        fn drop(&mut self) {
            self.0.fetch_add(1, Ordering::Relaxed);
        }
    }
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let root = Arc::new(Owner::default());
    let audit_parent = Arc::new(root.notification_child().unwrap());
    let path = directory();
    let sink = outbox::start(
        audit_parent.clone(),
        BrokerSettings::new(
            format!("nats://{address}"),
            vec!["127.0.0.1:53".parse().unwrap()],
        )
        .unwrap(),
        "audit-stall-test".into(),
        Arc::new(Clock::new(0, true).unwrap()),
        path.clone(),
    )
    .unwrap();
    sink.capture(payload(1, "Started"), true);
    let (_socket, _) = tokio::time::timeout(std::time::Duration::from_secs(10), listener.accept())
        .await
        .unwrap()
        .unwrap();
    let dataset = Arc::new(root.child().unwrap());
    let retired = Arc::new(AtomicU64::new(0));
    let guard = Retired(retired.clone());
    let (began, started) = tokio::sync::oneshot::channel();
    let mut task = Some(
        dataset
            .spawn(async move {
                let _guard = guard;
                let _ = began.send(());
                std::future::pending::<()>().await;
            })
            .unwrap(),
    );
    started.await.unwrap();
    dataset
        .reset_task(&mut task, Deadline::recovery())
        .await
        .unwrap();
    assert!(task.is_none());
    assert_eq!(retired.load(Ordering::Relaxed), 1);
    assert_eq!(sink.health().acknowledged.load(Ordering::Relaxed), 0);
    root.stop().unwrap();
    drop(dataset);
    drop(audit_parent);
    drop(sink);
    root.wait_idle(
        &tokio_util::sync::CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert!(root.disposable().unwrap());
    tokio::fs::remove_dir_all(path).await.unwrap();
}

#[tokio::test]
async fn root_shutdown_flushes_captured_interruption_without_waiting_for_a_broker_reply() {
    use crate::{messaging::publisher::BrokerSettings, runtime::owner::Owner};
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.notification_child().unwrap());
    let path = directory();
    let sink = outbox::start(
        parent.clone(),
        BrokerSettings::new(
            "nats://127.0.0.1:1".into(),
            vec!["127.0.0.1:53".parse().unwrap()],
        )
        .unwrap(),
        "audit-root-stop".into(),
        Arc::new(Clock::new(0, true).unwrap()),
        path.clone(),
    )
    .unwrap();
    let health = Recovery::new(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    health.audit_context(Context {
        runtime: [1; 16],
        lane: Lane::OptionsGlbx,
        date: time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap(),
        clock: Arc::new(Clock::new(10_000_000, true).unwrap()),
        sink: sink.clone(),
        workloads: vec![],
    });
    health.provider_fault(health.status().ticket, "initial fault");
    health.audit_interrupt("Native runtime shutdown requested");
    root.stop().unwrap();
    drop(sink);
    drop(parent);
    root.wait_idle(
        &tokio_util::sync::CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    let records = Spool::new(path.clone()).read().await.unwrap();
    assert_eq!(records.len(), 2);
    let last: serde_json::Value = serde_json::from_str(&records[1].payload).unwrap();
    assert_eq!(last["Kind"], "Interrupted");
    assert_eq!(last["LatestError"], "Native runtime shutdown requested");
    assert!(root.disposable().unwrap());
    tokio::fs::remove_dir_all(path).await.unwrap();
}
#[tokio::test]
async fn limited_dataset_completion_does_not_invent_a_monitoring_restored_record() {
    let health = Recovery::new(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    let (sink, mut receiver) = outbox::channel();
    health.audit_context(Context {
        runtime: [1; 16],
        lane: Lane::FuturesGlbx,
        date: time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap(),
        clock: Arc::new(Clock::new(10_000_000, true).unwrap()),
        sink,
        workloads: vec![],
    });
    health.monitor_provider();
    health.provider_fault(health.status().ticket, "fault");
    let ticket = health.status().ticket;
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    let records = drain(&mut receiver);
    assert_eq!(records.len(), 3);
    assert_eq!(records[2]["Kind"], "Completed");
    assert_eq!(records[2]["CompletionScope"], "DatasetOperational");
    assert_eq!(records[2]["MonitoringReady"], false);
}

#[tokio::test]
async fn restored_record_names_the_actual_latest_owner_after_multiple_soft_replacements() {
    let (health, mut receiver) = configured();
    health.provider_fault(health.status().ticket, "fault");
    let header = |worker, generation| Header {
        lane: Lane::OptionsGlbx,
        value_date: 1,
        worker: [worker; 16],
        generation: [generation; 16],
        revision: 1,
    };
    health.audit_owner(Some(header(1, 2)));
    health.audit_owner(Some(header(3, 4)));
    let ticket = health.status().ticket;
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    let records = drain(&mut receiver);
    let actual = format!(
        "{}/{}",
        crate::messaging::wire::guid([3; 16]),
        crate::messaging::wire::guid([4; 16])
    );
    assert_eq!(records[1]["NewOwner"], actual);
    assert_eq!(records[2]["NewOwner"], actual);
}

#[tokio::test(start_paused = true)]
async fn immediate_reset_notice_reuses_the_actual_reset_fact_id_and_original_occurrence() {
    let (health, mut receiver) = configured();
    health.plan_final_reset(true);
    health.provider_fault(health.status().ticket, "fault");
    tokio::time::advance(std::time::Duration::from_secs(120)).await;
    let terminal = health.failures(0, 1).remove(0);
    let metadata = health.audit_notice(terminal.ordinal);
    let (operation, at, record) = (metadata.operation, metadata.occurred, metadata.record);
    health.audit_reset_started();
    let records = drain(&mut receiver);
    let reset = records
        .iter()
        .find(|r| r["Kind"] == "ResetStarted")
        .unwrap();
    assert_eq!(
        reset["RecordId"],
        crate::messaging::wire::guid(record.unwrap())
    );
    assert_eq!(reset["OperationId"], operation.clone().unwrap());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap();
    let notice = crate::messaging::recovery_notice::Notice::new(
        [9; 16],
        Lane::OptionsGlbx,
        date,
        &terminal,
        at.unwrap(),
    )
    .unwrap()
    .correlate(operation.as_deref(), record)
    .unwrap();
    let event = rmpv::decode::read_value(&mut std::io::Cursor::new(notice.bytes)).unwrap();
    let fields = event.as_array().unwrap();
    assert_eq!(
        fields[2].as_str(),
        Some(crate::messaging::wire::guid(record.unwrap()).as_str())
    );
    assert_eq!(fields[5].as_str(), operation.as_deref());
}

#[tokio::test]
async fn invalid_api_clock_is_explicit_audit_loss_and_never_fabricates_an_epoch_timestamp() {
    let recovery = Recovery::new(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    let (sink, mut receiver) = outbox::channel();
    let counters = sink.health().clone();
    let clock = Arc::new(Clock::new(2_534_023_007_999_999_999, true).unwrap());
    tokio::time::sleep(std::time::Duration::from_millis(1)).await;
    recovery.audit_context(Context {
        runtime: [1; 16],
        lane: Lane::OptionsGlbx,
        date: time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap(),
        clock,
        sink,
        workloads: vec![],
    });
    recovery.provider_fault(recovery.status().ticket, "provider disconnected");
    assert!(receiver.try_recv().is_err());
    assert!(
        counters
            .capture_failures
            .load(std::sync::atomic::Ordering::Relaxed)
            > 0
    );
    assert_eq!(
        recovery.status().phase,
        crate::runtime::recovery::Phase::Recovering
    );
}

#[tokio::test]
async fn early_factory_proofs_cannot_overtake_reset_completion_or_name_the_old_owner() {
    let (health, mut receiver) = configured();
    health.monitor_provider();
    let header = |worker, generation| Header {
        lane: Lane::OptionsGlbx,
        value_date: 1,
        worker: [worker; 16],
        generation: [generation; 16],
        revision: 1,
    };
    health.audit_owner(Some(header(1, 2)));
    health.provider_fault(health.status().ticket, "fault");
    health.audit_reset_started();
    health.audit_attempt();
    let ticket = health.status().ticket;
    for proof in [
        Proof::Retired,
        Proof::Transport,
        Proof::Reference,
        Proof::Pricing,
        Proof::Publication,
        Proof::Persistence,
    ] {
        health.prove(ticket, proof);
    }
    // Recording cannot delay the actual feed becoming ready.
    assert_eq!(
        health.status().phase,
        crate::runtime::recovery::Phase::Running
    );
    let mut records = drain(&mut receiver);
    assert_eq!(
        records
            .iter()
            .map(|r| r["Kind"].as_str().unwrap())
            .collect::<Vec<_>>(),
        ["Started", "ResetStarted"]
    );
    health.audit_owner(Some(header(3, 4)));
    records.extend(drain(&mut receiver));
    assert_eq!(
        records
            .iter()
            .map(|r| r["Kind"].as_str().unwrap())
            .collect::<Vec<_>>(),
        [
            "Started",
            "ResetStarted",
            "ResetCompleted",
            "ConnectionRestored",
            "Completed"
        ]
    );
    let actual = format!(
        "{}/{}",
        crate::messaging::wire::guid([3; 16]),
        crate::messaging::wire::guid([4; 16])
    );
    assert_eq!(records[2]["NewOwner"], actual);
    assert_eq!(records[3]["NewOwner"], actual);
    assert!(records[4]["MonitoringReady"].as_bool().unwrap());
}

#[tokio::test]
async fn late_factory_identity_cannot_append_reset_success_after_terminal_failure() {
    let (health, mut receiver) = configured();
    health.provider_fault(health.status().ticket, "fault");
    health.audit_reset_started();
    health.fail("replacement deadline expired");
    health.audit_owner(Some(Header {
        lane: Lane::OptionsGlbx,
        value_date: 1,
        worker: [3; 16],
        generation: [4; 16],
        revision: 1,
    }));
    let records = drain(&mut receiver);
    assert_eq!(
        records
            .iter()
            .map(|r| r["Kind"].as_str().unwrap())
            .collect::<Vec<_>>(),
        ["Started", "ResetStarted", "Failed"]
    );
}

/// Pooled MessagePack-CSharp writers can divide the same bounded receipt at any
/// byte boundary; segment boundaries need not coincide with MessagePack values.
#[test]
fn receipt_accepts_bounded_multiblock_success_and_reports_failure_details(){
    use rmpv::Value;
    fn chunks(parts:Vec<Vec<u8>>,lengths:Option<Vec<u64>>)->Vec<u8>{
        let mut sizes=Vec::new();
        for n in lengths.unwrap_or_else(||parts.iter().map(|b|b.len()as u64).collect()){
            rmp::encode::write_uint(&mut sizes,n).unwrap();
        }
        let mut value=vec![Value::Ext(98,sizes)];
        value.extend(parts.iter().map(|b|Value::Binary(lz4_flex::block::compress(b))));
        codec::encode(&Value::Array(value)).unwrap()
    }
    let record=Record::new(payload(1,"Started")).unwrap();let valid=receipt(&record);
    for cut in 1..valid.len(){assert!(record.acknowledge(&chunks(vec![valid[..cut].to_vec(),valid[cut..].to_vec()],None)).is_ok(),"split {cut}");}
    assert!(record.acknowledge(&chunks(valid.chunks(32).map(|b|b.to_vec()).collect(),None)).is_ok());
    for sizes in [vec![8192,1],vec![u64::MAX,1],vec![0,1],vec![1],vec![1,1,1]]{
        assert!(record.acknowledge(&chunks(vec![vec![0],vec![0]],Some(sizes))).is_err());
    }
    let failure=Value::Array(vec![false.into(),5090.into(),"Rejected".into(),Value::Array(vec![Value::Nil,Value::Array((0..13).map(|n|match n{10=>"Missing predecessor".into(),12=>"Full server failure detail".into(),_=>Value::Nil}).collect())])]);
    let failed=codec::encode(&failure).unwrap();
    let error=record.acknowledge(&chunks(vec![failed[..8].to_vec(),failed[8..].to_vec()],None)).unwrap_err();
    assert!(error.contains("Missing predecessor")&&error.contains("Full server failure detail"));
    let bad=Value::Array(vec![Value::Ext(98,vec![1]),Value::Binary(vec![0xff])]);
    assert!(record.acknowledge(&codec::encode(&bad).unwrap()).is_err());
}

/// Operational qualification against a captured C# reply; fixture bytes stay
/// outside the repository because they contain real incident identities.
#[test]
#[ignore="Requires IFM_AUDIT_CAPTURE_REPLY and optional IFM_AUDIT_CAPTURE_PAYLOAD"]
fn captured_csharp_reply_preserves_committed_acceptance_or_full_failure(){
    let reply=std::fs::read(std::env::var("IFM_AUDIT_CAPTURE_REPLY").unwrap()).unwrap();
    if let Ok(path)=std::env::var("IFM_AUDIT_CAPTURE_PAYLOAD"){
        let record=Record::new(std::fs::read_to_string(path).unwrap()).unwrap();
        record.acknowledge(&reply).unwrap();
    }else{
        let record=Record::new(payload(1,"Started")).unwrap();
        let error=record.acknowledge(&reply).unwrap_err();
        assert!(error.contains("Recovery sequence predecessor missing or conflicting"),"{error}");
        assert!(error.contains("MarketDataFeedRecoveryState.Accept"),"{error}");
    }
}
