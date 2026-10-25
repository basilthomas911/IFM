use eframe::egui;
use ifm_ui_egui::presentation;

#[test]
fn shared_theme_matches_winforms_control_baseline() {
    let ctx = egui::Context::default();
    ifm_ui_egui::apply_winforms_typography(&ctx).unwrap();
    let style = ctx.style();
    assert_eq!(style.visuals.panel_fill, egui::Color32::BLACK);
    assert_eq!(style.visuals.window_fill, egui::Color32::BLACK);
    assert_eq!(style.visuals.extreme_bg_color, egui::Color32::BLACK);
    assert_eq!(
        style.visuals.override_text_color,
        Some(egui::Color32::WHITE)
    );
    assert_eq!(
        style.visuals.widgets.inactive.bg_fill,
        presentation::COMMAND
    );
    assert_eq!(style.visuals.widgets.hovered.bg_fill, presentation::HOVER);
    assert_eq!(style.visuals.widgets.active.bg_fill, presentation::PRESSED);
    assert!(style.spacing.scroll.foreground_color);
    for widget in [
        &style.visuals.widgets.noninteractive,
        &style.visuals.widgets.inactive,
        &style.visuals.widgets.hovered,
        &style.visuals.widgets.active,
        &style.visuals.widgets.open,
    ] {
        assert_eq!(
            widget.fg_stroke.color,
            egui::Color32::from_rgb(128, 128, 128)
        );
    }
    assert_eq!(
        style.visuals.widgets.inactive.corner_radius,
        egui::CornerRadius::ZERO
    );
    assert_eq!(style.spacing.item_spacing, egui::vec2(4.0, 4.0));
    assert!(
        style
            .text_styles
            .values()
            .all(|f| (f.size - 13.333333).abs() < 0.001)
    );
}

#[test]
fn details_tree_matches_winforms_split_at_two_window_sizes() {
    use egui_kittest::Harness;
    use ifm_ui_egui::{Catalog, DetailsView, Snapshot};
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let snapshot: Snapshot = serde_json::from_str(r#"{"schema":1,"epoch":"layout","generation":1,"mode":"Fixture","workflowId":"layout","revision":1,"roots":[]}"#).unwrap();
    for size in [egui::vec2(1100.0, 800.0), egui::vec2(1600.0, 900.0)] {
        let mut harness = Harness::builder().with_size(size).build_state(
            |ctx, view: &mut DetailsView| view.draw(ctx, &snapshot, &catalog),
            DetailsView::default(),
        );
        ifm_ui_egui::apply_winforms_typography(&harness.ctx).unwrap();
        harness.run();
        let state =
            egui::containers::panel::PanelState::load(&harness.ctx, egui::Id::new("objects"))
                .unwrap();
        let expected = size.y * presentation::DETAILS_TREE_RATIO;
        assert!(
            (state.rect.height() - expected).abs() <= 2.0,
            "{size:?}: {:?} expected {expected}",
            state.rect
        );
    }
}

#[cfg(feature = "visual-tests")]
#[test]
fn render_dark_trading_details_for_review() {
    use egui_kittest::Harness;
    use ifm_ui_egui::{Catalog, DetailsView, Snapshot};
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let snapshot: Snapshot = serde_json::from_str(r#"{"schema":1,"epoch":"review","generation":1,"mode":"Fixture","workflowId":"review","revision":1,"roots":[{"id":"workflow","label":"Workflow","properties":[{"name":"State","value":"Completed"}],"children":[]},{"id":"signal","label":"ITI Signal","properties":[{"name":"ValueDate","value":"2026-10-13"},{"name":"Price","value":"6000.25"}],"children":[]},{"id":"pipeline","label":"Workflow Pipeline","properties":[],"children":[]}]}"#).unwrap();
    let mut harness = Harness::builder()
        .with_size(egui::vec2(1100.0, 800.0))
        .build_state(
            |ctx, view: &mut DetailsView| view.draw(ctx, &snapshot, &catalog),
            DetailsView {
                selected: Some("signal".into()),
            },
        );
    ifm_ui_egui::apply_winforms_typography(&harness.ctx).unwrap();
    harness.run();
    let output = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .unwrap()
        .join(".artifacts/rust-ui/tests/dark-trading-details.png");
    std::fs::create_dir_all(output.parent().unwrap()).unwrap();
    harness.render().unwrap().save(output).unwrap();
}

#[test]
fn feed_toolbar_uses_exact_winforms_rgb_and_foreground_colors() {
    assert_eq!(
        presentation::feed_command_palette(Some(true)),
        (egui::Color32::RED, egui::Color32::WHITE)
    );
    assert_eq!(
        presentation::feed_command_palette(Some(false)),
        (egui::Color32::from_rgb(50, 205, 50), egui::Color32::BLACK)
    );
    for (state, rgb, foreground) in [
        ("Healthy", [50, 205, 50], egui::Color32::BLACK),
        ("Intermittent", [255, 255, 0], egui::Color32::BLACK),
        ("Critical", [255, 0, 0], egui::Color32::WHITE),
        ("Recovering", [255, 140, 0], egui::Color32::BLACK),
        ("OffHoursActive", [70, 130, 180], egui::Color32::WHITE),
        ("OffHoursDegraded", [255, 140, 0], egui::Color32::BLACK),
        ("Unknown", [105, 105, 105], egui::Color32::WHITE),
    ] {
        assert_eq!(
            presentation::feed_health_palette(state),
            (egui::Color32::from_rgb(rgb[0], rgb[1], rgb[2]), foreground)
        );
    }
    assert_eq!(
        presentation::feed_health_text("Healthy"),
        "Feed Health: Green"
    );
    assert_eq!(
        presentation::feed_health_text("Unknown"),
        "Feed Health: Unknown"
    );
}

#[test]
fn feed_toolbar_clicks_are_enabled_only_when_the_shared_model_allows_them() {
    use egui_kittest::{
        Harness,
        kittest::{NodeT, Queryable},
    };
    use ifm_ui_egui::sidebar::FeedControl;
    for active in [true, false] {
        let state = FeedControl {
            ready: true,
            can_toggle: true,
            busy: false,
            active,
        };
        let mut h = Harness::new_state(
            move |ctx, clicks: &mut usize| {
                egui::CentralPanel::default().show(ctx, |ui| {
                    if presentation::feed_command_button(ui, &state) {
                        *clicks += 1;
                    }
                });
            },
            0usize,
        );
        h.run();
        h.get_by_label(if active {
            "Stop Market Feeds"
        } else {
            "Start Market Feeds"
        })
        .click();
        h.run();
        assert_eq!(*h.state(), 1);
    }
    for state in [
        FeedControl {
            ready: false,
            can_toggle: true,
            busy: false,
            active: true,
        },
        FeedControl {
            ready: true,
            can_toggle: false,
            busy: false,
            active: true,
        },
        FeedControl {
            ready: true,
            can_toggle: true,
            busy: true,
            active: true,
        },
    ] {
        let mut h = Harness::new_state(
            move |ctx, clicks: &mut usize| {
                egui::CentralPanel::default().show(ctx, |ui| {
                    if presentation::feed_command_button(ui, &state) {
                        *clicks += 1;
                    }
                });
            },
            0usize,
        );
        h.run();
        assert!(
            h.get_by_label("Stop Market Feeds")
                .accesskit_node()
                .is_disabled()
        );
        assert_eq!(*h.state(), 0);
    }
}
