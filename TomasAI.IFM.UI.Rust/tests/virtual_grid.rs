use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    virtual_grid::{GridRow, GridView},
};
fn rows(count: usize) -> Vec<GridRow> {
    (1..=count)
        .map(|id| GridRow {
            id: id as u64,
            sequence: id as u64,
            timestamp: "2026-10-12T00:00:00Z".into(),
            daily_pnl: "-12.50".into(),
            forward_loss_ratio: "0.25".into(),
        })
        .collect()
}
#[test]
fn all_rows_addressable_but_only_viewport_rows_are_drawn() {
    for count in [1000, 10000, 100000] {
        let catalog: Catalog =
            serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
        let mut h = Harness::new_state(
            move |ctx, grid: &mut GridView| grid.draw(ctx, &catalog),
            GridView::new(rows(count)),
        );
        ifm_ui_rust::apply_winforms_typography(&h.ctx).unwrap();
        h.set_size(eframe::egui::vec2(1100.0, 800.0));
        h.run();
        assert_eq!(h.state().rows.len(), count);
        assert!(h.state().drawn.len() < 60);
        h.get_by_label("Row 1").click();
        h.run();
        assert_eq!(h.state().selected, Some(1));
        h.get_by_label("Last row").click();
        h.run();
        assert!(h.query_by_label(&format!("Row {count}")).is_some());
        assert!(h.state().drawn.len() < 60);
        h.get_by_label("First row").click();
        h.run();
        assert!(h.query_by_label("Row 1").is_some());
        h.key_press(eframe::egui::Key::End);
        h.run();
        assert_eq!(h.state().selected, Some(count as u64));
        h.key_press(eframe::egui::Key::Home);
        h.run();
        assert_eq!(h.state().selected, Some(1));
        h.key_press(eframe::egui::Key::ArrowDown);
        h.run();
        assert_eq!(h.state().selected, Some(2));
        h.key_press(eframe::egui::Key::ArrowUp);
        h.run();
        assert_eq!(h.state().selected, Some(1));
    }
}
#[test]
fn ordering_is_prepared_off_thread_and_selection_identity_survives() {
    let mut grid = GridView::new(rows(100000));
    grid.selected = Some(25);
    grid.descending = true;
    grid.filter = "25".into();
    grid.rebuild();
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let mut h = Harness::new_state(move |ctx, g: &mut GridView| g.draw(ctx, &catalog), grid);
    for _ in 0..200 {
        h.step();
        if h.state().order.len() < 100000 {
            break;
        }
        std::thread::sleep(std::time::Duration::from_millis(2));
    }
    assert_eq!(h.state().selected, Some(25));
    assert!(h.state().order.len() < 100000);
    assert!(h.state().order.windows(2).all(|w| w[0] > w[1]));
    assert!(
        h.state()
            .order
            .iter()
            .all(|&i| h.state().rows[i].id.to_string().contains("25"))
    );
}
#[test]
fn cached_grid_frame_timing_report() {
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let mut h = Harness::new_state(
        move |ctx, g: &mut GridView| g.draw(ctx, &catalog),
        GridView::new(rows(100000)),
    );
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    let mut samples = Vec::new();
    for _ in 0..120 {
        let start = std::time::Instant::now();
        h.step();
        samples.push(start.elapsed().as_secs_f64() * 1000.0);
    }
    samples.sort_by(f64::total_cmp);
    println!(
        "100k cached grid harness CPU frame ms: p50={:.3} p95={:.3} max={:.3}; drawn={}",
        samples[60],
        samples[114],
        samples[119],
        h.state().drawn.len()
    );
    assert!(h.state().drawn.len() < 60);
}
