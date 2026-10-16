//! Center document tab strip. Content is selected by the bridge's stable document key.
use super::*;
impl TradeWorkspace {
    pub(super) fn draw_workspace_tabs(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        bounds: egui::Rect,
    ) -> Option<&'static str> {
        let p = ui.ctx().pixels_per_point();
        let mut tabs: Vec<_> = s
            .result
            .rows
            .iter()
            .filter(|r| r.fields.get("kind").is_some_and(|k| k == "workspace-tab"))
            .map(data)
            .collect();
        // Older publications and offline layout fixtures contain the active document only.
        if tabs.is_empty() {
            if let Some(trade) = s
                .result
                .rows
                .iter()
                .find(|r| r.fields.get("kind").is_some_and(|k| k == "established"))
                .map(data)
            {
                tabs.push(serde_json::json!({"key":trade["id"].to_string(),"title":format!("{}:{}",text(&trade["id"],"orderId"),text(&trade["id"],"tradeId")),"active":true}));
            }
        }
        let mut action = None;
        let strip = egui::Rect::from_min_size(bounds.min, egui::vec2(bounds.width(), 32. / p));
        ui.painter().hline(
            strip.x_range(),
            strip.bottom(),
            egui::Stroke::new(1.0_f32, Color32::GRAY),
        );
        super::order_layout::at(ui, strip, "center-document-tabs", |ui| {
            ui.set_clip_rect(strip);
            egui::ScrollArea::horizontal()
                .id_salt("center-tabs-scroll")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    ui.horizontal(|ui| {
                        ui.spacing_mut().item_spacing.x = 0.;
                        for tab in tabs {
                            let key = text(&tab, "key");
                            let title = text(&tab, "title");
                            let active = tab["active"] == true;
                            let font = egui::TextStyle::Button.resolve(ui.style());
                            let width = (ui
                                .painter()
                                .layout_no_wrap(title.clone(), font, Color32::WHITE)
                                .size()
                                .x
                                + 46. / p)
                                .max(120. / p);
                            let (r, _) = ui.allocate_exact_size(
                                egui::vec2(width, 30. / p),
                                egui::Sense::hover(),
                            );
                            ui.painter().rect_filled(
                                r,
                                0.,
                                if active {
                                    crate::presentation::COMMAND
                                } else {
                                    Color32::BLACK
                                },
                            );
                            if active {
                                ui.painter().rect_stroke(
                                    r,
                                    0.,
                                    egui::Stroke::new(1.0_f32, Color32::GRAY),
                                    egui::StrokeKind::Inside,
                                );
                            }
                            let close = egui::Rect::from_min_max(
                                egui::pos2(r.right() - 24. / p, r.top() + 3. / p),
                                egui::pos2(r.right() - 4. / p, r.bottom() - 3. / p),
                            );
                            let title_rect = egui::Rect::from_min_max(
                                r.min,
                                egui::pos2(close.left() - 4. / p, r.bottom()),
                            );
                            let enabled = self.interaction_ready(s);
                            ui.push_id(&key, |ui| {
                                let mut caption = egui::RichText::new(&title).color(if active {
                                    Color32::WHITE
                                } else {
                                    Color32::LIGHT_GRAY
                                });
                                if active {
                                    caption = caption.strong();
                                }
                                let response =
                                    ui.put(title_rect, egui::Button::new(caption).frame(false));
                                if enabled && response.clicked() && !active {
                                    self.draft.insert("tradeTabId".into(), key.clone());
                                    action = Some("activate-trade");
                                }
                                let response = ui
                                    .put(close, egui::Button::new("\u{00d7}").frame(false))
                                    .on_hover_text(format!("Close trade {title}"));
                                response.widget_info(|| {
                                    egui::WidgetInfo::labeled(
                                        egui::WidgetType::Button,
                                        enabled,
                                        format!("Close trade {title}"),
                                    )
                                });
                                self.controls.insert(format!("close-trade:{key}"), close);
                                if enabled && response.clicked() {
                                    self.draft.insert("tradeTabId".into(), key.clone());
                                    action = Some("close-trade");
                                }
                            });
                            self.controls.insert(format!("trade-tab:{key}"), r);
                        }
                    });
                });
        });
        action
    }
}
