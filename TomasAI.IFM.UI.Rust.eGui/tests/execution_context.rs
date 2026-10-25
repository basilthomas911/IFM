use ifm_ui_egui::services::execution_context::Context;
#[test]
fn configured_broker_route_never_silently_changes_account_or_environment(){
 assert_eq!(Context::resolve("Development",None,None).unwrap().environment,"Emulator");
 assert!(Context::resolve("Production",None,None).is_err());assert!(Context::resolve("Production",Some(" "),Some("Live")).is_err());
 for(mode,expected)in[("Paper","Paper"),("Live","Live"),("Emulator","Emulator"),("2","Paper")]{let c=Context::resolve("Production",Some("DESKTOP-ACCOUNT"),Some(mode)).unwrap();assert_eq!(c.account_alias,"DESKTOP-ACCOUNT");assert_eq!(c.environment,expected);}
 for mode in ["Paper","Live","Unknown","0","7","live"]{assert!(Context::resolve("Development",Some("DESKTOP-ACCOUNT"),Some(mode)).is_err());}
}
