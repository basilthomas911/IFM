//! Economic calendar, status console, and feed-control publications. Rows retain stable selection identities, keyboard navigation stays within bounds, and new backend errors are surfaced once per error sequence.

use crate::control_parity::BorderedTable as _;
use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
/// Reserved numeric bridge action for toggling the managed market feed.
pub const FEED_TOGGLE_ACTION: u32 = 0xB0000000;
/// Backend-authoritative availability and running state of the market-feed command.
#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct FeedControl {
    /// Whether the backend feed controller is initialized and available.
    pub ready: bool,
    /// Whether the backend currently permits the feed start/stop action.
    pub can_toggle: bool,
    /// Whether the managed view currently reports work in progress.
    pub busy: bool,
    /// Whether the feed is currently reported active.
    pub active: bool,
}
// This declaration follows the shared publication's serde defaults.
/// Economic-calendar page, status history, feed-control eligibility, and sequenced error information.
#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct SidebarPublication {
    /// API AsOfUtc converted to the display zone, for calendar decoration only.
    /// Missing data never falls back to the workstation's date.
    pub api_today: String,
    /// Backend-authoritative feed action eligibility and running state.
    pub feed_control: FeedControl,
    /// Selected history/calendar/rate period sent to the managed query.
    pub period: String,
    /// Selected or published calendar country code.
    pub country: String,
    /// Available calendar country filter choices.
    pub countries: Vec<String>,
    /// Published date or selected UTC query date in the enclosing calendar contract.
    pub date: String,
    /// Published economic-calendar rows, or their count in the geometry report.
    pub calendar_rows: Vec<CalendarRow>,
    /// Backend-selected stable calendar event identity.
    pub selected_id: String,
    /// Published status-console rows, or their count in the geometry report.
    pub status_rows: Vec<StatusRow>,
    /// Whether the managed view currently reports work in progress.
    pub busy: bool,
    /// User-visible validation, submission, or transport error text.
    pub error: String,
    /// Monotonic error identity used to avoid redisplaying the same backend error.
    pub error_sequence: u64,
    /// Single latest explicit outcome; copied in memory, never part of the old bridge wire contract.
    #[serde(skip)]
    pub outcome_dialogs: Vec<crate::status_dialog::Notice>,
}
/// One published economic-calendar row with display-ready outcome values.
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct CalendarRow {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Already formatted row time from the managed display projection.
    pub time: String,
    /// Selected or published calendar country code.
    pub country: String,
    /// Published economic event name.
    pub event: String,
    /// Published actual release value.
    pub actual: String,
    /// Published forecast release value.
    pub forecast: String,
    /// Published prior release value.
    pub prior: String,
}
/// Stable status-console row with formatted time and message.
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct StatusRow {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Already formatted row time from the managed display projection.
    pub time: String,
    /// Human-readable status or request-outcome explanation.
    pub message: String,
}
/// Native sidebar row selection and one-time error presentation state.
#[derive(Default)]
pub struct SidebarView {
    /// Number of status rows actually rendered in the last frame.
    pub drawn_status_rows: usize,
    /// Stable identity of the selected status-console row.
    pub selected_status_id: String,
    /// Highest notification sequence displayed or acknowledged; stale snapshots cannot reopen it.
    last_dialog_sequence: u64,
    /// Optional currently displayed error-dialog text.
    error_dialog: Option<(String, crate::status_dialog::Severity)>,
}
impl SidebarPublication {
    /// Borrow the latest published status message, or None when status history is empty.
    pub fn latest_status(&self) -> Option<&str> {
        // Console replay and live events are merged in oldest-to-newest order.
        self.status_rows.last().map(|row| row.message.as_str())
    }
}
impl SidebarView {
    /// Keep one current notification. Newer outcomes replace the open dialog;
    /// acknowledging it never reveals an older retained error or outcome.
    fn observe_dialog(&mut self, snapshot: &SidebarPublication) {
        let outcome = snapshot.outcome_dialogs.iter().max_by_key(|notice| notice.sequence);
        let error_sequence = if snapshot.error.is_empty() { 0 } else { snapshot.error_sequence };
        if let Some(notice) = outcome.filter(|notice| notice.sequence > error_sequence) {
            if notice.sequence > self.last_dialog_sequence {
                self.last_dialog_sequence = notice.sequence;
                self.error_dialog = Some((notice.message.clone(), notice.severity));
            }
        } else if error_sequence > self.last_dialog_sequence {
            self.last_dialog_sequence = error_sequence;
            self.error_dialog = Some((snapshot.error.clone(), crate::status_dialog::Severity::Error));
        }
    }
    /// Render the economic-calendar tabs, filters, rows, and selected details; return the numeric action for a user query/selection.
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
                    crate::presentation::caption_widget(
                        egui::RichText::new("Economic Calendar").strong(),
                    )
                    .truncate(),
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
                ui.trading_caption("Country Code:");
            },
        );
        ui.horizontal(|ui| {
            ui.spacing_mut().item_spacing.x = 2.0;
            ui.spacing_mut().button_padding.x = 2.0;
            for (index, period) in ["Today", "Yesterday", "Tomorrow", "This Week", "Next Week"]
                .iter()
                .enumerate()
            {
                if crate::presentation::tab_label(
                    ui,
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
            crate::presentation::caption_widget(egui::RichText::new(&snapshot.date).strong())
                .truncate(),
        );
        let selected = snapshot
            .calendar_rows
            .iter()
            .find(|row| row.id == snapshot.selected_id);
        let list_height = (ui.available_height() - 30.0).max(18.0);
        let calendar_border = egui::Rect::from_min_size(
            ui.cursor().min,
            egui::vec2(ui.available_width(), list_height),
        );
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
        crate::control_parity::grid_viewport(ui, calendar_border, |ui| {
            let body_height =
                (ui.available_height() - ui.spacing().scroll.allocated_width()).max(0.);
            egui::ScrollArea::horizontal()
                .id_salt(("calendar-horizontal", &snapshot.period, &snapshot.country))
                .max_height(ui.available_height())
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    let list_ctx = ui.ctx().clone();
                    let mut table = egui_extras::TableBuilder::new(ui)
                        .id_salt((
                            "economic-calendar-list",
                            &snapshot.period,
                            &snapshot.country,
                        ))
                        .min_scrolled_height(body_height)
                        .max_scroll_height(body_height)
                        .sense(egui::Sense::click())
                        .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                        .column(egui_extras::Column::initial(80.0).at_least(60.0))
                        .column(egui_extras::Column::initial(80.0).at_least(45.0))
                        .column(egui_extras::Column::initial(450.0).at_least(180.0));
                    if let Some(index) = navigation {
                        table = table.scroll_to_row(index, Some(egui::Align::Center));
                    }
                    table.bordered_body(0., |body| {
                        body.rows(18.0, snapshot.calendar_rows.len(), |mut row| {
                            let index = row.index();
                            let item = &snapshot.calendar_rows[index];
                            row.set_selected(item.id == snapshot.selected_id);
                            let mut clicked = false;
                            for value in [&item.time, &item.country, &item.event] {
                                row.col(|ui| {
                                    clicked |= ui
                                        .add(
                                            egui::Label::new(
                                                egui::RichText::new(value)
                                                    .color(crate::presentation::FOREGROUND),
                                            )
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
        });
        draw_grid_border(ui, calendar_border);
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
                        columns[index * 2].add(
                            crate::presentation::caption_widget(
                                egui::RichText::new(*label).strong(),
                            )
                            .truncate(),
                        );
                        columns[index * 2 + 1]
                            .add(crate::presentation::label_widget(*value).truncate());
                    }
                });
            },
        );
        self.observe_dialog(snapshot);
        if let Some((message,severity)) = self.error_dialog.clone() {
            egui::Modal::new(egui::Id::new("calendar-error-dialog")).show(ui.ctx(), |ui| {
                if crate::status_dialog::body(ui,&message,severity).acknowledged { self.error_dialog=None; }
            });
        }
        action
    }
    /// Render the bounded visible status rows and present each new sequenced backend error once.
    pub fn status(&mut self, ui: &mut egui::Ui, snapshot: &SidebarPublication) {
        self.drawn_status_rows = 0;
        let status_border = ui.available_rect_before_wrap();
        let focus_id = ui.make_persistent_id("status-list-focus");
        ui.interact(
            ui.available_rect_before_wrap(),
            focus_id,
            egui::Sense::focusable_noninteractive(),
        );
        let selected_index = snapshot
            .status_rows
            .iter()
            .position(|row| row.id == self.selected_status_id)
            .map(|index| snapshot.status_rows.len() - 1 - index);
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
            self.selected_status_id = snapshot.status_rows[snapshot.status_rows.len() - 1 - index].id.clone();
        }
        crate::control_parity::grid_viewport(ui, status_border, |ui| {
            let body_height = (ui.available_height()
                - 24.
                - ui.spacing().item_spacing.y
                - ui.spacing().scroll.allocated_width())
            .max(0.);
            crate::console_capacity::observe(body_height,18.0+ui.spacing().item_spacing.y);
            egui::ScrollArea::horizontal()
                .id_salt("status-console-horizontal")
                .auto_shrink([false, false])
                .max_height(ui.available_height())
                .show(ui, |ui| {
                    let list_ctx = ui.ctx().clone();
                    let mut table = egui_extras::TableBuilder::new(ui)
                        .id_salt("status-console-list")
                        // Newest rows are at the top. Default top anchoring follows arrivals
                        // without forcing a reader who scrolled into history back to live.
                        .resizable(true)
                        .min_scrolled_height(body_height)
                        .max_scroll_height(body_height)
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
                                    let rect = ui.max_rect().expand2(egui::vec2(
                                        ui.spacing().item_spacing.x / 2.0,
                                        0.0,
                                    ));
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
                        .bordered_body(24.0, |body| {
                            body.rows(18.0, snapshot.status_rows.len(), |mut row| {
                                self.drawn_status_rows += 1;
                                // Retain chronological storage for replay merging; reverse only the display.
                                let item = &snapshot.status_rows[snapshot.status_rows.len() - 1 - row.index()];
                                row.set_selected(item.id == self.selected_status_id);
                                let mut clicked = false;
                                row.col(|ui| {
                                    clicked |= ui
                                        .add(
                                            egui::Label::new(
                                                egui::RichText::new(&item.time)
                                                    .color(crate::presentation::FOREGROUND),
                                            )
                                            .sense(egui::Sense::click()),
                                        )
                                        .clicked();
                                });
                                row.col(|ui| {
                                    clicked |= ui
                                        .add(
                                            egui::Label::new(
                                                egui::RichText::new(&item.message)
                                                    .color(crate::presentation::FOREGROUND),
                                            )
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
        });
        draw_grid_border(ui, status_border);
    }
}

/// Paint the calendar/status viewport's inside border without extending into adjacent controls.
fn draw_grid_border(ui: &egui::Ui, rect: egui::Rect) {
    ui.painter().rect_stroke(
        rect.intersect(ui.clip_rect()),
        0.,
        egui::Stroke::new(
            1. / ui.ctx().pixels_per_point(),
            crate::presentation::BORDER,
        ),
        egui::StrokeKind::Inside,
    );
}

/// Resolve arrow/Home/End/Page navigation within the available row count. Return a new in-bounds selection only when a relevant key is pressed.
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

#[cfg(test)]
mod latest_dialog_tests {
    use super::*;
    use crate::status_dialog::{Notice, Severity};

    #[test]
    fn latest_outcome_replaces_open_error_and_acknowledgement_has_no_backlog() {
        let mut view = SidebarView::default();
        let mut publication = SidebarPublication {error: "Resetting dataset".into(), error_sequence: 1, ..Default::default()};
        view.observe_dialog(&publication);
        assert_eq!(view.error_dialog.as_ref().unwrap().0, "Resetting dataset");
        publication.outcome_dialogs = vec![Notice {sequence: 2, message: "Connection restored".into(), severity: Severity::Success}, Notice {sequence: 3, message: "Monitoring verified".into(), severity: Severity::Success}];
        view.observe_dialog(&publication);
        assert_eq!(view.error_dialog.as_ref().unwrap().0, "Monitoring verified");
        view.error_dialog = None; // Same state transition as pressing OK.
        view.observe_dialog(&publication);
        assert!(view.error_dialog.is_none());
        publication.outcome_dialogs.clear(); // Older coalesced publication cannot reopen the error.
        view.observe_dialog(&publication);
        assert!(view.error_dialog.is_none());
        publication.error_sequence = 4;
        publication.error = "New failure".into();
        view.observe_dialog(&publication);
        assert_eq!(view.error_dialog, Some(("New failure".into(), Severity::Error)));
    }
}
