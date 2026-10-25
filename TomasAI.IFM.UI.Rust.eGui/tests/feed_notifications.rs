use ifm_ui_egui::{
    backend::{codec, contracts::*, schema::Registry},
    services::{feed_status::FeedStatus, notifications::Notifications},
};

#[test]
fn console_replay_merges_live_overlap_without_losing_old_rows_or_deduplication() {
    let mut notifications=Notifications::default();
    let at=chrono::DateTime::parse_from_rfc3339("2000-01-03T12:00:00Z").unwrap().to_utc();
    // Live reaches the UI before the oldest replay page. Replay exceeds both
    // former truncation limits (500 rows and 1024 remembered identities).
    let latest=uuid::Uuid::from_u128(1501).to_string();
    assert!(notifications.console_message(&latest,at+chrono::Duration::seconds(1500),"live during replay"));
    for index in 0..1500 {
        let id=uuid::Uuid::from_u128(index+1).to_string();
        assert!(notifications.console_message(&id,at+chrono::Duration::seconds(index as i64),&format!("message {index}")));
    }
    assert_eq!(notifications.publication.status_rows.len(),1501);
    assert_eq!(notifications.publication.status_rows.first().unwrap().message,"message 0");
    assert_eq!(notifications.publication.status_rows.last().unwrap().message,"live during replay");
    assert_eq!(notifications.publication.latest_status(),Some("live during replay"));
    assert!(!notifications.console_message(&latest,at+chrono::Duration::seconds(1500),"live during replay"));
    assert!(!notifications.console_message(&uuid::Uuid::from_u128(1).to_string(),at,"message 0"));
    assert_eq!(notifications.publication.status_rows.len(),1501);
    assert_eq!(notifications.publication.error_sequence,0);
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
    assert!(notifications.event(&registry, subject, &[0xc1]).is_err());
}

#[test]
fn daily_analytics_status_reports_failure_but_ready_status_is_console_only() {
    let registry=Registry::compiled().unwrap();let mut notifications=Notifications::default();
    let subject="Event.MarketDataFeedEvent.DailyAnalyticsInitializationStatus.2026-10-26";
    let ready=include_bytes!("fixtures/backend/daily-analytics-ready-event.wire.msgpack");
    assert!(notifications.event(&registry,subject,ready).unwrap());
    assert_eq!(notifications.publication.error_sequence,0);
    assert_eq!(notifications.publication.status_rows[0].message,"Daily indicators ready");
    assert!(!notifications.event(&registry,subject,ready).unwrap());
    let failed=include_bytes!("fixtures/backend/daily-analytics-failure-event.wire.msgpack");
    assert!(notifications.event(&registry,subject,failed).unwrap());
    assert_eq!(notifications.publication.error,"Daily EMA / Bollinger Initialization: Daily indicator refresh failed");
    assert_eq!(notifications.publication.error_sequence,1);
    assert!(!notifications.event(&registry,subject,failed).unwrap());
    assert_eq!(notifications.publication.status_rows.len(),2);
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
