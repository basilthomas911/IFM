//! Backup Databases Logs/Setup parity. Every operation is submitted to the original managed service.
use crate::models::backup::*;
use crate::presentation::TradingLabelUi as _;
use crate::{contract_editor::ContractPublication, lookup_editor::new_request_id};
use eframe::egui;
use std::collections::{BTreeMap, BTreeSet};
use std::sync::{Arc, Weak};
/// Local selection and source-specific drafts; no backup execution or policy enforcement lives here.
pub struct DatabaseBackup {
    pub setup: bool,
    pub source: String,
    pub pending: Option<String>,
    drafts: BTreeMap<String, BTreeMap<String, String>>,
    checked: BTreeMap<String, BTreeSet<String>>,
    selected_set: String,
    operation: String,
    operation_source: String,
    history_source: String,
    policy_identity: String,
    loaded: bool,
    error: String,
    cache: Option<(u64, Weak<()>, Arc<BackupDocuments>)>,
}
impl Default for DatabaseBackup {
    fn default() -> Self {
        Self {
            setup: false,
            source: "LocalWorkstation".into(),
            pending: None,
            drafts: BTreeMap::new(),
            checked: BTreeMap::new(),
            selected_set: String::new(),
            operation: String::new(),
            operation_source: String::new(),
            history_source: String::new(),
            policy_identity: String::new(),
            loaded: false,
            error: String::new(),
            cache: None,
        }
    }
}
/// Defaults are created before the first load request as well as before the first rendered editor frame.
fn default_fields(source: &str) -> BTreeMap<String, String> {
    let local = source == "LocalWorkstation";
    BTreeMap::from([
        ("mode".into(), "Full".into()),
        (
            "targetProfile".into(),
            if local { "development-on-demand" } else { "" }.into(),
        ),
        (
            "validationProfile".into(),
            if local { "development-restore" } else { "" }.into(),
        ),
    ])
}
fn text(v: &(impl TextFields + ?Sized), key: &str) -> String {
    v.display(key)
}
#[derive(Default)]
struct BackupDocuments {
    dashboard: Dashboard,
    history: BTreeMap<String, Vec<Operation>>,
    cursors: BTreeMap<String, String>,
    policy: Option<Policy>,
    editor: PolicyEditor,
    setup: Setup,
    health: Vec<Health>,
}
fn edit(
    ui: &mut egui::Ui,
    fields: &mut BTreeMap<String, String>,
    key: &str,
    label: &str,
    width: f32,
) {
    ui.trading_caption(label);
    let r = ui.add(
        egui::TextEdit::singleline(fields.entry(key.into()).or_default())
            .desired_width(width)
            .id_salt(key),
    );
    r.widget_info(|| egui::WidgetInfo::labeled(egui::WidgetType::TextEdit, true, label));
}
fn status(outcome: &str) -> egui::Color32 {
    match outcome {
        "Succeeded" => egui::Color32::GREEN,
        "Failed" | "Cancelled" | "Uncertain" => egui::Color32::RED,
        "None" => egui::Color32::YELLOW,
        _ => egui::Color32::GRAY,
    }
}
impl DatabaseBackup {
    fn documents(&mut self, s: &ContractPublication) -> Arc<BackupDocuments> {
        if let Some((generation, token, documents)) = &self.cache {
            if *generation == s.generation
                && token
                    .upgrade()
                    .is_some_and(|token| Arc::ptr_eq(&token, &s.render_token))
            {
                return documents.clone();
            }
        }
        let documents = Arc::new(BackupDocuments {
            dashboard: s.result.document("dashboard").unwrap_or_default(),
            history: s.result.document("history").unwrap_or_default(),
            cursors: s.result.document("continuations").unwrap_or_default(),
            policy: s.result.document::<Option<Policy>>("policy").flatten(),
            editor: s.result.document("policyEditor").unwrap_or_default(),
            setup: s.result.document("setup").unwrap_or_default(),
            health: s.result.document("health").unwrap_or_default(),
        });
        self.cache = Some((
            s.generation,
            Arc::downgrade(&s.render_token),
            documents.clone(),
        ));
        documents
    }
    /// Creates one correlated intent from the source-specific unsaved editor.
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        if action == "load" {
            self.loaded = true;
        }
        self.pending = Some(id.clone());
        let mut fields = self
            .drafts
            .entry(self.source.clone())
            .or_insert_with(|| default_fields(&self.source))
            .clone();
        fields.insert("historySource".into(), self.history_source.clone());
        fields.insert("source".into(), self.source.clone());
        fields.insert("operationId".into(), self.operation.clone());
        fields.insert("operationSource".into(), self.operation_source.clone());
        fields.insert("protectionSet".into(), self.selected_set.clone());
        fields.insert(
            "protectionSets".into(),
            self.checked
                .entry(self.source.clone())
                .or_default()
                .iter()
                .cloned()
                .collect::<Vec<_>>()
                .join("|"),
        );
        serde_json::json!({"schema":1,"requestId":id,"view":"database-backup","intent":action,"expectedGeneration":s.generation,"fields":fields}).to_string()
    }
    /// Loads the selected source on entry; reopening never submits a backup automatically.
    pub fn reopen(&mut self) {
        self.loaded = false;
    }
    /// Renders original controls using shared Rust captions, black grids and semantic status colors.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        live: bool,
    ) -> Option<String> {
        if let Some(id) = &self.pending {
            if let Some(outcome) = s
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
            {
                self.error = if outcome.state == "Completed" {
                    String::new()
                } else {
                    outcome.message.clone()
                };
                self.pending = None;
            }
        }
        if !self.loaded && live && !s.busy && self.pending.is_none() {
            self.loaded = true;
            return Some(self.request("load", s));
        }
        let matching = s.result.view == "database-backup";
        let idle = live && !s.busy && self.pending.is_none();
        let documents = self.documents(s);
        let history = &documents.history;
        let cursors = &documents.cursors;
        let policy = &documents.policy;
        let identity = format!(
            "{}:{}:{}",
            self.source,
            text(&policy, "id"),
            text(&policy, "revision")
        );
        let fields = self
            .drafts
            .entry(self.source.clone())
            .or_insert_with(|| default_fields(&self.source));
        if policy.is_some()
            && identity != self.policy_identity
            && s.result.context.get("source") == Some(&self.source)
        {
            for (key, property) in [
                ("policyId", "id"),
                ("daily", "daily"),
                ("weekly", "weekly"),
                ("monthly", "monthly"),
            ] {
                fields.insert(key.into(), text(&policy, property));
            }
            for (key, property, divisor) in [
                ("rpoMinutes", "rpo", 60.),
                ("rtoMinutes", "rto", 60.),
                ("maximumVerificationHours", "maximumVerificationAge", 3600.),
            ] {
                let _ = (property, divisor);
                let value = documents.editor.string(key);
                if !value.is_empty() {
                    fields.insert(key.into(), value.into());
                }
            }
            self.policy_identity = identity;
        }
        let mut action = None;
        crate::system_admin::panel(ctx, true, 0, !s.busy && self.pending.is_none(), |ui| {
            ui.horizontal(|ui| {
                for (label, on) in [("Logs", false), ("Setup", true)] {
                    if crate::presentation::tab_label(ui, self.setup == on, label).clicked() {
                        self.setup = on;
                    }
                }
            });
            ui.separator();
            if !self.setup {
                self.draw_logs(ui, s, &history, &cursors, idle, &mut action);
            } else {
                self.draw_setup(ui, s, &documents, idle && matching, &mut action);
            }
        });
        action.map(|action| self.request(action, s))
    }
}

impl DatabaseBackup {
    /// Fixed original Setup panels: source tabs, 267-pixel set/host pane and the five-row editor.
    fn draw_setup(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        documents: &BackupDocuments,
        idle: bool,
        action: &mut Option<&'static str>,
    ) {
        let dashboard = &documents.dashboard;
        let policy = &documents.policy;
        ui.horizontal(|ui| {
            for (source, label) in [
                ("AwsCloud", "AWS Backup"),
                ("LocalWorkstation", "Local Workstation"),
            ] {
                if ui
                    .add_enabled(
                        idle,
                        egui::Button::new(label).selected(self.source == source),
                    )
                    .clicked()
                {
                    self.source = source.into();
                    self.selected_set.clear();
                    *action = Some("backup-source");
                }
            }
        });
        let bounds = ui.available_rect_before_wrap();
        let (left, right) =
            crate::system_admin::split(ui, bounds, "backup-setup-split", 267. / bounds.width());
        if self.selected_set.is_empty() {
            self.selected_set = dashboard
                .protection_sets
                .first()
                .map(|set| text(set, "id"))
                .unwrap_or_default();
        }
        let host_rect =
            egui::Rect::from_min_max(egui::pos2(left.left(), left.bottom() - 150.), left.max);
        let sets_rect =
            egui::Rect::from_min_max(left.min, egui::pos2(left.right(), host_rect.top() - 4.));
        crate::system_admin::region(ui, sets_rect, |ui| {
            ui.painter().rect_stroke(
                sets_rect,
                0.,
                egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                egui::StrokeKind::Inside,
            );
            egui::ScrollArea::both()
                .id_salt("backup-protection-sets")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    for set in &dashboard.protection_sets {
                        let id = text(set, "id");
                        let mut checked = self
                            .checked
                            .entry(self.source.clone())
                            .or_default()
                            .contains(&id);
                        if ui
                            .add_enabled(
                                idle && set.enabled == Some(true),
                                crate::presentation::checkbox(&mut checked, &id),
                            )
                            .changed()
                        {
                            if checked {
                                self.checked
                                    .entry(self.source.clone())
                                    .or_default()
                                    .insert(id.clone());
                            } else {
                                self.checked
                                    .entry(self.source.clone())
                                    .or_default()
                                    .remove(&id);
                            }
                            self.selected_set = id;
                            *action = Some("backup-select");
                        }
                    }
                });
        });
        crate::system_admin::region(ui, host_rect, |ui| {
            ui.painter().rect_stroke(
                host_rect,
                0.,
                egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                egui::StrokeKind::Inside,
            );
            let host = documents
                .setup
                .backup_host_settings
                .iter()
                .map(|(key, value)| format!("{key}: {value}"))
                .collect::<Vec<_>>()
                .join("\n");
            egui::ScrollArea::both()
                .id_salt("backup-host-settings")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    ui.add(
                        egui::Label::new(host)
                            .selectable(true)
                            .wrap_mode(egui::TextWrapMode::Extend),
                    );
                });
        });
        let fields = self
            .drafts
            .entry(self.source.clone())
            .or_insert_with(|| default_fields(&self.source));
        let command = egui::Rect::from_min_size(right.min, egui::vec2(right.width(), 52.));
        crate::system_admin::region(ui, command, |ui| {
            ui.horizontal(|ui| {
                ui.trading_caption("Backup mode:");
                ui.add_enabled_ui(idle, |ui| {
                    egui::ComboBox::from_id_salt("backup-mode")
                        .selected_text(fields.get("mode").map(String::as_str).unwrap_or("Full"))
                        .width(145.)
                        .show_ui(ui, |ui| {
                            for v in ["Full", "Automatic", "Incremental"] {
                                ui.selectable_value(
                                    fields.entry("mode".into()).or_default(),
                                    v.into(),
                                    v,
                                );
                            }
                        });
                });
            });
            let run = egui::Rect::from_min_max(
                egui::pos2(command.right() - 160., command.top()),
                command.max,
            );
            let checked = self
                .checked
                .get(&self.source)
                .is_some_and(|s| !s.is_empty());
            ui.add_enabled_ui(idle && checked, |ui| {
                if ui.put(run, egui::Button::new("Request Backup")).clicked() {
                    *action = Some("backup-run");
                }
            });
        });
        let tools = egui::Rect::from_min_size(
            egui::pos2(right.left(), command.bottom()),
            egui::vec2(right.width(), 40.),
        );
        crate::system_admin::region(ui, tools, |ui| {
            ui.horizontal(|ui| {
                ui.add_enabled_ui(idle, |ui| {
                    let r = ui.add(
                        egui::TextEdit::singleline(fields.entry("policyId".into()).or_default())
                            .hint_text("Policy identifier")
                            .desired_width(220.),
                    );
                    r.widget_info(|| {
                        egui::WidgetInfo::labeled(
                            egui::WidgetType::TextEdit,
                            true,
                            "Policy identifier",
                        )
                    });
                    if ui.button("Load policy").clicked() {
                        *action = Some("backup-policy-load");
                    }
                    if ui
                        .add_enabled(policy.is_some(), egui::Button::new("Save policy"))
                        .clicked()
                    {
                        *action = Some("backup-policy-save");
                    }
                });
                let health = &documents.health;
                ui.trading_label(if health.is_empty() {
                    "No host readiness observation".into()
                } else {
                    health
                        .iter()
                        .map(|h| {
                            format!(
                                "{}: {}, ready={}",
                                text(h, "hostId"),
                                text(h, "capability"),
                                text(h, "ready")
                            )
                        })
                        .collect::<Vec<_>>()
                        .join("; ")
                });
            });
        });
        let remaining = (right.height() - 52. - 40. - 110.).max(60.);
        let property = egui::Rect::from_min_size(
            egui::pos2(right.left(), tools.bottom()),
            egui::vec2(right.width(), remaining * 0.65),
        );
        crate::system_admin::region(ui, property, |ui| {
            ui.painter().rect_stroke(
                property,
                0.,
                egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                egui::StrokeKind::Inside,
            );
            egui::ScrollArea::both()
                .id_salt("backup-policy-grid")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    if policy.is_some() {
                        ui.add_enabled_ui(idle, |ui| {
                            egui::Grid::new("backup-policy-properties").show(ui, |ui| {
                                for (label, key) in [
                                    ("Policy", "id"),
                                    ("AcceptedRevision", "revision"),
                                    ("Applied", "enforced"),
                                    ("EnabledSources", "enabledSources"),
                                    ("ProtectedSets", "protectionSets"),
                                    ("Verification", "verificationLevels"),
                                ] {
                                    ui.trading_caption(label);
                                    ui.trading_label(text(policy, key));
                                    ui.end_row();
                                }
                                for (key, label) in [
                                    ("daily", "Daily"),
                                    ("weekly", "Weekly"),
                                    ("monthly", "Monthly"),
                                    ("rpoMinutes", "RpoMinutes"),
                                    ("rtoMinutes", "RtoMinutes"),
                                    ("maximumVerificationHours", "MaximumVerificationHours"),
                                ] {
                                    edit(ui, fields, key, label, 150.);
                                    ui.end_row();
                                }
                            });
                        });
                    }
                });
        });
        let messages = egui::Rect::from_min_size(
            egui::pos2(right.left(), property.bottom()),
            egui::vec2(right.width(), remaining * 0.35),
        );
        crate::system_admin::region(ui, messages, |ui| {
            ui.painter().rect_stroke(
                messages,
                0.,
                egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                egui::StrokeKind::Inside,
            );
            egui::ScrollArea::both()
                .id_salt("backup-status-messages")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    for (key, label) in [
                        ("latestVerified", "Latest verified point"),
                        ("latestRestoreTested", "Latest restore-tested point"),
                    ] {
                        ui.trading_label(format!(
                            "{label}: {}",
                            if key == "latestVerified" {
                                dashboard.latest_verified.as_ref()
                            } else {
                                dashboard.latest_restore_tested.as_ref()
                            }
                            .and_then(|point| point.restore_point_id.as_deref())
                            .unwrap_or("none")
                        ));
                    }
                    if self.error.is_empty() {
                        ui.trading_label(
                            s.result
                                .context
                                .get("status")
                                .map(String::as_str)
                                .unwrap_or_default(),
                        );
                    } else {
                        ui.colored_label(egui::Color32::RED, &self.error);
                    }
                });
        });
        if fields.get("restorePoint").is_none_or(|v| v.is_empty()) {
            if let Some(id) = dashboard
                .latest_verified
                .as_ref()
                .and_then(|point| point.restore_point_id.as_deref())
            {
                fields.insert("restorePoint".into(), id.into());
            }
        }
        let restore =
            egui::Rect::from_min_max(egui::pos2(right.left(), messages.bottom()), right.max);
        crate::system_admin::region(ui, restore, |ui| {
            ui.add_enabled_ui(idle, |ui| {
                ui.columns(3, |cols| {
                    for (ui, (key, label)) in cols.iter_mut().zip([
                        ("restorePoint", "Verified restore point"),
                        ("targetProfile", "Target profile"),
                        ("validationProfile", "Restore copy name"),
                    ]) {
                        ui.trading_caption(label);
                        let r = ui.add(
                            egui::TextEdit::singleline(fields.entry(key.into()).or_default())
                                .desired_width(f32::INFINITY),
                        );
                        r.widget_info(|| {
                            egui::WidgetInfo::labeled(egui::WidgetType::TextEdit, true, label)
                        });
                    }
                });
                if ui
                    .add_sized(
                        egui::vec2(ui.available_width(), 40.),
                        egui::Button::new("Restore Backup (fresh copy)"),
                    )
                    .clicked()
                {
                    *action = Some("backup-restore");
                }
            });
        });
    }
}

impl DatabaseBackup {
    /// Draw the two dated source trees and the original retained-output toolbar.
    fn draw_logs(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        history: &BTreeMap<String, Vec<Operation>>,
        cursors: &BTreeMap<String, String>,
        idle: bool,
        action: &mut Option<&'static str>,
    ) {
        let bounds = ui.available_rect_before_wrap();
        let (tree, output) = crate::system_admin::split(ui, bounds, "backup-log-split", 0.5);
        crate::system_admin::region(ui, tree, |ui| {
            egui::ScrollArea::both().auto_shrink([false,false]).id_salt("backup-log-tree").show(ui,|ui|{
            for(source,label)in[("AwsCloud","AWS Backup"),("LocalWorkstation","Local Workstation Backup")] {
                let color=history.get(source).and_then(|rows|rows.first()).map(|op|status(&text(op,"outcome"))).unwrap_or(egui::Color32::GRAY);
                egui::CollapsingHeader::new(label).id_salt(source).default_open(true).icon(move|ui,_,response|{ui.painter().circle_filled(response.rect.center(),4.,color);}).show(ui,|ui|{
                    let mut dates:BTreeMap<String,BTreeMap<String,BTreeMap<String,Vec<&Operation>>>>=BTreeMap::new();
                    for operation in history.get(source).into_iter().flatten(){let date=text(operation,"createdUtc");if let Ok(at)=chrono::DateTime::parse_from_rfc3339(&date){let at=at.with_timezone(&chrono_tz::America::New_York);dates.entry(at.format("%Y").to_string()).or_default().entry(at.format("%m %B").to_string()).or_default().entry(at.format("%d %A").to_string()).or_default().push(operation);}}
                    for(year,months)in dates.iter().rev(){egui::CollapsingHeader::new(year).id_salt((source,year)).show(ui,|ui|{
                        for(month,days)in months.iter().rev(){egui::CollapsingHeader::new(month).id_salt((source,year,month)).show(ui,|ui|{
                            for(day,operations)in days.iter().rev(){egui::CollapsingHeader::new(day).id_salt((source,year,month,day)).show(ui,|ui|{
                                let mut runs:BTreeMap<String,Vec<&Operation>>=BTreeMap::new();for op in operations{let key=op.backup_set_id.as_deref().unwrap_or(op.operation_id.as_deref().unwrap_or_default());runs.entry(key.into()).or_default().push(op);}
                                for(run,ops)in runs{let first=ops[0];let color=status(&text(first,"outcome"));egui::CollapsingHeader::new(format!("{} | {} {}/{} | {} | ran {} | scheduled: unavailable",text(first,"protectionSet"),text(first,"kind"),text(first,"requestedMode"),text(first,"resolvedMode"),run,text(first,"createdUtc"))).id_salt(&run).icon(move|ui,_,response|{ui.painter().circle_filled(response.rect.center(),4.,color);}).show(ui,|ui|{
                                    for(engine,label)in[("ScyllaDb","ScyllaDB"),("PostgreSql","PostgreSQL")] {
                                        if let Some(op)=ops.iter().find(|o|text(o,"engine")==engine){let id=text(op,"operationId");let caption=format!("{label} | {} | {}% milestone | {}",text(op,"phase"),text(op,"progressPercent"),text(op,"outcome"));
                                            if ui.add_enabled(idle,egui::Button::new(egui::RichText::new(caption).color(status(&text(op,"outcome")))).selected(self.operation==id)).clicked(){self.operation=id;self.operation_source=source.into();*action=Some("backup-output");}
                                        }else{if ui.add_enabled(idle,egui::Button::new(egui::RichText::new(format!("{label} | no identified operation")).color(egui::Color32::GRAY))).clicked(){self.operation.clear();self.operation_source=source.into();*action=Some("backup-clear-output");}}
                                    }
                                });}
                            });}
                        });}
                    });}
                    if cursors.get(source).is_some_and(|v|!v.is_empty())&&ui.add_enabled(idle,egui::Button::new("Load older runs")).clicked(){self.history_source=source.into();*action=Some("backup-older");}
                });
            }
        });
        });
        let content = egui::Rect::from_min_max(
            output.min,
            egui::pos2(output.right(), output.bottom() - 82.),
        );
        crate::system_admin::region(ui, content, |ui| {
            let output = s.result.context.get("output").cloned().unwrap_or_default();
            egui::ScrollArea::both()
                .id_salt("backup-output-scroll")
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    ui.add(
                        egui::Label::new(output)
                            .selectable(true)
                            .wrap_mode(egui::TextWrapMode::Extend),
                    );
                });
        });
        crate::system_admin::region(
            ui,
            egui::Rect::from_min_size(
                egui::pos2(output.left(), content.bottom()),
                egui::vec2(output.width(), 42.),
            ),
            |ui| {
                ui.horizontal(|ui| {
                    if ui
                        .add_enabled(idle, egui::Button::new("Refresh logs / output"))
                        .clicked()
                    {
                        *action = Some(if s.result.view == "database-backup" {
                            "backup-refresh"
                        } else {
                            "load"
                        });
                    }
                    let running = history
                        .values()
                        .flatten()
                        .find(|op| text(op, "operationId") == self.operation)
                        .is_some_and(|op| {
                            text(op, "kind") == "Backup" && text(op, "outcome") == "None"
                        });
                    if ui
                        .add_enabled(idle && running, egui::Button::new("Cancel selected backup"))
                        .clicked()
                    {
                        *action = Some("backup-cancel");
                    }
                });
            },
        );
        crate::system_admin::region(
            ui,
            egui::Rect::from_min_max(egui::pos2(output.left(), output.bottom() - 40.), output.max),
            |ui| {
                let status=s.result.context.get("status").map(String::as_str).filter(|s|!s.is_empty()).unwrap_or("Select a PostgreSQL or ScyllaDB leaf to see phases and output. Manual backup and restore: Setup > Local Workstation.");
                if self.error.is_empty() {
                    ui.trading_label(status);
                } else {
                    ui.colored_label(egui::Color32::RED, &self.error);
                }
            },
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn prepared_documents_reuse_only_the_same_publication_identity_and_generation() {
        let mut view = DatabaseBackup::default();
        let mut publication = ContractPublication::default();
        publication.result.documents.insert(
            "dashboard".into(),
            crate::wire::encode(&Dashboard {
                source: "LocalWorkstation".into(),
                ..Default::default()
            })
            .unwrap()
            .into(),
        );
        let first = view.documents(&publication);
        let unchanged = view.documents(&publication);
        assert!(Arc::ptr_eq(&first, &unchanged));
        publication.result.documents.insert(
            "dashboard".into(),
            crate::wire::encode(&Dashboard {
                source: "AwsCloud".into(),
                ..Default::default()
            })
            .unwrap()
            .into(),
        );
        publication.invalidate_render_cache();
        let replaced = view.documents(&publication);
        assert!(!Arc::ptr_eq(&first, &replaced));
        assert_eq!(replaced.dashboard.source, "AwsCloud");
        publication.generation += 1;
        assert!(!Arc::ptr_eq(&replaced, &view.documents(&publication)));
    }
}
