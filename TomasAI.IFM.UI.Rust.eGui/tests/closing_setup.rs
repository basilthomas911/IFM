use ifm_ui_egui::{
    backend::contracts::ExactDecimal,
    models::trade_contracts::{FundOrderTradeProjectionReadModel, StrategyPositionSnapshot},
    services::order_close,
};
use serde_json::json;

fn trade(primary: bool, id: i32) -> FundOrderTradeProjectionReadModel {
    serde_json::from_value(
        json!({"PortfolioId":1,"FundId":2,"OrderId":123,"TradeId":id,
        "PrimaryTrade":primary,"TradeType":if primary {"ShortIronCondor"} else {"LongIronCondor"},
        "MaturityDate":if primary {Some("2026-11-20")} else {None},
        "RequestedMaturityDate":"2026-12-18","ExecutionOrderId":700,"ExecutionTradeId":455}),
    )
    .unwrap()
}
#[test]
fn close_inherits_actual_opening_maturity_instead_of_its_own_requested_date() {
    let primary = trade(true, 455);
    let mut close = trade(false, 456);
    let opening = order_close::inherit_opening_trade(&mut close, &[primary])
        .unwrap()
        .unwrap();
    assert_eq!(
        close.requested_maturity_date.unwrap().as_str(),
        "2026-11-20"
    );
    assert_eq!(order_close::primary_scope(&opening).unwrap().trade, 455);
}
#[test]
fn close_uses_requested_opening_maturity_when_execution_has_not_populated_actual_maturity() {
    let mut primary = trade(true, 455);
    primary.maturity_date = None;
    let mut close = trade(false, 456);
    order_close::inherit_opening_trade(&mut close, &[primary]).unwrap();
    assert_eq!(
        close.requested_maturity_date.unwrap().as_str(),
        "2026-12-18"
    );
}
#[test]
fn missing_or_ambiguous_opening_trade_is_an_error() {
    let mut close = trade(false, 456);
    assert!(order_close::inherit_opening_trade(&mut close, &[]).is_err());
    assert!(
        order_close::inherit_opening_trade(&mut close, &[trade(true, 455), trade(true, 454)])
            .is_err()
    );
    let mut primary = trade(true, 455);
    primary.maturity_date = None;
    primary.requested_maturity_date = None;
    assert!(order_close::inherit_opening_trade(&mut close, &[primary]).is_err());
}
fn position() -> StrategyPositionSnapshot {
    let row = |id: &str, strike: &str, call: u8, quantity: i32| {
        json!({"TradeLegId":uuid::Uuid::new_v4().to_string(),
        "ContractId":id,"AssetFamily":"FuturesOption","SignedQuantity":quantity,
        "Expiry":"2026-11-20","Strike":strike,"PutCall":call,"CashMultiplier":"50"})
    };
    serde_json::from_value(json!({"IsOpen":true,"Legs":[row("C1","6100",1,-3),row("C2","6150",1,3),row("P1","5900",2,-3),row("P2","5850",2,3)]})).unwrap()
}
#[test]
fn close_reverses_original_contracts_quantities_and_strikes_without_delta_reselection() {
    let date = chrono::NaiveDate::from_ymd_opt(2026, 11, 20).unwrap();
    let (quantity, legs) =
        order_close::reversed_legs(&position(), &ExactDecimal::parse("50").unwrap(), Some(date))
            .unwrap();
    assert_eq!(quantity, 3);
    assert_eq!(
        legs.iter()
            .map(|leg| (
                leg.quote.contract.as_str(),
                leg.quote.side,
                leg.role.as_str()
            ))
            .collect::<Vec<_>>(),
        vec![
            ("C1", 1, "+LC"),
            ("C2", -1, "-SC"),
            ("P1", 1, "+LP"),
            ("P2", -1, "-SP")
        ]
    );
    assert_eq!(legs[0].quote.strike.as_ref().unwrap().as_str(), "6100");
    assert!(
        order_close::reversed_legs(
            &position(),
            &ExactDecimal::parse("50").unwrap(),
            Some(date.succ_opt().unwrap())
        )
        .is_err()
    );
    let mut closed = position();
    closed.is_open = Some(false);
    assert!(
        order_close::reversed_legs(&closed, &ExactDecimal::parse("50").unwrap(), Some(date))
            .is_err()
    );
}

#[test]
fn closed_close_loads_original_position_without_replacing_close_execution() {
    let mut primary = trade(true, 1201);
    primary.execution_order_id = Some(1901);
    primary.execution_trade_id = Some(1501);
    primary.trade_state = Some("Closed".into());
    let mut close = trade(false, 4202);
    close.execution_order_id = Some(3901);
    close.execution_trade_id = Some(1501);
    close.trade_state = Some("Closed".into());
    let monitored = order_close::monitor_trade(&close, &[primary.clone(), close.clone()]).unwrap();
    let scope = order_close::primary_scope(&monitored).unwrap();
    assert_eq!((scope.order, scope.trade), (1901, 1501));
    assert_eq!(monitored.trade_type.as_deref(), Some("ShortIronCondor"));
    assert_eq!(close.execution_order_id, Some(3901));
    assert_eq!(close.trade_id, Some(4202));
    let opening = order_close::monitor_trade(&primary, &[]).unwrap();
    assert_eq!(order_close::primary_scope(&opening).unwrap().order, 1901);
    assert!(order_close::monitor_trade(&close, &[]).is_err());
    primary.fund_id = Some(999);
    assert!(order_close::monitor_trade(&close, &[primary]).is_err());
}
