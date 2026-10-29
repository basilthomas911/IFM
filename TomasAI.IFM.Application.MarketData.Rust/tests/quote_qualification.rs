//! Fixed quote validation, exact decimal boundaries and hostile ABI inputs.
use ifm_market_data_native::{
    ffi::pricing::ifm_market_data_qualify_quotes_v1,
    option_chain::qualification::{Decimal, Quote, Request, Result, validate},
};
use std::cmp::Ordering;
fn decimal(coefficient: u128, scale: u32, negative: bool) -> Decimal {
    Decimal {
        bits: [
            coefficient as u32,
            (coefficient >> 32) as u32,
            (coefficient >> 64) as u32,
            (scale << 16) | if negative { 1 << 31 } else { 0 },
        ],
    }
}
fn input() -> Request {
    let quote = Quote {
        generation_low: 1,
        event: 100_000_000,
        received: 100_000_000,
        sequence: 1,
        bid: decimal(1, 0, false),
        ask: decimal(2, 0, false),
        bid_size: decimal(1, 28, false),
        ask_size: decimal(0, 28, true),
        ..Default::default()
    };
    Request {
        size: 304,
        flags: 31,
        generation_low: 1,
        at: 100_000_000,
        maximum_age: 1000,
        maximum_skew: 250,
        strike: decimal(5000, 0, false),
        definition_strike: decimal(500000, 2, false),
        underlying: quote,
        option: quote,
        ..Default::default()
    }
}
#[test]
fn decimals_compare_without_overflow_or_scale_loss() {
    for scale in 0..=28 {
        for negative in [false, true] {
            assert_eq!(
                decimal(0, scale, negative).compare(Decimal::default()),
                Some(Ordering::Equal)
            );
            assert_eq!(
                decimal(1, scale, negative).compare(Decimal::default()),
                Some(if negative {
                    Ordering::Less
                } else {
                    Ordering::Greater
                })
            );
        }
        let maximum = (1_u128 << 96) - 1;
        assert_eq!(
            decimal(maximum, scale, false).compare(decimal(maximum, scale, false)),
            Some(Ordering::Equal)
        );
        assert_eq!(
            decimal(maximum, scale, false).compare(decimal(1, 28, false)),
            Some(Ordering::Greater)
        );
    }
    for a in 0..=28 {
        for b in 0..=28 {
            assert_eq!(
                decimal(10_u128.pow(a), a, false).compare(decimal(10_u128.pow(b), b, false)),
                Some(Ordering::Equal)
            );
        }
    }
    assert_eq!(
        Decimal {
            bits: [0, 0, 0, 29 << 16]
        }
        .compare(Decimal::default()),
        None
    );
    assert_eq!(
        Decimal { bits: [0, 0, 0, 1] }.compare(Decimal::default()),
        None
    );
}
#[test]
fn exact_thresholds_and_first_failure_match_current_validation_order() {
    let original = input();
    assert_eq!(validate(&original).unwrap().failure, 0);
    for delta in [-1, 0, 1] {
        let mut value = original;
        value.option.event -= 2_500_000 + delta;
        assert_eq!(
            validate(&value).unwrap().failure,
            if delta > 0 { 5 } else { 0 }
        );
        value.maximum_skew = 2000;
        value.option.event = value.at - 10_000_000 - delta;
        assert_eq!(
            validate(&value).unwrap().failure,
            if delta > 0 { 4 } else { 0 }
        );
        value = original;
        value.maximum_lead = 2000;
        value.option.event += 20_000_000 + delta;
        value.maximum_skew = 2000;
        assert_eq!(
            validate(&value).unwrap().failure,
            if delta > 0 { 3 } else { 0 }
        );
    }
    let mut value = original;
    value.option.bid = decimal(0, 28, true);
    assert_eq!(validate(&value).unwrap().failure, 3);
    value.option.generation_low = 2;
    value.flags = 0;
    value.underlying.event = 0;
    assert_eq!(validate(&value).unwrap().failure, 1);
    value.option.generation_low = 1;
    assert_eq!(validate(&value).unwrap().failure, 2);
    value.flags = 31;
    assert_eq!(validate(&value).unwrap().failure, 4); // underlying is checked first
    value = original;
    value.option.ask_size = decimal(1, 28, true);
    assert_eq!(validate(&value).unwrap().failure, 3);
    value = original;
    value.option.received_offset = 60;
    assert_eq!(validate(&value).unwrap().failure, 3);
}
#[test]
fn malformed_abi_preserves_output_and_valid_financial_failure_is_a_result() {
    assert_eq!(std::mem::size_of::<Request>(), 304);
    assert_eq!(std::mem::size_of::<Quote>(), 112);
    assert_eq!(std::mem::size_of::<Result>(), 16);
    let input = input();
    let sentinel = Result {
        size: 99,
        failure: 99,
        input: 99,
        reserved: 99,
    };
    let mut output = sentinel;
    // SAFETY: Valid accepted ranges are aligned, live, exclusive and nonoverlapping;
    // malformed pointers are rejected without dereferencing them.
    unsafe {
        assert_ne!(
            ifm_market_data_qualify_quotes_v1(std::ptr::null(), &mut output),
            0
        );
        assert_eq!(output, sentinel);
        for bad in 0..304 {
            let mut value = input;
            value.size = bad;
            assert_ne!(ifm_market_data_qualify_quotes_v1(&value, &mut output), 0);
            assert_eq!(output, sentinel);
        }
        for flags in [32, 255, u32::MAX] {
            let mut value = input;
            value.flags = flags;
            assert_ne!(ifm_market_data_qualify_quotes_v1(&value, &mut output), 0);
            assert_eq!(output, sentinel);
        }
        let mut value = input;
        value.option.bid.bits[3] = 29 << 16;
        assert_ne!(ifm_market_data_qualify_quotes_v1(&value, &mut output), 0);
        assert_eq!(output, sentinel);
        value = input;
        value.option.sequence = -1;
        assert_eq!(ifm_market_data_qualify_quotes_v1(&value, &mut output), 0);
        assert_eq!(output.failure, 3);
        assert_eq!(output.input, 1);
    }
}
