//! WinForms-compatible report-list selection, dimensions, and grid rules.
//!
//! Fill unused table space without reserving a scrollbar gutter unless it is needed.
//! Capture column geometry during layout, then clip painted rules against the final
//! scroll viewport so lines cannot extend below a shrinking grid. Native parent-center
//! helpers convert Windows physical coordinates to egui logical points. These helpers
//! only control presentation; they never retrieve more rows from the backend.

//! Native equivalents for WinForms report lists. Selection belongs to the row,
//! while data retrieval remains owned by the managed view model.
use eframe::egui::{self, Color32};
/// Keep headers, body and both scrollbars inside a one-physical-pixel grid border.
pub fn grid_viewport<R>(
    ui: &mut egui::Ui,
    bounds: egui::Rect,
    contents: impl FnOnce(&mut egui::Ui) -> R,
) -> R {
    let inside = bounds.shrink(1. / ui.ctx().pixels_per_point());
    let result = ui
        .scope_builder(egui::UiBuilder::new().max_rect(inside), |ui| {
            ui.set_clip_rect(inside.intersect(ui.clip_rect()));
            ui.set_max_size(inside.size());
            contents(ui)
        })
        .inner;
    ui.advance_cursor_after_rect(bounds);
    result
}
/// Fill the allotted grid area; reserve a vertical gutter only when rows overflow.
pub trait FilledGrid {
    /// Use row height/spacing and allotted body height to fill the viewport and enable vertical scrolling only on overflow.
    fn fill_grid(self, rows: usize, row_height: f32, row_spacing: f32, body_height: f32) -> Self;
}
impl FilledGrid for egui_extras::TableBuilder<'_> {
    /// Use row height/spacing and allotted body height to fill the viewport and enable vertical scrolling only on overflow.
    fn fill_grid(self, rows: usize, row_height: f32, row_spacing: f32, body_height: f32) -> Self {
        let content = rows as f32 * row_height + rows.saturating_sub(1) as f32 * row_spacing;
        self.vscroll(content > body_height + 0.01)
            .auto_shrink([false, false])
            .min_scrolled_height(body_height.max(0.))
            .max_scroll_height(body_height.max(0.))
    }
}
/// Render a report list with resizable columns and full-row selection. Column widths are physical reference pixels divided by DPI scale; returns clicked index and rendered-row count.
pub fn report_list(
    ui: &mut egui::Ui,
    id: &str,
    columns: &[(&str, f32)],
    row_count: usize,
    selected: Option<usize>,
    height: f32,
    mut text: impl FnMut(usize, usize) -> String,
) -> (Option<usize>, usize) {
    let bounds =
        egui::Rect::from_min_size(ui.cursor().min, egui::vec2(ui.available_width(), height));
    let result = grid_viewport(ui, bounds, |ui| {
        report_list_content(
            ui,
            id,
            columns,
            row_count,
            selected,
            ui.available_height(),
            &mut text,
        )
    });
    ui.painter().rect_stroke(
        bounds,
        0.,
        egui::Stroke::new(1. / ui.ctx().pixels_per_point(), Color32::from_gray(90)),
        egui::StrokeKind::Inside,
    );
    result
}
fn report_list_content(
    ui: &mut egui::Ui,
    id: &str,
    columns: &[(&str, f32)],
    row_count: usize,
    selected: Option<usize>,
    height: f32,
    mut text: impl FnMut(usize, usize) -> String,
) -> (Option<usize>, usize) {
    let scale = ui.ctx().pixels_per_point();
    let row_height = 24. / scale;
    let bounds =
        egui::Rect::from_min_size(ui.cursor().min, egui::vec2(ui.available_width(), height));
    let mut clicked = None;
    let mut drawn = 0;
    let spacing = ui.spacing().item_spacing.y;
    let width_id = ui.id().with((id, "column-width"));
    let overflow_id = ui.id().with((id, "horizontal-overflow"));
    let initial_width = columns.iter().map(|(_, w)| w / scale).sum::<f32>();
    let content_width = ui
        .ctx()
        .data_mut(|data| data.get_temp::<f32>(width_id))
        .unwrap_or(initial_width);
    let horizontal = ui
        .ctx()
        .data_mut(|data| data.get_temp::<bool>(overflow_id))
        .unwrap_or(content_width > bounds.width());
    let body_height = (height
        - row_height
        - spacing
        - if horizontal {
            ui.spacing().scroll.allocated_width()
        } else {
            0.
        })
    .max(0.);
    let mut measured_width = content_width;
    egui::ScrollArea::horizontal()
        .id_salt(format!("{id}-horizontal"))
        .scroll_bar_visibility(if horizontal {
            egui::scroll_area::ScrollBarVisibility::VisibleWhenNeeded
        } else {
            egui::scroll_area::ScrollBarVisibility::AlwaysHidden
        })
        .auto_shrink([false, false])
        .max_width(bounds.width())
        .max_height(height)
        .show(ui, |ui| {
            ui.set_min_width(content_width);
            ui.spacing_mut().item_spacing.x = 0.;
            ui.visuals_mut().selection.bg_fill = Color32::from_rgb(0, 120, 215);
            let sizing = ui.is_sizing_pass();
            let mut table = egui_extras::TableBuilder::new(ui)
                .id_salt(id)
                .striped(false)
                .sense(egui::Sense::click())
                .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                .fill_grid(row_count, row_height, spacing, body_height);
            for (_, width) in columns {
                let column = if sizing {
                    egui_extras::Column::exact(*width / scale)
                } else {
                    egui_extras::Column::initial(*width / scale)
                        .at_least(40. / scale)
                        .clip(true)
                };
                table = table.column(column.resizable(true));
            }
            table = table.column(egui_extras::Column::remainder().at_least(0.));
            table
                .header(row_height, |mut row| {
                    for (caption, _) in columns {
                        row.col(|ui| {
                            ui.painter().rect_filled(
                                ui.max_rect(),
                                0.,
                                Color32::from_rgb(45, 45, 48),
                            );
                            ui.add(
                                crate::presentation::label_widget(
                                    egui::RichText::new(*caption)
                                        .color(crate::presentation::CAPTION_FOREGROUND),
                                )
                                .truncate(),
                            );
                        });
                    }
                    row.col(|ui| {
                        ui.painter()
                            .rect_filled(ui.max_rect(), 0., Color32::from_rgb(45, 45, 48));
                    });
                })
                .bordered_body(row_height, |body| {
                    measured_width = body.widths().iter().take(columns.len()).sum();
                    body.rows(row_height, row_count, |mut row| {
                        drawn += 1;
                        let index = row.index();
                        row.set_selected(selected == Some(index));
                        for column in 0..columns.len() {
                            row.col(|ui| {
                                ui.add(
                                    crate::presentation::label_widget(
                                        egui::RichText::new(text(index, column))
                                            .color(Color32::WHITE),
                                    )
                                    .truncate(),
                                );
                            });
                        }
                        row.col(|_| {});
                        if row.response().clicked() {
                            clicked = Some(index);
                        }
                    });
                });
        });
    let rows_height = row_count as f32 * row_height + row_count.saturating_sub(1) as f32 * spacing;
    let vertical_gutter = if rows_height > body_height + 0.01 {
        ui.spacing().scroll.allocated_width()
    } else {
        0.
    };
    // Use actual column widths, not the fading vertical scrollbar's provisional
    // content bounds, to avoid inventing a horizontal gutter during row changes.
    let needs_horizontal = measured_width + vertical_gutter > bounds.width() + 0.5;
    ui.ctx().data_mut(|data| {
        data.insert_temp(width_id, measured_width);
        data.insert_temp(overflow_id, needs_horizontal);
    });
    if needs_horizontal != horizontal || (measured_width - content_width).abs() > 0.5 {
        ui.ctx().request_repaint();
    }
    (clicked, drawn)
}

/// WinForms CenterParent includes the native title bar and frame. eframe's
/// viewport rectangles may be unavailable; query our own UI thread's window.
#[cfg(target_os = "windows")]
pub fn native_parent_center(scale: f32) -> Option<egui::Pos2> {
    /// C-layout Windows rectangle measured in physical screen pixels.
    #[repr(C)]
    struct Rect {
        /** Left edge or left-pane bounds in the enclosing coordinate system. */
        left: i32,
        /** Top edge in the native/logical coordinate system specified by the enclosing structure. */
        top: i32,
        /** Right edge or right-pane bounds in the enclosing coordinate system. */
        right: i32,
        /** Bottom edge in the native/logical coordinate system specified by the enclosing structure. */
        bottom: i32,
    }
    /// C-layout Windows point measured in physical pixels.
    #[repr(C)]
    struct Point {
        /** Horizontal physical-pixel coordinate of the Windows point. */
        x: i32,
        /** Vertical physical-pixel coordinate of the Windows point. */
        y: i32,
    }
    #[link(name = "user32")]
    unsafe extern "system" {
        /** Return the calling thread's active native window handle, or null when none exists. */
        fn GetActiveWindow() -> *mut std::ffi::c_void;
        /** Read the native window bounds into caller-owned C-layout storage; zero indicates failure. */
        fn GetWindowRect(window: *mut std::ffi::c_void, rect: *mut Rect) -> i32;
        /** Translate a caller-owned point from client coordinates into physical screen coordinates; zero indicates failure. */
        fn ClientToScreen(window: *mut std::ffi::c_void, point: *mut Point) -> i32;
    }
    unsafe {
        let window = GetActiveWindow();
        if window.is_null() {
            return None;
        }
        let mut r = Rect {
            left: 0,
            top: 0,
            right: 0,
            bottom: 0,
        };
        let mut origin = Point { x: 0, y: 0 };
        if GetWindowRect(window, &mut r) == 0 || ClientToScreen(window, &mut origin) == 0 {
            return None;
        }
        Some(egui::pos2(
            ((r.left + r.right) as f32 / 2. - origin.x as f32) / scale,
            ((r.top + r.bottom) as f32 / 2. - origin.y as f32) / scale,
        ))
    }
}
/// Return the native parent center in egui logical coordinates when available; unsupported platforms return None.
#[cfg(not(target_os = "windows"))]
pub fn native_parent_center(_: f32) -> Option<egui::Pos2> {
    None
}

/// Draw rules only after egui has resolved the scroll viewport's final bounds.
/// During body layout its provisional clip can extend below a shrinking grid.
pub trait BorderedTable {
    /// Lay out the table body, then paint captured header/body column rules clipped to its final viewport.
    fn bordered_body<F>(
        self,
        header_height: f32,
        contents: F,
    ) -> egui::scroll_area::ScrollAreaOutput<()>
    where
        F: for<'b> FnOnce(egui_extras::TableBody<'b>);
}
/// Captured column-rule positions and painter state, finalized after body layout.
struct ColumnBorders {
    /// Painter retained to draw grid rules after viewport geometry is final.
    painter: egui::Painter,
    /// Ordered column definitions or captured column-rule positions for this grid.
    columns: Vec<f32>,
    /// Clipping rectangle captured from the table body.
    clip: egui::Rect,
    /// Column spacing included in the painted rule positions.
    spacing: f32,
    /// Line color/thickness used for the captured grid rules.
    stroke: egui::Stroke,
}
impl ColumnBorders {
    /// Capture the table's column positions and painter before body layout finishes.
    fn capture(body: &mut egui_extras::TableBody<'_>) -> Self {
        let bounds = body.max_rect();
        let widths = body.widths().to_vec();
        let ui = body.ui_mut();
        let spacing = ui.spacing().item_spacing;
        let mut x = bounds.left();
        let mut columns = Vec::new();
        for width in widths.iter().take(widths.len().saturating_sub(1)) {
            x += width;
            columns.push(x + spacing.x / 2.);
            x += spacing.x;
        }
        Self {
            painter: ui.painter().clone(),
            columns,
            clip: ui.clip_rect(),
            spacing: spacing.y,
            stroke: egui::Stroke::new(
                1. / ui.ctx().pixels_per_point(),
                crate::presentation::BORDER,
            ),
        }
    }
    /// Clip captured column rules to the final header/body viewport, including empty body space without drawing past the grid border.
    fn paint(self, viewport: egui::Rect, header_height: f32) {
        let mut grid = viewport;
        grid.min.y -= header_height + if header_height > 0. { self.spacing } else { 0. };
        let mut parent_clip = self.clip;
        parent_clip.min.y -= header_height + if header_height > 0. { self.spacing } else { 0. };
        let clip = grid.intersect(parent_clip);
        if !clip.is_positive() {
            return;
        }
        let painter = self.painter.with_clip_rect(clip);
        let inset = self.stroke.width / 2.;
        for x in self.columns {
            if x >= clip.left() && x <= clip.right() {
                painter.vline(x, clip.top() + inset..=clip.bottom() - inset, self.stroke);
            }
        }
    }
}
/// Wrap a table body with column-rule capture and final viewport clipping; keeps generated grid rules inside the control border.
macro_rules! bordered_table {
    ($table:ty) => {
        impl BorderedTable for $table {
            fn bordered_body<F>(
                self,
                header_height: f32,
                contents: F,
            ) -> egui::scroll_area::ScrollAreaOutput<()>
            where
                F: for<'b> FnOnce(egui_extras::TableBody<'b>),
            {
                let mut borders = None;
                let output = self.body(|mut body| {
                    borders = Some(ColumnBorders::capture(&mut body));
                    contents(body);
                });
                if let Some(borders) = borders {
                    borders.paint(output.inner_rect, header_height);
                }
                output
            }
        }
    };
}
bordered_table!(egui_extras::Table<'_>);
bordered_table!(egui_extras::TableBuilder<'_>);
