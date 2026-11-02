use eframe::egui;
use ifm_ui_egui::control_parity::BorderedTable as _;


/// Checks borders for grid column borders.
fn check_borders(rows: usize, shrink: bool) {
    let ctx = egui::Context::default();
    let mut viewport = egui::Rect::NOTHING;
    let output = ctx.run(
        egui::RawInput {
            screen_rect: Some(egui::Rect::from_min_size(
                egui::Pos2::ZERO,
                egui::vec2(600., 500.),
            )),
            ..Default::default()
        },
        |ctx| {
            egui::CentralPanel::default().show(ctx, |ui| {
                viewport = egui_extras::TableBuilder::new(ui)
                    .column(egui_extras::Column::exact(120.))
                    .column(egui_extras::Column::remainder())
                    .max_scroll_height(200.)
                    .auto_shrink([false, shrink])
                    .header(24., |mut row| {
                        row.col(|ui| {
                            ui.label("A");
                        });
                        row.col(|ui| {
                            ui.label("B");
                        });
                    })
                    .bordered_body(24., |body| {
                        body.rows(20., rows, |mut row| {
                            row.col(|ui| {
                                ui.label("value");
                            });
                            row.col(|ui| {
                                ui.label("value");
                            });
                        })
                    })
                    .inner_rect;
                ui.label("Content below grid");
            });
        },
    );
    let rules: Vec<_> = output
        .shapes
        .iter()
        .filter_map(|shape| {
            if let egui::Shape::LineSegment { points, stroke } = &shape.shape {
                if points[0].x == points[1].x && stroke.color == ifm_ui_egui::presentation::BORDER {
                    return Some((shape.clip_rect, *points));
                }
            }
            None
        })
        .collect();
    assert!(!rules.is_empty(), "No column rule was drawn");
    for (clip, points) in rules {
        assert!(
            clip.bottom() <= viewport.bottom() + 0.01,
            "Clip extends below the final grid viewport"
        );
        assert!(
            points[1].y <= viewport.bottom(),
            "Rule extends below the final grid viewport"
        );
        assert!(
            points[0].y >= viewport.top() - 28.01,
            "Rule extends above the header"
        );
    }
}


/// Verifies that rules stop at shrinking grid bounds.
#[test]
fn rules_stop_at_shrinking_grid_bounds() {
    check_borders(0, true);
    check_borders(1, true);
}


/// Verifies that rules fill but do not exceed fixed or scrolling grid bounds.
#[test]
fn rules_fill_but_do_not_exceed_fixed_or_scrolling_grid_bounds() {
    check_borders(1, false);
    check_borders(100, false);
}


/// Empty, fitting and overflowing table bodies retain inner scrollbar edges that meet the column rules.
#[test]
fn inner_scrollbar_borders_persist_and_gridlines_join_them() {
    for scale in [1.0, 2.0] {
        for vscroll in [false, true] {
            let ctx = egui::Context::default();
            ctx.set_pixels_per_point(scale);
            ifm_ui_egui::presentation::apply(&ctx);
            for rows in [0, 1, 100, 1] {
                let mut viewport = egui::Rect::NOTHING;
                let mut output = None;
                for _ in 0..4 {
                    output = Some(ctx.run(
                        egui::RawInput {
                            screen_rect: Some(egui::Rect::from_min_size(
                                egui::Pos2::ZERO,
                                egui::vec2(600., 500.),
                            )),
                            ..Default::default()
                        },
                        |ctx| {
                            egui::CentralPanel::default().show(ctx, |ui| {
                                viewport = egui_extras::TableBuilder::new(ui)
                                    .column(egui_extras::Column::exact(120.))
                                    .column(egui_extras::Column::remainder())
                                    .vscroll(vscroll)
                                    .auto_shrink([false, false])
                                    .min_scrolled_height(160.)
                                    .max_scroll_height(160.)
                                    .header(24., |mut row| {
                                        row.col(|ui| {
                                            ui.label("Id");
                                        });
                                        row.col(|ui| {
                                            ui.label("Value");
                                        });
                                    })
                                    .bordered_body(24., |body| {
                                        body.rows(20., rows, |mut row| {
                                            row.col(|ui| {
                                                ui.label("1");
                                            });
                                            row.col(|ui| {
                                                ui.label("value");
                                            });
                                        });
                                    })
                                    .inner_rect;
                            });
                        },
                    ));
                }
                let pixel = 1.0 / ctx.pixels_per_point();
                let bottom = viewport.bottom() - pixel / 2.0;
                let right = viewport.right() - pixel / 2.0;
                let output = output.unwrap();
                let rules: Vec<_> = output
                    .shapes
                    .iter()
                    .filter_map(|shape| {
                        if let egui::Shape::LineSegment { points, stroke } = &shape.shape {
                            if stroke.color == ifm_ui_egui::presentation::BORDER
                                && (stroke.width - pixel).abs() < 0.01
                            {
                                return Some((shape.clip_rect, *points));
                            }
                        }
                        None
                    })
                    .collect();
                assert!(
                    rules.iter().all(|(_, points)| points[0].y != points[1].y
                        || (points[0].y - bottom).abs() < 0.01),
                    "shared grid borders add a second header separator: rows={rows}, vscroll={vscroll}, scale={scale}"
                );
                assert!(
                    rules
                        .iter()
                        .any(|(clip, points)| (points[0].x - right).abs() < 0.01
                            && points[0].x == points[1].x
                            && (points[1].y - bottom).abs() < 0.01
                            && clip.contains(points[1])),
                    "missing inner vertical scrollbar border: rows={rows}, vscroll={vscroll}, scale={scale}"
                );
                assert!(
                    rules
                        .iter()
                        .any(|(_, points)| (points[0].y - bottom).abs() < 0.01
                            && points[0].y == points[1].y
                            && points[0].x <= viewport.left() + pixel
                            && (points[1].x - right).abs() < 0.01),
                    "missing inner horizontal scrollbar border: rows={rows}, vscroll={vscroll}, scale={scale}"
                );
                assert!(
                    rules.iter().any(|(_, points)| points[0].x == points[1].x
                        && points[0].x > viewport.left() + pixel
                        && points[0].x < right - pixel
                        && (points[1].y - bottom).abs() < 0.01),
                    "column rule does not meet the scrollbar border"
                );
            }
        }
    }
}
