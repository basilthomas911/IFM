use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
#[derive(Default)]
pub struct OperationsHealth {
    pub datasets: bool,
    pub selected: Option<String>,
    pub pending: Option<String>,
    pub drawn_rows: usize,
}
pub fn status_color(status: &str) -> egui::Color32 {
    match status.to_uppercase().as_str() {
        "GREEN" | "UP" | "HEALTHY" => egui::Color32::LIGHT_GREEN,
        "YELLOW" | "SUSPECT" => egui::Color32::from_rgb(240, 230, 140),
        "ORANGE" | "RECOVERING" | "RESETTING" | "QUALIFYING" => {
            egui::Color32::from_rgb(255, 165, 0)
        }
        "RED" | "DOWN" | "FAILED" => egui::Color32::from_rgb(250, 128, 114),
        _ => egui::Color32::GRAY,
    }
}
impl OperationsHealth {
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        if self.pending.as_ref().is_some_and(|id| {
            s.outcomes
                .iter()
                .any(|o| &o.request_id == id && o.state != "Pending")
        }) {
            self.pending = None;
        }
        let matching = s.result.view == "operations-health";
        let mut intent = None;
        egui::CentralPanel::default().show(ctx,|ui|{
   if ui.add_enabled(live&&!s.busy&&self.pending.is_none(),egui::Button::new(c.text(strings::HEALTH_REFRESH))).clicked(){let id=new_request_id();self.pending=Some(id.clone());intent=Some(serde_json::json!({"schema":1,"requestId":id,"view":"operations-health","intent":"load","expectedGeneration":s.generation,"originalId":null,"fields":{}}).to_string());}
   if matching {ui.trading_colored_label(status_color(s.result.context.get("status").map(String::as_str).unwrap_or("Unknown")),s.result.context.get("summary").map(String::as_str).unwrap_or(""));ui.trading_label(s.result.context.get("observation").map(String::as_str).unwrap_or(""));}else{ui.trading_label(&s.status);}
   ui.trading_label(c.text(strings::HEALTH_EXPLANATION));ui.horizontal(|ui|{if crate::presentation::tab_label(ui,!self.datasets,c.text(strings::HEALTH_STAGES)).clicked(){self.datasets=false;self.selected=None;}if crate::presentation::tab_label(ui,self.datasets,c.text(strings::HEALTH_DATASETS)).clicked(){self.datasets=true;self.selected=None;}});
   let kind=if self.datasets{"dataset"}else{"stage"};let rows=s.result.rows.iter().filter(|r|matching&&r.fields.get("kind").is_some_and(|k|k==kind)).collect::<Vec<_>>();
   egui::ScrollArea::vertical().max_height(250.).id_salt("health-rows").show_rows(ui,24.,rows.len(),|ui,range|{self.drawn_rows=range.len();for i in range{let row=rows[i];let name=row.fields.get(if self.datasets{"Dataset"}else{"Stage"}).unwrap_or(&row.id);let status=row.fields.get("Status").map(String::as_str).unwrap_or("Unknown");if ui.add(egui::Button::new(egui::RichText::new(format!("{} | {}",name,status)).color(status_color(status))).selected(self.selected.as_ref()==Some(&row.id))).clicked(){self.selected=Some(row.id.clone());}}});
   if let Some(row)=rows.iter().find(|r|Some(&r.id)==self.selected.as_ref()){egui::ScrollArea::both().id_salt("health-properties").show(ui,|ui|{egui::Grid::new("health-grid").num_columns(2).striped(true).show(ui,|ui|{for(key,value)in &row.fields{if key!="kind"{ui.trading_label(c.text(&format!("health_{}",key)));ui.trading_label(value);ui.end_row();}}});});}
  });
        intent
    }
}
