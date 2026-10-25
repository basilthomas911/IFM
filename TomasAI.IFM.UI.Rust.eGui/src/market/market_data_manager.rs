//! Combined Futures Contracts, Futures Option Contracts, and Yield Curve Rates window.
//!
//! The manager reuses contract/rate editors and exposes their published details in the
//! WinForms-compatible layout. Yield rates support row selection and inline editing;
//! Escape cancels the draft. Pending requests and transport errors are tracked locally
//! without inventing successful backend outcomes. Recorded control rectangles support
//! layout verification and accessibility tests.

use crate::control_parity::{BorderedTable as _, FilledGrid as _};
use crate::presentation::{self, TradingLabelUi as _};
use crate::{
    contract_editor::{ContractEditor, ContractPublication},
    yield_curve_editor::{TENORS, YieldCurveEditor},
};
use eframe::egui;
use std::collections::BTreeMap;

/// Market Data page captions in the same order as their native selector indexes.
const VIEWS: [&str; 3] = [
    "Futures Contracts",
    "Futures Option Contracts",
    "Yield Curve Rates",
];

/// WinForms layout with a wider 1200 x 444 client area to fit all yield tenors.
#[derive(Default)]
pub struct MarketDataManager {
    /// Whether the view/dialog is currently shown.
    pub open: bool,
    /// Selected Market Data page index.
    pub selection: usize,
    /// Contract editor instance reused by the enclosing screen.
    contract: ContractEditor,
    /// Reused yield-curve editor state.
    rates: YieldCurveEditor,
    /// Whether a local edit session is active.
    editing: bool,
    /// Whether the current inline edit creates a new record.
    adding: bool,
    /// Whether to bring the inline rate editor into view on the next layout.
    scroll_rate_edit: bool,
    /// Whether the inline rate removal confirmation is open.
    remove_confirmation: bool,
    /// Whether an initial manager query has already been emitted.
    load_requested: bool,
    /// Action key associated with the current pending request.
    pending_action: String,
    /// User-visible validation, submission, or transport error text.
    error: String,
    /// Latest logical-point control rectangles keyed by stable names for layout/accessibility verification.
    pub controls: BTreeMap<String, egui::Rect>,
}

impl MarketDataManager {
    /// Show the manager and reset the load-request flag so its next frame requests fresh managed data.
    pub fn open(&mut self) {
        self.open = true;
        self.load_requested = false;
    }
    /// Return the managed view key corresponding to the currently selected editor mode.
    fn view(&self) -> &str {
        match self.selection {
            0 => "futures-contract",
            1 => "option-contract",
            _ => "yield-curve",
        }
    }
    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
    fn request(&mut self, action: &str, publication: &ContractPublication) -> String {
        self.pending_action = action.into();
        self.error.clear();
        if self.selection == 2 {
            self.rates.request(action, publication)
        } else {
            self.contract.option = self.selection == 1;
            self.contract.request(action, publication)
        }
    }
    /// Release the current pending UI submission after the host reports a queue rejection.
    pub fn reject_submission(&mut self) {
        self.contract.pending = None;
        self.rates.pending = None;
        self.error = "The request queue is full. Close and reopen to retry.".into();
    }
    /// Release only the matching request that failed before backend execution and expose its submission error.
    pub fn submission_rejected(&mut self, id: &str, error: &str) {
        if self.contract.pending.as_deref() == Some(id) || self.rates.pending.as_deref() == Some(id)
        {
            self.contract.pending = None;
            self.rates.pending = None;
            self.error = error.into();
        }
    }
    /// Record a transport error and release local pending UI state; this does not confirm a business command's outcome.
    pub fn transport_failed(&mut self, error: &str) {
        self.contract.pending = None;
        self.rates.pending = None;
        self.error = error.into();
    }

    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns main Market Data dialog state, reused editors, inline-rate edit mode, and measured control bounds.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        publication: &ContractPublication,
        live: bool,
    ) -> Option<String> {
        if !self.open {
            return None;
        }
        let pending = if self.selection == 2 {
            &mut self.rates.pending
        } else {
            &mut self.contract.pending
        };
        let mut completed = false;
        if let Some(outcome) = pending.as_ref().and_then(|id| {
            publication
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
        }) {
            if outcome.state == "Completed" {
                completed = true;
                self.editing = false;
                self.remove_confirmation = false;
            } else {
                self.error = outcome.message.clone();
            }
            *pending = None;
        }
        if completed && publication.result.view == self.view() {
            let (selected, draft) = if self.selection == 2 {
                (&mut self.rates.selected, &mut self.rates.draft)
            } else {
                (&mut self.contract.selected, &mut self.contract.draft)
            };
            let saved = publication
                .result
                .rows
                .iter()
                .find(|row| {
                    if self.pending_action == "add" {
                        if self.selection == 2 {
                            row.fields.get("valueDate") == draft.get("valueDate")
                        } else {
                            row.fields.get("symbol") == draft.get("symbol")
                                && row.fields.get(if self.selection == 1 {
                                    "contractMonth"
                                } else {
                                    "lastTradeDate"
                                }) == draft.get(if self.selection == 1 {
                                    "contractMonth"
                                } else {
                                    "lastTradeDate"
                                })
                        }
                    } else {
                        Some(&row.id) == selected.as_ref()
                    }
                })
                .or_else(|| publication.result.rows.first());
            if let Some(row) = saved {
                *selected = Some(row.id.clone());
                *draft = row.fields.clone();
            } else {
                *selected = None;
                draft.clear();
            }
        }
        let ready = live
            && !publication.busy
            && self.contract.pending.is_none()
            && self.rates.pending.is_none();
        if self.selection == 2
            && self.editing
            && self.rates.pending.is_none()
            && ctx.input_mut(|input| input.consume_key(egui::Modifiers::NONE, egui::Key::Escape))
        {
            self.editing = false;
            self.scroll_rate_edit = false;
            self.rates.draft = publication
                .result
                .rows
                .iter()
                .find(|row| Some(&row.id) == self.rates.selected.as_ref())
                .map(|row| row.fields.clone())
                .unwrap_or_default();
        }
        let matches = publication.result.view == self.view();
        let mut action = None;
        let mut open = self.open;
        let p = 1.0_f32;
        let size = egui::vec2(1200., 444.)
            .min((ctx.screen_rect().size() - egui::vec2(24., 64.)).max(egui::vec2(320., 300.)));
        self.controls.clear();
        let window = egui::Window::new(presentation::dialog_title("Market Data Manager"))
            .id(egui::Id::new("market-data-manager"))
            .open(&mut open)
            .collapsible(false)
            .resizable(false)
            .fixed_size(size)
            .default_pos(ctx.screen_rect().center() - size / 2.)
            .show(ctx, |ui| {
                let bounds = ui.available_rect_before_wrap();
                ui.set_clip_rect(bounds.intersect(ui.clip_rect()));
                let selector =
                    egui::Rect::from_min_size(bounds.min, egui::vec2(bounds.width(), 37. / p));
                self.controls.insert("selector".into(), selector);
                ui.scope_builder(
                    egui::UiBuilder::new()
                        .id_salt("manager-selector")
                        .max_rect(selector),
                    |ui| {
                        ui.horizontal_centered(|ui| {
                            ui.add_space(35. / p);
                            ui.add_sized(
                                egui::vec2(79., 24.) / p,
                                presentation::caption_widget("Selection:"),
                            );
                            let previous = self.selection;
                            ui.add_enabled_ui(ready && !self.editing, |ui| {
                                egui::ComboBox::from_id_salt("market-data-selection")
                                    .width((bounds.width() - 232. / p).max(160. / p))
                                    .selected_text(VIEWS[self.selection])
                                    .show_ui(ui, |ui| {
                                        for (i, name) in VIEWS.iter().enumerate() {
                                            if ui
                                                .selectable_value(&mut self.selection, i, *name)
                                                .clicked()
                                            {
                                                ui.close();
                                            }
                                        }
                                    });
                            });
                            if previous != self.selection {
                                self.contract.reset();
                                self.rates.selected = None;
                                self.rates.draft.clear();
                                self.remove_confirmation = false;
                                self.load_requested = false;
                            }
                        });
                    },
                );
                let content = egui::Rect::from_min_max(
                    egui::pos2(bounds.left(), selector.bottom()),
                    egui::pos2(bounds.right() - 115. / p, bounds.bottom()),
                );
                let rail = egui::Rect::from_min_max(
                    egui::pos2(content.right(), content.top()),
                    bounds.max,
                );
                self.controls.insert("content".into(), content);
                self.controls.insert("commands".into(), rail);
                ui.scope_builder(
                    egui::UiBuilder::new().max_rect(rail.shrink2(egui::vec2(7., 4.) / p)),
                    |ui| {
                        let selected = if self.selection == 2 {
                            self.rates.selected.is_some()
                        } else {
                            self.contract.selected.is_some()
                        };
                        for (key, title, enabled) in [
                            (
                                "add",
                                if self.editing && self.adding {
                                    "Save"
                                } else {
                                    "Add"
                                },
                                ready && (!self.editing || self.adding),
                            ),
                            (
                                "change",
                                if self.editing && !self.adding {
                                    "Save"
                                } else {
                                    "Change"
                                },
                                ready && selected && (!self.editing || !self.adding),
                            ),
                            ("remove", "Remove", ready && selected && !self.editing),
                            (
                                "import",
                                "Import",
                                ready && self.selection == 2 && !self.editing,
                            ),
                            ("close", if self.editing { "Cancel" } else { "Close" }, true),
                        ] {
                            let response = ui.add_enabled(
                                enabled,
                                egui::Button::new(title).min_size(egui::vec2(99., 26.) / p),
                            );
                            self.controls.insert(key.into(), response.rect);
                            if response.clicked() {
                                match key {
                                    "add" | "change" if self.editing => action = Some(key),
                                    "add" => {
                                        self.editing = true;
                                        self.adding = true;
                                        self.scroll_rate_edit = true;
                                        if self.selection == 2 {
                                            self.rates.selected = None;
                                            self.rates.draft.clear();
                                            self.rates.draft.insert(
                                                "valueDate".into(),
                                                publication
                                                    .result
                                                    .context
                                                    .get("valueDate")
                                                    .cloned()
                                                    .unwrap_or_default(),
                                            );
                                            for (key, _) in TENORS {
                                                self.rates.draft.insert(key.into(), "0".into());
                                            }
                                        } else {
                                            self.contract.reset();
                                        }
                                    }
                                    "change" => {
                                        self.editing = true;
                                        self.adding = false;
                                        self.scroll_rate_edit = true;
                                        if self.selection == 2 {
                                            self.rates.draft = publication
                                                .result
                                                .rows
                                                .iter()
                                                .find(|r| {
                                                    Some(&r.id) == self.rates.selected.as_ref()
                                                })
                                                .map(|r| r.fields.clone())
                                                .unwrap_or_default();
                                        }
                                    }
                                    "remove" => self.remove_confirmation = true,
                                    "close" if self.editing => {
                                        self.editing = false;
                                        if self.selection == 2 {
                                            self.rates.draft = publication
                                                .result
                                                .rows
                                                .iter()
                                                .find(|r| {
                                                    Some(&r.id) == self.rates.selected.as_ref()
                                                })
                                                .map(|r| r.fields.clone())
                                                .unwrap_or_default();
                                        } else {
                                            self.contract.draft = publication
                                                .result
                                                .rows
                                                .iter()
                                                .find(|r| {
                                                    Some(&r.id) == self.contract.selected.as_ref()
                                                })
                                                .map(|r| r.fields.clone())
                                                .unwrap_or_default();
                                        }
                                    }
                                    "close" => self.open = false,
                                    "import" => action = Some("import"),
                                    _ => {}
                                }
                            }
                        }
                    },
                );
                ui.scope_builder(
                    egui::UiBuilder::new()
                        .id_salt("manager-content")
                        .max_rect(content),
                    |ui| {
                        ui.set_clip_rect(content.intersect(ui.clip_rect()));
                        if self.selection == 2 {
                            self.draw_rates(ui, publication, ready && matches, &mut action);
                        } else {
                            self.draw_contract(ui, publication, ready && matches, &mut action);
                        }
                    },
                );
                ui.allocate_rect(bounds, egui::Sense::hover());
            });
        if let Some(window) = window {
            ctx.memory_mut(|memory| memory.set_modal_layer(window.response.layer_id));
        }
        self.open &= open;
        if self.remove_confirmation {
            egui::Window::new(presentation::dialog_title("Remove Market Data"))
                .id(egui::Id::new("market-data-remove-confirm"))
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label("Remove the selected record?");
                    if ui.add_enabled(ready, egui::Button::new("Remove")).clicked() {
                        action = Some("remove");
                        self.remove_confirmation = false;
                    }
                    if ui.button("Cancel").clicked() {
                        self.remove_confirmation = false;
                    }
                });
        }
        if !self.error.is_empty() {
            let mut visible = true;
            egui::Window::new(presentation::dialog_title("Market Data Error"))
                .id(egui::Id::new("market-data-error"))
                .open(&mut visible)
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label(&self.error);
                });
            if !visible {
                self.error.clear();
            }
        }
        if self.open && ready && !self.load_requested {
            self.load_requested = true;
            action = Some("load");
        }
        action.map(|action| self.request(action, publication))
    }

    /// Lay out the selected futures/option contract list and details using the reused contract editor's draft and eligibility.
    fn draw_contract(
        &mut self,
        ui: &mut egui::Ui,
        publication: &ContractPublication,
        enabled: bool,
        action: &mut Option<&'static str>,
    ) {
        // The WinForms contract controls use Font autoscaling from a 7x15 design
        // baseline to the shared 10pt font's 8x16 baseline, before display DPI.
        let scale = egui::vec2(8.0 / 7.0, 16.0 / 15.0);
        let p = 1.0_f32;
        let bounds = ui.max_rect();
        let list_width = (232. * scale.x).min(bounds.width() * 0.35);
        let list = egui::Rect::from_min_size(bounds.min, egui::vec2(list_width, bounds.height()));
        self.controls.insert("contract-list".into(), list);
        ui.scope_builder(
            egui::UiBuilder::new()
                .id_salt("manager-contract-list-pane")
                .max_rect(list.shrink(1. / p)),
            |ui| {
                ui.add_sized(
                    [list.width(), 30. * scale.y],
                    presentation::caption_widget(if self.selection == 1 {
                        "Futures Option Contract Ids"
                    } else {
                        "Futures Contract Ids"
                    }),
                );
                egui::ScrollArea::both()
                    .id_salt(("manager-contract-list-scroll", self.selection))
                    .max_height((list.height() - 34.0).max(0.0))
                    .auto_shrink([false, false])
                    .show_rows(ui, 24., publication.result.rows.len(), |ui, rows| {
                        if publication.result.view != self.view() {
                            return;
                        }
                        for index in rows {
                            if self.selection == 1
                                && enabled
                                && !self.editing
                                && publication.result.has_more
                                && index + 1 == publication.result.rows.len()
                                && action.is_none()
                            {
                                *action = Some("next");
                            }
                            let item = &publication.result.rows[index];
                            if ui
                                .add_enabled(
                                    enabled && !self.editing,
                                    egui::Button::selectable(
                                        self.contract.selected.as_ref() == Some(&item.id),
                                        item.fields.get("contractId").unwrap_or(&item.id),
                                    )
                                    .min_size(egui::vec2(ui.available_width(), 24.)),
                                )
                                .clicked()
                            {
                                self.contract.selected = Some(item.id.clone());
                                self.contract.draft = item.fields.clone();
                            }
                        }
                    });
            },
        );
        border(ui, list);
        let fields =
            egui::Rect::from_min_max(egui::pos2(list.right() + 4. / p, bounds.top()), bounds.max);
        self.controls.insert("contract-fields".into(), fields);
        ui.scope_builder(
            egui::UiBuilder::new()
                .id_salt("manager-fields")
                .max_rect(fields),
            |ui| {
                ui.set_clip_rect(fields.intersect(ui.clip_rect()));
                let mut fields = vec![
                    ("contractId", "Contract Id:", 0.),
                    ("description", "Description:", 32.),
                ];
                if self.selection == 1 {
                    fields.extend([
                        ("contractMonth", "Contract Month:", 92.),
                        ("strikePrice", "Strike Price:", 120.),
                        ("optionType", "Option Type:", 148.),
                        ("symbol", "Symbol:", 178.),
                        ("localSymbol", "Local Symbol:", 210.),
                        ("securityType", "Security Type:", 239.),
                        ("currency", "Currency:", 269.),
                        ("exchange", "Exchange:", 298.),
                        ("multiplier", "Multiplier:", 328.),
                    ]);
                } else {
                    fields.extend([
                        ("lastTradeDate", "Last Trade Date:", 93.),
                        ("symbol", "Symbol:", 129.),
                        ("localSymbol", "Local Symbol:", 158.),
                        ("securityType", "Security Type:", 190.),
                        ("currency", "Currency:", 222.),
                        ("exchange", "Exchange:", 257.),
                        ("multiplier", "Multiplier:", 287.),
                        ("onTheRun", "On The Run:", 320.),
                        ("rollover", "Rollover:", 352.),
                    ]);
                }
                let origin = ui.cursor().min;
                let provider = self
                    .contract
                    .draft
                    .get("provider")
                    .is_some_and(|v| v == "true");
                for (key, title, y) in fields {
                    let caption = egui::Rect::from_min_size(
                        origin + egui::vec2(2., y + 2.) * scale,
                        egui::vec2(157., 26.) * scale,
                    );
                    let caption_id = ui.put(caption, presentation::caption_widget(title)).id;
                    let width = if key == "description" {
                        (ui.available_width() - 163. * scale.x).max(229. * scale.x)
                    } else {
                        229. * scale.x
                    };
                    let rect = egui::Rect::from_min_size(
                        origin + egui::vec2(163., y + 2.) * scale,
                        egui::vec2(
                            width,
                            if key == "description" {
                                55. * scale.y
                            } else {
                                24.
                            },
                        ),
                    );
                    self.controls.insert(key.into(), rect);
                    let symbol_filter = self.selection == 1 && key == "symbol" && !self.editing;
                    let editable = enabled
                        && (symbol_filter
                            || self.editing
                                && key != "contractId"
                                && (!provider || key == "description" || key == "onTheRun")
                                && !(self.selection == 1
                                    && !self.adding
                                    && ["symbol", "contractMonth", "strikePrice", "optionType"]
                                        .contains(&key)));
                    let value = self.contract.draft.entry(key.into()).or_default();
                    ui.scope_builder(
                        egui::UiBuilder::new()
                            .id_salt(("manager-field-pane", key))
                            .max_rect(rect),
                        |ui| {
                            ui.add_enabled_ui(editable, |ui| {
                                if key == "onTheRun" || key == "rollover" {
                                    let mut checked = value.eq_ignore_ascii_case("true");
                                    ui.add(crate::presentation::checkbox(&mut checked, ""))
                                        .labelled_by(caption_id);
                                    *value = checked.to_string();
                                } else if let Some(choices) = publication.result.choices.get(key) {
                                    egui::ComboBox::from_id_salt(("manager-field", key))
                                        .width(rect.width())
                                        .selected_text(&*value)
                                        .show_ui(ui, |ui| {
                                            for choice in choices {
                                                if ui
                                                    .selectable_value(value, choice.clone(), choice)
                                                    .changed()
                                                    && key == "symbol"
                                                    && self.selection == 1
                                                    && !self.editing
                                                {
                                                    *action = Some("load");
                                                    self.contract.selected = None;
                                                }
                                            }
                                        })
                                        .response
                                        .labelled_by(caption_id);
                                } else if key == "description" {
                                    ui.add_sized(rect.size(), egui::TextEdit::multiline(value))
                                        .labelled_by(caption_id);
                                } else {
                                    ui.add_sized(rect.size(), egui::TextEdit::singleline(value))
                                        .labelled_by(caption_id);
                                }
                            });
                        },
                    );
                }
            },
        );
        border(ui, fields);
    }

    /// Render the yield-rate report list and inline editor; use backend eligibility and emit the requested edit/save/cancel action.
    fn draw_rates(
        &mut self,
        ui: &mut egui::Ui,
        publication: &ContractPublication,
        enabled: bool,
        action: &mut Option<&'static str>,
    ) {
        let p = 1.0_f32;
        let scale = egui::vec2(8.0 / 7.0, 16.0 / 15.0);
        let bounds = ui.max_rect();
        let at = |x, y, w, h| {
            egui::Rect::from_min_size(
                bounds.min + egui::vec2(x, y) * scale,
                egui::vec2(w, h) * scale,
            )
        };
        ui.put(
            at(14., 2., 103., 24.),
            presentation::caption_widget("Time Period:"),
        );
        ui.scope_builder(
            egui::UiBuilder::new()
                .id_salt("manager-rate-period-pane")
                .max_rect(at(121., 2., 248., 24.)),
            |ui| {
                egui::ComboBox::from_id_salt("manager-rate-period")
                    .width(248. * scale.x)
                    .selected_text(&self.rates.period)
                    .show_ui(ui, |ui| {
                        for period in publication
                            .result
                            .choices
                            .get("timePeriod")
                            .into_iter()
                            .flatten()
                        {
                            if ui
                                .selectable_value(&mut self.rates.period, period.clone(), period)
                                .changed()
                                && enabled
                                && !self.editing
                            {
                                *action = Some("range");
                            }
                        }
                    });
            },
        );
        ui.put(
            at(421., 2., 101., 24.),
            presentation::caption_widget("Import Date:"),
        );
        ui.put(
            at(526., 2., 120., 24.),
            egui::TextEdit::singleline(&mut self.rates.import_date),
        );
        if self.rates.period.is_empty() {
            self.rates.period = publication
                .result
                .context
                .get("timePeriod")
                .cloned()
                .unwrap_or_default();
        }
        if self.rates.import_date.is_empty() {
            self.rates.import_date = publication
                .result
                .context
                .get("valueDate")
                .cloned()
                .unwrap_or_default();
        }
        let grid = egui::Rect::from_min_max(
            egui::pos2(bounds.left(), bounds.top() + 32. * scale.y),
            bounds.max,
        );
        self.controls.insert("rates-grid".into(), grid);
        ui.scope_builder(
            egui::UiBuilder::new()
                .id_salt("manager-rate-grid-pane")
                .max_rect(grid.shrink(1. / p)),
            |ui| {
                egui::ScrollArea::horizontal()
                    .id_salt("manager-rates-scroll")
                    .scroll_bar_visibility(
                        egui::scroll_area::ScrollBarVisibility::VisibleWhenNeeded,
                    )
                    .auto_shrink([false, false])
                    .show(ui, |ui| {
                        ui.set_min_width(900. / p);
                        ui.style_mut().interaction.selectable_labels = false;
                        ui.spacing_mut().item_spacing.x = 0.;
                        let row_spacing = ui.spacing().item_spacing.y;
                        let mut table = egui_extras::TableBuilder::new(ui)
                            .id_salt("manager-rates")
                            .resizable(true)
                            .sense(egui::Sense::click())
                            .column(egui_extras::Column::initial(180. / p).at_least(180. / p));
                        for index in 0..TENORS.len() {
                            table = table.column(if index + 1 == TENORS.len() {
                                egui_extras::Column::remainder().at_least(60. / p)
                            } else {
                                egui_extras::Column::initial(60. / p).at_least(60. / p)
                            });
                        }
                        let rows = if publication.result.view == "yield-curve" {
                            publication.result.rows.as_slice()
                        } else {
                            &[]
                        };
                        if self.editing && std::mem::take(&mut self.scroll_rate_edit) {
                            let index = if self.adding {
                                rows.len()
                            } else {
                                rows.iter()
                                    .position(|row| self.rates.selected.as_ref() == Some(&row.id))
                                    .unwrap_or(0)
                            };
                            table = table.scroll_to_row(index, Some(egui::Align::Center));
                        }
                        table
                            .fill_grid(
                                rows.len() + usize::from(self.editing && self.adding),
                                26. / p,
                                row_spacing,
                                (grid.height() - 24. / p - row_spacing - 2. / p).max(0.),
                            )
                            .header(24. / p, |mut header| {
                                for name in [
                                    "Date", "1 Mo", "2 Mo", "3 Mo", "6 Mo", "1 Yr", "2 Yr", "3 Yr",
                                    "5 Yr", "7 Yr", "10 Yr", "20 Yr", "30 Yr",
                                ] {
                                    header.col(|ui| {
                                        ui.add(presentation::caption_widget(name));
                                        border(ui, ui.max_rect());
                                    });
                                }
                            })
                            .bordered_body(24. / p, |body| {
                                body.rows(
                                    26. / p,
                                    rows.len() + usize::from(self.editing && self.adding),
                                    |mut row| {
                                        let index = row.index();
                                        let item = rows.get(index);
                                        let selected = item.is_some_and(|item| {
                                            self.rates.selected.as_ref() == Some(&item.id)
                                        });
                                        let draft_row = self.editing
                                            && (selected || self.adding && index == rows.len());
                                        row.set_selected(selected || draft_row);
                                        for key in std::iter::once("valueDate")
                                            .chain(TENORS.iter().map(|(key, _)| *key))
                                        {
                                            row.col(|ui| {
                                                if draft_row {
                                                    ui.add_enabled(
                                                        enabled
                                                            && (key != "valueDate" || self.adding),
                                                        egui::TextEdit::singleline(
                                                            self.rates
                                                                .draft
                                                                .entry(key.into())
                                                                .or_default(),
                                                        )
                                                        .desired_width(ui.available_width()),
                                                    );
                                                } else if let Some(item) = item {
                                                    let value = item
                                                        .fields
                                                        .get(key)
                                                        .map(String::as_str)
                                                        .unwrap_or("");
                                                    ui.trading_label(value);
                                                }
                                            });
                                        }
                                        let response = row.response();
                                        if (response.clicked() || response.double_clicked())
                                            && enabled
                                            && !self.editing
                                        {
                                            if let Some(item) = item {
                                                self.rates.selected = Some(item.id.clone());
                                                self.rates.draft = item.fields.clone();
                                                if response.double_clicked() {
                                                    self.editing = true;
                                                    self.adding = false;
                                                }
                                            }
                                        }
                                    },
                                );
                            });
                    });
            },
        );
        border(ui, grid);
    }
}
/// Paint a one-pixel inside border clipped to the control's visible bounds.
fn border(ui: &egui::Ui, rect: egui::Rect) {
    ui.painter().rect_stroke(
        rect.intersect(ui.clip_rect()),
        0.,
        egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
        egui::StrokeKind::Inside,
    );
}
