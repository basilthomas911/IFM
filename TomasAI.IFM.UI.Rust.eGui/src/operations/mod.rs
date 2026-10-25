//! Operational diagnostics and scheduled-task administration. Actions are sent through existing managed services and require the published eligibility/confirmation state.

/// Actor hierarchy, resource health, and operator actions. A confirmation captures generation and target identity so a changed selection cannot silently redirect an operation.
pub mod actor_health;
/// Operational health and dataset projections. Visible rows are rendered from the published query result; severity colors communicate backend state without recalculating it.
pub mod operations_health;
/// Scheduled-task setup, daily-time editing, preview/confirmation, and run history. Draft schedule fields are sent to the managed scheduler; Rust does not create its own timer or execute jobs.
pub mod scheduled_tasks;

/// Shared framing keeps health content reusable while desktop navigation opens
/// an independent, resizable dialog without consuming the trade's center pane.
fn health_surface(ctx:&eframe::egui::Context,title:&str,open:Option<&mut bool>,contents:impl FnOnce(&mut eframe::egui::Ui)) {
    use eframe::egui;
    if let Some(open)=open {
        let mut close=false;
        egui::Window::new(crate::presentation::dialog_title(title))
            .open(open).default_pos(ctx.screen_rect().center()-egui::vec2(500.,340.)).default_size(egui::vec2(1000.,680.)).min_size(egui::vec2(720.,420.))
            .resizable(true).scroll([false,true]).show(ctx,|ui|{
                contents(ui);ui.separator();close=ui.button("Close").clicked();
            });
        if close{*open=false;}
    }else{egui::CentralPanel::default().show(ctx,contents);}
}
