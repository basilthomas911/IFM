use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    scheduled_tasks::{ScheduledTasks, status_color},
};
use std::collections::BTreeMap;
fn fixture() -> (Catalog, ContractPublication) {
    let c = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let s = ContractPublication {
        schema: 1,
        generation: 7,
        status: "Ready".into(),
        busy: false,
        result: ContractResult {
            view: "scheduled-tasks".into(),
            rows: vec![ContractRow {
                id: "task-id".into(),
                fields: BTreeMap::from([
                    ("name".into(), "Market close".into()),
                    ("environment".into(), "Development".into()),
                    ("hostId".into(), "development".into()),
                ]),
            }],
            ..Default::default()
        },
        outcomes: vec![],
    };
    (c, s)
}
#[test]
fn refresh_and_setup_tabs_dispatch_once_without_running_tasks() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ScheduledTasks, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (ScheduledTasks::default(), None),
    );
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    h.get_by_label("Setup").click();
    h.run();
    assert!(h.state().0.setup);
    h.get_by_label("Refresh schedules").click();
    h.run();
    let p: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(p["intent"], "load");
    assert_eq!(p["fields"]["environment"], "Development");
    assert!(h.state().0.pending.is_some());
}
#[test]
fn cancelling_task_confirmation_never_dispatches() {
    let (c, s) = fixture();
    let mut view = ScheduledTasks::default();
    view.confirmation = Some("run".into());
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ScheduledTasks, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (view, None),
    );
    h.run();
    h.get_by_label("Cancel task action").click();
    h.run();
    assert!(h.state().1.is_none());
    assert!(h.state().0.confirmation.is_none());
}
#[test]
fn recorded_status_colors_preserve_business_outcome() {
    assert_eq!(status_color("Running"), eframe::egui::Color32::YELLOW);
    assert_eq!(status_color("Succeeded"), eframe::egui::Color32::GREEN);
    assert_eq!(
        status_color("Scheduled"),
        eframe::egui::Color32::from_rgb(30, 144, 255)
    );
    assert_eq!(status_color("Unscheduled"), eframe::egui::Color32::GRAY);
    assert_eq!(status_color("Uncertain"), eframe::egui::Color32::RED);
}

#[test]
fn cached_date_tree_expands_collapses_and_selects_the_exact_run() {
    let (c, mut s) = fixture();
    let run = serde_json::json!({"id":"run-id","intendedFireTimeUtc":"2026-10-13T21:01:00Z","status":"Succeeded","stage":"Complete","detail":"Finished","startedAtUtc":"2026-10-13T21:01:02Z"});
    s.result.context.insert(
        "histories".into(),
        serde_json::json!({"task-id":[run]}).to_string(),
    );
    s.result
        .context
        .insert("historySchedule".into(), "task-id".into());
    let mut view = ScheduledTasks::default();
    let input: serde_json::Value = serde_json::from_str(&view.request("load", &s)).unwrap();
    s.outcomes.push(ifm_ui_rust::lookup_editor::EditorOutcome {
        request_id: input["requestId"].as_str().unwrap().into(),
        state: "Completed".into(),
        message: "Loaded".into(),
    });
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ScheduledTasks, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (view, None),
    );
    h.set_size(eframe::egui::vec2(1600., 1000.));
    h.run();
    let root = "Market close | Scheduled UTC - | Ran UTC 2026-10-13T21:01:02Z";
    h.get_by_label(root).click();
    h.run();
    h.get_by_label("2026").click();
    h.run();
    h.get_by_label("10").click();
    h.run();
    h.get_by_label("13").click();
    h.run();
    h.get_by_label("Market close | Scheduled UTC 2026-10-13T21:01:00Z | Ran UTC 2026-10-13T21:01:02Z | Succeeded").click();
    h.run();
    let p: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(p["intent"], "output");
    assert_eq!(p["originalId"], "task-id");
    assert_eq!(p["fields"]["runId"], "run-id");
    h.get_by_label("13").click();
    h.run();
    assert!(h.query_by_label("Market close | Scheduled UTC 2026-10-13T21:01:00Z | Ran UTC 2026-10-13T21:01:02Z | Succeeded").is_none());
}

#[test]
fn daily_time_helper_matches_the_existing_quartz_expression() {
    let mut view = ScheduledTasks::default();
    view.hour = 18;
    view.minute = 0;
    view.days = [true, true, true, true, true, false, false];
    view.apply_daily_time();
    assert_eq!(view.draft["expression"], "0 0 18 ? * SUN,MON,TUE,WED,THU");
}
