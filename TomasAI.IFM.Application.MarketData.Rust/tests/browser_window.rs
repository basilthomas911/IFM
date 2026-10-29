//! Actual managed selector outcomes qualify the native replacement, including
//! ordered membership, exact Decimal bounds, method labels and buffered scopes.
use ifm_market_data_native::{
    option_chain::{
        browser_window::{self, Method, Row},
        qualification::Decimal,
    },
    reference::decimal,
    runtime::Deadline,
};
use std::collections::BTreeSet;
use tokio_util::sync::CancellationToken;
fn bits(value: &serde_json::Value) -> Option<Decimal> {
    value.as_array().map(|words| Decimal {
        bits: std::array::from_fn(|i| words[i].as_u64().unwrap() as u32),
    })
}
fn dec(value: &serde_json::Value) -> Option<Decimal> {
    value.as_str().and_then(decimal::parse)
}
#[test]
fn all_managed_windows_and_subscription_buffers_match() {
    let fixtures: serde_json::Value = serde_json::from_slice(
        &zstd::decode_all(
            include_bytes!("../qualification/WindowOracle/windows.json.zst").as_slice(),
        )
        .unwrap(),
    )
    .unwrap();
    for (i, test) in fixtures.as_array().unwrap().iter().enumerate() {
        let rows = test["rows"]
            .as_array()
            .unwrap()
            .iter()
            .map(|row| Row {
                contract: row["ContractId"].as_str().unwrap(),
                strike: row["StrikePrice"].as_f64().unwrap(),
                kind: row["OptionType"].as_str().unwrap(),
            })
            .collect::<Vec<_>>();
        let required = test["required"]
            .as_array()
            .unwrap()
            .iter()
            .map(|v| v.as_str().unwrap().to_owned())
            .collect::<BTreeSet<_>>();
        let method = if test["mode"] == "iv" {
            Method::Implied {
                volatility: test["iv"].as_f64().unwrap(),
                seconds: test["seconds"].as_f64().unwrap(),
                z: 1.65,
                buffer: 0.15,
            }
        } else {
            Method::Bollinger {
                deviation: dec(&test["deviation"]),
                multiplier: test["multiplier"].as_f64().unwrap(),
            }
        };
        let stop = CancellationToken::new();
        let deadline = Deadline::recovery();
        let window = browser_window::select(
            &rows,
            dec(&test["price"]),
            method,
            &required,
            &stop,
            deadline,
        )
        .unwrap();
        let selected = window
            .rows
            .iter()
            .map(|n| rows[*n].contract)
            .collect::<Vec<_>>();
        let expected = test["selected"]
            .as_array()
            .unwrap()
            .iter()
            .map(|v| v.as_str().unwrap())
            .collect::<Vec<_>>();
        assert_eq!(selected, expected, "case {i}");
        assert_eq!(window.method, test["Method"].as_str().unwrap(), "case {i}");
        // CLR preserves irrelevant scale in arithmetic: compare numeric bounds here;
        // exact bits are separately qualified by the decimal arithmetic oracle.
        for (actual, expected) in [
            (window.lower, bits(&test["lower"])),
            (window.upper, bits(&test["upper"])),
        ] {
            assert_eq!(actual.is_some(), expected.is_some(), "case {i}");
            if let (Some(a), Some(b)) = (actual, expected) {
                assert_eq!(
                    a.compare(b),
                    Some(std::cmp::Ordering::Equal),
                    "case {i}: {:?} != {:?}",
                    a.bits,
                    b.bits
                );
            }
        }
        let buffered =
            browser_window::buffer(&rows, &window, dec(&test["wing"]).unwrap(), &stop, deadline)
                .unwrap();
        assert_eq!(
            buffered
                .iter()
                .map(|n| rows[*n].contract)
                .collect::<Vec<_>>(),
            test["buffer"]
                .as_array()
                .unwrap()
                .iter()
                .map(|v| v.as_str().unwrap())
                .collect::<Vec<_>>(),
            "buffer case {i}"
        );
    }
}
#[test]
fn invalid_inputs_and_original_cancellation_do_not_invent_windows() {
    let rows = [Row {
        contract: "one",
        strike: 6000.0,
        kind: "Call",
    }];
    let stop = CancellationToken::new();
    let required = BTreeSet::new();
    for multiplier in [f64::NAN, f64::INFINITY, 0.0, -1.0] {
        assert!(
            browser_window::select(
                &rows,
                decimal::parse("6000"),
                Method::Bollinger {
                    deviation: None,
                    multiplier
                },
                &required,
                &stop,
                Deadline::recovery()
            )
            .is_err()
        );
    }
    for value in [f64::NAN, f64::INFINITY, 0.0, -1.0] {
        assert!(
            browser_window::select(
                &rows,
                decimal::parse("6000"),
                Method::Implied {
                    volatility: value,
                    seconds: 1.0,
                    z: 1.65,
                    buffer: 0.15
                },
                &required,
                &stop,
                Deadline::recovery()
            )
            .is_err()
        );
    }
    stop.cancel();
    assert!(
        browser_window::select(
            &rows,
            decimal::parse("6000"),
            Method::Bollinger {
                deviation: None,
                multiplier: 2.5
            },
            &required,
            &stop,
            Deadline::recovery()
        )
        .is_err()
    );
}

#[test]
fn buffering_never_clips_over_capacity_and_nonfinite_rows_do_not_enter_display() {
    let names = (0..5000).map(|n| format!("ES-{n}")).collect::<Vec<_>>();
    let rows = names
        .iter()
        .enumerate()
        .map(|(i, id)| Row {
            contract: id,
            strike: (i + 1) as f64,
            kind: "Call",
        })
        .collect::<Vec<_>>();
    let stop = CancellationToken::new();
    let deadline = Deadline::recovery();
    let required = [names[0].clone(), names[4999].clone()]
        .into_iter()
        .collect();
    let window = browser_window::select(
        &rows,
        decimal::parse("2500"),
        Method::Bollinger {
            deviation: decimal::parse("10"),
            multiplier: 1.0,
        },
        &required,
        &stop,
        deadline,
    )
    .unwrap();
    assert_eq!(window.rows.len(), 23);
    assert_eq!(
        browser_window::buffer(&rows, &window, Decimal::default(), &stop, deadline).unwrap(),
        window.rows
    );
    let invalid = [
        Row {
            contract: "nan",
            strike: f64::NAN,
            kind: "Call",
        },
        Row {
            contract: "infinity",
            strike: f64::INFINITY,
            kind: "Call",
        },
        Row {
            contract: "negative",
            strike: -1.0,
            kind: "Call",
        },
    ];
    // Band arithmetic occurs after empty/unavailable-source checks in C#.
    let empty = browser_window::select(
        &invalid,
        None,
        Method::Bollinger {
            deviation: decimal::parse("79228162514264337593543950335"),
            multiplier: f64::MAX,
        },
        &BTreeSet::new(),
        &stop,
        deadline,
    )
    .unwrap();
    assert_eq!(empty.method, "Empty");
    let oversized = (0..100001)
        .map(|_| Row {
            contract: "one",
            strike: 1.0,
            kind: "Call",
        })
        .collect::<Vec<_>>();
    assert!(
        browser_window::select(
            &oversized,
            decimal::parse("1"),
            Method::Bollinger {
                deviation: None,
                multiplier: 1.0
            },
            &BTreeSet::new(),
            &stop,
            deadline
        )
        .is_err()
    );
}
#[test]
#[ignore = "Explicit fixture regeneration after running the unchanged managed WindowOracle"]
fn refresh_managed_fixture() {
    let input = std::env::var("IFM_WINDOW_ORACLE_JSON")
        .expect("Absolute path to WindowOracle JSON output is required");
    let bytes = std::fs::read(input).unwrap();
    let cases: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(cases.as_array().unwrap().len(), 1200);
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("qualification/WindowOracle/windows.json.zst");
    std::fs::write(path, zstd::encode_all(bytes.as_slice(), 9).unwrap()).unwrap();
}
