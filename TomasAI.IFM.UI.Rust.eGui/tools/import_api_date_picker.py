"""Import the pinned egui_extras calendar rendering with API-owned date context.

Development utility only. It edits the standalone copy; the hybrid is untouched.
The upstream MIT notice accompanies the adapted source and is embedded by main.
"""
from pathlib import Path
import sys

source=Path(sys.argv[1])
root=Path(__file__).resolve().parents[1]
target=root/"src"/"date_picker"
target.mkdir(exist_ok=True)
notice="// Adapted from egui_extras 0.32.3; Copyright Emil Ernerfeldt and contributors.\n// Distributed under the accompanying MIT license. Date authority is IFM API-owned.\n"
for filename in ("button.rs","popup.rs","mod.rs"):
 text=(source/filename).read_text(encoding="utf-8")
 text=text.replace('#[cfg_attr(feature = "serde", derive(serde::Deserialize, serde::Serialize))]\n',"")
 if filename in ("button.rs","popup.rs"):text="use eframe::egui;\n"+text
 if filename=="popup.rs":
  text=text.replace("use crate::{Column, Size, StripBuilder, TableBuilder};","use egui_extras::{Column, Size, StripBuilder, TableBuilder};")
  text=text.replace("let today = chrono::offset::Utc::now().date_naive();","// A missing API clock omits the today marker; it never uses wall time.\n        let today = super::api_today(ui.ctx());")
  text=text.replace("None => (today.year() - 100, today.year() + 10),","None => super::year_bounds(*self.selection),")
  text=text.replace("if day == today {","if Some(day) == today {")
  text=text.replace("if button_response.clicked() {","if button_response.clicked() && (1..=9999).contains(&day.year()) {")
  text=text.replace("let button_response = ui.add(","let button_response = ui.add_enabled((1..=9999).contains(&day.year()),")
  text=text.replace('if ui.button("Save").clicked() {','if ui.add_enabled((1..=9999).contains(&popup_state.year) && NaiveDate::from_ymd_opt(popup_state.year, popup_state.month, popup_state.day).is_some(), Button::new("Save")).clicked() {')
  text=text.replace("let weeks = month_data(popup_state.year, popup_state.month);","if popup_state.year < 1 { popup_state.year=1;popup_state.month=1;popup_state.day=1; }\n        if popup_state.year > 9999 { popup_state.year=9999;popup_state.month=12;popup_state.day=31; }\n        popup_state.day=popup_state.day.min(popup_state.last_day_of_month());\n        let weeks = month_data(popup_state.year, popup_state.month);")
 if filename=="button.rs":
  text=text.replace("(Default: today's year - 100 to today's year + 10)","(Default: selected year - 100 to selected year + 10, within .NET DateOnly bounds)")
  text=text.replace("let id = ui.make_persistent_id(self.id_salt);",'// Default widget identity keeps sibling calendars independent.\n        let id = self.id_salt.map(|salt| ui.make_persistent_id(salt)).unwrap_or_else(|| ui.next_auto_id());')
  text=text.replace("Area::new(ui.make_persistent_id(self.id_salt))",'Area::new(id.with("picker-popup"))')
  text=text.replace("Add id source.\n    /// Must be set if multiple date picker buttons are in the same Ui.","Override the stable widget identity.\n    /// The default automatic widget ID keeps sibling popups independent.")
 if filename=="mod.rs":
  text="//! Calendar presentation adapted from egui_extras, with no workstation date authority.\n"+text
  text+='''
/// Upstream permission notice, retained in the one-executable distribution.
pub const LICENSE: &str = include_str!("LICENSE-MIT");
const API_DATE: &str = "ifm-api-clock-civil-date";
/// Only a successfully decoded API session may update this context value.
pub fn set_api_today(ctx: &eframe::egui::Context, date: NaiveDate) {
    ctx.data_mut(|data| data.insert_temp(eframe::egui::Id::new(API_DATE),date));
}
/// A missing clock has no today marker; the selected explicit date remains usable.
pub fn api_today(ctx: &eframe::egui::Context) -> Option<NaiveDate> {
    ctx.data(|data|data.get_temp(eframe::egui::Id::new(API_DATE)))
}
/// Dropdown navigation follows its explicit selected date, never workstation UTC.
fn year_bounds(date: NaiveDate) -> (i32,i32) {
    ((date.year()-100).max(1),(date.year()+10).min(9999))
}
#[cfg(test)]
mod tests;
'''
 (target/filename).write_text(notice+text,encoding="utf-8")
for file in (root/"src").rglob("*.rs"):
 if file.parent==target:continue
 text=file.read_text(encoding="utf-8")
 replaced=text.replace("egui_extras::DatePickerButton","crate::date_picker::DatePickerButton")
 if replaced!=text:file.write_text(replaced,encoding="utf-8")
