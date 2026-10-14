use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
use std::{
    ops::Range,
    sync::{Arc, mpsc},
};
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GridRow {
    pub id: u64,
    pub sequence: u64,
    pub timestamp: String,
    pub daily_pnl: String,
    pub forward_loss_ratio: String,
}
pub struct GridView {
    pub rows: Arc<Vec<GridRow>>,
    pub order: Vec<usize>,
    pub selected: Option<u64>,
    pub drawn: Range<usize>,
    pub filter: String,
    pub descending: bool,
    pending: Option<mpsc::Receiver<Vec<usize>>>,
    jump: Option<f32>,
    cursor: Option<usize>,
}
impl GridView {
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
                let label = ui.trading_label(catalog.text(crate::strings::FILTER));
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
                ui.trading_label(catalog.text(crate::strings::PROCESSING));
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
                    ui.add_sized([width, 20.0], crate::presentation::label_widget(catalog.text(label)));
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
