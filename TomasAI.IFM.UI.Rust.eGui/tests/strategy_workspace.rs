use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog, DetailsView, Snapshot,
    strategy_workspace::{StrategyTab, StrategyWorkspace},
};

/// State used by the strategy workspace test fixtures.
struct State {
    /// Workspace associated with this state.
    workspace: StrategyWorkspace,
    /// Details associated with this state.
    details: DetailsView,
    /// Ordered actions retained by State.
    actions: Vec<u32>,
}


/// Provides fixture for the strategy workspace implementation.
fn fixture(rows: usize) -> (Catalog, Snapshot) {
    let catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let entries:Vec<_>=(0..rows).map(|i|serde_json::json!({"id":format!("workflow-{i}"),"action":1000+i,"triggerIdentity":"signal-1","occurredOn":format!("2026-10-13T00:00:{i:02}Z"),"signal":"TrendDirectionChanged","trend":"UpTrend","futuresPrice":"6200.25","outcome":"PipelineFailed","endState":"RegimeDiscovery","actors":[]})).collect();
    let snapshot=serde_json::from_value(serde_json::json!({"schema":1,"epoch":"workspace","generation":1,"mode":"Fixture","workflowId":"workflow-0","revision":1,"roots":[],"rows":entries,"pageNumber":1,"pageCount":2,"hasMore":true,"totalRows":rows,"signals":[{"identity":"signal-1","utcSeconds":1791849600.0,"price":6200.25,"direction":"Up","tooltip":"Signal"}],"summary":[{"title":"Regime Discovery","state":"Failed","summary":"Missing model","content":"Authoritative failure details"}],"header":"Workflow 0"})).unwrap();
    (catalog, snapshot)
}


/// Verifies that tabs match winforms summary placeholder and emit one paging action.
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


/// Verifies that workflow updates virtualize one hundred thousand rows.
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


/// Verifies that graph keeps reserved height when initial snapshot has no signals.
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


/// Legend wrapping must not grow the graph panel over successive live frames.
#[test]
fn wrapped_legend_does_not_expand_graph_over_successive_frames() {
    for width in [430.0, 1000.0] {
        for empty in [false, true] {
            let (catalog, mut snapshot) = fixture(1);
            if empty {
                snapshot.signals.clear();
            }
            let mut harness = Harness::builder()
                .with_size(eframe::egui::vec2(width, 900.0))
                .build_state(
                    move |ctx, state: &mut State| {
                        state.workspace.draw(ctx, &snapshot, &catalog, &mut state.details);
                    },
                    State {
                        workspace: StrategyWorkspace::for_shell(),
                        details: Default::default(),
                        actions: vec![],
                    },
                );
            ifm_ui_egui::apply_winforms_typography(&harness.ctx).unwrap();
            harness.run();
            let initial = eframe::egui::containers::panel::PanelState::load(
                &harness.ctx,
                eframe::egui::Id::new("strategy_graph"),
            ).unwrap().rect.height();
            for _ in 0..120 {
                harness.step();
            }
            let final_height = eframe::egui::containers::panel::PanelState::load(
                &harness.ctx,
                eframe::egui::Id::new("strategy_graph"),
            ).unwrap().rect.height();
            assert!(
                (final_height - initial).abs() < 1.0,
                "graph grows without dragging: width={width}, empty={empty}, initial={initial}, final={final_height}"
            );
            assert!(harness.query_by_label("Strategy Updates").is_some());
        }
    }
}


/// Verifies terminal decisions use distinct colors and end the indicator sequence.
#[test]
fn pipeline_circles_distinguish_no_trade_from_errors() {
    use eframe::egui::Color32;
    use ifm_ui_egui::strategy_workspace::{WorkflowActor, pipeline_color, visible_pipeline_count};
    assert_eq!(pipeline_color("Processing"), Color32::YELLOW);
    assert_eq!(pipeline_color("Continued"), Color32::GREEN);
    assert_eq!(pipeline_color("No Trade"), Color32::from_rgb(90, 155, 255));
    for state in ["Stopped", "Failed", "Failure"] {
        assert_eq!(pipeline_color(state), Color32::RED);
    }
    let mut actors: Vec<_> = ["Continued", "Processing", "Stopped", "Continued"]
        .iter()
        .map(|state| WorkflowActor {
            name: "actor".into(),
            value: "status".into(),
            state: (*state).into(),
        })
        .collect();
    assert_eq!(visible_pipeline_count(&actors), 3);
    actors[2].state = "No Trade".into();
    assert_eq!(visible_pipeline_count(&actors), 3);
    assert_eq!(visible_pipeline_count(&actors[..2]), 2);
    assert_eq!(visible_pipeline_count(&[]), 0);
}


/// Strategy columns must reach the horizontal scrollbar for empty, fitting and overflowing workflow sets.
#[test]
fn updates_grid_fills_the_viewport_down_to_the_horizontal_scrollbar() {
    use eframe::egui;
    for scale in [1.0, 2.0] {
        for height in [760., 815.] {
            for rows in [0, 1, 100] {
                let (catalog, snapshot) = fixture(rows);
                let mut harness = Harness::builder()
                    .with_size(egui::vec2(430., height))
                    .with_pixels_per_point(scale)
                    .build_state(
                        move |ctx, state: &mut State| {
                            ifm_ui_egui::presentation::apply(ctx);
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
                ifm_ui_egui::apply_winforms_typography(&harness.ctx).unwrap();
                harness.run();
                let thumb = harness
                    .output()
                    .shapes
                    .iter()
                    .filter_map(|shape| {
                        if let egui::Shape::Rect(rect) = &shape.shape {
                            if rect.fill == ifm_ui_egui::presentation::SCROLLBAR
                                && rect.rect.width() > rect.rect.height()
                            {
                                return Some(rect.rect);
                            }
                        }
                        None
                    })
                    .next()
                    .expect("Strategy Updates must retain its horizontal scrollbar");
                let pixel = 1. / harness.ctx.pixels_per_point();
                let bottom =
                    thumb.top() - harness.ctx.style().spacing.scroll.bar_inner_margin - pixel / 2.;
                let mut columns: Vec<_> = harness
                    .output()
                    .shapes
                    .iter()
                    .filter_map(|shape| {
                        if let egui::Shape::LineSegment { points, stroke } = &shape.shape {
                            if stroke.color == ifm_ui_egui::presentation::BORDER
                                && points[0].x == points[1].x
                                && points[1].y - points[0].y > 100.
                                && (points[1].y - bottom).abs() < pixel
                            {
                                return Some(points[0].x);
                            }
                        }
                        None
                    })
                    .collect();
                columns.sort_by(f32::total_cmp);
                columns.dedup_by(|a, b| (*a - *b).abs() < pixel);
                assert!(
                    columns.len() >= 3,
                    "columns stop above scrollbar: rows={rows}, scale={scale}, height={height}, bottom={bottom}, columns={columns:?}"
                );
                assert!(harness.query_by_label("Date/Time").is_some());
                if rows == 0 {
                    assert!(
                        harness
                            .query_by_label("No workflows in this timeframe")
                            .is_some()
                    );
                }
                if scale == 1.0 && height == 760. {
                    if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
                        harness
                            .render()
                            .unwrap()
                            .save(
                                std::path::Path::new(&directory)
                                    .join(format!("strategy-updates-{rows}.png")),
                            )
                            .unwrap();
                    }
                }
            }
        }
    }
}
