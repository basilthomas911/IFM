//! Fund contract tree and compact symbol-selection editor.
use super::*;

impl PortfolioManager {
    /// Keeps both asset roots visible; Futures contains the Fund's resolved contracts.
    pub(super) fn base_contract_tree(
        &mut self,
        ui: &mut egui::Ui,
        document: &Document,
        height: f32,
    ) {
        let bounds =
            egui::Rect::from_min_size(ui.cursor().min, egui::vec2(ui.available_width(), height));
        self.controls.insert("base-contracts.tree".into(), bounds);
        crate::control_parity::grid_viewport(ui, bounds, |ui| {
            egui::ScrollArea::both()
                .auto_shrink([false, false])
                .id_salt("fund-base-contract-tree")
                .max_height(height)
                .bordered_show(ui, |ui| {
                    egui::CollapsingHeader::new("Futures")
                        .default_open(true)
                        .show(ui, |ui| {
                            let symbols = document
                                .base_contracts
                                .iter()
                                .map(|r| r.symbol.as_str())
                                .collect::<std::collections::BTreeSet<_>>();
                            for symbol in symbols {
                                egui::CollapsingHeader::new(symbol)
                                    .id_salt(("fund-symbol", symbol))
                                    .default_open(true)
                                    .show(ui, |ui| {
                                        for row in document
                                            .base_contracts
                                            .iter()
                                            .filter(|r| r.symbol == symbol)
                                        {
                                            if row.contract_id.is_empty() {
                                                ui.label(&row.description);
                                                continue;
                                            }
                                            egui::CollapsingHeader::new(&row.contract_id)
                                                .id_salt(("fund-contract", &row.contract_id))
                                                .show(ui, |ui| {
                                                    ui.label(&row.description);
                                                });
                                        }
                                    });
                            }
                        });
                    egui::CollapsingHeader::new("Equities").show(ui, |_| {});
                });
        });
        ui.painter().rect_stroke(
            bounds,
            0.,
            egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
            egui::StrokeKind::Inside,
        );
    }

    /// Shows pinned selections and filtered choices with centered Save/Cancel actions.
    pub(super) fn base_contract_dialog(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        presentation::form_grid(ui, "base-contract-asset-field")
            .num_columns(2)
            .show(ui, |ui| {
                let label = presentation::form_caption_with_width(ui, "Base Asset:", 100.);
                self.controls
                    .insert("base-contracts.BaseAsset.label".into(), label.rect);
                let asset = self
                    .fields
                    .entry("base-contracts.BaseAsset".into())
                    .or_insert_with(|| "Futures".into());
                let response = egui::ComboBox::from_id_salt("base-contract-asset")
                    .selected_text(asset.as_str())
                    .show_ui(ui, |ui| {
                        ui.selectable_value(asset, "Futures".into(), "Futures");
                        ui.selectable_value(asset, "Equities".into(), "Equities");
                    });
                self.controls
                    .insert("base-contracts.BaseAsset".into(), response.response.rect);
                ui.end_row();
                let futures = asset == "Futures";
                let label = presentation::form_caption_with_width(ui, "Symbol filter:", 100.);
                self.controls
                    .insert("base-contracts.SymbolFilter.label".into(), label.rect);
                let filter = self
                    .fields
                    .entry("base-contracts.SymbolFilter".into())
                    .or_default();
                let response = ui.add_enabled(
                    ready && futures,
                    egui::TextEdit::singleline(filter)
                        .id_salt("fund-symbol-filter")
                        .hint_text("Type a symbol prefix...")
                        .desired_width(240.),
                );
                self.controls
                    .insert("base-contracts.SymbolFilter".into(), response.rect);
                ui.end_row();
            });
        let futures = self
            .fields
            .get("base-contracts.BaseAsset")
            .is_some_and(|v| v == "Futures");
        let symbols = d
            .fields
            .iter()
            .filter(|f| f.key.starts_with("selected."))
            .collect::<Vec<_>>();
        let selected = symbols
            .iter()
            .copied()
            .filter(|f| {
                self.fields
                    .get(&format!("base-contracts.{}", f.key))
                    .unwrap_or(&f.value)
                    == "true"
            })
            .collect::<Vec<_>>();
        let filter = self
            .fields
            .get("base-contracts.SymbolFilter")
            .map(|v| v.trim().to_ascii_uppercase())
            .unwrap_or_default();
        let available_symbols = symbols
            .iter()
            .copied()
            .filter(|f| {
                self.fields
                    .get(&format!("base-contracts.{}", f.key))
                    .unwrap_or(&f.value)
                    != "true"
                    && f.caption.to_ascii_uppercase().starts_with(&filter)
            })
            .collect::<Vec<_>>();
        let row_height = ui.spacing().interact_size.y.max(28.);
        let available = (ui.available_height() - 145.).max(60.);
        let selected_height = if futures {
            (selected.len().max(1) as f32 * (row_height + ui.spacing().item_spacing.y)).min(90.)
        } else {
            row_height
        };
        ui.strong("Selected symbols");
        self.base_contract_symbol_rows(
            ui,
            if futures { &selected } else { &[] },
            selected_height,
            ready,
            "selected",
        );
        ui.add_space(3.);
        ui.separator();
        ui.add_space(3.);
        ui.strong(format!(
            "Available symbols ({})",
            if futures { available_symbols.len() } else { 0 }
        ));
        self.base_contract_symbol_rows(
            ui,
            if futures { &available_symbols } else { &[] },
            (available - selected_height).max(row_height),
            ready,
            "available",
        );
        let any = !selected.is_empty();
        ui.add_space(6.);
        let width = 180. + ui.spacing().item_spacing.x;
        ui.horizontal(|ui| {
            ui.add_space(((ui.available_width() - width) / 2.).max(0.));
            for label in ["Save", "Cancel"] {
                let response = ui.add_enabled(
                    ready && (label != "Save" || futures && any),
                    egui::Button::new(label).min_size(egui::vec2(90., 26.)),
                );
                self.controls
                    .insert(format!("base-contracts.button.{label}"), response.rect);
                if response.clicked() {
                    *action = Some((
                        if label == "Save" {
                            "portfolio-save"
                        } else {
                            "portfolio-cancel"
                        }
                        .into(),
                        None,
                        self.dialog_fields(d),
                    ));
                }
            }
        });
    }

    /// Virtualizes report rows and preserves the same header, column and scrollbar borders as Strategy Updates.
    fn base_contract_symbol_rows(
        &mut self,
        ui: &mut egui::Ui,
        symbols: &[&Field],
        height: f32,
        ready: bool,
        section: &str,
    ) {
        let row_height = ui.spacing().interact_size.y.max(28.);
        let header_height = 24.;
        let spacing = ui.spacing().item_spacing.y;
        let bounds = egui::Rect::from_min_size(
            ui.cursor().min,
            egui::vec2(ui.available_width(), height + header_height + spacing),
        );
        self.controls
            .insert(format!("base-contracts.grid.{section}"), bounds);
        crate::control_parity::grid_viewport(ui, bounds, |ui| {
            ui.spacing_mut().scroll.floating = false;
            egui_extras::TableBuilder::new(ui)
                .id_salt(("fund-symbols", section))
                .fill_grid(symbols.len(), row_height, spacing, height)
                // Reserve the same gutter in both sections even when selected rows do not overflow.
                .vscroll(true)
                .scroll_bar_visibility(egui::scroll_area::ScrollBarVisibility::AlwaysVisible)
                .striped(true)
                .resizable(false)
                .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                .column(egui_extras::Column::exact(28.))
                .column(egui_extras::Column::exact(90.))
                .column(egui_extras::Column::remainder().at_least(100.))
                .column(egui_extras::Column::exact(110.))
                .header(header_height, |mut header| {
                    for (column, caption) in ["Use", "Symbol", "Description", "Forward cycles"]
                        .into_iter()
                        .enumerate()
                    {
                        header.col(|ui| {
                            self.controls.insert(
                                format!("base-contracts.grid.{section}.column.{column}"),
                                ui.max_rect(),
                            );
                            let rect = ui
                                .max_rect()
                                .expand2(egui::vec2(ui.spacing().item_spacing.x / 2., 0.));
                            ui.painter().rect(
                                rect,
                                0.,
                                presentation::COMMAND,
                                egui::Stroke::new(1_f32, presentation::BORDER),
                                egui::StrokeKind::Inside,
                            );
                            ui.add(
                                presentation::label_widget(
                                    egui::RichText::new(caption)
                                        .color(presentation::CAPTION_FOREGROUND),
                                )
                                .truncate(),
                            );
                        });
                    }
                })
                .bordered_body(header_height, |body| {
                    body.rows(row_height, symbols.len(), |mut row| {
                        let field = symbols[row.index()];
                        let key = format!("base-contracts.{}", field.key);
                        let mut checked = self.fields.get(&key).unwrap_or(&field.value) == "true";
                        row.col(|ui| {
                            let response =
                                ui.add_enabled(ready, egui::Checkbox::without_text(&mut checked));
                            self.controls.insert(key.clone(), response.rect);
                        });
                        self.fields.insert(key, checked.to_string());
                        row.col(|ui| {
                            ui.add(egui::Label::new(&field.caption).truncate());
                        });
                        row.col(|ui| {
                            let description = field
                                .choices
                                .as_ref()
                                .and_then(|c| c.first())
                                .map(|c| c.label.as_str())
                                .unwrap_or("");
                            ui.add(egui::Label::new(description).truncate())
                                .on_hover_text(description);
                        });
                        row.col(|ui| {
                            let key = format!("base-contracts.cycles.{}", field.caption);
                            let mut cycles = self
                                .fields
                                .get(&key)
                                .and_then(|v| v.parse::<u32>().ok())
                                .unwrap_or(2);
                            ui.add_enabled_ui(ready && checked, |ui| {
                                ui.horizontal(|ui| {
                                    let response =
                                        ui.add(egui::DragValue::new(&mut cycles).range(0..=120));
                                    self.controls.insert(key.clone(), response.rect);
                                    if section == "selected" {
                                        ui.spacing_mut().item_spacing = egui::vec2(2., 0.);
                                        ui.spacing_mut().interact_size.y = 12.;
                                        ui.spacing_mut().button_padding.y = 0.;
                                        ui.vertical(|ui| {
                                            for (suffix, caption) in
                                                [("up", "\u{2191}"), ("down", "\u{2193}")]
                                            {
                                                let enabled = if suffix == "up" {
                                                    cycles < 120
                                                } else {
                                                    cycles > 0
                                                };
                                                let response = ui.add_enabled(
                                                    enabled,
                                                    egui::Button::new(
                                                        egui::RichText::new(caption).size(10.),
                                                    )
                                                    .min_size(egui::vec2(18., 0.)),
                                                );
                                                self.controls.insert(
                                                    format!("{key}.{suffix}"),
                                                    response.rect,
                                                );
                                                if response.clicked() {
                                                    cycles = if suffix == "up" {
                                                        (cycles + 1).min(120)
                                                    } else {
                                                        cycles.saturating_sub(1)
                                                    };
                                                }
                                            }
                                        });
                                    }
                                });
                            });
                            self.fields.insert(key, cycles.to_string());
                        });
                    });
                });
        });
        ui.painter().rect_stroke(
            bounds,
            0.,
            egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
            egui::StrokeKind::Inside,
        );
    }
}
