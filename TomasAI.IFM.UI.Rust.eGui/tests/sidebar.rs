use eframe::egui;
use egui_kittest::{Harness, kittest::{Queryable, NodeT}};
use ifm_ui_egui::sidebar::{CalendarRow, SidebarPublication, SidebarView, StatusRow};
#[test]
fn status_list_virtualizes_large_snapshots_and_status_bar_uses_the_newest_message() {
    let snapshot = SidebarPublication {
        status_rows: (0..100_000)
            .map(|i| StatusRow {
                id: i.to_string(),
                time: "01:23:45.678 PM".into(),
                message: format!("Status {i}"),
            })
            .collect(),
        ..Default::default()
    };
    assert_eq!(snapshot.latest_status(), Some("Status 99999"));
    let mut harness = Harness::builder()
        .with_size(egui::vec2(750.0, 350.0))
        .build_state(
            move |ctx, view: &mut SidebarView| {
                egui::CentralPanel::default().show(ctx, |ui| view.status(ui, &snapshot));
            },
            SidebarView::default(),
        );
    harness.run();
    assert!(harness.state().drawn_status_rows > 0 && harness.state().drawn_status_rows < 30);
    for header in ["Status Time", "Status"] {
        assert!(harness.query_by_label(header).is_some());
    }
    assert!(harness.query_by_label("Status Console").is_none());
    assert!(harness.query_by_label("Status 0").is_none());
    assert!(harness.query_by_label("Status 99999").is_some());
    harness.get_by_label("Status 99999").click();
    harness.run();
    assert_eq!(harness.state().selected_status_id, "99999");
    harness.key_press(egui::Key::ArrowDown);
    harness.run();
    assert_eq!(harness.state().selected_status_id, "99998");
}

#[test]
fn console_follows_live_arrivals_but_preserves_manual_history_scrolling() {
    fn append(snapshot: &mut SidebarPublication) {
        let index = snapshot.status_rows.len();
        snapshot.status_rows.push(StatusRow {
            id: index.to_string(), time: "06:30:00 PM".into(), message: format!("Status {index}"),
        });
    }
    let mut snapshot = SidebarPublication::default();
    for _ in 0..100 { append(&mut snapshot); }
    let mut harness = Harness::builder().with_size(egui::vec2(750., 350.)).build_state(
        |ctx, state: &mut (SidebarView, SidebarPublication)| {
            egui::CentralPanel::default().show(ctx, |ui| state.0.status(ui, &state.1));
        },
        (SidebarView::default(), snapshot),
    );
    harness.run();
    assert!(harness.query_by_label("Status 99").is_some());
    append(&mut harness.state_mut().1);
    harness.run();
    assert!(harness.query_by_label("Status 100").is_some(), "New live row must be visible");
    let newest = harness.get_by_label("Status 100").accesskit_node().bounding_box().unwrap();
    let older = harness.get_by_label("Status 99").accesskit_node().bounding_box().unwrap();
    assert!(newest.y0 < older.y0, "Newest message must appear above the preceding message");
    assert_eq!(harness.state().1.latest_status(), Some("Status 100"));

    // Scrolling down into older messages must not jump back to the newest row.
    harness.input_mut().events.extend([
        egui::Event::PointerMoved(egui::pos2(300., 150.)),
        egui::Event::MouseWheel { unit: egui::MouseWheelUnit::Point,
            delta: egui::vec2(0., -350.), modifiers: egui::Modifiers::NONE },
    ]);
    harness.run_steps(8);
    assert!(harness.query_by_label("Status 100").is_none());
    append(&mut harness.state_mut().1);
    harness.run();
    assert!(harness.query_by_label("Status 101").is_none(), "History inspection must not jump to live");
    assert_eq!(harness.state().1.latest_status(), Some("Status 101"));

    // Returning to the top resumes following subsequent arrivals.
    harness.input_mut().events.push(egui::Event::MouseWheel { unit: egui::MouseWheelUnit::Point,
        delta: egui::vec2(0., 100_000.), modifiers: egui::Modifiers::NONE });
    harness.run_steps(8);
    assert!(harness.query_by_label("Status 101").is_some());
    append(&mut harness.state_mut().1);
    harness.run();
    assert!(harness.query_by_label("Status 102").is_some());
}
#[test]
fn calendar_preserves_period_actions_and_selected_event_details() {
    let snapshot = SidebarPublication {
        period: "Today".into(),
        country: "US".into(),
        countries: vec!["US".into()],
        date: "Tuesday, October 13, 2026".into(),
        selected_id: "event".into(),
        calendar_rows: vec![CalendarRow {
            id: "event".into(),
            time: "2:00 PM".into(),
            country: "US".into(),
            event: "Test event".into(),
            actual: "1.5".into(),
            forecast: "1.6".into(),
            prior: "1.4".into(),
        }],
        ..Default::default()
    };
    let mut harness = Harness::builder()
        .with_size(egui::vec2(750.0, 300.0))
        .build_state(
            move |ctx, state: &mut (SidebarView, Vec<u32>)| {
                egui::CentralPanel::default().show(ctx, |ui| {
                    if let Some(action) = state.0.calendar(ui, &snapshot) {
                        state.1.push(action);
                    }
                });
            },
            (SidebarView::default(), vec![]),
        );
    harness.run();
    for label in [
        "Economic Calendar",
        "Country Code:",
        "Today",
        "Yesterday",
        "Tomorrow",
        "This Week",
        "Next Week",
        "Actual",
        "Forecast",
        "Prior",
        "1.5",
        "1.6",
        "1.4",
        "Test event",
    ] {
        assert!(harness.query_by_label(label).is_some(), "Missing {label}");
    }
    harness.get_by_label("Next Week").click();
    harness.run();
    assert_eq!(harness.state().1, vec![0x80000004]);
    harness.get_by_label("Test event").click();
    harness.run();
    assert_eq!(harness.state().1, vec![0x80000004, 0xA0000000]);
}
