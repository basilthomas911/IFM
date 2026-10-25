use ifm_ui_egui::{backend::contracts::ExactDecimal,services::broker_order_ui as ui};
fn dec(s:&str)->ExactDecimal{ExactDecimal::parse(s).unwrap()}
#[test]
fn exact_tick_modulus_keeps_decimal_precision_sign_and_scale(){
 for(price,tick,valid)in[("6050.50","0.25",true),("-1.25","0.05",true),("-1.23","0.05",false),("0","0.0000000000000000000000000001",true),("79228162514264337593543950335","0.0000000000000000000000000001",true),("0.0000000000000000000000000001","79228162514264337593543950335",false),("1.2500",".25",true)]{let tick=if tick.starts_with('.') {format!("0{tick}")}else{tick.into()};assert_eq!(ui::is_tick_multiple(&dec(price),&dec(&tick)),valid,"{price}/{tick}");}
 assert!(!ui::is_tick_multiple(&dec("1"),&dec("0")));
}
