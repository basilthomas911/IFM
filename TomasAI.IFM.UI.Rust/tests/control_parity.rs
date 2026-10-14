use eframe::egui;
use egui_kittest::{Harness,kittest::{Queryable,NodeT}};
#[test]
fn report_list_selects_row_from_empty_space_after_last_column(){
 let mut h=Harness::new_ui_state(|ui,state:&mut Option<usize>|{let(result,_)=ifm_ui_rust::control_parity::report_list(ui,"orders",&[("Order Id",80.),("Reference",200.)],2,*state,300.,|row,column|format!("{row}:{column}"));if result.is_some(){*state=result;}},None);
 h.set_size(egui::vec2(800.,400.));h.run();
 let pos=egui::pos2(650.,65.);h.input_mut().events.push(egui::Event::PointerMoved(pos));h.input_mut().events.push(egui::Event::PointerButton{pos,button:egui::PointerButton::Primary,pressed:true,modifiers:egui::Modifiers::NONE});h.run();h.input_mut().events.push(egui::Event::PointerButton{pos,button:egui::PointerButton::Primary,pressed:false,modifiers:egui::Modifiers::NONE});h.run();assert_eq!(*h.state(),Some(1));assert!(h.query_by_label("1:0").is_some());
}
#[test]
fn report_list_keeps_wide_columns_inside_viewport(){
 let mut h=Harness::new_ui(|ui|{ifm_ui_rust::control_parity::report_list(ui,"wide",&[("Reference",800.),("Source",150.)],100000,None,300.,|row,_|row.to_string());});h.set_size(egui::vec2(600.,400.));h.run();assert!(h.query_by_label("Reference").is_some());
}

#[test]
fn report_columns_resize_by_dragging_the_header_divider(){
 let mut h=Harness::new_ui(|ui|{ifm_ui_rust::control_parity::report_list(ui,"resizing",&[("Order Id",80.),("Reference",200.)],2,None,300.,|row,_|row.to_string());});h.set_size(egui::vec2(800.,400.));h.run();
 let before=h.get_by_label("Reference").accesskit_node().bounding_box().unwrap().x0;
 let start=egui::pos2(before as f32,20.);let end=start+egui::vec2(60.,0.);
 h.input_mut().events.extend([egui::Event::PointerMoved(start),egui::Event::PointerButton{pos:start,button:egui::PointerButton::Primary,pressed:true,modifiers:egui::Modifiers::NONE}]);h.run();
 h.input_mut().events.push(egui::Event::PointerMoved(end));h.run();h.input_mut().events.push(egui::Event::PointerButton{pos:end,button:egui::PointerButton::Primary,pressed:false,modifiers:egui::Modifiers::NONE});h.run();
 let after=h.get_by_label("Reference").accesskit_node().bounding_box().unwrap().x0;assert!(after-before>=40.,"Divider did not move: {before} -> {after}");h.run();assert_eq!(after,h.get_by_label("Reference").accesskit_node().bounding_box().unwrap().x0);
}

#[test]
fn report_columns_keep_initial_widths_inside_a_new_window(){
 let mut h=Harness::new(|ctx|{egui::Window::new("Orders").fixed_size([1300.,400.]).show(ctx,|ui|{ifm_ui_rust::control_parity::report_list(ui,"initial-window",&[("Order Id",80.),("Reference",800.),("Source",150.)],1,None,300.,|_,_|String::new());});});h.set_size(egui::vec2(1500.,600.));h.run();
 let order=h.get_by_label("Order Id").accesskit_node().bounding_box().unwrap().x0;
 let reference=h.get_by_label("Reference").accesskit_node().bounding_box().unwrap().x0;
 let source=h.get_by_label("Source").accesskit_node().bounding_box().unwrap().x0;
 assert!(reference-order>=79.,"First column collapsed");assert!(source-reference>=799.,"Reference column collapsed");
}
