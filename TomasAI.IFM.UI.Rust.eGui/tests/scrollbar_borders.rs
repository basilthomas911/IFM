use eframe::egui;
use egui_kittest::Harness;
use ifm_ui_egui::control_parity::BorderedScrollArea as _;

/// Verify content-facing edges join at one physical pixel and stay within their viewport.
fn assert_inner_edges(output: &egui::FullOutput, viewport: egui::Rect, pixel: f32) {
    let right = viewport.right() - pixel / 2.0;
    let bottom = viewport.bottom() - pixel / 2.0;
    let mut vertical = false;
    let mut horizontal = false;
    for shape in &output.shapes {
        if let egui::Shape::LineSegment { points, stroke } = &shape.shape {
            if stroke.color != ifm_ui_egui::presentation::BORDER
                || (stroke.width - pixel).abs() > 0.01
            {
                continue;
            }
            if (points[0].x - right).abs() < 0.01
                && points[0].x == points[1].x
                && (points[0].y - viewport.top()).abs() < 0.01
                && (points[1].y - bottom).abs() < 0.01
            {
                vertical = true;
                assert!(shape.clip_rect.right() <= viewport.right());
            }
            if (points[0].y - bottom).abs() < 0.01
                && points[0].y == points[1].y
                && (points[0].x - viewport.left()).abs() < 0.01
                && (points[1].x - right).abs() < 0.01
            {
                horizontal = true;
                assert!(shape.clip_rect.bottom() <= viewport.bottom());
            }
        }
    }
    assert!(vertical, "missing right inner border at {viewport:?}");
    assert!(horizontal, "missing bottom inner border at {viewport:?}");
}


/// Both scroll axes retain their inner border as content overflows and later fits again, with thumbs shown or hidden.
#[test]
fn borders_follow_both_scroll_axes_with_and_without_visible_bars() {
    for scale in [1.0, 2.0] {
        for hidden in [false, true] {
            let ctx = egui::Context::default();
            ctx.set_pixels_per_point(scale);
            ifm_ui_egui::presentation::apply(&ctx);
            for content in [
                egui::vec2(0., 0.),
                egui::vec2(40., 20.),
                egui::vec2(800., 20.),
                egui::vec2(40., 800.),
                egui::vec2(800., 800.),
                egui::vec2(40., 20.),
            ] {
                for _ in 0..4 {
                    let mut viewport = egui::Rect::NOTHING;
                    let output = ctx.run(
                        egui::RawInput {
                            screen_rect: Some(egui::Rect::from_min_size(
                                egui::Pos2::ZERO,
                                egui::vec2(600., 400.),
                            )),
                            ..Default::default()
                        },
                        |ctx| {
                            egui::CentralPanel::default().show(ctx, |ui| {
                                viewport = egui::ScrollArea::both()
                                    .id_salt("border-transition")
                                    .scroll_bar_visibility(if hidden {
                                        egui::scroll_area::ScrollBarVisibility::AlwaysHidden
                                    } else {
                                        egui::scroll_area::ScrollBarVisibility::VisibleWhenNeeded
                                    })
                                    .auto_shrink([false, false])
                                    .max_width(260.)
                                    .max_height(180.)
                                    .bordered_show(ui, |ui| {
                                        ui.allocate_space(content);
                                    })
                                    .inner_rect;
                            });
                        },
                    );
                    assert_inner_edges(&output, viewport, 1.0 / ctx.pixels_per_point());
                }
            }
        }
    }
}


/// Adding viewport edges must preserve visible-row virtualization and allow empty grids.
#[test]
fn virtualized_rows_keep_borders_without_rendering_the_full_dataset() {
    let ctx = egui::Context::default();
    ifm_ui_egui::presentation::apply(&ctx);
    for count in [0, 4, 10000, 4] {
        for _ in 0..4 {
            let mut viewport = egui::Rect::NOTHING;
            let mut drawn = 0;
            let output = ctx.run(
                egui::RawInput {
                    screen_rect: Some(egui::Rect::from_min_size(
                        egui::Pos2::ZERO,
                        egui::vec2(600., 400.),
                    )),
                    ..Default::default()
                },
                |ctx| {
                    egui::CentralPanel::default().show(ctx, |ui| {
                        viewport = egui::ScrollArea::vertical()
                            .auto_shrink([false, false])
                            .max_height(180.)
                            .bordered_show_rows(ui, 20., count, |ui, rows| {
                                drawn = rows.len();
                                for row in rows {
                                    ui.label(format!("row {row}"));
                                }
                            })
                            .inner_rect;
                    });
                },
            );
            assert_inner_edges(&output, viewport, 1.0 / ctx.pixels_per_point());
            assert!(drawn <= count);
            assert!(drawn < 40, "border rendering disabled row virtualization");
        }
    }
}


/// Render the real shared report grid for optional visual verification, including an empty body and both scrollbars.
#[test]
fn report_grid_border_preview() {
    for (name, rows, width) in [
        ("empty", 0, 120.),
        ("fitting", 3, 120.),
        ("scrolling", 100, 700.),
    ] {
        let mut harness = Harness::builder()
            .with_size(egui::vec2(520., 340.))
            .build_ui(move |ui| {
                ifm_ui_egui::presentation::apply(ui.ctx());
                ifm_ui_egui::control_parity::report_list(
                    ui,
                    "border-preview",
                    &[("Id", 80.), ("Published value", width)],
                    rows,
                    None,
                    240.,
                    |row, col| format!("Row {row}, column {col}"),
                );
            });
        harness.run();
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            let image = harness.render().unwrap();
            if name == "scrolling" {
                let thumbs: Vec<_> = harness
                    .output()
                    .shapes
                    .iter()
                    .filter_map(|shape| {
                        if let egui::Shape::Rect(rect) = &shape.shape {
                            if rect.fill == ifm_ui_egui::presentation::SCROLLBAR {
                                return Some(rect.rect);
                            }
                        }
                        None
                    })
                    .collect();
                let vertical = thumbs.iter().find(|r| r.height() > r.width()).unwrap();
                let horizontal = thumbs.iter().find(|r| r.width() > r.height()).unwrap();
                let margin = harness.ctx.style().spacing.scroll.bar_inner_margin;
                let left = (vertical.left() - margin - 2.0).floor() as u32;
                let right = vertical.left().floor() as u32;
                let top = vertical.bottom().ceil() as u32 + 2;
                let bottom = horizontal.top().floor() as u32 - 2;
                let color = ifm_ui_egui::presentation::BORDER;
                assert!(bottom > top);
                assert!(
                    (left..right).any(|x| (top..bottom).all(|y| {
                        let pixel = image.get_pixel(x, y).0;
                        pixel[0].abs_diff(color.r()) <= 1
                            && pixel[1].abs_diff(color.g()) <= 1
                            && pixel[2].abs_diff(color.b()) <= 1
                    })),
                    "rendered preview is missing the continuous inner vertical border"
                );
            }
            image
                .save(std::path::Path::new(&directory).join(format!("scroll-border-{name}.png")))
                .unwrap();
        }
    }
}


/// A wide report grid relocates its vertical thumb into the parent clip; its inner border must follow it.
#[test]
fn wide_report_grid_keeps_vertical_border_next_to_the_visible_thumb() {
    for scale in [1.0, 2.0] {
        let ctx = egui::Context::default();
        ctx.set_pixels_per_point(scale);
        ifm_ui_egui::presentation::apply(&ctx);
        let mut output = None;
        for _ in 0..8 {
            output = Some(ctx.run(
                egui::RawInput {
                    screen_rect: Some(egui::Rect::from_min_size(
                        egui::Pos2::ZERO,
                        egui::vec2(520., 340.),
                    )),
                    ..Default::default()
                },
                |ctx| {
                    egui::CentralPanel::default().show(ctx, |ui| {
                        ifm_ui_egui::control_parity::report_list(
                            ui,
                            "wide-report",
                            &[("Id", 80.), ("Value", 1400.)],
                            100,
                            None,
                            240.,
                            |row, col| format!("{row}:{col}"),
                        );
                    });
                },
            ));
        }
        let output = output.unwrap();
        let thumb = output
            .shapes
            .iter()
            .filter_map(|shape| {
                if let egui::Shape::Rect(rect) = &shape.shape {
                    if rect.fill == ifm_ui_egui::presentation::SCROLLBAR
                        && rect.rect.height() > rect.rect.width()
                        && rect.rect.width() > 2.
                    {
                        return Some(rect.rect);
                    }
                }
                None
            })
            .next()
            .expect("wide overflowing report must expose its vertical thumb");
        let pixel = 1.0 / ctx.pixels_per_point();
        let margin = ctx.style().spacing.scroll.bar_inner_margin;
        let border = output.shapes.iter().any(|shape| {
            if let egui::Shape::LineSegment { points, stroke } = &shape.shape {
                return stroke.color == ifm_ui_egui::presentation::BORDER
                    && points[0].x == points[1].x
                    && points[0].x < thumb.left()
                    && thumb.left() - points[0].x <= margin + 2.0 * pixel
                    && points[0].y <= thumb.top()
                    && points[1].y >= thumb.bottom()
                    && shape.clip_rect.contains(points[0])
                    && shape.clip_rect.contains(points[1]);
            }
            false
        });
        assert!(
            border,
            "missing visible inner vertical border: scale={scale}, thumb={thumb:?}"
        );
    }
}
