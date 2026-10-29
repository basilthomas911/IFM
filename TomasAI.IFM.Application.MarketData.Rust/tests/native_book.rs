//! Native input-to-cache/session integration, including stale/reset boundaries.
use ifm_market_data_native::{
    dataset::{
        book::{Book, Update},
        records::{Header, Observation, Quote, Trade},
    },
    latest::Asset,
};
fn date() -> time::Date {
    time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap()
}
fn header(sequence: u32, replay: bool) -> Header {
    Header {
        instrument: 42,
        publisher: 1,
        sequence,
        event_ns: 123456789,
        received_ns: 123456799,
        flags: if replay { 2 } else { 0 },
        schema: 0,
    }
}
fn trade(sequence: u32, replay: bool) -> Observation {
    Observation::Trade(Trade {
        header: header(sequence, replay),
        price: 6500000000000,
        size: 7,
        action: b'T',
        side: b'N',
        dbn_flags: 0,
        depth: 0,
        delta_ns: 0,
    })
}
#[test]
fn replay_seeds_authoritative_volume_without_publishing_stale_live_prices() {
    let mut book = Book::<()>::new(date(), 1, 2).unwrap();
    let reader = book.register(42, "ES20261218", Asset::Futures, 4).unwrap();
    assert_eq!(book.apply(1, trade(1, true), 0).unwrap(), Update::Unchanged);
    assert!(reader.trade().unwrap().is_none());
    assert_eq!(book.complete_replay(1, 42, 99).unwrap().unwrap().volume, 7);
    assert_eq!(
        book.apply(1, trade(2, false), 0).unwrap(),
        Update::Trade {
            price_changed: true
        }
    );
    assert_eq!(book.statistics(42, date()).unwrap().volume, 14);
    assert_eq!(reader.trade().unwrap().unwrap().value.size, 7);
    assert_eq!(
        book.apply(1, trade(2, false), 0).unwrap(),
        Update::Unchanged
    );
    assert_eq!(book.statistics(42, date()).unwrap().volume, 14);
    let quote = Observation::Quote(Quote {
        header: header(3, false),
        bid: 6500000000000,
        ask: 6500250000000,
        bid_size: 5,
        ask_size: 6,
        bid_count: 1,
        ask_count: 2,
    });
    assert_eq!(book.apply(1, quote, 99).unwrap(), Update::Quote);
    let q = reader.quote().unwrap().unwrap();
    assert_eq!(q.event_ticks, 1234567);
    assert_eq!(q.value.local_received_ticks, 99);
    assert_eq!(
        book.apply(2, trade(4, false), 0).unwrap(),
        Update::Unchanged
    );
    assert_eq!(book.statistics(42, date()).unwrap().volume, 14);
    book.retire().unwrap();
    assert!(reader.quote().unwrap().is_none());
    assert!(reader.trade().unwrap().is_none());
    assert_eq!(
        book.apply(1, trade(5, false), 0).unwrap(),
        Update::Unchanged
    );
    assert!(book.statistics(42, date()).is_none());
    let mut replacement = Book::<()>::new(date(), 2, 1).unwrap();
    replacement
        .register(42, "ES20261218", Asset::Futures, 4)
        .unwrap();
    replacement.apply(2, trade(1, false), 0).unwrap();
    assert!(reader.trade().unwrap().is_none());
}
#[test]
fn duplicate_registration_is_atomic_and_raii_retires_retained_readers() {
    let mut book = Book::<()>::new(date(), 1, 2).unwrap();
    let reader = book.register(42, "ES20261218", Asset::Futures, 4).unwrap();
    assert!(book.register(42, "OTHER", Asset::Option, 4).is_err());
    assert!(book.register(43, "ES20261218", Asset::Option, 4).is_err());
    assert!(book.reader("OTHER").unwrap().is_none());
    assert_eq!(book.contract_id(42), Some("ES20261218"));
    book.apply(1, trade(1, false), 0).unwrap();
    drop(book);
    assert!(reader.trade().unwrap().is_none());
}

#[test]
fn queued_publication_captures_do_not_read_later_mutable_prices_or_aggregates() {
    use ifm_market_data_native::ffi::publication::NativePublication;
    assert_eq!(std::mem::size_of::<NativePublication>(), 656);
    let mut book = Book::<()>::new(date(), 1, 2).unwrap();
    book.register(42, "ES20261218", Asset::Futures, 4).unwrap();
    book.complete_replay(1, 42, 99).unwrap();
    let first = book.apply(1, trade(1, false), 0).unwrap();
    let first = book.publication_state(42, first);
    let second = book.apply(1, trade(2, false), 0).unwrap();
    let second = book.publication_state(42, second);
    assert_eq!(first.session.unwrap().volume, 7);
    assert_eq!(second.session.unwrap().volume, 14);
    assert_eq!(first.market.unwrap().trade.unwrap().value.ordinal, 1);
    // Ordered execution publications include unchanged prices, independently
    // of the filtered query/display cache.
    assert_eq!(second.market.unwrap().trade.unwrap().value.ordinal, 2);
    assert_eq!(
        book.market_price(42).unwrap().trade.unwrap().value.ordinal,
        1
    );
    assert_eq!(
        second.update,
        Some(Update::Trade {
            price_changed: false
        })
    );
    book.retire().unwrap();
    assert_eq!(first.session.unwrap().volume, 7);
    assert_eq!(second.session.unwrap().volume, 14);
}

#[test]
fn every_accepted_trade_advances_vwap_even_when_price_publication_is_suppressed() {
    let mut book = Book::<()>::new(date(), 1, 1).unwrap();
    book.register_with_policy(42, "ES20261218", Asset::Futures, 4, Some(250000000), true)
        .unwrap();
    book.apply(1, trade(1, true), 0).unwrap();
    assert_eq!(book.vwap(42).unwrap().volume, 7);
    assert!(book.market_price(42).is_none());
    book.complete_replay(1, 42, 99).unwrap();
    assert_eq!(
        book.apply(1, trade(2, false), 0).unwrap(),
        Update::Trade {
            price_changed: true
        }
    );
    assert_eq!(
        book.apply(1, trade(3, false), 0).unwrap(),
        Update::Trade {
            price_changed: false
        }
    );
    let checkpoint = book.vwap(42).unwrap();
    assert_eq!(
        (checkpoint.volume, checkpoint.eligible, checkpoint.ordinal),
        (21, 3, 2)
    );
    assert!(checkpoint.valid && checkpoint.replay_complete);
    let price = book.market_price(42).unwrap().trade.unwrap();
    assert_eq!(price.sequence, 2);
    assert_eq!((price.value.epoch, price.value.ordinal), (99, 1));
    assert_eq!(
        book.reader("ES20261218")
            .unwrap()
            .unwrap()
            .trade()
            .unwrap()
            .unwrap()
            .sequence,
        3
    );
    assert_eq!(
        book.apply(1, trade(3, false), 0).unwrap(),
        Update::Unchanged
    );
    assert_eq!(book.vwap(42).unwrap(), checkpoint);
    book.retire().unwrap();
    assert!(book.vwap(42).is_none());
    assert!(book.market_price(42).is_none());
}
