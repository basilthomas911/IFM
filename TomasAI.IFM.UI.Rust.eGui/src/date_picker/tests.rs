use super::*;
#[test]
fn calendar_reference_is_api_owned_and_scoped_to_its_ui_context() {
    let first=eframe::egui::Context::default();let second=eframe::egui::Context::default();
    assert_eq!(api_today(&first),None);
    let historical=NaiveDate::from_ymd_opt(1998,7,14).unwrap();
    set_api_today(&first,historical);
    assert_eq!(api_today(&first),Some(historical));assert_eq!(api_today(&second),None);
    set_api_today(&first,NaiveDate::from_ymd_opt(1998,7,15).unwrap());
    assert_eq!(api_today(&first).unwrap().day(),15);
}
#[test]
fn calendar_year_navigation_follows_selection_and_respects_dotnet_dateonly_bounds() {
    assert_eq!(year_bounds(NaiveDate::from_ymd_opt(1850,1,1).unwrap()),(1750,1860));
    assert_eq!(year_bounds(NaiveDate::from_ymd_opt(1,1,1).unwrap()),(1,11));
    assert_eq!(year_bounds(NaiveDate::from_ymd_opt(9999,12,31).unwrap()),(9899,9999));
    for (year,month) in [(1,1),(9999,12),(2024,2)] {
        let weeks=month_data(year,month);
        assert!(weeks.len()>=4&&weeks.len()<=6);
        assert!(weeks.iter().all(|w|w.days.len()==7));
    }
}

#[test]
fn sibling_date_pickers_save_independent_selections() {
    use egui_kittest::{Harness,kittest::Queryable};
    let first=NaiveDate::from_ymd_opt(2026,10,26).unwrap();
    let second=NaiveDate::from_ymd_opt(2026,10,25).unwrap();
    let mut harness=Harness::new_state(|ctx,state:&mut (NaiveDate,NaiveDate)| {
        eframe::egui::CentralPanel::default().show(ctx,|ui| {
            ui.add(DatePickerButton::new(&mut state.0));
            ui.add(DatePickerButton::new(&mut state.1));
        });
    },(first,second));
    harness.set_size(eframe::egui::vec2(900.,900.));harness.run();
    harness.get_by_label("2026-10-26 📆").click();harness.run();
    harness.get_by_label("20").click();harness.run();
    harness.get_by_label("Save").click();harness.run();
    assert_eq!(harness.state().0,NaiveDate::from_ymd_opt(2026,10,20).unwrap());
    assert_eq!(harness.state().1,second);
    harness.get_by_label("2026-10-25 📆").click();harness.run();
    harness.get_by_label("21").click();harness.run();
    harness.get_by_label("Save").click();harness.run();
    assert_eq!(harness.state().0,NaiveDate::from_ymd_opt(2026,10,20).unwrap());
    assert_eq!(harness.state().1,NaiveDate::from_ymd_opt(2026,10,21).unwrap());
}
