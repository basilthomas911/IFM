use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Run {
    pub id: String,
    pub intended_fire_time_utc: String,
    pub status: String,
    pub stage: String,
    pub detail: String,
    pub started_at_utc: Option<String>,
}
pub fn status_color(status: &str) -> egui::Color32 {
    match status {
        "Scheduled" | "Requested" | "Dispatched" => egui::Color32::from_rgb(30, 144, 255),
        "Running" => egui::Color32::YELLOW,
        "Succeeded" | "Successful" => egui::Color32::GREEN,
        "Failed" | "Uncertain" | "Rejected" => egui::Color32::RED,
        _ => egui::Color32::GRAY,
    }
}
pub struct ScheduledTasks {
    pub setup: bool,
    pub selected: Option<String>,
    pub selected_run: Option<String>,
    pub draft: BTreeMap<String, String>,
    pub pending: Option<String>,
    pub confirmation: Option<String>,
    pub reason: String,
    pub drawn_rows: usize,
    pub hour: u8,
    pub minute: u8,
    pub days: [bool; 7],
    preview_fields: BTreeMap<String, String>,
    target: (String, String),
    new: bool,
    last_action: String,
}
impl Default for ScheduledTasks {
    fn default() -> Self {
        Self {
            setup: false,
            selected: None,
            selected_run: None,
            draft: defaults(),
            pending: None,
            confirmation: None,
            reason: String::new(),
            drawn_rows: 0,
            hour: 17,
            minute: 1,
            days: [false, true, true, true, true, true, false],
            preview_fields: BTreeMap::new(),
            target: (String::new(), String::new()),
            new: false,
            last_action: String::new(),
        }
    }
}
fn defaults() -> BTreeMap<String, String> {
    BTreeMap::from(
        [
            ("environment", "Development"),
            ("hostId", "development"),
            ("taskKey", ""),
            ("name", ""),
            ("expression", "0 1 17 ? * MON-FRI"),
            ("timeZoneId", "America/New_York"),
            ("oneTime", "false"),
            ("startsAtUtc", ""),
            ("endsAtUtc", ""),
            ("maximumRuntimeSeconds", "1800"),
            ("dispatchToleranceSeconds", "60"),
            ("description", ""),
        ]
        .map(|(k, v)| (k.into(), v.into())),
    )
}
impl ScheduledTasks {
    pub fn apply_daily_time(&mut self) {
        let days = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"]
            .into_iter()
            .zip(self.days)
            .filter_map(|(day, on)| on.then_some(day))
            .collect::<Vec<_>>()
            .join(",");
        self.draft.insert(
            "expression".into(),
            format!("0 {} {} ? * {}", self.minute, self.hour, days),
        );
    }

    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        if action == "preview" {
            self.preview_fields = self.draft.clone();
        }
        let mut fields = self.draft.clone();
        fields.insert(
            "runId".into(),
            self.selected_run.clone().unwrap_or_default(),
        );
        fields.insert("reason".into(), self.reason.clone());
        serde_json::json!({"schema":1,"requestId":id,"view":"scheduled-tasks","intent":action,"expectedGeneration":s.generation,"originalId":self.selected,"fields":fields}).to_string()
    }
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let matching = s.result.view == "scheduled-tasks";
        if let Some(id) = &self.pending {
            if let Some(o) = s
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
            {
                if o.state == "Completed" && self.last_action == "load" {
                    self.target = (
                        self.draft["environment"].clone(),
                        self.draft["hostId"].clone(),
                    );
                    self.selected = None;
                    self.selected_run = None;
                }
                if o.state == "Completed"
                    && ["add", "change", "enable", "disable", "remove"]
                        .contains(&self.last_action.as_str())
                {
                    if let Some(row) = s
                        .result
                        .rows
                        .iter()
                        .find(|r| Some(&r.id) == self.selected.as_ref())
                    {
                        self.draft = row.fields.clone();
                    } else {
                        self.selected = None;
                    }
                    self.new = false;
                }
                self.pending = None;
            }
        }
        let idle = live && !s.busy && self.pending.is_none();
        let target_ok = matching
            && self.target
                == (
                    self.draft.get("environment").cloned().unwrap_or_default(),
                    self.draft.get("hostId").cloned().unwrap_or_default(),
                );
        let mut action = None;
        egui::CentralPanel::default().show(ctx,|ui| {
   ui.horizontal_wrapped(|ui| {
    for (key,label) in [("environment",strings::SCHEDULE_ENVIRONMENT),("hostId",strings::SCHEDULE_HOST)] {ui.trading_label(c.text(label));ui.add_enabled(idle,egui::TextEdit::singleline(self.draft.entry(key.into()).or_default()).desired_width(160.).id_salt(key));}
    if ui.add_enabled(idle,egui::Button::new(c.text(strings::SCHEDULE_REFRESH))).clicked(){action=Some("load");}
   });
   ui.trading_label(s.result.context.get("hostStatus").map(String::as_str).unwrap_or(&s.status));ui.trading_label(&s.status);
   if matching && !target_ok {ui.trading_label(c.text(strings::SCHEDULE_HOST_CHANGED));}
   ui.horizontal(|ui|{if crate::presentation::tab_label(ui,!self.setup,c.text(strings::SCHEDULE_LOGS)).clicked(){self.setup=false;}if crate::presentation::tab_label(ui,self.setup,c.text(strings::SCHEDULE_SETUP)).clicked(){self.setup=true;}});
   ui.separator();
   if self.setup {
    ui.horizontal_wrapped(|ui| {
     if ui.add_enabled(idle&&target_ok,egui::Button::new(c.text(strings::SCHEDULE_NEW))).clicked(){let env=self.draft["environment"].clone();let host=self.draft["hostId"].clone();self.draft=defaults();self.draft.insert("environment".into(),env);self.draft.insert("hostId".into(),host);self.selected=None;self.new=true;}
     for (intent,key,allowed) in [("preview",strings::SCHEDULE_PREVIEW,true),(if self.new {"add"} else {"change"},strings::SCHEDULE_SAVE,self.new||self.selected.is_some()),("enable",strings::SCHEDULE_ENABLE,self.selected.is_some()),("disable",strings::SCHEDULE_DISABLE,self.selected.is_some()),("remove",strings::SCHEDULE_REMOVE,self.selected.is_some()),("run",strings::SCHEDULE_RUN,self.selected.is_some())] {
      if ui.add_enabled(idle&&target_ok&&allowed,egui::Button::new(c.text(key))).clicked(){if ["remove","run"].contains(&intent){self.confirmation=Some(intent.into());}else{action=Some(intent);}}
     }
    });
    ui.trading_label(c.text(strings::SCHEDULE_SAVED_ACCEPTANCE));
    ui.horizontal_wrapped(|ui|{ui.trading_label(c.text(strings::SCHEDULE_DAILY_TIME));ui.add_enabled(idle,egui::DragValue::new(&mut self.hour).range(0..=23));ui.trading_label(":");ui.add_enabled(idle,egui::DragValue::new(&mut self.minute).range(0..=59));ui.trading_label(c.text(strings::SCHEDULE_DAYS));for(i,day)in ["SUN","MON","TUE","WED","THU","FRI","SAT"].iter().enumerate(){ui.add_enabled(idle,egui::Checkbox::new(&mut self.days[i],*day));}if ui.add_enabled(idle,egui::Button::new(c.text(strings::SCHEDULE_USE_TIME))).clicked(){self.apply_daily_time();}});

    egui::ScrollArea::vertical().id_salt("schedule-definitions").max_height(150.).show_rows(ui,24.,if matching{s.result.rows.len()}else{0},|ui,range|{
     self.drawn_rows=range.len();for i in range {let r=&s.result.rows[i];if ui.add_enabled(idle,egui::Button::new(format!("{} | {} | {}",r.fields.get("name").unwrap_or(&r.id),r.fields.get("expression").map(String::as_str).unwrap_or(""),r.fields.get("installationStatus").map(String::as_str).unwrap_or(""))).selected(self.selected.as_ref()==Some(&r.id))).clicked(){self.selected=Some(r.id.clone());self.draft=r.fields.clone();self.new=false;}}
    });
    egui::ScrollArea::vertical().id_salt("schedule-fields").show(ui,|ui| {egui::Grid::new("schedule-edit").num_columns(2).show(ui,|ui| {
     ui.trading_label(c.text(strings::SCHEDULE_PROJECT));let selected=self.draft.get("taskKey").cloned().unwrap_or_default();egui::ComboBox::from_id_salt("schedule-project").selected_text(&selected).show_ui(ui,|ui|{for v in s.result.choices.get("taskKey").into_iter().flatten(){if ui.add_enabled(idle&&self.new,egui::Button::new(v).selected(v==&selected)).clicked(){self.draft.insert("taskKey".into(),v.clone());}}});ui.end_row();
     for (key,label) in [("name",strings::SCHEDULE_NAME),("expression",strings::SCHEDULE_CRON),("timeZoneId",strings::SCHEDULE_ZONE),("startsAtUtc",strings::SCHEDULE_START),("endsAtUtc",strings::SCHEDULE_END),("maximumRuntimeSeconds",strings::SCHEDULE_RUNTIME),("dispatchToleranceSeconds",strings::SCHEDULE_TOLERANCE),("description",strings::SCHEDULE_DESCRIPTION)] {ui.trading_label(c.text(label));ui.add_enabled(idle,egui::TextEdit::singleline(self.draft.entry(key.into()).or_default()).desired_width(380.).id_salt(key));ui.end_row();}
     ui.trading_label(c.text(strings::SCHEDULE_ONE_TIME));let mut one=self.draft.get("oneTime").is_some_and(|v|v.eq_ignore_ascii_case("true"));if ui.add_enabled(idle,egui::Checkbox::without_text(&mut one)).changed(){self.draft.insert("oneTime".into(),one.to_string());}ui.end_row();
    });if let Some(v)=s.result.document::<serde_json::Value>("preview").filter(|_| self.preview_fields==self.draft){{ui.trading_label(c.text(if v["valid"].as_bool()==Some(true){strings::SCHEDULE_PREVIEW_VALID}else{strings::SCHEDULE_PREVIEW_INVALID}));ui.monospace(serde_json::to_string_pretty(&v).unwrap_or_default());}} });
   }else{
    ui.horizontal_wrapped(|ui| {if ui.add_enabled(idle&&target_ok&&self.selected.is_some(),egui::Button::new(c.text(strings::SCHEDULE_HISTORY))).clicked(){action=Some("history");}if ui.add_enabled(idle&&target_ok&&s.result.has_more,egui::Button::new(c.text(strings::SCHEDULE_MORE_HISTORY))).clicked(){action=Some("next");}if ui.add_enabled(idle&&target_ok&&self.selected_run.is_some()&&s.result.context.get("outputEnd").is_some_and(|v|v.eq_ignore_ascii_case("false")),egui::Button::new(c.text(strings::SCHEDULE_MORE_OUTPUT))).clicked(){action=Some("more-output");}});
    let runs:Vec<Run>=s.result.document("runs").unwrap_or_default();
    let histories:BTreeMap<String,Vec<Run>>=s.result.document("histories").unwrap_or_default();
    let fires:BTreeMap<String,String>=s.result.document("nextFires").unwrap_or_default();
    let content_size = ui.available_size();
    let tree_width = (content_size.x - 5.0) * crate::presentation::SCHEDULE_LOG_TREE_RATIO;
    ui.horizontal_top(|ui| {
     ui.allocate_ui_with_layout(egui::vec2(tree_width, content_size.y), egui::Layout::top_down(egui::Align::Min), |ui| {
     egui::ScrollArea::both().auto_shrink([false, false]).id_salt("schedule-tree").show(ui,|ui| {
      for row in s.result.rows.iter().filter(|_|matching){let name=row.fields.get("name").unwrap_or(&row.id);
                        let root_color={let latest=histories.get(&row.id).and_then(|r|r.last());let status=if row.fields.get("enabled").is_some_and(|v|v.eq_ignore_ascii_case("false")){"Unscheduled"}else{latest.map(|r|r.status.as_str()).unwrap_or("Scheduled")};status_color(status)};
                        egui::CollapsingHeader::new({
                            let cached=histories.get(&row.id);let latest=cached.and_then(|r|r.last());
                            let status=if row.fields.get("enabled").is_some_and(|v|v.eq_ignore_ascii_case("false")){"Unscheduled"}else{latest.map(|r|r.status.as_str()).unwrap_or("Scheduled")};
                            let text=format!("{} | {} {} | {} {}",name,c.text(strings::SCHEDULE_SCHEDULED),fires.get(&row.id).map(String::as_str).filter(|v|!v.is_empty()).unwrap_or("-"),c.text(strings::SCHEDULE_RAN),latest.and_then(|r|r.started_at_utc.as_deref()).unwrap_or("-"));
                            egui::RichText::new(text).color(status_color(status))
                        }).id_salt(&row.id).icon(move |ui,_open,response|{ui.painter().circle_filled(response.rect.center(),4.,root_color);}).show(ui,|ui| {
       if ui.add_enabled(idle&&target_ok,egui::Button::new(c.text(strings::SCHEDULE_HISTORY))).clicked(){self.selected=Some(row.id.clone());action=Some("history");}
       if histories.contains_key(&row.id) || s.result.context.get("historySchedule")==Some(&row.id){
        let mut groups:BTreeMap<String,BTreeMap<String,BTreeMap<String,Vec<&Run>>>>=BTreeMap::new();for r in histories.get(&row.id).unwrap_or(&runs) {if r.intended_fire_time_utc.len()>=10 {groups.entry(r.intended_fire_time_utc[..4].into()).or_default().entry(r.intended_fire_time_utc[5..7].into()).or_default().entry(r.intended_fire_time_utc[8..10].into()).or_default().push(r);}}
        for (year,months) in groups {egui::CollapsingHeader::new(&year).id_salt((&row.id,&year)).show(ui,|ui| {for (month,days) in months {egui::CollapsingHeader::new(&month).id_salt((&row.id,&year,&month)).show(ui,|ui|{for (day,items) in days {egui::CollapsingHeader::new(&day).id_salt((&row.id,&year,&month,&day)).show(ui,|ui| {for r in items {let label=format!("{} | {} {} | {} {} | {}",name,c.text(strings::SCHEDULE_SCHEDULED),r.intended_fire_time_utc,c.text(strings::SCHEDULE_RAN),r.started_at_utc.as_deref().unwrap_or("-"),r.status);ui.horizontal(|ui|{let (rect,_)=ui.allocate_exact_size(egui::vec2(10.,10.),egui::Sense::hover());ui.painter().circle_filled(rect.center(),4.,status_color(&r.status));if ui.add_enabled(idle&&target_ok,egui::Button::new(label).selected(self.selected_run.as_ref()==Some(&r.id))).clicked(){self.selected=Some(row.id.clone());self.selected_run=Some(r.id.clone());action=Some("output");}});}});}});}});}
       }
      });}if runs.is_empty(){ui.trading_label(c.text(strings::SCHEDULE_NO_RUNS));}
     });
     });
     ui.separator();
     ui.allocate_ui_with_layout(egui::vec2((content_size.x - tree_width - 5.0).max(0.0), content_size.y), egui::Layout::top_down(egui::Align::Min), |ui| {
     ui.heading(c.text(strings::SCHEDULE_OUTPUT));if s.result.context.get("outputRun")==self.selected_run.as_ref(){let lines=s.result.context.get("output").map(String::as_str).unwrap_or("").lines().collect::<Vec<_>>();egui::ScrollArea::both().id_salt("task-output").show_rows(ui,20.,lines.len(),|ui,range|{for i in range{ui.monospace(lines[i]);}});}else{ui.trading_label(c.text(strings::SCHEDULE_NO_OUTPUT));}
     });
    });
    if runs.iter().any(|r|Some(&r.id)==self.selected_run.as_ref()&&r.status=="Uncertain"){ui.horizontal(|ui|{ui.trading_label(c.text(strings::SCHEDULE_REASON));ui.text_edit_singleline(&mut self.reason);if ui.add_enabled(idle&&target_ok&&self.reason.trim().len()>=3,egui::Button::new(c.text(strings::SCHEDULE_RESOLVE))).clicked(){self.confirmation=Some("resolve".into());}});}
   }
  });
        if let Some(intent) = self.confirmation.clone() {
            egui::Window::new(c.text(strings::SCHEDULE_CONFIRM))
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label(&intent);
                    if ui
                        .add_enabled(idle, egui::Button::new(c.text(strings::SCHEDULE_CONFIRM)))
                        .clicked()
                    {
                        self.confirmation = None;
                        action = Some(match intent.as_str() {
                            "remove" => "remove",
                            "resolve" => "resolve",
                            _ => "run",
                        });
                    }
                    if ui.button(c.text(strings::SCHEDULE_CANCEL)).clicked() {
                        self.confirmation = None;
                    }
                });
        }
        action.map(|a| self.request(a, s))
    }
}
