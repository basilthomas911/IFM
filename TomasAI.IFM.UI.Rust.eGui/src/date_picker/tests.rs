use super::*;


/// Four-, five-, and six-week calendars leave a small gap rather than reserving a blank row.
#[test]
fn calendar_footer_tracks_the_actual_number_of_weeks() {
    use egui_kittest::{Harness, kittest::Queryable};
    for (year, month, day, week_count) in [(2021, 2, 1, 4), (2026, 10, 26, 5), (2026, 8, 1, 6)] {
        assert_eq!(month_data(year, month).len(), week_count);
        let mut h = Harness::new_state(
            |ctx, date| {
                crate::presentation::apply(ctx);
                eframe::egui::CentralPanel::default().show(ctx, |ui| {
                    ui.add(DatePickerButton::new(date).calendar_week(false));
                });
            },
            NaiveDate::from_ymd_opt(year, month, day).unwrap(),
        );
        h.set_size(eframe::egui::vec2(900., 900.));
        h.run();
        h.get_by_label(&format!("{year:04}-{month:02}-{day:02} 📆"))
            .click();
        h.run();
        let mut bottom = 0_f32;
        for number in 1..=31 {
            for node in h.query_all_by_label(&number.to_string()) {
                bottom = bottom.max(node.rect().bottom());
            }
        }
        let gap = h.get_by_label("Save").rect().top() - bottom;
        assert!(
            (4. ..=10.).contains(&gap),
            "{week_count} weeks: footer gap {gap}"
        );
    }
}


/// Calendar actions sit together at the popup center, with navigation and day text centered in each button.
#[test]
fn calendar_buttons_and_footer_are_centered() {
    use egui_kittest::{Harness, kittest::Queryable};
    use eframe::egui;
    let mut h = Harness::new_state(
        |ctx, date| {
            crate::presentation::apply(ctx);
            egui::CentralPanel::default().show(ctx, |ui| {
                ui.add(DatePickerButton::new(date));
            });
        },
        NaiveDate::from_ymd_opt(2026, 10, 26).unwrap(),
    );
    h.set_size(egui::vec2(900., 900.));
    h.run();
    h.get_by_label("2026-10-26 📆").click();
    h.run();
    let cancel = h.get_by_label("Cancel").rect();
    let save = h.get_by_label("Save").rect();
    let first = h.get_by_label("<<<").rect();
    let last = h.get_by_label(">>>").rect();
    assert!(
        ((cancel.center().x + save.center().x) * 0.5 - (first.left() + last.right()) * 0.5).abs()
            < 1.
    );
    assert!((cancel.width() - save.width()).abs() < 1.);
    assert!(cancel.left() - save.right() >= 5.);
    let mut bottom_day = 0_f32;
    for day in 1..=31 {
        for node in h.query_all_by_label(&day.to_string()) {
            bottom_day = bottom_day.max(node.rect().bottom());
        }
    }
    assert!(
        (4. ..=10.).contains(&(save.top() - bottom_day)),
        "calendar footer gap should be small: {}",
        save.top() - bottom_day
    );


    /// Locate the actual painted text rather than relying on the accessibility rectangle.
    fn painted(shapes: &[egui::epaint::ClippedShape], caption: &str) -> egui::Rect {
        /// Find a text run inside nested paint shapes.
        fn find(shape: &egui::Shape, caption: &str) -> Option<egui::Rect> {
            match shape {
                egui::Shape::Text(text) if text.galley.text() == caption => {
                    Some(egui::Rect::from_min_size(text.pos, text.galley.size()))
                }
                egui::Shape::Vec(shapes) => shapes.iter().find_map(|shape| find(shape, caption)),
                _ => None,
            }
        }
        shapes
            .iter()
            .find_map(|shape| find(&shape.shape, caption))
            .unwrap()
    }
    for caption in ["<<<", "<<", "<", ">", ">>", ">>>", "20", "Cancel", "Save"] {
        let button = h.get_by_label(caption).rect();
        let text = painted(&h.output().shapes, caption);
        assert!(
            button.center().distance(text.center()) <= 1.,
            "text not centered: {caption}: {button:?} {text:?}"
        );
    }
    if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
        h.render()
            .unwrap()
            .save(std::path::Path::new(&directory).join("calendar.png"))
            .unwrap();
    }
    h.get_by_label("20").click();
    h.run();
    h.get_by_label("Save").click();
    h.run();
    assert_eq!(*h.state(), NaiveDate::from_ymd_opt(2026, 10, 20).unwrap());
    h.get_by_label("2026-10-20 📆").click();
    h.run();
    h.get_by_label("21").click();
    h.run();
    h.get_by_label("Cancel").click();
    h.run();
    assert_eq!(*h.state(), NaiveDate::from_ymd_opt(2026, 10, 20).unwrap());
}


/// Verifies that calendar reference is api owned and scoped to its ui context.
#[test]
fn calendar_reference_is_api_owned_and_scoped_to_its_ui_context() {
    let first = eframe::egui::Context::default();
    let second = eframe::egui::Context::default();
    assert_eq!(api_today(&first), None);
    let historical = NaiveDate::from_ymd_opt(1998, 7, 14).unwrap();
    set_api_today(&first, historical);
    assert_eq!(api_today(&first), Some(historical));
    assert_eq!(api_today(&second), None);
    set_api_today(&first, NaiveDate::from_ymd_opt(1998, 7, 15).unwrap());
    assert_eq!(api_today(&first).unwrap().day(), 15);
}


/// Verifies that calendar year navigation follows selection and respects dotnet dateonly bounds.
#[test]
fn calendar_year_navigation_follows_selection_and_respects_dotnet_dateonly_bounds() {
    assert_eq!(
        year_bounds(NaiveDate::from_ymd_opt(1850, 1, 1).unwrap()),
        (1750, 1860)
    );
    assert_eq!(
        year_bounds(NaiveDate::from_ymd_opt(1, 1, 1).unwrap()),
        (1, 11)
    );
    assert_eq!(
        year_bounds(NaiveDate::from_ymd_opt(9999, 12, 31).unwrap()),
        (9899, 9999)
    );
    for (year, month) in [(1, 1), (9999, 12), (2024, 2)] {
        let weeks = month_data(year, month);
        assert!(weeks.len() >= 4 && weeks.len() <= 6);
        assert!(weeks.iter().all(|w| w.days.len() == 7));
    }
}


/// Verifies that sibling date pickers save independent selections.
#[test]
fn sibling_date_pickers_save_independent_selections() {
    use egui_kittest::{Harness, kittest::Queryable};
    let first = NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let second = NaiveDate::from_ymd_opt(2026, 10, 25).unwrap();
    let mut harness = Harness::new_state(
        |ctx, state: &mut (NaiveDate, NaiveDate)| {
            eframe::egui::CentralPanel::default().show(ctx, |ui| {
                ui.add(DatePickerButton::new(&mut state.0));
                ui.add(DatePickerButton::new(&mut state.1));
            });
        },
        (first, second),
    );
    harness.set_size(eframe::egui::vec2(900., 900.));
    harness.run();
    harness.get_by_label("2026-10-26 📆").click();
    harness.run();
    harness.get_by_label("20").click();
    harness.run();
    harness.get_by_label("Save").click();
    harness.run();
    assert_eq!(
        harness.state().0,
        NaiveDate::from_ymd_opt(2026, 10, 20).unwrap()
    );
    assert_eq!(harness.state().1, second);
    harness.get_by_label("2026-10-25 📆").click();
    harness.run();
    harness.get_by_label("21").click();
    harness.run();
    harness.get_by_label("Save").click();
    harness.run();
    assert_eq!(
        harness.state().0,
        NaiveDate::from_ymd_opt(2026, 10, 20).unwrap()
    );
    assert_eq!(
        harness.state().1,
        NaiveDate::from_ymd_opt(2026, 10, 21).unwrap()
    );
}
