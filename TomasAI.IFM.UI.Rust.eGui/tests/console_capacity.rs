use ifm_ui_egui::{console_capacity::for_viewport,services::notifications::Notifications};
#[test]
fn retention_is_two_visible_pages_at_boundaries_and_resize() {
    assert_eq!(for_viewport(264.,22.),24);
    assert_eq!(for_viewport(265.,22.),26);
    assert_eq!(for_viewport(88.,22.),8);
    assert_eq!(for_viewport(0.,22.),2);
    assert_eq!(for_viewport(f32::NAN,22.),2);
}
#[test]
fn warning_storm_keeps_one_current_warning_without_evicting_recovery_deduplication() {
    let mut n=Notifications::default();n.set_console_capacity(24);
    assert!(n.error("recovery-operation".into(),"Recovery failed".into()));
    for i in 0..1050 {n.warning(format!("warning-{i}"),format!("Latest warning {i}"));}
    assert_eq!(n.publication.status_rows.iter().filter(|r|r.id.starts_with("current-warning|")).count(),1);
    assert_eq!(n.publication.latest_status(),Some("Latest warning 1049"));
    assert_eq!(n.publication.error,"Recovery failed");
    assert!(!n.error("recovery-operation".into(),"Duplicate failure".into()));
}

#[test]
fn actual_console_viewport_resize_updates_capacity_and_virtualizes_rows() {
    use ifm_ui_egui::sidebar::{SidebarView,SidebarPublication,StatusRow};
    let publication=SidebarPublication{status_rows:(0..1000).map(|i|StatusRow{id:i.to_string(),time:"10:00".into(),message:format!("row {i}")}).collect(),..Default::default()};
    let mut h=egui_kittest::Harness::new_state(move |ctx,state:&mut SidebarView| {
        eframe::egui::CentralPanel::default().show(ctx,|ui|state.status(ui,&publication));
    },SidebarView::default());
    h.set_size(eframe::egui::vec2(800.,400.));h.run();let initial=ifm_ui_egui::console_capacity::current();
    assert!(initial>2 && initial%2==0);assert!(h.state().drawn_status_rows<1000);
    h.set_size(eframe::egui::vec2(800.,800.));h.run();let larger=ifm_ui_egui::console_capacity::current();assert!(larger>initial);
    h.set_size(eframe::egui::vec2(800.,200.));h.run();assert!(ifm_ui_egui::console_capacity::current()<initial);
}
