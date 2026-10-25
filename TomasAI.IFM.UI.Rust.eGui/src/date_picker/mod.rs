// Adapted from egui_extras 0.32.3; Copyright Emil Ernerfeldt and contributors.
// Distributed under the accompanying MIT license. Date authority is IFM API-owned.
//! Calendar presentation adapted from egui_extras, with no workstation date authority.
mod button;
mod popup;

pub use button::DatePickerButton;
use chrono::{Datelike as _, Duration, NaiveDate, Weekday};

#[derive(Debug)]
struct Week {
    number: u8,
    days: Vec<NaiveDate>,
}

fn month_data(year: i32, month: u32) -> Vec<Week> {
    let first = NaiveDate::from_ymd_opt(year, month, 1).expect("Could not create NaiveDate");
    let mut start = first;
    while start.weekday() != Weekday::Mon {
        start = start.checked_sub_signed(Duration::days(1)).unwrap();
    }
    let mut weeks = vec![];
    let mut week = vec![];
    while start < first || start.month() == first.month() || start.weekday() != Weekday::Mon {
        week.push(start);

        if start.weekday() == Weekday::Sun {
            weeks.push(Week {
                number: start.iso_week().week() as u8,
                days: std::mem::take(&mut week),
            });
        }
        start = start.checked_add_signed(Duration::days(1)).unwrap();
    }

    weeks
}

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
