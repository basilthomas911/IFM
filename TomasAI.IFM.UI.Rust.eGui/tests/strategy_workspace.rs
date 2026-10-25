use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog, DetailsView, Snapshot,
    strategy_workspace::{StrategyTab, StrategyWorkspace},
};
struct State {
    workspace: StrategyWorkspace,
    details: DetailsView,
    actions: Vec<u32>,
}
fn fixture(rows: usize) -> (Catalog, Snapshot) {
    let catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let entries:Vec<_>=(0..rows).map(|i|serde_json::json!({"id":format!("workflow-{i}"),"action":1000+i,"triggerIdentity":"signal-1","occurredOn":format!("2026-10-13T00:00:{i:02}Z"),"signal":"TrendDirectionChanged","trend":"UpTrend","futuresPrice":"6200.25","outcome":"PipelineFailed","endState":"RegimeDiscovery","actors":[]})).collect();
    let snapshot=serde_json::from_value(serde_json::json!({"schema":1,"epoch":"workspace","generation":1,"mode":"Fixture","workflowId":"workflow-0","revision":1,"roots":[],"rows":entries,"pageNumber":1,"pageCount":2,"hasMore":true,"totalRows":rows,"signals":[{"identity":"signal-1","utcSeconds":1791849600.0,"price":6200.25,"direction":"Up","tooltip":"Signal"}],"summary":[{"title":"Regime Discovery","state":"Failed","summary":"Missing model","content":"Authoritative failure details"}],"header":"Workflow 0"})).unwrap();
    (catalog, snapshot)
}
#[test]
fn tabs_match_winforms_summary_placeholder_and_emit_one_paging_action() {
    let (c, s) = fixture(10);
    let mut h = Harness::new_state(
        move |ctx, state: &mut State| {
            if let Some(action) = state.workspace.draw(ctx, &s, &c, &mut state.details) {
                state.actions.push(action);
            }
        },
        State {
            workspace: Default::default(),
            details: Default::default(),
            actions: vec![],
        },
    );
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    h.get_by_label("Summary").click();
    h.run();
    assert!(h.state().workspace.tab == StrategyTab::Summary);
    assert!(h.query_by_label("Summary is not available.").is_some());
    assert!(h.query_by_label("Authoritative failure details").is_none());
    h.get_by_label("Strategy Updates").click();
    h.run();
    assert!(h.state().workspace.tab == StrategyTab::Updates);
    assert!(h.query_by_label("Load more workflows").is_none());
    for header in [
        "Date/Time",
        "Futures ITI Signal Event",
        "Trend Type",
        "Futures Price",
        "Pipeline State",
        "Workflow End State",
    ] {
        assert!(h.query_by_label(header).is_some(), "Missing {header}");
    }
    assert_eq!(h.state().actions, vec![13]);
    h.run();
    assert_eq!(h.state().actions, vec![13]);
}
#[test]
fn workflow_updates_virtualize_one_hundred_thousand_rows() {
    let (c, s) = fixture(100_000);
    let mut workspace = StrategyWorkspace::default();
    workspace.tab = StrategyTab::Updates;
    let mut h = Harness::new_state(
        move |ctx, state: &mut State| {
            if let Some(action) = state.workspace.draw(ctx, &s, &c, &mut state.details) {
                state.actions.push(action);
            }
        },
        State {
            workspace,
            details: Default::default(),
            actions: vec![],
        },
    );
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    assert!(h.state().workspace.drawn_rows > 0 && h.state().workspace.drawn_rows < 60);
}

#[test]
fn graph_keeps_reserved_height_when_initial_snapshot_has_no_signals() {
    let (catalog, mut snapshot) = fixture(0);
    snapshot.signals.clear();
    let mut harness = Harness::builder()
        .with_size(eframe::egui::vec2(1000.0, 900.0))
        .build_state(
            move |ctx, state: &mut State| {
                state
                    .workspace
                    .draw(ctx, &snapshot, &catalog, &mut state.details);
            },
            State {
                workspace: StrategyWorkspace::for_shell(),
                details: Default::default(),
                actions: vec![],
            },
        );
    harness.run();
    let graph = eframe::egui::containers::panel::PanelState::load(
        &harness.ctx,
        eframe::egui::Id::new("strategy_graph"),
    )
    .unwrap()
    .rect;
    assert!(
        graph.height() >= 280.0,
        "Initial empty graph collapsed: {graph:?}"
    );
}

#[test]
fn pipeline_circles_stop_at_first_red_and_use_requested_colors() {
    use eframe::egui::Color32;
    use ifm_ui_egui::strategy_workspace::{WorkflowActor, pipeline_color, visible_pipeline_count};
    assert_eq!(pipeline_color("Processing"), Color32::YELLOW);
    assert_eq!(pipeline_color("Continued"), Color32::GREEN);
    for state in ["Stopped", "Failed", "Failure"] {
        assert_eq!(pipeline_color(state), Color32::RED);
    }
    let actors: Vec<_> = ["Continued", "Processing", "Stopped", "Continued"]
        .iter()
        .map(|state| WorkflowActor {
            name: "actor".into(),
            value: "status".into(),
            state: (*state).into(),
        })
        .collect();
    assert_eq!(visible_pipeline_count(&actors), 3);
    assert_eq!(visible_pipeline_count(&actors[..2]), 2);
    assert_eq!(visible_pipeline_count(&[]), 0);
}
