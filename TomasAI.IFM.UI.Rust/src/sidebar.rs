use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
pub const FEED_TOGGLE_ACTION: u32 = 0xB0000000;
#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct FeedControl {
    pub ready: bool,
    pub can_toggle: bool,
    pub busy: bool,
    pub active: bool,
}
// This declaration follows the shared publication's serde defaults.
#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct SidebarPublication {
    pub feed_control: FeedControl,
    pub period: String,
    pub country: String,
    pub countries: Vec<String>,
    pub date: String,
    pub calendar_rows: Vec<CalendarRow>,
    pub selected_id: String,
    pub status_rows: Vec<StatusRow>,
    pub busy: bool,
    pub error: String,
    pub error_sequence: u64,
}
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct CalendarRow {
    pub id: String,
    pub time: String,
    pub country: String,
    pub event: String,
    pub actual: String,
    pub forecast: String,
    pub prior: String,
}
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct StatusRow {
    pub id: String,
    pub time: String,
    pub message: String,
}
#[derive(Default)]
pub struct SidebarView {
    pub drawn_status_rows: usize,
    pub selected_status_id: String,
    last_error_sequence: u64,
    error_dialog: Option<String>,
}
impl SidebarPublication {
    pub fn latest_status(&self) -> Option<&str> {
        self.status_rows.first().map(|row| row.message.as_str())
    }
}
impl SidebarView {
    pub fn calendar(&mut self, ui: &mut egui::Ui, snapshot: &SidebarPublication) -> Option<u32> {
        let mut action = None;
        let (header_rect, _) =
            ui.allocate_exact_size(egui::vec2(ui.available_width(), 25.0), egui::Sense::hover());
        let title_rect = egui::Rect::from_min_max(
            header_rect.min,
            egui::pos2(
                (header_rect.right() - 164.0).max(header_rect.left() + 20.0),
                header_rect.bottom(),
            ),
        );
        ui.scope_builder(
            egui::UiBuilder::new()
                .max_rect(title_rect)
                .layout(egui::Layout::left_to_right(egui::Align::Center)),
            |ui| {
                ui.add(
                    crate::presentation::label_widget(egui::RichText::new("Economic Calendar").strong()).truncate(),
                );
            },
        );
        let country_rect = egui::Rect::from_min_max(
            egui::pos2(
                (header_rect.right() - 160.0).max(header_rect.left()),
                header_rect.top(),
            ),
            header_rect.max,
        );
        ui.scope_builder(
            egui::UiBuilder::new()
                .max_rect(country_rect)
                .layout(egui::Layout::right_to_left(egui::Align::Center)),
            |ui| {
                egui::ComboBox::from_id_salt("economic-calendar-country")
                    .width(48.0)
                    .selected_text(&snapshot.country)
                    .show_ui(ui, |ui| {
                        for (index, country) in snapshot.countries.iter().enumerate() {
                            if ui
                                .selectable_label(country == &snapshot.country, country)
                                .clicked()
                            {
                                action = Some(0x90000000 + index as u32);
                            }
                        }
                    });
                ui.trading_label("Country Code:");
            },
        );
        ui.horizontal(|ui| {
            ui.spacing_mut().item_spacing.x = 2.0;
            ui.spacing_mut().button_padding.x = 2.0;
            for (index, period) in ["Today", "Yesterday", "Tomorrow", "This Week", "Next Week"]
                .iter()
                .enumerate()
            {
                if crate::presentation::tab_label(ui,
                        snapshot.period == *period || snapshot.period.is_empty() && index == 0,
                        *period,
                    )
                    .clicked()
                {
                    action = Some(0x80000000 + index as u32);
                }
            }
        });
        ui.add_sized(
            [ui.available_width(), 17.0],
            crate::presentation::label_widget(egui::RichText::new(&snapshot.date).strong()).truncate(),
        );
        let selected = snapshot
            .calendar_rows
            .iter()
            .find(|row| row.id == snapshot.selected_id);
        let list_height = (ui.available_height() - 30.0).max(18.0);
        let focus_id = ui.make_persistent_id("calendar-list-focus");
        ui.interact(
            egui::Rect::from_min_size(
                ui.cursor().min,
                egui::vec2(ui.available_width(), list_height),
            ),
            focus_id,
            egui::Sense::focusable_noninteractive(),
        );
        let selected_index = snapshot
            .calendar_rows
            .iter()
            .position(|row| row.id == snapshot.selected_id);
        let navigation = if ui.memory(|m| m.has_focus(focus_id)) {
            navigate(
                ui,
                selected_index,
                snapshot.calendar_rows.len(),
                (list_height / 18.0) as usize,
            )
        } else {
            None
        };
        if let Some(index) = navigation {
            action = Some(0xA0000000 + index as u32);
        }
        egui::ScrollArea::horizontal()
            .id_salt(("calendar-horizontal", &snapshot.period, &snapshot.country))
            .max_height(list_height)
            .auto_shrink([false, false])
            .show(ui, |ui| {
                let list_ctx = ui.ctx().clone();
                let mut table = egui_extras::TableBuilder::new(ui)
                    .id_salt((
                        "economic-calendar-list",
                        &snapshot.period,
                        &snapshot.country,
                    ))
                    .min_scrolled_height((list_height - 14.0).max(0.0))
                    .max_scroll_height((list_height - 14.0).max(0.0))
                    .sense(egui::Sense::click())
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .column(egui_extras::Column::initial(80.0).at_least(60.0))
                    .column(egui_extras::Column::initial(80.0).at_least(45.0))
                    .column(egui_extras::Column::initial(450.0).at_least(180.0));
                if let Some(index) = navigation {
                    table = table.scroll_to_row(index, Some(egui::Align::Center));
                }
                table.body(|body| {
                    body.rows(18.0, snapshot.calendar_rows.len(), |mut row| {
                        let index = row.index();
                        let item = &snapshot.calendar_rows[index];
                        row.set_selected(item.id == snapshot.selected_id);
                        let mut clicked = false;
                        for value in [&item.time, &item.country, &item.event] {
                            row.col(|ui| {
                                clicked |= ui
                                    .add(
                                        crate::presentation::label_widget(value)
                                            .truncate()
                                            .sense(egui::Sense::click()),
                                    )
                                    .clicked();
                            });
                        }
                        if clicked || row.response().clicked() {
                            action = Some(0xA0000000 + index as u32);
                            list_ctx.memory_mut(|memory| memory.request_focus(focus_id));
                        }
                    })
                });
            });
        ui.allocate_ui_with_layout(
            egui::vec2(ui.available_width(), 26.0),
            egui::Layout::top_down(egui::Align::Min),
            |ui| {
                ui.columns(6, |columns| {
                    for (index, (label, value)) in [
                        ("Actual", selected.map(|r| r.actual.as_str()).unwrap_or("")),
                        (
                            "Forecast",
                            selected.map(|r| r.forecast.as_str()).unwrap_or(""),
                        ),
                        ("Prior", selected.map(|r| r.prior.as_str()).unwrap_or("")),
                    ]
                    .iter()
                    .enumerate()
                    {
                        columns[index * 2]
                            .add(crate::presentation::label_widget(egui::RichText::new(*label).strong()).truncate());
                        columns[index * 2 + 1].add(crate::presentation::label_widget(*value).truncate());
                    }
                });
            },
        );
        if snapshot.error_sequence > self.last_error_sequence && !snapshot.error.is_empty() {
            self.last_error_sequence = snapshot.error_sequence;
            self.error_dialog = Some(snapshot.error.clone());
        }
        if let Some(message) = self.error_dialog.clone() {
            egui::Modal::new(egui::Id::new("calendar-error-dialog")).show(ui.ctx(), |ui| {
                ui.heading("Economic Calendar Error");
                ui.trading_label(message);
                if ui.button("OK").clicked() {
                    self.error_dialog = None;
                }
            });
        }
        action
    }
    pub fn status(&mut self, ui: &mut egui::Ui, snapshot: &SidebarPublication) {
        self.drawn_status_rows = 0;
        let focus_id = ui.make_persistent_id("status-list-focus");
        ui.interact(
            ui.available_rect_before_wrap(),
            focus_id,
            egui::Sense::focusable_noninteractive(),
        );
        let selected_index = snapshot
            .status_rows
            .iter()
            .position(|row| row.id == self.selected_status_id);
        let navigation = if ui.memory(|m| m.has_focus(focus_id)) {
            navigate(
                ui,
                selected_index,
                snapshot.status_rows.len(),
                (ui.available_height() / 18.0) as usize,
            )
        } else {
            None
        };
        if let Some(index) = navigation {
            self.selected_status_id = snapshot.status_rows[index].id.clone();
        }
        egui::ScrollArea::horizontal()
            .id_salt("status-console-horizontal")
            .auto_shrink([false, false])
            .show(ui, |ui| {
                let list_ctx = ui.ctx().clone();
                let mut table = egui_extras::TableBuilder::new(ui)
                    .id_salt("status-console-list")
                    .resizable(true)
                    .sense(egui::Sense::click())
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .column(egui_extras::Column::initial(170.0).at_least(100.0))
                    .column(egui_extras::Column::initial(500.0).at_least(250.0));
                if let Some(index) = navigation {
                    table = table.scroll_to_row(index, Some(egui::Align::Center));
                }
                table
                    .header(24.0, |mut header| {
                        for label in ["Status Time", "Status"] {
                            header.col(|ui| {
                                let rect = ui
                                    .max_rect()
                                    .expand2(egui::vec2(ui.spacing().item_spacing.x / 2.0, 0.0));
                                ui.painter().rect(
                                    rect,
                                    0.0,
                                    crate::presentation::COMMAND,
                                    egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                                    egui::StrokeKind::Inside,
                                );
                                ui.add(
                                    crate::presentation::label_widget(
                                        egui::RichText::new(label)
                                            .color(crate::presentation::FOREGROUND),
                                    )
                                    .truncate(),
                                );
                            });
                        }
                    })
                    .body(|body| {
                        body.rows(18.0, snapshot.status_rows.len(), |mut row| {
                            self.drawn_status_rows += 1;
                            let item = &snapshot.status_rows[row.index()];
                            row.set_selected(item.id == self.selected_status_id);
                            let mut clicked = false;
                            row.col(|ui| {
                                clicked |= ui
                                    .add(crate::presentation::label_widget(&item.time).sense(egui::Sense::click()))
                                    .clicked();
                            });
                            row.col(|ui| {
                                clicked |= ui
                                    .add(
                                        crate::presentation::label_widget(&item.message)
                                            .truncate()
                                            .sense(egui::Sense::click()),
                                    )
                                    .on_hover_text(&item.message)
                                    .clicked();
                            });
                            if clicked || row.response().clicked() {
                                self.selected_status_id = item.id.clone();
                                list_ctx.memory_mut(|m| m.request_focus(focus_id));
                            }
                        })
                    });
            });
    }
}

fn navigate(ui: &egui::Ui, selected: Option<usize>, count: usize, page: usize) -> Option<usize> {
    if count == 0 {
        return None;
    }
    let index = selected.unwrap_or(0);
    ui.input(|input| {
        if input.key_pressed(egui::Key::Home) {
            Some(0)
        } else if input.key_pressed(egui::Key::End) {
            Some(count - 1)
        } else if input.key_pressed(egui::Key::ArrowDown) {
            Some(selected.map_or(0, |i| (i + 1).min(count - 1)))
        } else if input.key_pressed(egui::Key::ArrowUp) {
            Some(index.saturating_sub(1))
        } else if input.key_pressed(egui::Key::PageDown) {
            Some((index + page.max(1)).min(count - 1))
        } else if input.key_pressed(egui::Key::PageUp) {
            Some(index.saturating_sub(page.max(1)))
        } else {
            None
        }
    })
}
