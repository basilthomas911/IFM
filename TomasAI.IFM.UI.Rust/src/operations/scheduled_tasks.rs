//! Scheduled-task setup, daily-time editing, preview/confirmation, and run history. Draft schedule fields are sent to the managed scheduler; Rust does not create its own timer or execute jobs.

use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
/// Published scheduled-task execution status and timestamps.
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Run {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Scheduled run's intended firing time in UTC as published by the scheduler.
    pub intended_fire_time_utc: String,
    /// Display-ready status supplied by the managed projection or current view state.
    pub status: String,
    /// Current profiling or task-run stage.
    pub stage: String,
    /// Published explanation of this scheduler run status/stage.
    pub detail: String,
    /// Optional actual task start time in UTC.
    pub started_at_utc: Option<String>,
}
/// Map published status/severity text to its view-specific indicator color.
pub fn status_color(status: &str) -> egui::Color32 {
    match status {
        "Scheduled" | "Requested" | "Dispatched" => egui::Color32::from_rgb(30, 144, 255),
        "Running" => egui::Color32::YELLOW,
        "Succeeded" | "Successful" => egui::Color32::GREEN,
        "Failed" | "Uncertain" | "Rejected" => egui::Color32::RED,
        _ => egui::Color32::GRAY,
    }
}
/// Schedule editor, weekday/time draft, run selection, and preview/confirmation state.
pub struct ScheduledTasks {
    /// Whether the scheduled-task setup editor is open.
    pub setup: bool,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Stable identity of the selected task execution.
    pub selected_run: Option<String>,
    /// Unsaved local edit values retained across immediate-mode frames.
    pub draft: BTreeMap<String, String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Pending local confirmation action/dialog key.
    pub confirmation: Option<String>,
    /// Operator-entered justification accompanying an administrative action.
    pub reason: String,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Edited daily schedule hour.
    pub hour: u8,
    /// Edited daily schedule minute.
    pub minute: u8,
    /// Enabled days in Sunday-through-Saturday order, serialized to scheduler weekday names.
    pub days: [bool; 7],
    /// Field values captured for the current preview/confirmation.
    preview_fields: BTreeMap<String, String>,
    /// Captured schedule ID/version identity used when submitting the current preview or edit.
    target: (String, String),
    /// Whether the schedule draft represents a newly created definition.
    new: bool,
    /// Last submitted action key used to interpret completion and refresh behavior.
    last_action: String,
    /// Safe administrative failure shown in the persistent status line.
    last_error: String,
}
impl Default for ScheduledTasks {
    /// Construct the view's initial filters, selection, and draft settings without issuing backend commands.
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
            last_action: String::new(),last_error:String::new(),
        }
    }
}
/// Create the scheduler's initial editable field values without persisting a schedule.
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
    /// Copy the hour/minute/weekday controls into the scheduler draft's daily recurrence fields.
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

    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
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
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns schedule editor, weekday/time draft, run selection, and preview/confirmation state.
    pub fn draw(&mut self, ctx:&egui::Context, s:&ContractPublication, c:&Catalog, live:bool)->Option<String> {self.draw_impl(ctx,s,c,live,false)}
    /// Render inside the original System Admin Manager selector shell.
    pub fn draw_system(&mut self, ctx:&egui::Context, s:&ContractPublication, c:&Catalog, live:bool)->Option<String> {self.draw_impl(ctx,s,c,live,true)}
    fn draw_impl(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
        manager: bool,
    ) -> Option<String> {
        let matching = s.result.view == "scheduled-tasks";
        if let Some(id) = &self.pending {
            if let Some(o) = s
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
            {
                self.last_error=if o.state=="Completed" {String::new()}else{o.message.clone()};
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
        crate::system_admin::panel(ctx,manager,1,!s.busy&&self.pending.is_none(),|ui| {
   if !manager {ui.horizontal_wrapped(|ui| {
    for (key,label) in [("environment",strings::SCHEDULE_ENVIRONMENT),("hostId",strings::SCHEDULE_HOST)] {ui.trading_caption(c.text(label));ui.add_enabled(idle,egui::TextEdit::singleline(self.draft.entry(key.into()).or_default()).desired_width(160.).id_salt(key));}
    if ui.add_enabled(idle,egui::Button::new(c.text(strings::SCHEDULE_REFRESH))).clicked(){action=Some("load");}
   });
   ui.trading_label(s.result.context.get("hostStatus").map(String::as_str).unwrap_or(&s.status));ui.trading_label(&s.status);
   if matching && !target_ok {ui.trading_caption(c.text(strings::SCHEDULE_HOST_CHANGED));}
   }
   ui.horizontal(|ui|{if crate::presentation::tab_label(ui,!self.setup,c.text(strings::SCHEDULE_LOGS)).clicked(){self.setup=false;}if crate::presentation::tab_label(ui,self.setup,c.text(strings::SCHEDULE_SETUP)).clicked(){self.setup=true;}});
   ui.separator();
   if self.setup && manager {self.draw_setup_parity(ui,s,idle,&mut action);} else if !self.setup && manager {self.draw_logs_parity(ui,s,idle&&target_ok,&mut action);} else if self.setup {
    ui.horizontal_wrapped(|ui| {
     if ui.add_enabled(idle&&target_ok,egui::Button::new(c.text(strings::SCHEDULE_NEW))).clicked(){let env=self.draft["environment"].clone();let host=self.draft["hostId"].clone();self.draft=defaults();self.draft.insert("environment".into(),env);self.draft.insert("hostId".into(),host);self.selected=None;self.new=true;}
     for (intent,key,allowed) in [("preview",strings::SCHEDULE_PREVIEW,true),(if self.new {"add"} else {"change"},strings::SCHEDULE_SAVE,self.new||self.selected.is_some()),("enable",strings::SCHEDULE_ENABLE,self.selected.is_some()),("disable",strings::SCHEDULE_DISABLE,self.selected.is_some()),("remove",strings::SCHEDULE_REMOVE,self.selected.is_some()),("run",strings::SCHEDULE_RUN,self.selected.is_some())] {
      if ui.add_enabled(idle&&target_ok&&allowed,egui::Button::new(c.text(key))).clicked(){if ["remove","run"].contains(&intent){self.confirmation=Some(intent.into());}else{action=Some(intent);}}
     }
    });
    ui.trading_caption(c.text(strings::SCHEDULE_SAVED_ACCEPTANCE));
    ui.horizontal_wrapped(|ui|{ui.trading_caption(c.text(strings::SCHEDULE_DAILY_TIME));ui.add_enabled(idle,egui::DragValue::new(&mut self.hour).range(0..=23));ui.trading_caption(":");ui.add_enabled(idle,egui::DragValue::new(&mut self.minute).range(0..=59));ui.trading_caption(c.text(strings::SCHEDULE_DAYS));for(i,day)in ["SUN","MON","TUE","WED","THU","FRI","SAT"].iter().enumerate(){ui.add_enabled(idle,crate::presentation::checkbox(&mut self.days[i],*day));}if ui.add_enabled(idle,egui::Button::new(c.text(strings::SCHEDULE_USE_TIME))).clicked(){self.apply_daily_time();}});

    egui::ScrollArea::vertical().id_salt("schedule-definitions").max_height(150.).show_rows(ui,24.,if matching{s.result.rows.len()}else{0},|ui,range|{
     self.drawn_rows=range.len();for i in range {let r=&s.result.rows[i];if ui.add_enabled(idle,egui::Button::new(format!("{} | {} | {}",r.fields.get("name").unwrap_or(&r.id),r.fields.get("expression").map(String::as_str).unwrap_or(""),r.fields.get("installationStatus").map(String::as_str).unwrap_or(""))).selected(self.selected.as_ref()==Some(&r.id))).clicked(){self.selected=Some(r.id.clone());self.draft=r.fields.clone();self.new=false;}}
    });
    egui::ScrollArea::vertical().id_salt("schedule-fields").show(ui,|ui| {egui::Grid::new("schedule-edit").num_columns(2).show(ui,|ui| {
     ui.trading_caption(c.text(strings::SCHEDULE_PROJECT));let selected=self.draft.get("taskKey").cloned().unwrap_or_default();egui::ComboBox::from_id_salt("schedule-project").selected_text(&selected).show_ui(ui,|ui|{for v in s.result.choices.get("taskKey").into_iter().flatten(){if ui.add_enabled(idle&&self.new,egui::Button::new(v).selected(v==&selected)).clicked(){self.draft.insert("taskKey".into(),v.clone());}}});ui.end_row();
     for (key,label) in [("name",strings::SCHEDULE_NAME),("expression",strings::SCHEDULE_CRON),("timeZoneId",strings::SCHEDULE_ZONE),("startsAtUtc",strings::SCHEDULE_START),("endsAtUtc",strings::SCHEDULE_END),("maximumRuntimeSeconds",strings::SCHEDULE_RUNTIME),("dispatchToleranceSeconds",strings::SCHEDULE_TOLERANCE),("description",strings::SCHEDULE_DESCRIPTION)] {ui.trading_caption(c.text(label));ui.add_enabled(idle,egui::TextEdit::singleline(self.draft.entry(key.into()).or_default()).desired_width(380.).id_salt(key));ui.end_row();}
     ui.trading_caption(c.text(strings::SCHEDULE_ONE_TIME));let mut one=self.draft.get("oneTime").is_some_and(|v|v.eq_ignore_ascii_case("true"));if ui.add_enabled(idle,egui::Checkbox::without_text(&mut one)).changed(){self.draft.insert("oneTime".into(),one.to_string());}ui.end_row();
    });if let Some(v)=s.result.document::<serde_json::Value>("preview").filter(|_| self.preview_fields==self.draft){{ui.trading_caption(c.text(if v["valid"].as_bool()==Some(true){strings::SCHEDULE_PREVIEW_VALID}else{strings::SCHEDULE_PREVIEW_INVALID}));ui.monospace(serde_json::to_string_pretty(&v).unwrap_or_default());}} });
   }else{
    if manager {ui.horizontal_wrapped(|ui|{
        if ui.add_enabled(idle,egui::Button::new("Refresh logs")).clicked(){action=Some("load");}
        for(label,color)in[("Scheduled",egui::Color32::from_rgb(30,144,255)),("Unscheduled",egui::Color32::GRAY),("Running",egui::Color32::YELLOW),("Succeeded",egui::Color32::GREEN),("Failed/uncertain",egui::Color32::RED)]{ui.colored_label(color,format!("? {label}"));}
    });}
    if !manager {ui.horizontal_wrapped(|ui| {if ui.add_enabled(idle&&target_ok&&self.selected.is_some(),egui::Button::new(c.text(strings::SCHEDULE_HISTORY))).clicked(){action=Some("history");}if ui.add_enabled(idle&&target_ok&&s.result.has_more,egui::Button::new(c.text(strings::SCHEDULE_MORE_HISTORY))).clicked(){action=Some("next");}if ui.add_enabled(idle&&target_ok&&self.selected_run.is_some()&&s.result.context.get("outputEnd").is_some_and(|v|v.eq_ignore_ascii_case("false")),egui::Button::new(c.text(strings::SCHEDULE_MORE_OUTPUT))).clicked(){action=Some("more-output");}});
    }
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
       if !manager&&ui.add_enabled(idle&&target_ok,egui::Button::new(c.text(strings::SCHEDULE_HISTORY))).clicked(){self.selected=Some(row.id.clone());action=Some("history");}
       if histories.contains_key(&row.id) || s.result.context.get("historySchedule")==Some(&row.id){
        let mut groups:BTreeMap<String,BTreeMap<String,BTreeMap<String,Vec<&Run>>>>=BTreeMap::new();for r in histories.get(&row.id).unwrap_or(&runs) {if r.intended_fire_time_utc.len()>=10 {groups.entry(r.intended_fire_time_utc[..4].into()).or_default().entry(r.intended_fire_time_utc[5..7].into()).or_default().entry(r.intended_fire_time_utc[8..10].into()).or_default().push(r);}}
        for (year,months) in groups {egui::CollapsingHeader::new(&year).id_salt((&row.id,&year)).show(ui,|ui| {for (month,days) in months {egui::CollapsingHeader::new(&month).id_salt((&row.id,&year,&month)).show(ui,|ui|{for (day,items) in days {egui::CollapsingHeader::new(&day).id_salt((&row.id,&year,&month,&day)).show(ui,|ui| {for r in items {let label=format!("{} | {} {} | {} {} | {}",name,c.text(strings::SCHEDULE_SCHEDULED),r.intended_fire_time_utc,c.text(strings::SCHEDULE_RAN),r.started_at_utc.as_deref().unwrap_or("-"),r.status);ui.horizontal(|ui|{let (rect,_)=ui.allocate_exact_size(egui::vec2(10.,10.),egui::Sense::hover());ui.painter().circle_filled(rect.center(),4.,status_color(&r.status));if ui.add_enabled(idle&&target_ok,egui::Button::new(label).selected(self.selected_run.as_ref()==Some(&r.id))).clicked(){self.selected=Some(row.id.clone());self.draft=row.fields.clone();self.selected_run=Some(r.id.clone());action=Some("output");}});}});}});}});}
       }
      });}if runs.is_empty(){ui.trading_caption(c.text(strings::SCHEDULE_NO_RUNS));}
     });
     });
     ui.separator();
     ui.allocate_ui_with_layout(egui::vec2((content_size.x - tree_width - 5.0).max(0.0), content_size.y), egui::Layout::top_down(egui::Align::Min), |ui| {
     if !manager{ui.heading(c.text(strings::SCHEDULE_OUTPUT));}if s.result.context.get("outputRun")==self.selected_run.as_ref(){let lines=s.result.context.get("output").map(String::as_str).unwrap_or("").lines().collect::<Vec<_>>();egui::ScrollArea::both().id_salt("task-output").show_rows(ui,20.,lines.len(),|ui,range|{for i in range{ui.monospace(lines[i]);}});}else{ui.trading_caption(c.text(strings::SCHEDULE_NO_OUTPUT));}
     if manager&&ui.add_enabled(idle&&self.selected_run.is_some()&&s.result.context.get("outputEnd").is_some_and(|v|v.eq_ignore_ascii_case("false")),egui::Button::new("Load more output")).clicked(){action=Some("more-output");}
     });
    });
    if runs.iter().any(|r|Some(&r.id)==self.selected_run.as_ref()&&r.status=="Uncertain"){ui.horizontal(|ui|{ui.trading_caption(c.text(strings::SCHEDULE_REASON));ui.text_edit_singleline(&mut self.reason);if ui.add_enabled(idle&&target_ok&&self.reason.trim().len()>=3,egui::Button::new(c.text(strings::SCHEDULE_RESOLVE))).clicked(){self.confirmation=Some("resolve".into());}});}
   }
  });
        if let Some(intent) = self.confirmation.clone() {
            egui::Window::new(crate::presentation::dialog_title(c.text(strings::SCHEDULE_CONFIRM)))
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

#[path = "scheduled_tasks_parity.rs"]
mod parity;
