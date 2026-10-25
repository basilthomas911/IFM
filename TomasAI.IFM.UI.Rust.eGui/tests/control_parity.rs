use eframe::egui;
use egui_kittest::{
    Harness,
    kittest::{NodeT, Queryable},
};

#[test]
fn report_grid_reclaims_hidden_scrollbar_gutters() {
    let ctx = egui::Context::default();
    ifm_ui_egui::presentation::apply(&ctx);
    for rows in [0, 1, 30, 1] {
        let mut bounds = egui::Rect::NOTHING;
        let mut output = None;
        for _ in 0..4 {
            output = Some(ctx.run(
                egui::RawInput {
                    screen_rect: Some(egui::Rect::from_min_size(
                        egui::Pos2::ZERO,
                        egui::vec2(600., 400.),
                    )),
                    ..Default::default()
                },
                |ctx| {
                    egui::CentralPanel::default().show(ctx, |ui| {
                        bounds = egui::Rect::from_min_size(
                            ui.cursor().min,
                            egui::vec2(ui.available_width(), 180.),
                        );
                        ifm_ui_egui::control_parity::report_list(
                            ui,
                            "fit-grid",
                            &[("Id", 80.), ("Value", 120.)],
                            rows,
                            None,
                            180.,
                            |_, _| "Value".into(),
                        );
                    });
                },
            ));
        }
        let output = output.unwrap();
        let header_right = output
            .shapes
            .iter()
            .filter_map(|shape| match &shape.shape {
                egui::Shape::Rect(rect) if rect.fill == ifm_ui_egui::presentation::COMMAND => {
                    Some(rect.rect.right())
                }
                _ => None,
            })
            .fold(f32::NEG_INFINITY, f32::max);
        let expected_right = bounds.right()
            - 1. / ctx.pixels_per_point()
            - if rows == 30 {
                ctx.style().spacing.scroll.allocated_width()
            } else {
                0.
            };
        assert!(
            (header_right - expected_right).abs() < 1.,
            "Header does not match the actual scrollbar visibility: rows={rows}, {header_right} vs {expected_right}"
        );
        let bottom = output
            .shapes
            .iter()
            .filter_map(|shape| match &shape.shape {
                egui::Shape::LineSegment { points, stroke }
                    if stroke.color == ifm_ui_egui::presentation::BORDER
                        && points[0].x == points[1].x =>
                {
                    Some(points[1].y)
                }
                _ => None,
            })
            .fold(f32::NEG_INFINITY, f32::max);
        assert!(
            (bottom - (bounds.bottom() - 1. / ctx.pixels_per_point())).abs() < 1.,
            "Gridlines stop at a hidden horizontal scrollbar gutter: {bottom} vs {bounds:?}"
        );
    }
}
#[test]
fn report_list_selects_row_from_empty_space_after_last_column() {
    let mut h = Harness::new_ui_state(
        |ui, state: &mut Option<usize>| {
            let (result, _) = ifm_ui_egui::control_parity::report_list(
                ui,
                "orders",
                &[("Order Id", 80.), ("Reference", 200.)],
                2,
                *state,
                300.,
                |row, column| format!("{row}:{column}"),
            );
            if result.is_some() {
                *state = result;
            }
        },
        None,
    );
    h.set_size(egui::vec2(800., 400.));
    h.run();
    let pos = egui::pos2(650., 65.);
    h.input_mut().events.push(egui::Event::PointerMoved(pos));
    h.input_mut().events.push(egui::Event::PointerButton {
        pos,
        button: egui::PointerButton::Primary,
        pressed: true,
        modifiers: egui::Modifiers::NONE,
    });
    h.run();
    h.input_mut().events.push(egui::Event::PointerButton {
        pos,
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: egui::Modifiers::NONE,
    });
    h.run();
    assert_eq!(*h.state(), Some(1));
    assert!(h.query_by_label("1:0").is_some());
}
#[test]
fn report_list_keeps_wide_columns_inside_viewport() {
    let mut h = Harness::new_ui(|ui| {
        ifm_ui_egui::control_parity::report_list(
            ui,
            "wide",
            &[("Reference", 800.), ("Source", 150.)],
            100000,
            None,
            300.,
            |row, _| row.to_string(),
        );
    });
    h.set_size(egui::vec2(600., 400.));
    h.run();
    assert!(h.query_by_label("Reference").is_some());
}

#[test]
fn report_columns_resize_by_dragging_the_header_divider() {
    let mut h = Harness::new_ui(|ui| {
        ifm_ui_egui::control_parity::report_list(
            ui,
            "resizing",
            &[("Order Id", 80.), ("Reference", 200.)],
            2,
            None,
            300.,
            |row, _| row.to_string(),
        );
    });
    h.set_size(egui::vec2(800., 400.));
    h.run();
    let before = h
        .get_by_label("Reference")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    let start = egui::pos2(before as f32, 20.);
    let end = start + egui::vec2(60., 0.);
    h.input_mut().events.extend([
        egui::Event::PointerMoved(start),
        egui::Event::PointerButton {
            pos: start,
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: egui::Modifiers::NONE,
        },
    ]);
    h.run();
    h.input_mut().events.push(egui::Event::PointerMoved(end));
    h.run();
    h.input_mut().events.push(egui::Event::PointerButton {
        pos: end,
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: egui::Modifiers::NONE,
    });
    h.run();
    let after = h
        .get_by_label("Reference")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    assert!(
        after - before >= 40.,
        "Divider did not move: {before} -> {after}"
    );
    h.run();
    assert_eq!(
        after,
        h.get_by_label("Reference")
            .accesskit_node()
            .bounding_box()
            .unwrap()
            .x0
    );
}

#[test]
fn report_columns_keep_initial_widths_inside_a_new_window() {
    let mut h = Harness::new(|ctx| {
        egui::Window::new("Orders")
            .fixed_size([1300., 400.])
            .show(ctx, |ui| {
                ifm_ui_egui::control_parity::report_list(
                    ui,
                    "initial-window",
                    &[("Order Id", 80.), ("Reference", 800.), ("Source", 150.)],
                    1,
                    None,
                    300.,
                    |_, _| String::new(),
                );
            });
    });
    h.set_size(egui::vec2(1500., 600.));
    h.run();
    let order = h
        .get_by_label("Order Id")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    let reference = h
        .get_by_label("Reference")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    let source = h
        .get_by_label("Source")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    assert!(reference - order >= 79., "First column collapsed");
    assert!(source - reference >= 799., "Reference column collapsed");
}

#[test]
fn grid_headers_and_scrollbars_stay_inside_border_at_multiple_dpi_scales() {
    for scale in [1., 1.25, 1.5, 2.] {
        for rows in [0, 1, 100] {
            let ctx = egui::Context::default();
            ctx.set_pixels_per_point(scale);
            ifm_ui_egui::presentation::apply(&ctx);
            let mut bounds = egui::Rect::NOTHING;
            let mut output = None;
            for _ in 0..4 {
                output = Some(ctx.run(
                    egui::RawInput {
                        screen_rect: Some(egui::Rect::from_min_size(
                            egui::Pos2::ZERO,
                            egui::vec2(600., 400.),
                        )),
                        ..Default::default()
                    },
                    |ctx| {
                        egui::CentralPanel::default().show(ctx, |ui| {
                            bounds = egui::Rect::from_min_size(
                                ui.cursor().min,
                                egui::vec2(ui.available_width(), 180.),
                            );
                            ifm_ui_egui::control_parity::report_list(
                                ui,
                                "border-grid",
                                &[("First", 400.), ("Second", 500.)],
                                rows,
                                None,
                                180.,
                                |_, _| "Value".into(),
                            );
                        });
                    },
                ));
            }
            let inside = bounds.shrink(1. / ctx.pixels_per_point());
            let mut headers = 0;
            for shape in output.unwrap().shapes {
                if let egui::Shape::Rect(rect) = shape.shape {
                    if rect.fill == ifm_ui_egui::presentation::COMMAND
                        || rect.fill == ifm_ui_egui::presentation::SCROLLBAR
                    {
                        let visible = rect.rect.intersect(shape.clip_rect);
                        if visible.is_positive() {
                            assert!(
                                inside.expand(0.01).contains_rect(visible),
                                "Grid paint escaped border at scale {scale}, rows {rows}: {visible:?} vs {inside:?}"
                            );
                            headers += usize::from(rect.fill == ifm_ui_egui::presentation::COMMAND);
                        }
                    }
                }
            }
            assert!(headers > 0);
        }
    }
}
