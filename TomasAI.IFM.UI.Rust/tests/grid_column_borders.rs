use eframe::egui;
use ifm_ui_rust::control_parity::BorderedTable as _;

fn check_borders(rows: usize, shrink: bool) {
    let ctx=egui::Context::default();
    let mut viewport=egui::Rect::NOTHING;
    let output=ctx.run(egui::RawInput {screen_rect:Some(egui::Rect::from_min_size(egui::Pos2::ZERO,egui::vec2(600.,500.))),..Default::default()},|ctx| {
        egui::CentralPanel::default().show(ctx,|ui| {
            viewport=egui_extras::TableBuilder::new(ui)
                .column(egui_extras::Column::exact(120.))
                .column(egui_extras::Column::remainder())
                .max_scroll_height(200.).auto_shrink([false,shrink])
                .header(24.,|mut row|{row.col(|ui|{ui.label("A");});row.col(|ui|{ui.label("B");});})
                .bordered_body(24.,|body|body.rows(20.,rows,|mut row|{row.col(|ui|{ui.label("value");});row.col(|ui|{ui.label("value");});})).inner_rect;
            ui.label("Content below grid");
        });
    });
    let rules:Vec<_>=output.shapes.iter().filter_map(|shape| {
        if let egui::Shape::LineSegment {points,stroke}=&shape.shape {
            if points[0].x==points[1].x && stroke.color==ifm_ui_rust::presentation::BORDER {return Some((shape.clip_rect,*points));}
        }
        None
    }).collect();
    assert!(!rules.is_empty(),"No column rule was drawn");
    for (clip,points) in rules {
        assert!(clip.bottom()<=viewport.bottom()+0.01,"Clip extends below the final grid viewport");
        assert!(points[1].y<=viewport.bottom(),"Rule extends below the final grid viewport");
        assert!(points[0].y>=viewport.top()-28.01,"Rule extends above the header");
    }
}
#[test]
fn rules_stop_at_shrinking_grid_bounds() {check_borders(0,true);check_borders(1,true);}
#[test]
fn rules_fill_but_do_not_exceed_fixed_or_scrolling_grid_bounds() {check_borders(1,false);check_borders(100,false);}
