use ifm_ui_egui::{
    backend::{codec, contracts::*, schema::Registry},
    services::{feed_status::FeedStatus, notifications::Notifications},
};

#[test]
fn diagnostic_request_ids_do_not_turn_one_outage_into_repeated_dialogs() {
    let mut notifications=Notifications::default();
    let first=format!("Backend request failed: no responders [subject=Query.Feed.Status.x; operationId={}]",uuid::Uuid::new_v4());
    let second=format!("Backend request failed: no responders [subject=Query.Feed.Status.x; operationId={}]",uuid::Uuid::new_v4());
    assert!(notifications.feed_status_unavailable(&first));
    assert!(!notifications.feed_status_unavailable(&second));
    assert!(notifications.warning(format!("calendar:{first}"),first));
    assert!(!notifications.warning(format!("calendar:{second}"),second));
    assert_eq!(notifications.publication.error_sequence,0);
}

#[test]
fn console_keeps_two_viewport_pages_in_chronological_storage() {
    let mut notifications=Notifications::default();notifications.set_console_capacity(24);
    let at=chrono::DateTime::parse_from_rfc3339("2000-01-03T12:00:00Z").unwrap().to_utc();
    for index in 0..1500 {
        assert!(notifications.console_message(&uuid::Uuid::from_u128(index+1).to_string(),at+chrono::Duration::seconds(index as i64),&format!("message {index}")));
    }
    assert_eq!(notifications.publication.status_rows.len(),24);
    assert_eq!(notifications.publication.status_rows.first().unwrap().message,"message 1476");
    assert_eq!(notifications.publication.latest_status(),Some("message 1499"));
    assert!(!notifications.console_message(&uuid::Uuid::from_u128(1500).to_string(),at,"duplicate retained ID"));
    notifications.set_console_capacity(8);assert_eq!(notifications.publication.status_rows.len(),8);
    assert_eq!(notifications.publication.status_rows.first().unwrap().message,"message 1492");
    notifications.set_console_capacity(48);assert_eq!(notifications.publication.status_rows.len(),8,"Growing cannot replay evicted rows");
}

#[test]
fn terminal_recovery_failure_reaches_the_existing_error_dialog_publication_once() {
    let registry = Registry::compiled().unwrap();
    let mut notifications = Notifications::default();
    let bytes = include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack");
    let subject = "Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26";
    assert!(notifications.event(&registry, subject, bytes).unwrap());
    assert!(
        notifications
            .publication
            .error
            .contains("two-minute recovery deadline expired")
    );
    assert_eq!(notifications.publication.error_sequence, 1);
    assert_eq!(notifications.publication.error,"Option Pipeline Recovery Failed: Option pipeline recovery failed: The two-minute recovery deadline expired.");
    assert_eq!(notifications.publication.status_rows[0].message,"Option pipeline recovery failed: The two-minute recovery deadline expired.");
    assert_eq!(notifications.publication.status_rows[0].time,"08:30:00.000 AM");
    assert!(!notifications.event(&registry, subject, bytes).unwrap());
    assert_eq!(notifications.publication.error_sequence, 1);
    assert!(
        !notifications
            .event(&registry, "Event.OtherActor.Failed.none", bytes)
            .unwrap()
    );
    let log=std::path::PathBuf::from(std::env::var_os("LOCALAPPDATA").unwrap()).join("TomasAI/IFM/UI.Rust.eGui/Logs/ui-errors.jsonl");
    let records:Vec<serde_json::Value>=std::fs::read_to_string(log).unwrap().lines().map(|line|serde_json::from_str(line).unwrap()).collect();
    let deliveries:Vec<_>=records.iter().filter(|record|record["event"]=="backend.failure_notification_received"&&record["requestSubject"]==subject).collect();
    assert!(deliveries.len()>=2,"duplicate deliveries must be audited without duplicating dialogs");
    let decoded=codec::decode(bytes).unwrap();
    let command=registry.field("TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataDatasetRecoveryFailedEvent","CommandId",&decoded).unwrap();
    assert!(deliveries.iter().any(|record|record["operationId"].as_str()==command.as_str()));
    assert!(notifications.event(&registry, subject, &[0xc1]).is_err());
}

#[test]
fn daily_analytics_retryable_failure_and_ready_status_are_console_only() {
    let registry=Registry::compiled().unwrap();let mut notifications=Notifications::default();
    let subject="Event.MarketDataFeedEvent.DailyAnalyticsInitializationStatus.2026-10-26";
    let ready=include_bytes!("fixtures/backend/daily-analytics-ready-event.wire.msgpack");
    assert!(notifications.event(&registry,subject,ready).unwrap());
    assert_eq!(notifications.publication.error_sequence,0);
    assert_eq!(notifications.publication.status_rows[0].message,"Daily indicators ready");
    assert!(!notifications.event(&registry,subject,ready).unwrap());
    let failed=include_bytes!("fixtures/backend/daily-analytics-failure-event.wire.msgpack");
    assert!(notifications.event(&registry,subject,failed).unwrap());
    assert!(notifications.publication.error.is_empty());
    assert_eq!(notifications.publication.error_sequence,0);
    assert!(!notifications.event(&registry,subject,failed).unwrap());
    assert_eq!(notifications.publication.status_rows.len(),2);
    assert_eq!(notifications.publication.status_rows[1].message,"Daily indicator refresh failed");
    // A retryable status cannot replace or clear an outstanding fatal dialog.
    let fatal=include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack");
    assert!(notifications.event(&registry,"Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26",fatal).unwrap());
    let error=notifications.publication.error.clone();
    assert_eq!(notifications.publication.error_sequence,1);
    let mut retry=codec::decode(failed).unwrap();
    // A new daily refresh operation must also remain console-only.
    if let rmpv::Value::Array(ref mut fields)=retry {fields[3]="aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee".into();}
    let retry=codec::encode(&retry).unwrap();
    assert!(notifications.event(&registry,subject,&retry).unwrap());
    assert_eq!(notifications.publication.error,error);
    assert_eq!(notifications.publication.error_sequence,1);
}

#[test]
fn runtime_status_keeps_futures_running_when_options_fail() {
    let reply = codec::decode(include_bytes!(
        "fixtures/backend/feed-status-result.wire.msgpack"
    ))
    .unwrap();
    let status = FeedStatus::from_wire(service_result(&reply).unwrap()).unwrap();
    assert!(status.running);
    assert!(!status.recovering);
    assert!(
        status
            .terminal_error
            .unwrap()
            .contains("Option pipeline recovery failed")
    );
    let instant = datetime_offset(
        &codec::decode(include_bytes!(
            "fixtures/backend/datetime-offset.raw.msgpack"
        ))
        .unwrap(),
    )
    .unwrap();
    assert_eq!(instant.to_rfc3339(), "2026-10-26T12:30:00+00:00");
}

#[test]
fn status_console_consumer_preserves_eastern_time_without_raising_an_error_dialog() {
    let registry = Registry::compiled().unwrap();
    let mut notifications = Notifications::default();
    let bytes = include_bytes!("fixtures/backend/status-notification.wire.msgpack");
    assert!(
        notifications
            .event(&registry, "Notify.StatusConsoleEvent.Logged.none", bytes)
            .unwrap()
    );
    assert_eq!(notifications.publication.status_rows.len(), 1);
    let row = &notifications.publication.status_rows[0];
    assert_eq!(row.time, "08:30:00.000 AM");
    assert_eq!(row.message, "Option subscriptions restored");
    assert_eq!(
        row.id,
        "2026-10-26T12:30:00.0000000Z|00112233-4455-6677-8899-aabbccddeeff"
    );
    assert_eq!(notifications.publication.error_sequence, 0);
    assert!(notifications.publication.error.is_empty());
    assert!(
        !notifications
            .event(&registry, "Notify.StatusConsoleEvent.Logged.none", bytes)
            .unwrap()
    );
}

#[test]
fn restart_no_responders_cannot_replace_or_hide_terminal_recovery_dialog() {
    let registry = Registry::compiled().unwrap();
    let mut notifications = Notifications::default();
    let body = include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack");
    let subject = "Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26";
    assert!(notifications.event(&registry, subject, body).unwrap());
    let terminal = notifications.publication.error.clone();
    let sequence = notifications.publication.error_sequence;
    assert!(notifications.feed_status_unavailable("Backend request failed: no responders"));
    assert_eq!(notifications.publication.error, terminal);
    assert_eq!(notifications.publication.error_sequence, sequence);
    assert!(!notifications.publication.feed_control.ready);
    assert!(!notifications.publication.feed_control.can_toggle);
    assert!(notifications.publication.status_rows.last().unwrap().message.contains("no responders"));
    assert!(!notifications.feed_status_unavailable("Backend request failed: no responders"));
    // The UI may only receive the last coalesced snapshot: it still carries the
    // original terminal dialog, not merely the transient connection observation.
    assert!(notifications.publication.error.contains("two-minute recovery deadline expired"));
}

#[test]
fn status_query_failure_is_visible_without_inventing_a_terminal_feed_failure() {
    let registry = Registry::compiled().unwrap();
    let mut notifications = Notifications::default();
    assert!(notifications.feed_status_unavailable("Backend request failed: no responders"));
    assert_eq!(notifications.publication.error_sequence, 0);
    assert!(notifications.publication.error.is_empty());
    assert_eq!(notifications.publication.status_rows.len(), 1);
    assert!(notifications.event(&registry,
        "Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26",
        include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack")).unwrap());
    assert_eq!(notifications.publication.error_sequence, 1);
    assert!(notifications.publication.error.starts_with("Option Pipeline Recovery Failed:"));
}

#[test]
fn warnings_never_replace_a_fatal_dialog_and_can_escalate_the_same_operation() {
    let mut notifications=Notifications::default();
    for kind in ["console-connect","console-ack","calendar-refresh","calendar-task",
        "calendar-action","feed-operation","decode","transport","controller-task"] {
        assert!(notifications.warning(kind.into(),format!("{kind}: retryable details")));
        assert!(!notifications.warning(kind.into(),"duplicate".into()));
    }
    assert!(notifications.publication.error.is_empty());
    assert_eq!(notifications.publication.error_sequence,0);
    assert_eq!(notifications.publication.status_rows.len(),1);
    assert!(notifications.error("transport".into(),"Terminal dataset recovery failed".into()));
    let fatal=notifications.publication.error.clone();
    assert_eq!(notifications.publication.error_sequence,1);
    assert!(notifications.warning("next-retry".into(),"Still retrying unrelated calendar".into()));
    assert_eq!(notifications.publication.error,fatal);
    assert_eq!(notifications.publication.error_sequence,1);
}

#[test]
fn native_records_deduplicate_by_record_id_while_later_failure_in_same_operation_is_visible() {
    let registry=Registry::compiled().unwrap();let mut notifications=Notifications::default();
    let subject="Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26";
    let mut first=codec::decode(include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack")).unwrap();
    let rmpv::Value::Array(fields)=&mut first else{panic!("fixture shape")};
    fields[6]="RustMarketDataOwner".into();fields[5]="11111111-1111-4111-8111-111111111111".into();
    fields[2]="22222222-2222-4222-8222-222222222222".into();fields[7]="Resetting Options/GLBX.MDP3".into();
    let bytes=codec::encode(&first).unwrap();assert!(notifications.event(&registry,subject,&bytes).unwrap());
    assert!(!notifications.event(&registry,subject,&bytes).unwrap());
    let rmpv::Value::Array(fields)=&mut first else{panic!("fixture shape")};
    fields[2]="33333333-3333-4333-8333-333333333333".into();fields[7]="Options/GLBX.MDP3 final reset failed".into();
    assert!(notifications.event(&registry,subject,&codec::encode(&first).unwrap()).unwrap());
    assert_eq!(notifications.publication.error_sequence,2);
}
