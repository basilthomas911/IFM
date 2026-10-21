use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{Catalog, DetailsView, shell::MainShell, strategy_workspace::StrategyWorkspace};

#[test]
fn main_shell_has_three_regions_with_winforms_side_widths() {
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    for size in [egui::vec2(1600.0, 900.0), egui::vec2(1920.0, 1080.0)] {
        let mut harness = Harness::builder().with_size(size).build_state(
            |ctx, state: &mut (MainShell, StrategyWorkspace, DetailsView)| {
                egui::TopBottomPanel::top("main-menu")
                    .exact_height(27.0)
                    .show(ctx, |ui| {
                        ui.label("Reference");
                    });
                egui::TopBottomPanel::bottom("main-status")
                    .exact_height(24.0)
                    .show(ctx, |ui| {
                        ui.label("Ready");
                    });
                state
                    .0
                    .draw_panes(ctx, None, &catalog, &mut state.1, &mut state.2);
                egui::CentralPanel::default().show(ctx, |_ui| {});
            },
            (
                MainShell::default(),
                StrategyWorkspace::default(),
                DetailsView::default(),
            ),
        );
        ifm_ui_rust::apply_winforms_typography(&harness.ctx).unwrap();
        harness.run();
        assert_eq!(harness.state().0.time_frame, 0);
        for label in [
            "Strategy",
            "Latency",
            "Traffic",
            "Errors",
            "Saturation",
            "Time Frame:",
            "Market Direction",
            "ES",
            "ES-BB",
            "VX",
            "Economic Calendar",
        ] {
            assert!(harness.query_by_label(label).is_some(), "Missing {label}");
        }
        assert!(harness.query_by_label("Operations").is_none());
        assert!(harness.query_by_label("Status Console").is_none());
        let left = egui::containers::panel::PanelState::load(
            &harness.ctx,
            egui::Id::new("operations-pane"),
        )
        .unwrap()
        .rect;
        let right =
            egui::containers::panel::PanelState::load(&harness.ctx, egui::Id::new("market-pane"))
                .unwrap()
                .rect;
        assert!(
            (left.width() / size.x - 0.22).abs() < 0.01,
            "size={size:?} left={left:?}"
        );
        assert!(
            (right.width() / size.x - 0.22).abs() < 0.01,
            "size={size:?} right={right:?}"
        );
        assert!(left.right() < right.left());
        assert_eq!(left.top(), 27.0);
        assert_eq!(left.bottom(), size.y - 24.0);
    }
}


#[test]
fn both_splitters_match_status_border_at_rest_and_are_three_physical_pixels_cyan_while_dragging() {
    for scale in [1., 2.] {
        for (name, left) in [("operations-pane", true), ("market-pane", false)] {
            let ctx = egui::Context::default();
            ctx.set_pixels_per_point(scale);
            ifm_ui_rust::presentation::apply(&ctx);
            let catalog: Catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
            let mut shell = MainShell::default();
            let mut strategy = StrategyWorkspace::default();
            let mut details = DetailsView::default();
            let mut frame = |events| ctx.run(egui::RawInput {
                screen_rect: Some(egui::Rect::from_min_size(egui::Pos2::ZERO, egui::vec2(1600., 1000.))),
                events, ..Default::default()
            }, |ctx| {
                shell.draw_panes(ctx, None, &catalog, &mut strategy, &mut details);
                egui::CentralPanel::default().show(ctx, |_| {});
            });
            frame(vec![]);
            let idle = frame(vec![]);
            let pane = egui::containers::panel::PanelState::load(&ctx, egui::Id::new(name)).unwrap().rect;
            let edge = if left { pane.right() } else { pane.left() };
            let has_line = |output: &egui::FullOutput, color, physical_width: f32| output.shapes.iter().any(|shape| {
                matches!(&shape.shape, egui::Shape::LineSegment { points, stroke }
                    if stroke.color == color && (stroke.width * ctx.pixels_per_point() - physical_width).abs() < 0.01
                    && (points[0].x - edge).abs() < 40. && points[0].x == points[1].x
                    && (points[1].y - points[0].y).abs() > 500.)
            });
            let border_color = ctx.style().visuals.widgets.noninteractive.bg_stroke.color;
            assert!(has_line(&idle, border_color, 1.), "Idle splitter does not match status border: {name}");
            let start = egui::pos2(edge, pane.center().y);
            frame(vec![egui::Event::PointerMoved(start), egui::Event::PointerButton {
                pos: start, button: egui::PointerButton::Primary, pressed: true, modifiers: Default::default()
            }]);
            let moved = start + egui::vec2(if left { 20. } else { -20. }, 0.);
            let dragging = frame(vec![egui::Event::PointerMoved(moved)]);
            assert!(has_line(&dragging, egui::Color32::from_rgb(0, 191, 255), 3.), "Missing active splitter: {name}");
            let stopped = frame(vec![egui::Event::PointerButton {
                pos: moved, button: egui::PointerButton::Primary, pressed: false, modifiers: Default::default()
            }]);
            assert!(has_line(&stopped, border_color, 1.), "Splitter did not return to status border color: {name}");
        }
    }
}
