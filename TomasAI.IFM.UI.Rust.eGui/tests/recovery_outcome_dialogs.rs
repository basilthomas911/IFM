use ifm_ui_egui::{
    backend::schema::Registry,
    services::notifications::Notifications,
    status_dialog::{self, Severity},
};


/// Provides outcome for the recovery outcome dialogs implementation.
fn outcome(id: uuid::Uuid, role: &str, severity: &str) -> Vec<u8> {
    serde_json::to_vec(&serde_json::json!({"Version":1,"RecordId":id.to_string(),"OperationId":uuid::Uuid::from_u128(42).to_string(),"Role":role,"Dataset":"GLBX.MDP3","ValueDate":"2026-10-29","OccurredAtUtc":"2026-10-29T12:00:00Z","Severity":severity,"MonitoringReady":false,"Message":format!("{role} reset completed. Connection restored; monitoring is not yet verified.")})).unwrap()
}


/// Verifies that completion is deduplicated and only the latest outcome is retained.
#[test]
fn completion_is_deduplicated_and_only_the_latest_outcome_is_retained() {
    let registry = Registry::compiled().unwrap();
    let mut n = Notifications::default();
    n.error(
        "failed-operation".into(),
        "Recovery deadline expired; resetting dataset".into(),
    );
    let sequence = n.publication.error_sequence;
    let error = n.publication.error.clone();
    let body = outcome(uuid::Uuid::from_u128(1), "Futures", "success");
    let subject = "Notify.MarketDataRecoveryOutcome.2026-10-29";
    assert!(n.event(&registry, subject, &body).unwrap());
    assert!(!n.event(&registry, subject, &body).unwrap());
    assert_eq!(n.publication.error_sequence, sequence);
    assert_eq!(n.publication.error, error);
    assert_eq!(n.publication.outcome_dialogs.len(), 1);
    assert_eq!(n.publication.outcome_dialogs[0].severity, Severity::Success);
    assert!(
        n.publication.outcome_dialogs[0]
            .message
            .contains("not yet verified")
    );
    assert!(
        n.event(
            &registry,
            subject,
            &outcome(uuid::Uuid::from_u128(2), "Options", "warning")
        )
        .unwrap()
    );
    assert_eq!(n.publication.outcome_dialogs.len(), 1);
    assert_eq!(n.publication.outcome_dialogs[0].severity, Severity::Warning);
}


#[test]
fn successful_options_recovery_leaves_the_status_console_and_warning_untouched() {
    let registry = Registry::compiled().unwrap();
    let mut notifications = Notifications::default();
    let subject = "Notify.MarketDataRecoveryOutcome.2026-10-29";
    let warning = outcome(uuid::Uuid::from_u128(1), "Options", "warning");
    assert!(notifications.event(&registry, subject, &warning).unwrap());
    let success = outcome(uuid::Uuid::from_u128(2), "Options", "success");
    assert!(!notifications.event(&registry, subject, &success).unwrap());
    assert_eq!(notifications.publication.status_rows.len(), 1);
    assert_eq!(notifications.publication.outcome_dialogs.len(), 1);
    assert_eq!(notifications.publication.outcome_dialogs[0].severity, Severity::Warning);
}


/// Verifies that a burst retains one latest dialog and retries do not restore older messages.
#[test]
fn a_burst_retains_one_latest_dialog_and_retries_do_not_restore_older_messages() {
    let registry = Registry::compiled().unwrap();
    let mut n = Notifications::default();
    let subject = "Notify.MarketDataRecoveryOutcome.2026-10-29";
    for id in 1..=100 {
        assert!(
            n.event(
                &registry,
                subject,
                &outcome(uuid::Uuid::from_u128(id), "Futures", "success")
            )
            .unwrap()
        );
        assert_eq!(n.publication.outcome_dialogs.len(), 1);
        assert_eq!(n.publication.outcome_dialogs[0].sequence, id as u64);
    }
    assert!(
        !n.event(
            &registry,
            subject,
            &outcome(uuid::Uuid::from_u128(1), "Futures", "success")
        )
        .unwrap()
    );
    assert_eq!(n.publication.outcome_dialogs[0].sequence, 100);
}


/// Verifies that invalid outcomes never open a dialog.
#[test]
fn invalid_outcomes_never_open_a_dialog() {
    let registry = Registry::compiled().unwrap();
    let mut n = Notifications::default();
    for body in [
        outcome(uuid::Uuid::from_u128(1), "Options", "unknown"),
        b"{bad".to_vec(),
        vec![b'x'; 16385],
    ] {
        assert!(
            n.event(
                &registry,
                "Notify.MarketDataRecoveryOutcome.2026-10-29",
                &body
            )
            .is_err()
        );
    }
    assert!(
        n.event(
            &registry,
            "Notify.MarketDataRecoveryOutcome.2026-10-28",
            &outcome(uuid::Uuid::from_u128(1), "Options", "success")
        )
        .is_err()
    );
    assert!(n.publication.outcome_dialogs.is_empty());
}


/// Verifies that all status icons have equal bounds and ok is centered.
#[test]
fn all_status_icons_have_equal_bounds_and_ok_is_centered() {
    for severity in [Severity::Error, Severity::Success, Severity::Warning, Severity::Question] {
        let ctx = eframe::egui::Context::default();
        let input = eframe::egui::RawInput {
            screen_rect: Some(eframe::egui::Rect::from_min_size(
                eframe::egui::Pos2::ZERO,
                eframe::egui::vec2(1000.0, 700.0),
            )),
            ..Default::default()
        };
        let _=ctx.run(input,|ctx|{eframe::egui::CentralPanel::default().show(ctx,|ui|{
            let layout=status_dialog::body(ui,"A long recovery message that wraps across several lines and retains its operation identity.",severity);
            assert_eq!(layout.icon.size(),eframe::egui::Vec2::splat(56.0));
            assert!((layout.ok.center().x-layout.content.center().x).abs()<1.0);
            assert!(layout.icon.center().x>layout.content.center().x);
        });});
    }
}


/// Measure the actual modal after its centered area has settled for one frame.
fn message_layout(
    ctx: &eframe::egui::Context,
    message: &str,
    viewport_width: f32,
    dpi: f32,
) -> (status_dialog::Layout, eframe::egui::Rect) {
    use eframe::egui;
    let mut measured = None;
    for _ in 0..2 {
        let mut input = egui::RawInput {
            screen_rect: Some(egui::Rect::from_min_size(
                egui::Pos2::ZERO,
                egui::vec2(viewport_width, 700.0),
            )),
            ..Default::default()
        };
        input
            .viewports
            .get_mut(&egui::ViewportId::ROOT)
            .unwrap()
            .native_pixels_per_point = Some(dpi);
        let _ = ctx.run(input, |ctx| {
            let modal = egui::Modal::new(egui::Id::new("message-size-test"))
                .show(ctx, |ui| status_dialog::body(ui, message, Severity::Error));
            measured = Some((modal.inner, modal.response.rect));
        });
    }
    measured.unwrap()
}


/// Fit the longest explicit line and shrink the same modal for later messages,
/// rather than retaining a fixed width or wrapping text that fits the viewport.
#[test]
fn message_dialog_fits_its_longest_line_and_shrinks_for_short_messages() {
    const MESSAGE: &str = "Backend error 503: The native market-data browser source is unavailable for the selected trading account and session.";
    for dpi in [1.0, 1.5, 2.0] {
        let ctx = eframe::egui::Context::default();
        let (wide, wide_modal) = message_layout(&ctx, MESSAGE, 1600.0, dpi);
        assert!(wide.content.width() > 620.0);
        assert!(wide.message.right() <= wide.icon.left());
        assert!((wide.icon.right() - wide.content.right()).abs() < 2.0);
        let (multiline, _) = message_layout(
            &ctx,
            &format!("Short line.\n{MESSAGE}\nLast line."),
            1600.0,
            dpi,
        );
        assert!((multiline.content.width() - wide.content.width()).abs() < 2.0);
        assert!(multiline.message.height() > wide.message.height() * 2.0);
        let (short, short_modal) = message_layout(&ctx, "Ready.", 1600.0, dpi);
        assert!(short.content.width() < 400.0);
        assert!(short_modal.width() < wide_modal.width() - 100.0);
        assert!((short.ok.center().x - short.content.center().x).abs() < 1.0);
        assert!((short.message.height() - wide.message.height()).abs() < 2.0);
    }
}


/// Keep long text, the icon and acknowledgement inside a narrow window.
#[test]
fn message_dialog_wraps_at_the_window_boundary() {
    for dpi in [1.0, 1.5, 2.0] {
        let ctx = eframe::egui::Context::default();
        let message =
            "The market-data source is unavailable. Retry after the connection has recovered. "
                .repeat(4);
        let (layout, modal) = message_layout(&ctx, &message, 360.0, dpi);
        assert!(layout.content.width() <= 296.0);
        assert!(layout.message.height() > 56.0);
        assert!(layout.message.right() <= layout.icon.left());
        assert!(layout.icon.right() <= layout.content.right() + 1.0);
        assert!(layout.ok.left() >= layout.content.left());
        assert!(layout.ok.right() <= layout.content.right());
        assert!(
            modal.left() >= 0.0 && modal.right() <= 360.0,
            "DPI {dpi}: modal {modal:?}, content {:?}",
            layout.content
        );
    }
}


/// Measure the active font rather than estimating width from character count.
#[test]
fn message_dialog_width_tracks_the_message_font() {
    use eframe::egui::{Context, FontId, TextStyle};
    let message = "The native browser source is unavailable for this trading session.";
    let ctx = Context::default();
    let (normal, _) = message_layout(&ctx, message, 2000.0, 1.0);
    ctx.style_mut(|style| {
        style
            .text_styles
            .insert(TextStyle::Body, FontId::proportional(24.0));
    });
    let (large, _) = message_layout(&ctx, message, 2000.0, 1.0);
    assert!(large.content.width() > normal.content.width() * 1.4);
    assert!(large.message.height() > normal.message.height());
    assert!(large.message.right() <= large.icon.left());
}


/// Verifies that actual nats push reaches the dialog while queries are unavailable.
#[tokio::test]
#[ignore = "Requires IFM_STANDALONE_TEST_NATS_URL pointing to an isolated NATS broker"]
async fn actual_nats_push_reaches_the_dialog_while_queries_are_unavailable() {
    use ifm_ui_egui::backend::transport::{Backend, Settings};
    use std::{sync::Arc, time::Duration};
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    let backend = Backend::connect(Settings {
        url: url.clone(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let raw = async_nats::connect(url).await.unwrap();
    let (send, mut receive) = tokio::sync::mpsc::channel(32);
    let (_actions, actions) = tokio::sync::mpsc::channel(32);
    let task = tokio::spawn(ifm_ui_egui::services::notifications::monitor(
        backend,
        Arc::new(Registry::compiled().unwrap()),
        send,
        actions,
    ));
    // The first publication follows subscription setup and its broker flush.
    tokio::time::timeout(Duration::from_secs(5), receive.recv())
        .await
        .unwrap()
        .unwrap();
    raw.publish(
        "Event.MarketDataFeedEvent.DatasetRecoveryFailed.2026-10-26",
        include_bytes!("fixtures/backend/recovery-failure-event.wire.msgpack")
            .as_slice()
            .to_vec()
            .into(),
    )
    .await
    .unwrap();
    let failure = tokio::time::timeout(Duration::from_secs(5), async {
        loop {
            let state = receive.recv().await.unwrap();
            if state.error.contains("Option Pipeline Recovery Failed") {
                break state;
            }
        }
    })
    .await
    .unwrap();
    let bytes = outcome(uuid::Uuid::from_u128(1), "Options", "success");
    raw.publish(
        "Notify.MarketDataRecoveryOutcome.2026-10-29",
        bytes.clone().into(),
    )
    .await
    .unwrap();
    raw.publish("Notify.MarketDataRecoveryOutcome.2026-10-29", bytes.into())
        .await
        .unwrap();
    raw.publish(
        "Notify.MarketDataRecoveryOutcome.2026-10-29",
        outcome(uuid::Uuid::from_u128(2), "Options", "warning").into(),
    )
    .await
    .unwrap();
    raw.flush().await.unwrap();
    let completed = tokio::time::timeout(Duration::from_secs(5), async {
        loop {
            let state = receive.recv().await.unwrap();
            if !state.outcome_dialogs.is_empty() {
                break state;
            }
        }
    })
    .await
    .unwrap();
    assert_eq!(completed.outcome_dialogs.len(), 1);
    assert_eq!(completed.outcome_dialogs[0].severity, Severity::Warning);
    assert!(completed.error_history.iter().any(|(_, message)| message == &failure.error));
    assert!(!completed.status_rows.iter().any(|row| row.id == "outcome:00000000-0000-0000-0000-000000000001"));
    task.abort();
    let _ = task.await;
}
