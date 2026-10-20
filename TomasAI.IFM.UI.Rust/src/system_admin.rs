//! System Admin Manager mirrors the WinForms selector and modal client area using shared Rust colors.
use eframe::egui;
/// Draw the 1293 by 850 administration client or a standalone legacy test surface.
pub fn panel(ctx: &egui::Context, manager: bool, selected: usize, ready: bool, body: impl FnOnce(&mut egui::Ui)) {
    if !manager { egui::CentralPanel::default().show(ctx, body); return; }
    let size = egui::vec2(1293., 850.).min(ctx.screen_rect().size() - egui::vec2(24., 64.));
    #[cfg(target_os="windows")]
    let center=crate::control_parity::native_parent_center(ctx.pixels_per_point()).unwrap_or(ctx.screen_rect().center());
    #[cfg(not(target_os="windows"))]
    let center=ctx.screen_rect().center();
    let mut open = true;
    egui::Window::new("System Admin Manager").id(egui::Id::new("system-admin-manager"))
        .open(&mut open).collapsible(false).default_pos(center-size*0.5).default_size(size).min_size(egui::vec2(900., 550.).min(size))
        .frame(egui::Frame::window(&ctx.style()).fill(crate::presentation::BACKGROUND)
            .stroke(egui::Stroke::new(3.0_f32, crate::presentation::BORDER)).inner_margin(3))
        .show(ctx, |ui| {
            ctx.memory_mut(|memory| memory.set_modal_layer(ui.layer_id()));
            let bounds=ui.available_rect_before_wrap();
            let caption=ui.put(egui::Rect::from_min_size(bounds.min+egui::vec2(11.,10.),egui::vec2(172.,20.)),crate::presentation::caption_widget("System Admin Function:"));
            let selector=egui::Rect::from_min_size(bounds.min+egui::vec2(187.,7.),egui::vec2((bounds.width()-189.).max(100.),26.));
            ui.scope_builder(egui::UiBuilder::new().max_rect(selector),|ui|{
                ui.add_enabled_ui(ready,|ui|{
                    egui::ComboBox::from_id_salt("system-admin-selector").selected_text(["Backup Databases","Scheduled Tasks"][selected]).width(selector.width()).show_ui(ui,|ui|{
                        for (index,label) in ["Backup Databases","Scheduled Tasks"].iter().enumerate() {
                            if ui.selectable_label(index==selected,*label).clicked(){ctx.data_mut(|d|d.insert_temp(egui::Id::new("system-admin-selection"),index));ui.close();}
                        }
                    }).response.labelled_by(caption.id);
                });
            });
            let content=egui::Rect::from_min_max(bounds.min+egui::vec2(0.,37.),bounds.max);
            ctx.data_mut(|d|d.insert_temp(egui::Id::new("system-admin-body"),content));
            let mut child=ui.new_child(egui::UiBuilder::new().max_rect(content).layout(egui::Layout::top_down(egui::Align::Min)));
            child.set_clip_rect(content.intersect(ui.clip_rect()));body(&mut child);
            ui.allocate_rect(bounds,egui::Sense::hover());
        });
    if !open {ctx.data_mut(|d|d.insert_temp(egui::Id::new("system-admin-closed"),true));}
}

/// Draw within a fixed panel without allowing long text to resize the containing dialog.
pub fn region(ui:&mut egui::Ui,rect:egui::Rect,body:impl FnOnce(&mut egui::Ui)) {
    let mut child=ui.new_child(egui::UiBuilder::new().max_rect(rect).layout(egui::Layout::top_down(egui::Align::Min)));
    child.set_clip_rect(rect.intersect(ui.clip_rect()));body(&mut child);
}
/// Retain a user-draggable splitter with the original default proportion.
pub fn split(ui:&mut egui::Ui,rect:egui::Rect,key:&str,ratio:f32)->(egui::Rect,egui::Rect) {
    let id=ui.id().with(key);let mut fraction=ui.ctx().data_mut(|d|*d.get_temp_mut_or_insert_with(id,||ratio));
    let x=rect.left()+(rect.width()-5.)*fraction;
    let bar=egui::Rect::from_min_max(egui::pos2(x,rect.top()),egui::pos2(x+5.,rect.bottom()));
    let response=ui.interact(bar,id,egui::Sense::drag()).on_hover_cursor(egui::CursorIcon::ResizeHorizontal);
    if response.dragged(){fraction=(fraction+response.drag_delta().x/rect.width()).clamp(0.15,0.85);ui.ctx().data_mut(|d|d.insert_temp(id,fraction));}
    ui.painter().vline(x+2.,rect.y_range(),egui::Stroke::new(1.0_f32,crate::presentation::BORDER));
    (egui::Rect::from_min_max(rect.min,egui::pos2(x,rect.bottom())),egui::Rect::from_min_max(egui::pos2(x+5.,rect.top()),rect.max))
}
