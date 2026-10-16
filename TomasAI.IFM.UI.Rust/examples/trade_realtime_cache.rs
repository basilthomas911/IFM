//! Isolated visual integration probe. Reads managed-generated publications; no broker or feed connections.
use ifm_ui_rust::{contract_editor::ContractPublication, trade_workspace::TradeWorkspace};
use std::time::{Duration, Instant};

struct Probe {
    view: TradeWorkspace,
    publications: Vec<ContractPublication>,
    started: Instant,
}
impl eframe::App for Probe {
    fn update(&mut self, ctx: &eframe::egui::Context, _: &mut eframe::Frame) {
        let index = ((self.started.elapsed().as_secs() / 3) as usize).min(self.publications.len() - 1);
        let publication = &mut self.publications[index];
        if let Some(request) = self.view.draw(ctx, publication) {
            let request: serde_json::Value = serde_json::from_str(&request).unwrap();
            publication.outcomes = serde_json::from_value(serde_json::json!([{
                "requestId":request["requestId"],"state":"Completed","message":"","generation":publication.generation
            }])).unwrap();
        }
        ctx.request_repaint_after(Duration::from_millis(100));
    }
}
fn main() -> eframe::Result {
    let file = std::env::args().nth(1).expect("Pass the managed-generated publication fixture JSON file");
    let mut publications: Vec<ContractPublication> = serde_json::from_slice(&std::fs::read(file).unwrap()).unwrap();
    assert!(!publications.is_empty());
    let mut view = TradeWorkspace::default();
    let request: serde_json::Value = serde_json::from_str(&view.request("load-trade", &publications[0])).unwrap();
    for publication in &mut publications {
        publication.outcomes = serde_json::from_value(serde_json::json!([{
            "requestId":request["requestId"],"state":"Completed","message":"","generation":publication.generation
        }])).unwrap();
    }
    eframe::run_native("IFM Realtime Cache Verification", eframe::NativeOptions {
        viewport: eframe::egui::ViewportBuilder::default().with_inner_size([1900., 1100.]),
        ..Default::default()
    }, Box::new(move |cc| {
        cc.egui_ctx.enable_accesskit();
        Ok(Box::new(Probe {view,publications,started:Instant::now()}))
    }))
}
