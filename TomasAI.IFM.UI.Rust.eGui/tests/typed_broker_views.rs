use ifm_ui_egui::{
    contract_editor::ContractRow,
    models::{backend_document::request_document, trade_contracts::*},
    services::{broker_market_display, broker_order_ui as ui},
    wire,
};
#[allow(dead_code)]
#[path = "support/broker_order_ui_baseline.rs"]
mod baseline;
use serde_json::Value;
fn rows() -> Vec<ContractRow> {
    wire::decode(include_bytes!(
        "fixtures/backend/broker-evidence-rows.msgpack"
    ))
    .unwrap()
}
#[test]
fn typed_broker_records_keep_all_managed_fields_and_computed_identity_getters() {
    for row in rows() {
        let value: Value = wire::decode(row.data.as_ref().unwrap()).unwrap();
        let check = |actual: Value| assert_eq!(actual, value, "{}", row.id);
        match row.fields.get("kind").map(String::as_str) {
            Some("broker-order") => check(
                serde_json::to_value(
                    serde_json::from_value::<BrokerOrderDefinition>(value.clone()).unwrap(),
                )
                .unwrap(),
            ),
            Some("execution") => check(
                serde_json::to_value(
                    serde_json::from_value::<OrderExecutionDefinition>(value.clone()).unwrap(),
                )
                .unwrap(),
            ),
            Some("fill") => check(
                serde_json::to_value(
                    serde_json::from_value::<ExecutionFillEvidence>(value.clone()).unwrap(),
                )
                .unwrap(),
            ),
            _ => {}
        }
    }
}
#[test]
fn typed_mutation_eligibility_captions_summaries_and_property_order_match_original() {
    let rows = rows();
    let order_row = rows
        .iter()
        .find(|r| r.fields.get("kind").is_some_and(|k| k == "broker-order"))
        .unwrap();
    let mut order: BrokerOrderDefinition = wire::decode(order_row.data.as_ref().unwrap()).unwrap();
    let execution = rows
        .iter()
        .find(|r| r.fields.get("kind").is_some_and(|k| k == "execution"))
        .map(|r| wire::decode::<OrderExecutionDefinition>(r.data.as_ref().unwrap()).unwrap());
    for status in [
        BrokerOrderStatus::Working,
        BrokerOrderStatus::PartiallyFilled,
        BrokerOrderStatus::Cancelled,
        BrokerOrderStatus::Unrecognized(987),
    ] {
        order.status = Some(status);
        for execution in [None, execution.as_ref()] {
            let old_order = request_document(&order).unwrap();
            let old_execution = execution.map(|e| request_document(e).unwrap());
            assert_eq!(
                ui::caption(&order).unwrap(),
                baseline::caption(&old_order).unwrap()
            );
            let actual = ui::summary(&order, execution).unwrap();
            let expected = baseline::summary(&old_order, old_execution.as_ref()).unwrap();
            assert_eq!(serde_json::to_value(&actual).unwrap(), expected);
            assert_eq!(
                serde_json::to_value(ui::properties(&actual, ui::SUMMARY_NAMES).unwrap()).unwrap(),
                baseline::properties(&expected, baseline::SUMMARY_NAMES).unwrap()
            );
            for pending in [false, true] {
                assert_eq!(
                    ui::can_mutate(&order, execution, pending).unwrap(),
                    baseline::can_mutate(&old_order, old_execution.as_ref(), pending).unwrap()
                );
                assert_eq!(
                    serde_json::to_value(ui::mutation(&order, execution, pending).unwrap())
                        .unwrap(),
                    baseline::mutation(&old_order, old_execution.as_ref(), pending).unwrap()
                );
            }
        }
    }
}
#[test]
fn typed_account_publication_has_one_identity_with_validity_and_lossless_amounts() {
    let account:BrokerAccountDefinition=serde_json::from_value(serde_json::json!({"Id":{"AccountAlias":"IFM-EMULATOR-PAPER"},"Snapshot":{"CashBalance":"123456.7890123456789012345678"},"Gate":"Open"})).unwrap();
    let bytes = wire::encode(&broker_market_display::account(&account).unwrap()).unwrap();
    let actual: Value = wire::decode(&bytes).unwrap();
    assert_eq!(actual["id"]["accountAlias"], "IFM-EMULATOR-PAPER");
    assert_eq!(actual["id"]["isValid"], true);
    assert_eq!(
        actual["snapshot"]["cashBalance"],
        "123456.7890123456789012345678"
    );
    let restored: BrokerAccountDefinition = wire::decode(&bytes).unwrap();
    assert_eq!(restored, account);
}

#[test]
fn unnamed_broker_strategy_keeps_the_original_caption_fallback() {
    let row = rows()
        .into_iter()
        .find(|r| r.fields.get("kind").is_some_and(|k| k == "broker-order"))
        .unwrap();
    let mut order: BrokerOrderDefinition = wire::decode(row.data.as_ref().unwrap()).unwrap();
    for component in order.order.as_mut().unwrap().components.as_mut().unwrap() {
        component.strategy_kind = Some(TradeStrategyKind::Unrecognized(999));
    }
    assert_eq!(
        ui::caption(&order).unwrap(),
        baseline::caption(&request_document(&order).unwrap()).unwrap()
    );
}

#[test]
fn trade_dispatch_future_stays_within_the_native_worker_stack_budget() {
    use ifm_ui_egui::{
        backend::{schema::Registry, transport::Backend},
        state::trades::TradeController,
    };
    fn size<F, Fut>(_: F) -> usize
    where
        F: FnOnce(
            &'static mut TradeController,
            &'static Backend,
            &'static Registry,
            &'static serde_json::Value,
        ) -> Fut,
    {
        std::mem::size_of::<Fut>()
    }
    let bytes = size(TradeController::execute);
    assert!(
        bytes <= 16 * 1024,
        "Trade dispatch retains {bytes} bytes inline; large operation futures belong on the heap"
    );
}
