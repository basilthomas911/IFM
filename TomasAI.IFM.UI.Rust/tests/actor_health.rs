use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    actor_health::ActorHealth,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
};
use std::collections::BTreeMap;
fn fixture() -> (Catalog, ContractPublication) {
    (
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap(),
        ContractPublication {
            generation: 4,
            result: ContractResult {
                view: "actor-health".into(),
                context: BTreeMap::from([("controls".into(), "true".into())]),
                choices: BTreeMap::from([(
                    "operations".into(),
                    vec!["Pause".into(), "Restart".into()],
                )]),
                rows: vec![
                    ContractRow {
                        id: "actor-fixture".into(),
                        fields: BTreeMap::from([
                            ("name".into(), "FixtureActor".into()),
                            ("parent".into(), "FixtureDomain".into()),
                            ("status".into(), "Green".into()),
                        ]),
                    },
                    ContractRow {
                        id: "mailbox-fixture".into(),
                        fields: BTreeMap::from([
                            ("name".into(), "fixture-mailbox".into()),
                            ("parent".into(), "actor-fixture".into()),
                            ("status".into(), "Yellow".into()),
                            ("Generation".into(), "7".into()),
                            ("canExecute".into(), "true".into()),
                        ]),
                    },
                ],
                ..Default::default()
            },
            ..Default::default()
        },
    )
}
#[test]
fn domain_actor_mailbox_expand_select_and_collapse_uses_cached_data() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut ActorHealth| {
            state.draw(ctx, &s, &c, true);
        },
        ActorHealth::default(),
    );
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    h.get_by_label("FixtureDomain").click();
    h.run();
    h.get_by_label("FixtureActor").click();
    h.run();
    h.get_by_label("fixture-mailbox").click();
    h.run();
    assert_eq!(h.state().selected.as_deref(), Some("mailbox-fixture"));
    h.get_by_label("Generation");
    h.get_by_label("7");
    h.get_by_label("FixtureActor").click();
    h.run();
    h.get_by_label("FixtureDomain").click();
    h.run();
    assert!(h.state().pending.is_none());
}
#[test]
fn cancel_supervisor_confirmation_never_dispatches() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ActorHealth, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p);
            }
        },
        (
            ActorHealth {
                selected: Some("mailbox-fixture".into()),
                reason: "fixture reason".into(),
                confirm: true,
                ..Default::default()
            },
            None,
        ),
    );
    h.run();
    h.get_by_label("Cancel").click();
    h.run();
    assert!(!h.state().0.confirm);
    assert!(h.state().1.is_none());
}
