use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog, DetailsView,
    actor_health::ActorHealth,
    operations_health::OperationsHealth,
    shell::{MainShell, HealthPane},
    strategy_workspace::StrategyWorkspace,
    contract_editor::ContractPublication,
};

/// Shell and health views retain their own selection and refresh state while switching sidebar tabs.
#[derive(Default)]
struct State {
    /// Main-screen pane navigation.
    shell: MainShell,
    /// Cached strategy renderer.
    strategy: StrategyWorkspace,
    /// Strategy property renderer.
    details: DetailsView,
    /// Processing-stage and dataset selection.
    operations: OperationsHealth,
    /// Actor hierarchy and command state.
    actors: ActorHealth,
    /// Health view actually rendered in the latest frame.
    rendered: Option<HealthPane>,
}


/// Both Operations subtabs and Actors use equal-height, bounded panes inside the sidebar.
#[test]
fn sidebar_health_tabs_embed_equal_vertical_panes_and_preserve_center_workspace() {
    for size in [egui::vec2(1600., 900.), egui::vec2(1920., 1080.)] {
        let catalog: Catalog =
            serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
        let operations: ContractPublication = serde_json::from_value(serde_json::json!({"schema":1,"generation":1,"status":"Ready","busy":false,"outcomes":[],"result":{"view":"operations-health","choices":{},"hasMore":false,"context":{"status":"Green","summary":"Health snapshot","observation":"Central observation"},"rows":[{"id":"stage:Aggregation","fields":{"kind":"stage","Stage":"Aggregation","Status":"Green","Pending":"4"}},{"id":"dataset:GLBX.MDP3","fields":{"kind":"dataset","Dataset":"GLBX.MDP3","Status":"Green","ProcessId":"123"}}]}})).unwrap();
        let actors: ContractPublication = serde_json::from_value(serde_json::json!({"schema":1,"generation":1,"status":"Ready","busy":false,"outcomes":[],"result":{"view":"actor-health","choices":{},"hasMore":false,"context":{"summary":"Actors observed"},"rows":[{"id":"actor:1","fields":{"name":"FixtureActor","parent":"FixtureDomain","status":"Green"}},{"id":"mailbox:1","fields":{"name":"FixtureMailbox","parent":"actor:1","status":"Yellow","Generation":"7"}}]}})).unwrap();
        let mut h = Harness::builder().with_size(size).build_state(
            move |ctx, state: &mut State| {
                ifm_ui_egui::presentation::apply(ctx);
                state.rendered = None;
                state.shell.draw_panes_with_health(
                    ctx,
                    None,
                    &catalog,
                    &mut state.strategy,
                    &mut state.details,
                    |ui, pane| {
                        state.rendered = Some(pane);
                        match pane {
                            HealthPane::Operations => {
                                state
                                    .operations
                                    .draw_inside(ui, &operations, &catalog, false);
                            }
                            HealthPane::Actors => {
                                state.actors.draw_inside(ui, &actors, &catalog, false);
                            }
                        }
                    },
                );
                egui::CentralPanel::default().show(ctx, |ui| {
                    ui.label("Trading workspace stays open");
                });
            },
            State::default(),
        );
        h.run();
        assert_eq!(h.state().rendered, None);
        for removed in ["Latency", "Traffic", "Errors", "Saturation"] {
            assert!(h.query_by_label(removed).is_none());
        }
        h.get_by_label("Operations").click();
        h.run();
        assert_eq!(h.state().rendered, Some(HealthPane::Operations));
        assert_split(h.state().operations.panes.unwrap(), &h.ctx);
        assert!(h.query_all_by_label("Aggregation").next().is_some());
        assert!(h.query_by_label("Pending").is_some());
        h.get_by_label("Dataset workers").click();
        h.run();
        assert_split(h.state().operations.panes.unwrap(), &h.ctx);
        assert!(h.query_all_by_label("GLBX.MDP3").next().is_some());
        assert!(h.query_by_label("Process Id").is_some());
        assert!(h.query_by_label("123").is_some());
        assert!(
            h.query_by_label("Operations Health").is_none(),
            "health must not open a separate window"
        );
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            if size.x == 1920. {
                h.render()
                    .unwrap()
                    .save(std::path::Path::new(&directory).join("operations-sidebar.png"))
                    .unwrap();
            }
        }
        h.get_by_label("Actors").click();
        h.run();
        assert_eq!(h.state().rendered, Some(HealthPane::Actors));
        assert_split(h.state().actors.panes.unwrap(), &h.ctx);
        h.get_by_label("FixtureDomain").click();
        h.run();
        h.get_by_label("FixtureActor").click();
        h.run();
        h.get_by_label("FixtureMailbox").click();
        h.run();
        assert_eq!(h.state().actors.selected.as_deref(), Some("mailbox:1"));
        let right = egui::containers::panel::PanelState::load(&h.ctx, egui::Id::new("market-pane"))
            .unwrap()
            .rect;
        assert!(
            right.left() < size.x && right.right() <= size.x + 1.,
            "right pane escaped viewport: {right:?}"
        );
        let left =
            egui::containers::panel::PanelState::load(&h.ctx, egui::Id::new("operations-pane"))
                .unwrap()
                .rect;
        assert!(
            left.width() < size.x * 0.4,
            "health expanded sidebar: {left:?}"
        );
        assert!(h.query_by_label("Generation").is_some());
        assert!(h.query_by_label("7").is_some());
        assert!(h.query_by_label("Trading workspace stays open").is_some());
        assert!(h.query_by_label("Actor Health").is_none());
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            if size.x == 1920. {
                h.render()
                    .unwrap()
                    .save(std::path::Path::new(&directory).join("actors-sidebar.png"))
                    .unwrap();
            }
        }
        h.get_by_label("Strategy").click();
        h.run();
        assert_eq!(
            h.state().rendered,
            None,
            "hidden health tabs must not render or poll"
        );
        h.get_by_label("Operations").click();
        h.run();
        assert!(
            h.state().operations.datasets,
            "dataset subtab selection should survive switching tabs"
        );
    }
}


/// Check that both content panes fill the sidebar width and divide its remaining height equally.
fn assert_split(panes: [egui::Rect; 2], ctx: &egui::Context) {
    let left = egui::containers::panel::PanelState::load(ctx, egui::Id::new("operations-pane"))
        .unwrap()
        .rect;
    assert!((panes[0].height() - panes[1].height()).abs() <= 1.);
    assert!((panes[0].left() - panes[1].left()).abs() <= 1.);
    assert!((panes[0].right() - panes[1].right()).abs() <= 1.);
    assert!(panes[0].bottom() < panes[1].top());
    assert!(panes[0].left() >= left.left() && panes[1].right() <= left.right());
    assert!(panes[1].bottom() <= left.bottom() + 1.);
}
