//! C# latest-price acceptance plus coherent concurrent reads and epoch retirement.
use ifm_market_data_native::latest::{Asset, Failure, Priced, QuoteValue, Store, Tick, TradeValue};
use serde_json::Value;
use std::sync::Arc;
fn date() -> time::Date {
    time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap()
}
#[test]
fn latest_store_matches_all_unchanged_csharp_transitions() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/LatestOracle.json")).unwrap();
    let store = Store::<i64>::new(date(), 1, 2).unwrap();
    let handle = store.register("O", Asset::Option).unwrap();
    for (index, c) in cases.iter().enumerate() {
        let n = |key: &str| c[key].as_i64().unwrap();
        let b = |key: &str| c[key].as_bool().unwrap();
        let trade = Tick {
            sequence: n("sequence"),
            event_ticks: n("ticks"),
            receive_ticks: n("ticks") + 1,
            value: TradeValue {
                price: n("risk"),
                size: 7,
            },
        };
        let quote = Tick {
            sequence: trade.sequence,
            event_ticks: trade.event_ticks,
            receive_ticks: trade.receive_ticks,
            value: QuoteValue {
                bid: Some(n("risk")),
                ask: Some(n("risk") + 1),
                bid_size: 2,
                ask_size: 4,
                bid_count: 3,
                ask_count: 5,
                local_received_ticks: trade.receive_ticks + 1,
            },
        };
        let changed = match c["operation"].as_str().unwrap() {
            "trade" => handle.update_trade(trade).unwrap(),
            "quote" => handle.update_quote(quote).unwrap(),
            "tradeRisk" => handle
                .update_trade_risk(Priced {
                    tick: trade,
                    greeks: Arc::new(n("risk")),
                })
                .unwrap(),
            "quoteRisk" => handle
                .update_quote_risk(Priced {
                    tick: quote,
                    greeks: Arc::new(n("risk")),
                })
                .unwrap(),
            "reset" => {
                store.reset().unwrap();
                false
            }
            "retire" => {
                store.retire().unwrap();
                false
            }
            _ => panic!("unknown transition"),
        };
        assert_eq!(changed, b("changed"), "transition {index}");
        let t = handle.trade().unwrap();
        let q = handle.quote().unwrap();
        let tr = handle.trade_risk().unwrap();
        let qr = handle.quote_risk().unwrap();
        assert_eq!(
            [t.is_some(), q.is_some(), tr.is_some(), qr.is_some()],
            [
                b("hasTrade"),
                b("hasQuote"),
                b("hasTradeRisk"),
                b("hasQuoteRisk")
            ],
            "transition {index}"
        );
        assert_eq!(
            [
                t.map_or(0, |v| v.sequence),
                t.map_or(0, |v| v.event_ticks),
                t.map_or(0, |v| v.value.price),
                q.map_or(0, |v| v.sequence),
                q.map_or(0, |v| v.event_ticks),
                q.and_then(|v| v.value.bid).unwrap_or(0),
                tr.map_or(0, |v| *v.greeks),
                qr.map_or(0, |v| *v.greeks)
            ],
            [
                n("tradeSequence"),
                n("tradeTicks"),
                n("tradePrice"),
                n("quoteSequence"),
                n("quoteTicks"),
                n("quotePrice"),
                n("tradeRisk"),
                n("quoteRisk")
            ],
            "transition {index}"
        );
    }
}
#[test]
fn capacities_dates_assets_and_retained_handles_are_enforced() {
    let store = Store::<i64>::new(date(), 123, 1).unwrap();
    let handle = store.register("F", Asset::Futures).unwrap();
    assert!(matches!(
        store.register("X", Asset::Option),
        Err(Failure::Capacity)
    ));
    assert!(matches!(
        store.register("F", Asset::Option),
        Err(Failure::AssetMismatch)
    ));
    assert!(store.lookup("F", date(), 124).unwrap().is_none());
    assert!(
        store
            .lookup("F", date().next_day().unwrap(), 123)
            .unwrap()
            .is_none()
    );
    let tick = Tick {
        sequence: 1,
        event_ticks: 1,
        receive_ticks: 2,
        value: TradeValue { price: 1, size: 1 },
    };
    assert!(
        !handle
            .update_trade_risk(Priced {
                tick,
                greeks: Arc::new(1)
            })
            .unwrap()
    );
    assert!(handle.update_trade(tick).unwrap());
    store.retire().unwrap();
    assert!(handle.trade().unwrap().is_none());
    assert!(!handle.update_trade(tick).unwrap());
    assert!(matches!(
        store.register("F", Asset::Futures),
        Err(Failure::Retired)
    ));
    let replacement = Store::<i64>::new(date(), 124, 1)
        .unwrap()
        .register("F", Asset::Futures)
        .unwrap();
    assert!(replacement.update_trade(tick).unwrap());
    assert!(handle.trade().unwrap().is_none());
}
#[test]
fn concurrent_risk_reads_never_mix_observation_and_payload() {
    let store = Store::<i64>::new(date(), 1, 1).unwrap();
    let slot = store.register("O", Asset::Option).unwrap();
    let writer = slot.clone();
    let task = std::thread::spawn(move || {
        for sequence in 1..=100_000 {
            writer
                .update_trade_risk(Priced {
                    tick: Tick {
                        sequence,
                        event_ticks: sequence,
                        receive_ticks: sequence,
                        value: TradeValue {
                            price: sequence,
                            size: 1,
                        },
                    },
                    greeks: Arc::new(sequence),
                })
                .unwrap();
        }
    });
    for _ in 0..100_000 {
        if let Some(risk) = slot.trade_risk().unwrap() {
            assert_eq!(risk.tick.sequence, *risk.greeks);
            assert_eq!(risk.tick.value.price, *risk.greeks);
        }
    }
    task.join().unwrap();
    store.retire().unwrap();
    assert!(slot.trade_risk().unwrap().is_none());
}
