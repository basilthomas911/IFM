//! DBN boundary checks for wire precision, sentinel flags and buffer independence.

use databento::dbn::{
    FlagSet, MboMsg, Mbp1Msg, RecordRef, Schema, StatMsg, SystemMsg, TradeMsg, UNDEF_PRICE,
};
use ifm_market_data_native::dataset::records::{Observation, normalize};

#[test]
fn quote_and_order_copies_preserve_units_flags_and_every_payload_field() {
    let mut quote = Mbp1Msg {
        sequence: 17,
        ts_recv: u64::MAX,
        flags: FlagSet::new(32),
        ..Mbp1Msg::default()
    };
    quote.hd.instrument_id = 42;
    quote.hd.publisher_id = 3;
    quote.hd.ts_event = 900;
    quote.levels[0].bid_px = -100;
    quote.levels[0].ask_px = UNDEF_PRICE;
    quote.levels[0].bid_sz = 9;
    quote.levels[0].ask_sz = 10;
    quote.levels[0].bid_ct = 11;
    quote.levels[0].ask_ct = 12;
    let copied = normalize(RecordRef::from(&quote), false, false).unwrap();
    // The source may be overwritten immediately after returning from normalize.
    quote.levels[0].bid_px = 999;
    assert_eq!(quote.levels[0].bid_px, 999);
    let Observation::Quote(value) = copied else {
        panic!("wrong record kind")
    };
    assert_eq!(
        (
            value.bid,
            value.ask,
            value.bid_size,
            value.ask_size,
            value.bid_count,
            value.ask_count
        ),
        (-100, 0, 9, 10, 11, 12)
    );
    assert_eq!(
        (
            value.header.instrument,
            value.header.publisher,
            value.header.flags,
            value.header.sequence,
            value.header.event_ns,
            value.header.received_ns,
            value.header.schema
        ),
        (42, 3, 5, 17, 900, -1, Schema::Mbp1 as u16)
    );
    let mut order = MboMsg {
        order_id: u64::MAX,
        price: UNDEF_PRICE,
        size: 15,
        action: b'M' as _,
        side: b'A' as _,
        flags: FlagSet::new(32 | 128),
        channel_id: 8,
        ts_in_delta: -7,
        sequence: 19,
        ..MboMsg::default()
    };
    order.hd.instrument_id = 43;
    let Observation::Order(value) = normalize(RecordRef::from(&order), false, false).unwrap()
    else {
        panic!("wrong record kind")
    };
    assert_eq!(
        (
            value.order_id,
            value.price,
            value.size,
            value.action,
            value.side,
            value.dbn_flags,
            value.channel,
            value.delta_ns
        ),
        (u64::MAX, 0, 15, b'M', b'A', 160, 8, -7)
    );
    assert_eq!(
        (
            value.header.instrument,
            value.header.sequence,
            value.header.flags,
            value.header.schema
        ),
        (43, 19, 5, Schema::Mbo as u16)
    );
}

#[test]
fn replay_and_undefined_sentinels_remain_distinct_from_live_zero_prices() {
    let mut trade = TradeMsg {
        price: UNDEF_PRICE,
        flags: FlagSet::new(32),
        size: 20,
        depth: 1,
        ts_in_delta: -30,
        action: b'T' as _,
        side: b'B' as _,
        ..TradeMsg::default()
    };
    trade.hd.instrument_id = 44;
    let Observation::Trade(value) = normalize(RecordRef::from(&trade), false, true).unwrap() else {
        panic!("wrong record kind")
    };
    assert_eq!(
        (
            value.header.flags,
            value.price,
            value.size,
            value.depth,
            value.delta_ns,
            value.action,
            value.side,
            value.dbn_flags
        ),
        (7, 0, 20, 1, -30, b'T', b'B', 32)
    );
    let stat = StatMsg {
        price: -50,
        quantity: i64::MAX,
        ts_ref: u64::MAX,
        stat_type: 6,
        channel_id: 22,
        update_action: 1,
        stat_flags: 7,
        sequence: 100,
        ..StatMsg::default()
    };
    let Observation::Statistic(value) = normalize(RecordRef::from(&stat), true, false).unwrap()
    else {
        panic!("wrong record kind")
    };
    assert_eq!(
        (
            value.price,
            value.quantity,
            value.reference_ns,
            value.kind,
            value.channel,
            value.action,
            value.flags,
            value.header.flags,
            value.header.sequence
        ),
        (-50, i64::MAX, -1, 6, 22, 1, 7, 2, 100)
    );
    assert!(normalize(RecordRef::from(&SystemMsg::heartbeat(123)), false, false).is_none());
}
