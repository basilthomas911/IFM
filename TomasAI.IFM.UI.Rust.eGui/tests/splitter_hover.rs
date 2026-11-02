use eframe::egui;
use ifm_ui_egui::{
    Catalog, DetailsView, Snapshot, shell::MainShell, strategy_workspace::StrategyWorkspace,
};

/// Pointer motion and idle repaints must preserve widths for every period, DPI and publication state.
#[test]
fn pointer_motion_without_a_drag_keeps_sidebar_widths_stable() {
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    for published in [false, true] {
        for period in 0..3 {
            for scale in [1.0, 2.0] {
                for size in [egui::vec2(1600., 900.), egui::vec2(1920., 1080.)] {
                    let ctx = egui::Context::default();
                    ctx.set_pixels_per_point(scale);
                    ifm_ui_egui::apply_winforms_typography(&ctx).unwrap();
                    ifm_ui_egui::presentation::apply(&ctx);
                    let mut shell = MainShell::default();
                    shell.time_frame = period;
                    let mut strategy = StrategyWorkspace::default();
                    let mut details = DetailsView::default();
                    let snapshot = Snapshot {
                        contract_id: "ES20261218".into(),
                        ..Default::default()
                    };
                    let mut baseline = None;
                    for frame in 0..60 {
                        let events = if frame < 10 {
                            vec![]
                        } else {
                            vec![egui::Event::PointerMoved(egui::pos2(
                                50. + frame as f32 * 20.,
                                250.,
                            ))]
                        };
                        let _ = ctx.run(
                            egui::RawInput {
                                screen_rect: Some(egui::Rect::from_min_size(
                                    egui::Pos2::ZERO,
                                    size,
                                )),
                                events,
                                ..Default::default()
                            },
                            |ctx| {
                                shell.draw_panes(
                                    ctx,
                                    published.then_some(&snapshot),
                                    &catalog,
                                    &mut strategy,
                                    &mut details,
                                );
                                egui::CentralPanel::default().show(ctx, |_| {});
                            },
                        );
                        assert!(!ctx.input(|input| input.pointer.primary_down()));
                        let widths = ["operations-pane", "market-pane"].map(|id| {
                            egui::containers::panel::PanelState::load(&ctx, egui::Id::new(id))
                                .unwrap()
                                .rect
                                .width()
                        });
                        // Egui applies a requested DPI change on its next frame; let the
                        // viewport settle before checking ordinary idle/hover repaints.
                        if frame < 3 {
                            continue;
                        }
                        let expected = *baseline.get_or_insert(widths);
                        assert_eq!(
                            widths, expected,
                            "pane changed without drag: published={published}, period={period}, scale={scale}, viewport={size:?}, frame={frame}"
                        );
                    }
                }
            }
        }
    }
}


/// Intentional resizing still works and subsequent pointer movement after release must leave it fixed.
#[test]
fn intentional_resize_stops_changing_after_mouse_release() {
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    for name in ["operations-pane", "market-pane"] {
        let ctx = egui::Context::default();
        ifm_ui_egui::apply_winforms_typography(&ctx).unwrap();
        ifm_ui_egui::presentation::apply(&ctx);
        let mut shell = MainShell::default();
        let mut strategy = StrategyWorkspace::default();
        let mut details = DetailsView::default();
        let mut frame = |events| {
            let _ = ctx.run(
                egui::RawInput {
                    screen_rect: Some(egui::Rect::from_min_size(
                        egui::Pos2::ZERO,
                        egui::vec2(1600., 900.),
                    )),
                    events,
                    ..Default::default()
                },
                |ctx| {
                    shell.draw_panes(ctx, None, &catalog, &mut strategy, &mut details);
                    egui::CentralPanel::default().show(ctx, |_| {});
                },
            );
            egui::containers::panel::PanelState::load(&ctx, egui::Id::new(name))
                .unwrap()
                .rect
        };
        let initial = frame(vec![]);
        let left = name == "operations-pane";
        let start = egui::pos2(
            if left {
                initial.right()
            } else {
                initial.left()
            },
            initial.center().y,
        );
        frame(vec![
            egui::Event::PointerMoved(start),
            egui::Event::PointerButton {
                pos: start,
                button: egui::PointerButton::Primary,
                pressed: true,
                modifiers: Default::default(),
            },
        ]);
        let moved = start + egui::vec2(if left { 80. } else { -80. }, 0.);
        frame(vec![egui::Event::PointerMoved(moved)]);
        let resized = frame(vec![]);
        assert!(
            (resized.width() - initial.width() - 80.).abs() < 1.,
            "resize failed for {name}"
        );
        frame(vec![egui::Event::PointerButton {
            pos: moved,
            button: egui::PointerButton::Primary,
            pressed: false,
            modifiers: Default::default(),
        }]);
        for index in 0..20 {
            let rect = frame(vec![egui::Event::PointerMoved(egui::pos2(
                100. + index as f32 * 40.,
                400.,
            ))]);
            // Panel edges may snap by a fraction of a pixel after releasing a drag.
            assert!(
                (rect.width() - resized.width()).abs() < 1.0 / ctx.pixels_per_point(),
                "pane width changed after release: {name}: {resized:?} -> {rect:?}"
            );
            assert!(
                (rect.left() - resized.left()).abs() < 1.0 / ctx.pixels_per_point(),
                "pane edge moved after release: {name}: {resized:?} -> {rect:?}"
            );
        }
    }
}
