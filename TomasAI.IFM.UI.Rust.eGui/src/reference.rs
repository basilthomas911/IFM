//! WinForms Reference Data Manager shell: selector, unchanged five-command rail, and editor body.
use eframe::egui;

/// Eligibility and edit mode supplied by the reference editor, never inferred from row position.
pub struct Commands {
    pub editing: bool,
    pub changing: bool,
    pub ready: bool,
    pub add: bool,
    pub change: bool,
    pub remove: bool,
    pub save: bool,
}

/// Render the original 1450 by 850 reference client area with its 37-pixel selector and 115-pixel rail.
pub fn show(
    ctx: &egui::Context,
    selected: usize,
    commands: Commands,
    mut body: impl FnMut(&mut egui::Ui, Option<&'static str>) -> Option<String>,
) -> Option<String> {
    let mut result = None;
    if !commands.editing
        && ctx.data(|data| {
            data.get_temp::<bool>(egui::Id::new("reference-close-after-discard"))
                .unwrap_or(false)
        })
    {
        ctx.data_mut(|data| {
            data.remove::<bool>(egui::Id::new("reference-close-after-discard"));
            data.insert_temp(egui::Id::new("reference-closed"), true);
        });
    }
    let size = egui::vec2(1450., 850.).min(ctx.screen_rect().size() - egui::vec2(24., 64.));
    let background = crate::presentation::BACKGROUND;
    let mut open = true;
    egui::Window::new("Reference Data Manager")
        .id(egui::Id::new("reference-data-manager"))
        .open(&mut open)
        .collapsible(false)
        .default_size(size)
        .min_size(egui::vec2(900., 550.).min(size))
        .frame(
            egui::Frame::window(&ctx.style())
                .fill(background)
                .stroke(egui::Stroke::new(3.0_f32, crate::presentation::BORDER))
                .inner_margin(3),
        )
        .show(ctx, |ui| {
            // Match ShowModal<ReferenceForm>: keep interaction inside the manager and its children.
            ctx.memory_mut(|memory| memory.set_modal_layer(ui.layer_id()));
            let bounds = ui.available_rect_before_wrap();
            ctx.data_mut(|data| data.insert_temp(egui::Id::new("reference-bounds"), bounds));
            let header = egui::Rect::from_min_size(bounds.min, egui::vec2(bounds.width(), 37.));
            let labels = ["Lookup Types", "trade strategy families", "parameter sets"];
            let mut choice = selected;
            let caption_rect =
                egui::Rect::from_min_size(header.min + egui::vec2(32., 8.), egui::vec2(79., 20.));
            let caption = ui.put(
                caption_rect,
                crate::presentation::caption_widget("Selection:"),
            );
            let selector_rect = egui::Rect::from_min_size(
                header.min + egui::vec2(115., 5.),
                egui::vec2((header.width() - 236.).max(100.), 24.),
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(selector_rect), |ui| {
                ui.add_enabled_ui(commands.ready && !commands.editing, |ui| {
                    let response = egui::ComboBox::from_id_salt("reference-selector")
                        .selected_text(labels[selected])
                        .width(selector_rect.width())
                        .show_ui(ui, |ui| {
                            for (index, label) in labels.iter().enumerate() {
                                if ui.selectable_value(&mut choice, index, *label).clicked() {
                                    ui.close();
                                }
                            }
                        })
                        .response
                        .labelled_by(caption.id);
                    response.widget_info(|| {
                        egui::WidgetInfo::labeled(
                            egui::WidgetType::ComboBox,
                            commands.ready && !commands.editing,
                            "Selection:",
                        )
                    });
                });
            });
            let rail = egui::Rect::from_min_max(
                egui::pos2(bounds.right() - 115., header.bottom()),
                bounds.max,
            );
            let mut command = ctx
                .data_mut(|data| data.remove_temp::<bool>(egui::Id::new("reference-discard")))
                .filter(|v| *v)
                .map(|_| "close");
            for (index, (label, intent, enabled)) in [
                (
                    if commands.editing && !commands.changing {
                        "Save"
                    } else {
                        "Add"
                    },
                    "add",
                    if commands.editing {
                        !commands.changing && commands.save
                    } else {
                        commands.add
                    },
                ),
                (
                    if commands.editing && commands.changing {
                        "Save"
                    } else {
                        "Change"
                    },
                    "change",
                    if commands.editing {
                        commands.changing && commands.save
                    } else {
                        commands.change
                    },
                ),
                ("Remove", "remove", commands.remove && !commands.editing),
                ("Import", "import", false),
                (
                    if commands.editing { "Cancel" } else { "Close" },
                    "close",
                    true,
                ),
            ]
            .into_iter()
            .enumerate()
            {
                let rect = egui::Rect::from_min_size(
                    rail.min + egui::vec2(7., 4. + index as f32 * 30.),
                    egui::vec2(99., 26.),
                );
                let response = ui
                    .add_enabled_ui(commands.ready && enabled, |ui| {
                        ui.put(rect, egui::Button::new(label))
                    })
                    .inner;
                if response.clicked() {
                    command = Some(intent);
                }
            }
            let content = egui::Rect::from_min_max(
                egui::pos2(bounds.left(), header.bottom()),
                egui::pos2(rail.left(), bounds.bottom()),
            );
            if choice != selected {
                ctx.data_mut(|data| data.insert_temp(egui::Id::new("reference-selection"), choice));
            } else if command == Some("close") && !commands.editing {
                ctx.data_mut(|data| data.insert_temp(egui::Id::new("reference-closed"), true));
            } else {
                ui.scope_builder(egui::UiBuilder::new().max_rect(content), |ui| {
                    ui.set_clip_rect(content.intersect(ui.clip_rect()));
                    ui.visuals_mut().panel_fill = background;
                    result = body(ui, command);
                });
            }
            ui.allocate_rect(bounds, egui::Sense::hover());
        });
    if !open {
        ctx.data_mut(|data| {
            data.insert_temp(
                egui::Id::new(if commands.editing {
                    "reference-confirm-close"
                } else {
                    "reference-closed"
                }),
                true,
            )
        });
    }
    if ctx.data(|data| {
        data.get_temp::<bool>(egui::Id::new("reference-confirm-close"))
            .unwrap_or(false)
    }) {
        egui::Modal::new(egui::Id::new("reference-discard-confirmation")).show(ctx, |ui| {
            ui.label("Discard the working copy?");
            ui.horizontal(|ui| {
                if ui.button("Yes").clicked() {
                    ctx.data_mut(|data| {
                        data.insert_temp(egui::Id::new("reference-discard"), true);
                        data.insert_temp(egui::Id::new("reference-close-after-discard"), true);
                        data.remove::<bool>(egui::Id::new("reference-confirm-close"));
                    });
                }
                if ui.button("No").clicked() {
                    ctx.data_mut(|data| {
                        data.remove::<bool>(egui::Id::new("reference-confirm-close"))
                    });
                }
            });
        });
    }
    result
}

/// Original list-title band using the shared dark trading caption colors.
pub fn header(ui: &mut egui::Ui, title: &str) {
    let (rect, _) =
        ui.allocate_exact_size(egui::vec2(ui.available_width(), 30.), egui::Sense::hover());
    ui.painter()
        .rect_filled(rect, 0., crate::presentation::BACKGROUND);
    ui.put(rect, crate::presentation::caption_widget(title));
}
