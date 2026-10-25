//! Virtualized fixture grid with background filtering and stable row selection.
//!
//! The full deterministic fixture is held in an `Arc`; a background worker returns
//! filtered/sorted row indexes while rendering touches only the visible row range.
//! Selection uses row IDs rather than positions. This component demonstrates local
//! render virtualization, not server paging. Production trade plans use the separate
//! offset/page-size loading path in the trade workspace.

use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
use std::{
    ops::Range,
    sync::{Arc, mpsc},
};
/// Display-ready fixture row used to exercise virtual rendering and navigation.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GridRow {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: u64,
    /// Published sequence/order value; transfer sequences are tracked independently for each kind.
    pub sequence: u64,
    /// Display-ready timestamp for the fixture grid.
    pub timestamp: String,
    /// Published/formatted daily P&L text; rendering does not revalue it.
    pub daily_pnl: String,
    /// Published/formatted forward-loss ratio text in the fixture grid.
    pub forward_loss_ratio: String,
}
/// Locally virtualized fixture grid with immutable source rows, background index filtering, and stable ID selection.
pub struct GridView {
    /// Complete immutable fixture dataset shared with background filter/sort workers.
    pub rows: Arc<Vec<GridRow>>,
    /// Indexes into immutable fixture rows after filtering/sorting.
    pub order: Vec<usize>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<u64>,
    /// Logical row-index range actually rendered in the latest virtual-grid frame.
    pub drawn: Range<usize>,
    /// Editable text used to filter the fixture rows or native file chooser.
    pub filter: String,
    /// Whether fixture rows are ordered by descending sequence.
    pub descending: bool,
    /// Receiver for the most recently requested background filter/sort index calculation.
    pending: Option<mpsc::Receiver<Vec<usize>>>,
    /// Pending vertical scroll offset requested by navigation.
    jump: Option<f32>,
    /// Current keyboard-navigation index in the filtered/sorted row order.
    cursor: Option<usize>,
}
impl GridView {
    /// Create a grid holding all immutable fixture rows with their initial natural index order; no background work is started yet.
    pub fn new(rows: Vec<GridRow>) -> Self {
        let order = (0..rows.len()).collect();
        Self {
            rows: Arc::new(rows),
            order,
            selected: None,
            drawn: 0..0,
            filter: String::new(),
            descending: false,
            pending: None,
            jump: None,
            cursor: None,
        }
    }
    /// Filter/sort row indexes on a background thread while keeping the full immutable fixture rows shared.
    pub fn rebuild(&mut self) {
        let rows = self.rows.clone();
        let filter = self.filter.clone();
        let descending = self.descending;
        let (tx, rx) = mpsc::sync_channel(1);
        self.pending = Some(rx);
        std::thread::spawn(move || {
            let mut order: Vec<_> = rows
                .iter()
                .enumerate()
                .filter(|(_, r)| {
                    filter.is_empty()
                        || r.id.to_string().contains(&filter)
                        || r.daily_pnl.contains(&filter)
                })
                .map(|(i, _)| i)
                .collect();
            order.sort_unstable_by_key(|&i| rows[i].sequence);
            if descending {
                order.reverse();
            }
            let _ = tx.send(order);
        });
    }
    /// Accept completed background index work, render only visible fixture rows, and maintain ID-based selection and bounded keyboard/scroll navigation.
    pub fn draw(&mut self, ctx: &egui::Context, catalog: &crate::Catalog) {
        if let Some(rx) = &self.pending {
            if let Ok(order) = rx.try_recv() {
                self.order = order;
                self.pending = None;
                self.cursor = None;
            } else {
                ctx.request_repaint_after(std::time::Duration::from_millis(16));
            }
        }
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.trading_label(format!(
                "{}: {} / {}",
                catalog.text(crate::strings::ROWS),
                self.order.len(),
                self.rows.len()
            ));
            let mut editing = false;
            ui.horizontal(|ui| {
                let label = ui.trading_caption(catalog.text(crate::strings::FILTER));
                editing = ui
                    .text_edit_singleline(&mut self.filter)
                    .labelled_by(label.id)
                    .has_focus();
                if ui.button(catalog.text(crate::strings::APPLY)).clicked() {
                    self.rebuild();
                }
                if ui.button(catalog.text(crate::strings::REVERSE)).clicked() {
                    self.descending = !self.descending;
                    self.rebuild();
                }
                if ui.button(catalog.text(crate::strings::FIRST)).clicked() {
                    self.jump = Some(0.0);
                }
                if ui.button(catalog.text(crate::strings::LAST)).clicked() {
                    self.jump = Some(
                        self.order.len() as f32
                            * (ui
                                .text_style_height(&egui::TextStyle::Body)
                                .max(ui.spacing().interact_size.y)
                                + ui.spacing().item_spacing.y),
                    );
                }
            });
            if self.pending.is_some() {
                ui.trading_caption(catalog.text(crate::strings::PROCESSING));
            }
            if let Some(id) = self.selected {
                ui.trading_label(format!(
                    "{}: {}",
                    catalog.text(crate::strings::SELECTED),
                    id
                ));
            }
            ui.horizontal(|ui| {
                for (label, width) in [
                    (crate::strings::ROW_ID, 80.0),
                    (crate::strings::SEQUENCE, 90.0),
                    (crate::strings::TIMESTAMP, 310.0),
                    (crate::strings::DAILY_PNL, 120.0),
                    (crate::strings::FORWARD_LOSS_RATIO, 150.0),
                ] {
                    ui.add_sized(
                        [width, 20.0],
                        crate::presentation::caption_widget(catalog.text(label)),
                    );
                }
            });
            let height = ui
                .text_style_height(&egui::TextStyle::Body)
                .max(ui.spacing().interact_size.y)
                + ui.spacing().item_spacing.y;
            if !editing && !self.order.is_empty() {
                let pressed = ui.input(|i| {
                    [
                        egui::Key::Home,
                        egui::Key::End,
                        egui::Key::ArrowDown,
                        egui::Key::ArrowUp,
                    ]
                    .into_iter()
                    .find(|key| i.key_pressed(*key))
                });
                if let Some(key) = pressed {
                    let last = self.order.len() - 1;
                    let position = match key {
                        egui::Key::Home => 0,
                        egui::Key::End => last,
                        egui::Key::ArrowDown => self.cursor.map_or(0, |p| (p + 1).min(last)),
                        _ => self.cursor.unwrap_or(0).saturating_sub(1),
                    };
                    self.cursor = Some(position);
                    self.selected = Some(self.rows[self.order[position]].id);
                    if !self.drawn.contains(&position) {
                        self.jump = Some(position as f32 * height);
                    }
                }
            }
            let mut scroll = egui::ScrollArea::vertical().id_salt("virtual_grid");
            if let Some(y) = self.jump.take() {
                scroll = scroll.vertical_scroll_offset(y);
                ctx.request_repaint();
            }
            scroll.show_rows(
                ui,
                height - ui.spacing().item_spacing.y,
                self.order.len(),
                |ui, range| {
                    self.drawn = range.clone();
                    for position in range {
                        let row = &self.rows[self.order[position]];
                        ui.push_id(row.id, |ui| {
                            ui.horizontal(|ui| {
                                if ui
                                    .add_sized(
                                        [80.0, height - ui.spacing().item_spacing.y],
                                        egui::Button::selectable(
                                            self.selected == Some(row.id),
                                            format!("Row {}", row.id),
                                        ),
                                    )
                                    .clicked()
                                {
                                    self.selected = Some(row.id);
                                    self.cursor = Some(position);
                                }
                                for (text, width) in [
                                    (row.sequence.to_string(), 90.0),
                                    (row.timestamp.clone(), 310.0),
                                    (row.daily_pnl.clone(), 120.0),
                                    (row.forward_loss_ratio.clone(), 150.0),
                                ] {
                                    ui.add_sized(
                                        [width, height - ui.spacing().item_spacing.y],
                                        crate::presentation::label_widget(text),
                                    );
                                }
                            });
                        });
                    }
                },
            );
        });
    }
}
