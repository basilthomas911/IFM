//! Exact C# unified futures-engine numerical oracle, no blanket tolerances.
use ifm_market_data_native::{
    pricing::{Calculator, EuropeanKernel, Failure, Request, Settings, Values},
    runtime::{Deadline, Interrupted},
};
use serde_json::Value;
use tokio_util::sync::CancellationToken;
fn bits(v: Values) -> [u64; 7] {
    [
        v.price,
        v.volatility,
        v.delta,
        v.gamma,
        v.vega,
        v.theta,
        v.rho,
    ]
    .map(f64::to_bits)
}
fn check(actual: Result<Values, Failure>, case: &Value, name: &str, index: usize) -> bool {
    match actual {
        Ok(v) => {
            let expected: Vec<u64> = case[name]
                .as_array()
                .map(|a| a.iter().map(|x| x.as_u64().unwrap()).collect())
                .unwrap_or_default();
            let actual = bits(v);
            if actual.as_slice() != expected {
                eprintln!("case={index} operation={name} expected={expected:?} actual={actual:?}");
                false
            } else {
                true
            }
        }
        Err(e) => {
            let expected = case[format!("{name}Failure")].as_str().unwrap();
            if format!("{e:?}") != expected {
                eprintln!("case={index} {name} expectedFailure={expected} actual={e:?}");
                false
            } else {
                true
            }
        }
    }
}
#[tokio::test]
async fn futures_prices_greeks_and_iv_match_csharp_bits() {
    run_oracle(
        include_str!("../baseline/PricingOracle.json"),
        EuropeanKernel::ManagedCompatibility,
    )
    .await;
    run_oracle(
        include_str!("../baseline/LegacyPricingOracle.json"),
        EuropeanKernel::LegacyRustCompatibility,
    )
    .await;
}
async fn run_oracle(json: &str, kernel: EuropeanKernel) {
    let cases: Vec<Value> = serde_json::from_str(json).unwrap();
    let token = CancellationToken::new();
    let mut mismatches = 0;
    for (index, c) in cases.iter().enumerate() {
        let d = |key: &str| f64::from_bits(c[key].as_u64().unwrap());
        let b = |key: &str| c[key].as_bool().unwrap();
        let r = Request {
            forward: d("forward"),
            strike: d("strike"),
            time: d("time"),
            rate: d("rate"),
            call: b("call"),
            american: b("american"),
            futures_style: b("futuresStyle"),
        };
        let calc = Calculator::with_kernel(
            Settings {
                steps: c["steps"].as_u64().unwrap() as usize,
                ..Default::default()
            },
            kernel,
        )
        .unwrap();
        let result = calc
            .theoretical(r, d("sigma"), &token, Deadline::recovery())
            .await;
        match result {
            Ok(p) => {
                if Some(p.to_bits()) != c["theoretical"].as_u64() {
                    eprintln!(
                        "case={index} theoretical expected={} actual={}",
                        c["theoretical"],
                        p.to_bits()
                    );
                    mismatches += 1;
                }
            }
            Err(e) => {
                if format!("{e:?}") != c["theoreticalFailure"].as_str().unwrap() {
                    mismatches += 1;
                }
            }
        }
        if !check(
            calc.price(r, d("sigma"), &token, Deadline::recovery())
                .await,
            c,
            "price",
            index,
        ) {
            mismatches += 1;
        }
        if !check(
            calc.implied(r, d("market"), &token, Deadline::recovery())
                .await,
            c,
            "iv",
            index,
        ) {
            mismatches += 1;
        }
        match calc
            .price_delta(r, d("sigma"), &token, Deadline::recovery())
            .await
        {
            Ok(value) => assert_eq!(
                vec![value.price.to_bits(), value.delta.to_bits()],
                c["delta"]
                    .as_array()
                    .unwrap()
                    .iter()
                    .map(|v| v.as_u64().unwrap())
                    .collect::<Vec<_>>(),
                "case {index} price/delta"
            ),
            Err(error) => assert_eq!(
                format!("{error:?}"),
                c["deltaFailure"].as_str().unwrap(),
                "case {index} price/delta failure"
            ),
        }
        match calc
            .solve(r, d("market"), &token, Deadline::recovery())
            .await
        {
            Ok(value) => assert_eq!(
                vec![
                    value.volatility.to_bits(),
                    value.price.to_bits(),
                    value.error.to_bits(),
                    u64::from(value.iterations)
                ],
                c["solve"]
                    .as_array()
                    .unwrap()
                    .iter()
                    .map(|v| v.as_u64().unwrap())
                    .collect::<Vec<_>>(),
                "case {index} IV-only"
            ),
            Err(error) => assert_eq!(
                format!("{error:?}"),
                c["solveFailure"].as_str().unwrap(),
                "case {index} IV-only failure"
            ),
        }
    }
    assert_eq!(
        mismatches, 0,
        "unexplained exact numerical differences fail acceptance"
    );
}
#[tokio::test]
async fn tree_and_solver_cancel_and_do_not_reset_expired_budgets() {
    let calc = Calculator::new(Settings {
        steps: 4096,
        ..Default::default()
    })
    .unwrap();
    let token = CancellationToken::new();
    let r = Request {
        forward: 6500.0,
        strike: 6500.0,
        time: 0.1,
        rate: 0.04,
        call: false,
        american: true,
        futures_style: false,
    };
    let cancel = token.clone();
    tokio::spawn(async move {
        tokio::task::yield_now().await;
        cancel.cancel();
    });
    assert_eq!(
        calc.price(r, 0.2, &token, Deadline::recovery()).await,
        Err(Failure::Interrupted(Interrupted::Cancelled))
    );
    assert_eq!(
        calc.implied(
            r,
            150.0,
            &CancellationToken::new(),
            Deadline::after(std::time::Duration::ZERO)
        )
        .await,
        Err(Failure::Interrupted(Interrupted::DeadlineExpired))
    );
}
