//! WinForms Setup layout: toolbar, definitions, flowing timing editor/preview, run grid/details, status.
use super::*;
use chrono::TimeZone;
/// Reserve a complete label/control pair before the surrounding flow layout decides where to wrap.
fn pair<R>(ui:&mut egui::Ui,label:&str,width:f32,height:f32,body:impl FnOnce(&mut egui::Ui)->R)->R {
    let label_width=ui.painter().layout_no_wrap(label.into(),egui::FontId::proportional(crate::presentation::FONT_SIZE),crate::presentation::CAPTION_FOREGROUND).size().x;
    ui.allocate_ui_with_layout(egui::vec2(label_width+width+8.,height),egui::Layout::left_to_right(egui::Align::Min),|ui|{
        ui.add(crate::presentation::caption_widget(label).wrap_mode(egui::TextWrapMode::Extend));body(ui)
    }).inner
}
fn field(ui:&mut egui::Ui,draft:&mut BTreeMap<String,String>,key:&str,label:&str,width:f32){
    pair(ui,label,width,30.,|ui|{let r=ui.add_sized(egui::vec2(width,24.),egui::TextEdit::singleline(draft.entry(key.into()).or_default()).id_salt(key));r.widget_info(||egui::WidgetInfo::labeled(egui::WidgetType::TextEdit,true,label));});
}
fn bound(ui:&mut egui::Ui,draft:&mut BTreeMap<String,String>,key:&str,label:&str){
    let zone=draft.get("timeZoneId").and_then(|z|z.parse::<chrono_tz::Tz>().ok()).unwrap_or(chrono_tz::America::New_York);
    let parsed=draft.get(key).and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok()).map(|d|d.with_timezone(&zone));
    let mut enabled=parsed.is_some();let local=parsed.unwrap_or_else(||chrono::Utc::now().with_timezone(&zone));let mut date=local.date_naive();let mut time=local.format("%H:%M:%S").to_string();
    pair(ui,label,210.,30.,|ui|{
        let mut changed=ui.checkbox(&mut enabled,"").changed();
        changed|=ui.add_enabled(enabled,egui_extras::DatePickerButton::new(&mut date).id_salt(key)).changed();
        changed|=ui.add_enabled(enabled,egui::TextEdit::singleline(&mut time).desired_width(65.).id_salt((key,"time"))).changed();
        if changed {if !enabled{draft.insert(key.into(),String::new());}else if let Ok(time)=chrono::NaiveTime::parse_from_str(&time,"%H:%M:%S") {
            if let Some(at)=zone.from_local_datetime(&date.and_time(time)).single(){draft.insert(key.into(),at.with_timezone(&chrono::Utc).to_rfc3339());}
        }}
    });
}
impl ScheduledTasks {
    /// Draw every original Setup control with the original row proportions and 68/32 editor split.
    pub(super) fn draw_setup_parity(&mut self,ui:&mut egui::Ui,s:&ContractPublication,idle:bool,action:&mut Option<&'static str>){
        if self.selected.is_none()&&!self.new {if let Some(row)=s.result.rows.first(){self.selected=Some(row.id.clone());self.draft=row.fields.clone();}}
        let bounds=ui.available_rect_before_wrap().shrink(6.);
        let toolbar=egui::Rect::from_min_size(bounds.min,egui::vec2(bounds.width(),38.));
        crate::system_admin::region(ui,toolbar,|ui|{ui.horizontal_wrapped(|ui|{
            let runs:Vec<serde_json::Value>=s.result.document("runs").unwrap_or_default();
            let uncertain=runs.iter().any(|r|r["id"].as_str()==self.selected_run.as_deref()&&r["status"]=="Uncertain");
            for(label,intent,enabled)in[("Refresh","load",true),("New","new",true),("Save",if self.new{"add"}else{"change"},self.new||self.selected.is_some()),("Enable","enable",self.selected.is_some()),("Disable","disable",self.selected.is_some()),("Remove","remove",self.selected.is_some()),("Run Now","run",self.selected.is_some()),("Preview","preview",true),("Resolve as Failed","resolve",uncertain)] {
                if ui.add_enabled(idle&&enabled&&(intent=="load" || s.result.view=="scheduled-tasks" && self.target==(self.draft.get("environment").cloned().unwrap_or_default(),self.draft.get("hostId").cloned().unwrap_or_default())),egui::Button::new(label)).clicked(){match intent {
                    "new"=>{let env=self.draft.get("environment").cloned().unwrap_or_default();let host=self.draft.get("hostId").cloned().unwrap_or_default();self.draft=defaults();self.draft.insert("environment".into(),env);self.draft.insert("hostId".into(),host);self.selected=None;self.new=true;},
                    _=>*action=Some(intent),
                }}
            }
        });});
        let height=(bounds.height()-38.-28.-220.).max(120.);let definitions_height=height*0.35;let runs_height=height*0.65;
        let definitions=egui::Rect::from_min_size(egui::pos2(bounds.left(),toolbar.bottom()),egui::vec2(bounds.width(),definitions_height));
        crate::system_admin::region(ui,definitions,|ui|{
            let columns=[("Name",295.),("Task",140.),("Enabled",85.),("Timing",140.),("Desired",82.),("Applied",82.),("Installation",100.),("Active run",96.)];
            let selected=s.result.rows.iter().position(|r|self.selected.as_ref()==Some(&r.id));
            let(picked,_)=crate::control_parity::report_list(ui,"system-schedule-definitions",&columns,s.result.rows.len(),selected,definitions.height(),|r,c|{
                s.result.rows[r].fields.get(["name","taskKey","enabled","expression","desiredRevision","appliedRevision","installationStatus","activeRunId"][c]).cloned().unwrap_or_default()
            });
            if idle{if let Some(index)=picked{let row=&s.result.rows[index];self.selected=Some(row.id.clone());self.draft=row.fields.clone();self.new=false;*action=Some("history");}}
        });
        let size=egui::vec2(bounds.width(),220.);
        let editor=egui::Rect::from_min_size(egui::pos2(bounds.left(),definitions.bottom()),size);
        let editor_left=egui::Rect::from_min_size(editor.min,egui::vec2(editor.width()*0.68-4.,220.));
        let editor_right=egui::Rect::from_min_max(egui::pos2(editor_left.right()+4.,editor.top()),editor.max);
        crate::system_admin::region(ui,editor_left,|ui|{
                egui::ScrollArea::vertical().id_salt("system-schedule-editor").auto_shrink([false,false]).show(ui,|ui|{
                    ui.add_enabled_ui(idle,|ui|{ui.horizontal_wrapped(|ui|{
                        pair(ui,"Task:",230.,30.,|ui|{let key=self.draft.get("taskKey").cloned().unwrap_or_default();let projects:Vec<serde_json::Value>=s.result.document("projects").unwrap_or_default();
                            ui.add_enabled_ui(self.new,|ui|{egui::ComboBox::from_id_salt("system-schedule-task").selected_text(projects.iter().find(|p|p["taskKey"]==key).and_then(|p|p["name"].as_str()).unwrap_or(&key)).width(230.).show_ui(ui,|ui|{
                                for project in projects {if let Some(key)=project["taskKey"].as_str(){let name=project["name"].as_str().unwrap_or(key);if ui.add_enabled(self.new,egui::Button::new(name)).clicked(){self.draft.insert("taskKey".into(),key.into());ui.close();}}}
                            });});
                        });
                        for(key,label,width)in[("name","Name:",230.),("environment","Environment:",110.),("hostId","Host:",160.),("expression","Cron:",230.),("timeZoneId","Time zone:",180.)]{field(ui,&mut self.draft,key,label,width);}
                        let mut once=self.draft.get("oneTime").is_some_and(|s|s.eq_ignore_ascii_case("true"));if ui.checkbox(&mut once,"One-time schedule").changed(){self.draft.insert("oneTime".into(),once.to_string());}
                        bound(ui,&mut self.draft,"startsAtUtc","Start (zone):");bound(ui,&mut self.draft,"endsAtUtc","End (zone):");
                        for(key,label,max,default)in[("maximumRuntimeSeconds","Runtime (seconds):",86400,1800),("dispatchToleranceSeconds","Dispatch tolerance (seconds):",3600,60)]{pair(ui,label,100.,30.,|ui|{let mut value=self.draft.get(key).and_then(|s|s.parse::<i32>().ok()).unwrap_or(default);if ui.add(egui::DragValue::new(&mut value).range(1..=max)).changed(){self.draft.insert(key.into(),value.to_string());}});}
                        pair(ui,"Daily time:",110.,30.,|ui|{ui.add(egui::DragValue::new(&mut self.hour).range(0..=23));ui.label(":");ui.add(egui::DragValue::new(&mut self.minute).range(0..=59));});
                        pair(ui,"Days:",110.,100.,|ui|{egui::ScrollArea::vertical().id_salt("system-schedule-days").max_height(100.).show(ui,|ui|{ui.vertical(|ui|{for(i,day)in["SUN","MON","TUE","WED","THU","FRI","SAT"].iter().enumerate(){ui.checkbox(&mut self.days[i],*day);}});});});
                        if ui.button("Set cron from days/time").clicked(){self.apply_daily_time();}
                    });});
                });
            });
        crate::system_admin::region(ui,editor_right,|ui|{
            let preview:serde_json::Value=s.result.document("preview").unwrap_or_default();let content=if preview["valid"]==true {preview["nextFireTimesUtc"].as_array().map(|times|times.iter().filter_map(|v|v.as_str()).map(|time|{
                let zone=self.draft.get("timeZoneId").and_then(|s|s.parse::<chrono_tz::Tz>().ok()).unwrap_or(chrono_tz::UTC);
                chrono::DateTime::parse_from_rfc3339(time).map(|at|format!("{} / {} UTC",at.with_timezone(&zone).format("%Y-%m-%d %H:%M:%S %:z"),at.format("%H:%M:%S"))).unwrap_or(time.into())
            }).collect::<Vec<_>>().join("\n")).unwrap_or_default()}else{preview["errors"].as_array().map(|v|v.iter().map(|s|s.as_str().unwrap_or_default()).collect::<Vec<_>>().join("\n")).unwrap_or_default()};
            ui.painter().rect_stroke(editor_right,0.,egui::Stroke::new(1.0_f32,crate::presentation::BORDER),egui::StrokeKind::Inside);
            egui::ScrollArea::both().id_salt("system-schedule-preview").auto_shrink([false,false]).show(ui,|ui|{ui.add(egui::Label::new(content).selectable(true).wrap_mode(egui::TextWrapMode::Extend));});
        });
        let run_rect=egui::Rect::from_min_size(egui::pos2(bounds.left(),editor.bottom()),egui::vec2(bounds.width(),runs_height));
        let (run_left,run_right)=crate::system_admin::split(ui,run_rect,"system-schedule-run-split",0.5);
        crate::system_admin::region(ui,run_left,|ui|{
            let runs:Vec<serde_json::Value>=s.result.document("runs").unwrap_or_default();
            if self.selected_run.is_none(){self.selected_run=runs.first().and_then(|r|r["id"].as_str()).map(str::to_owned);}
            let columns=[("Id",300.),("IntendedFireTimeUtc",220.),("Status",110.),("Stage",140.),("StartedAtUtc",220.),("FinishedAtUtc",220.),("ExitCode",70.),("ValueDate",100.),("ProcessId",80.)];
            let selected=runs.iter().position(|r|r["id"].as_str()==self.selected_run.as_deref());
            let(picked,_)=crate::control_parity::report_list(ui,"system-schedule-run-grid",&columns,runs.len(),selected,run_left.height(),|r,c|{
                let value=&runs[r][["id","intendedFireTimeUtc","status","stage","startedAtUtc","finishedAtUtc","exitCode","valueDate","processId"][c]];
                value.as_str().map(str::to_owned).unwrap_or_else(||if value.is_null(){String::new()}else{value.to_string()})
            });
            if let Some(index)=picked{self.selected_run=runs[index]["id"].as_str().map(str::to_owned);}
        });
        crate::system_admin::region(ui,run_right,|ui|{
            let runs:Vec<serde_json::Value>=s.result.document("runs").unwrap_or_default();
                let details=runs.iter().find(|v|v["id"].as_str()==self.selected_run.as_deref()).map(run_details).unwrap_or_else(||"Select a run to view its outcome and output.".into());
                let detail_rect=egui::Rect::from_min_max(run_right.min,egui::pos2(run_right.right(),run_right.bottom()-38.));
                crate::system_admin::region(ui,detail_rect,|ui|{egui::ScrollArea::both().auto_shrink([false,false]).id_salt("system-run-details").show(ui,|ui|{ui.add(egui::Label::new(details).selectable(true).wrap_mode(egui::TextWrapMode::Extend));});});
                crate::system_admin::region(ui,egui::Rect::from_min_max(egui::pos2(run_right.left(),run_right.bottom()-38.),run_right.max),|ui|{
                ui.horizontal(|ui|{let label=ui.trading_caption("Review reason:");let response=ui.add(egui::TextEdit::singleline(&mut self.reason).desired_width(300.));response.widget_info(||egui::WidgetInfo::labeled(egui::WidgetType::TextEdit,true,"Review reason:"));response.labelled_by(label.id);});
                });
            });
        crate::system_admin::region(ui,egui::Rect::from_min_size(egui::pos2(bounds.left(),run_rect.bottom()),egui::vec2(bounds.width(),28.)),|ui|{if self.last_error.is_empty(){let host=s.result.context.get("hostStatus").map(String::as_str).unwrap_or(&s.status);let detail=s.result.rows.iter().find(|r|Some(&r.id)==self.selected.as_ref()).map(|r|format!(" | {}: {}",r.fields.get("installationStatus").map(String::as_str).unwrap_or_default(),r.fields.get("installationDetail").map(String::as_str).unwrap_or_default())).unwrap_or_default();ui.trading_label(format!("{host}{detail}"));}else{ui.colored_label(egui::Color32::RED,&self.last_error);}});
    }
}

fn run_details(run:&serde_json::Value)->String {
    let get=|key:&str|run[key].as_str().map(str::to_owned).unwrap_or_else(||if run[key].is_null(){String::new()}else{run[key].to_string()});
    format!("Run: {}\nStatus: {}\nStage: {}\nValue date: {}\nStarted: {}\nFinished: {}\nExit code: {}\nOperation: {}\nProcess: {}\nOutput artifact: {}\n\n{}\n\nStandard output (tail):\n{}\n\nStandard error (tail):\n{}",get("id"),get("status"),get("stage"),get("valueDate"),get("startedAtUtc"),get("finishedAtUtc"),get("exitCode"),get("operationCommandId"),get("processId"),get("outputDirectory"),get("detail"),get("standardOutputTail"),get("standardErrorTail"))
}
impl ScheduledTasks {
    /// Draw the original dated logs tree, semantic legend, output pane and continuation control.
    pub(super) fn draw_logs_parity(&mut self,ui:&mut egui::Ui,s:&ContractPublication,idle:bool,action:&mut Option<&'static str>) {
        let bounds=ui.available_rect_before_wrap().shrink(6.);
        crate::system_admin::region(ui,egui::Rect::from_min_size(bounds.min,egui::vec2(bounds.width(),38.)),|ui|{ui.horizontal(|ui|{
            if ui.add_enabled(idle,egui::Button::new("Refresh logs")).clicked(){*action=Some("load");}
            for(label,color)in[("Scheduled",egui::Color32::from_rgb(30,144,255)),("Unscheduled",egui::Color32::GRAY),("Running",egui::Color32::YELLOW),("Succeeded",egui::Color32::GREEN),("Failed/uncertain",egui::Color32::RED)]{ui.colored_label(color,format!("? {label}"));}
        });});
        let content=egui::Rect::from_min_max(bounds.min+egui::vec2(0.,38.),bounds.max);
        let (tree,output)=crate::system_admin::split(ui,content,"system-schedule-log-split",crate::presentation::SCHEDULE_LOG_TREE_RATIO);
        let histories:BTreeMap<String,Vec<Run>>=s.result.document("histories").unwrap_or_default();
        let fires:BTreeMap<String,String>=s.result.document("nextFires").unwrap_or_default();
        let more:BTreeMap<String,bool>=s.result.document("historyMore").unwrap_or_default();
        crate::system_admin::region(ui,tree,|ui|{egui::ScrollArea::both().auto_shrink([false,false]).id_salt("system-schedule-log-tree").show(ui,|ui|{
            for row in &s.result.rows {
                let name=row.fields.get("name").unwrap_or(&row.id);
                let latest=histories.get(&row.id).and_then(|r|r.iter().max_by_key(|r|&r.intended_fire_time_utc));
                let state=if row.fields.get("enabled").is_some_and(|v|v.eq_ignore_ascii_case("false")){"Unscheduled"}else{latest.map(|r|r.status.as_str()).unwrap_or("Scheduled")};let color=status_color(state);
                let title=format!("{} | scheduled: {} | ran: {}",name,fires.get(&row.id).map(String::as_str).unwrap_or("-"),latest.and_then(|r|r.started_at_utc.as_deref()).unwrap_or("-"));
                egui::CollapsingHeader::new(title).id_salt((&row.id,"system-logs")).default_open(true).icon(move|ui,_,response|{ui.painter().circle_filled(response.rect.center(),4.,color);}).show(ui,|ui|{
                    let mut groups:BTreeMap<String,BTreeMap<String,BTreeMap<String,Vec<&Run>>>>=BTreeMap::new();
                    for run in histories.get(&row.id).into_iter().flatten(){let t=&run.intended_fire_time_utc;if t.len()>=10{groups.entry(t[..4].into()).or_default().entry(t[5..7].into()).or_default().entry(t[8..10].into()).or_default().push(run);}}
                    for(year,months)in groups.iter().rev(){egui::CollapsingHeader::new(year).id_salt((&row.id,year)).show(ui,|ui|{
                        for(month,days)in months.iter().rev(){egui::CollapsingHeader::new(month).id_salt((&row.id,year,month)).show(ui,|ui|{
                            for(day,runs)in days.iter().rev(){egui::CollapsingHeader::new(day).id_salt((&row.id,year,month,day)).show(ui,|ui|{
                                for run in runs {let color=status_color(&run.status);ui.horizontal(|ui|{let(rect,_)=ui.allocate_exact_size(egui::vec2(10.,10.),egui::Sense::hover());ui.painter().circle_filled(rect.center(),4.,color);
                                    if ui.add_enabled(idle,egui::Button::new(format!("{} | scheduled: {} | ran: {} | {}",name,run.intended_fire_time_utc,run.started_at_utc.as_deref().unwrap_or("-"),run.status)).selected(self.selected_run.as_ref()==Some(&run.id))).clicked(){self.selected=Some(row.id.clone());self.draft=row.fields.clone();self.selected_run=Some(run.id.clone());*action=Some("output");}
                                });}
                            });}
                        });}
                    });}
                    if more.get(&row.id)==Some(&true)&&ui.add_enabled(idle,egui::Button::new("Load older runs")).clicked(){self.selected=Some(row.id.clone());self.draft=row.fields.clone();*action=Some("next");}
                });
            }
        });});
        let output_text=s.result.context.get("output").cloned().unwrap_or_default();
        let text_rect=egui::Rect::from_min_max(output.min,egui::pos2(output.right(),output.bottom()-42.));
        crate::system_admin::region(ui,text_rect,|ui|{egui::ScrollArea::both().auto_shrink([false,false]).id_salt("system-schedule-stdout").show(ui,|ui|{ui.add(egui::Label::new(output_text).selectable(true).wrap_mode(egui::TextWrapMode::Extend));});});
        crate::system_admin::region(ui,egui::Rect::from_min_max(egui::pos2(output.left(),output.bottom()-42.),output.max),|ui|{ui.horizontal(|ui|{
            if ui.add_enabled(idle&&self.selected_run.is_some()&&s.result.context.get("outputEnd").is_some_and(|s|s.eq_ignore_ascii_case("false")),egui::Button::new("Load more output")).clicked(){*action=Some("more-output");}
            ui.trading_label(if self.selected_run.is_some(){s.status.as_str()}else{"Select an occurrence to read retained standard output."});
        });});
    }
}
