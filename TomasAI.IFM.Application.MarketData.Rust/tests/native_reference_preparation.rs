//! Full current C# reference admission, MessagePack and semantic identity oracle.
use base64::Engine;
use ifm_market_data_native::{
    option_chain::qualification::Decimal,
    reference::{
        semantic,
        treasury_curve::{Curve, Point},
        treasury_policy::{Conversion, Observation, Rate},
    },
};
#[test]
fn current_csharp_reference_results_and_both_digests_match() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ReferencePreparationBridge/rates.json"
    ))
    .unwrap();
    for row in rows.as_array().unwrap() {
        let curve = Curve {
            date: time::Date::from_julian_day(row["day"].as_i64().unwrap() as i32 + 1721426)
                .unwrap(),
            retrieved: row["retrieved"].as_i64().unwrap(),
            rates: row["rates"]
                .as_array()
                .unwrap()
                .iter()
                .map(|point| Point {
                    months: point["tenor"].as_u64().unwrap() as u16,
                    percentage: Decimal {
                        bits: std::array::from_fn(|i| point["words"][i].as_u64().unwrap() as u32),
                    },
                })
                .collect(),
        };
        let observation = Observation {
            curve,
            source: row["Source"].as_str().unwrap().into(),
            country: row["CountryCode"].as_str().unwrap().into(),
            currency: row["CurrencyCode"].as_str().unwrap().into(),
            offset_minutes: row["offset"].as_i64().unwrap() as i32,
        };
        let policy = &row["policy"];
        let policy = Conversion {
            source: policy["Source"].as_str().unwrap().into(),
            series: policy["SourceSeriesId"].as_str().unwrap().into(),
            convention: policy["Convention"].as_i64().unwrap() as i32,
            version: policy["Version"].as_str().unwrap().into(),
            evidence: policy["EvidenceId"].as_str().unwrap().into(),
        };
        assert_eq!(
            observation.digest().unwrap(),
            row["curveDigest"].as_str().unwrap(),
            "curve {}",
            row["name"]
        );
        let result = Rate::convert(&observation, row["tenor"].as_u64().unwrap() as u16, &policy);
        assert_eq!(
            result.as_ref().err().copied(),
            row["error"].as_str(),
            "{}",
            row["name"]
        );
        if let Ok(rate) = result {
            let mut actual = Vec::new();
            rmpv::encode::write_value(&mut actual, &rate.value().unwrap()).unwrap();
            let expected = base64::engine::general_purpose::STANDARD
                .decode(row["value"].as_str().unwrap())
                .unwrap();
            assert_eq!(actual, expected, "rate DTO {}", row["name"]);
            assert_eq!(
                semantic::compute(&rate.semantic().unwrap()).unwrap(),
                row["semanticDigest"].as_str().unwrap(),
                "semantic {}",
                row["name"]
            );
        }
    }
}
