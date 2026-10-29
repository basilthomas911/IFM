//! Cold copied reference import. This accepts reviewed configuration, not a
//! managed-prepared numeric context or resource-owning callback. Native code
//! qualifies it, acquires references and rebuilds future generations itself.
use super::runtime::{Instance, Status};
use crate::option_chain::{
    convention::Convention,
    preparation::{Blueprint, Definition, References, ReviewedScope},
    qualification::Decimal,
};
use serde_json::Value;
use std::sync::Arc;
fn text(v: &Value) -> Result<String, Status> {
    v.as_str()
        .filter(|s| !s.trim().is_empty() && s.len() <= 16384)
        .map(str::to_owned)
        .ok_or(Status::InvalidInput)
}
fn integer(v: &Value) -> Result<i64, Status> {
    v.as_i64().ok_or(Status::InvalidInput)
}
fn number(v: &Value) -> Result<u32, Status> {
    u32::try_from(integer(v)?).map_err(|_| Status::InvalidInput)
}
fn decimal(v: &Value) -> Result<Decimal, Status> {
    let words = v
        .as_array()
        .filter(|a| a.len() == 4)
        .ok_or(Status::InvalidInput)?;
    let mut bits = [0; 4];
    for (part, word) in bits.iter_mut().zip(words) {
        *part = number(word)?;
    }
    let value = Decimal { bits };
    value
        .compare(Decimal::default())
        .ok_or(Status::InvalidInput)?;
    Ok(value)
}
fn contract(v: &Value) -> Result<Convention, Status> {
    Convention::from_command(v).map_err(|_| Status::InvalidInput)
}
/// Imports one complete bounded native scope intent using existing source/time
/// services. Validation opens no provider, broker or HTTP connection.
pub(super) fn parse(v: &Value, instance: &Instance) -> Result<Arc<ReviewedScope>, Status> {
    parse_inner(v, instance, None).map(|(scope, _)| scope)
}
/// Builds every per-option convention/calendar/identity from raw copied models.
/// No managed preparation, pricing context or subscription lease is imported.
pub(super) fn parse_raw(
    v: &Value,
    instance: &Instance,
    date: time::Date,
    models: &[crate::reference::option_contract::OptionContract],
) -> Result<Arc<ReviewedScope>, Status> {
    if models.is_empty() || models.len() > 2048 {
        return Err(Status::InvalidInput);
    }
    parse_inner(v, instance, Some((date, models))).map(|(scope, _)| scope)
}
/// Parses dataset-global native policy before background definitions exist.
/// No placeholder convention/calendar or managed preparation is manufactured.
pub(super) fn parse_background(
    v: &Value,
    instance: &Instance,
    date: time::Date,
) -> Result<
    (
        Arc<ReviewedScope>,
        crate::option_chain::preparation::RawPolicy,
    ),
    Status,
> {
    parse_inner(v, instance, Some((date, &[])))
}
fn parse_inner(
    v: &Value,
    instance: &Instance,
    raw: Option<(
        time::Date,
        &[crate::reference::option_contract::OptionContract],
    )>,
) -> Result<
    (
        Arc<ReviewedScope>,
        crate::option_chain::preparation::RawPolicy,
    ),
    Status,
> {
    let calendar = if raw.is_some() {
        None
    } else {
        Some(crate::reference::inputs::calendar(&v["calendar"]).map_err(|_| Status::InvalidInput)?)
    };
    let policy = crate::reference::inputs::policy(v).map_err(|_| Status::InvalidInput)?;
    let publications = policy.publications.clone();
    let conversion = policy.conversion.clone();
    let maximum_lead = policy.maximum_lead;
    let definitions = if let Some((date, models)) = raw {
        if models.is_empty() {
            vec![]
        } else {
            policy
                .definitions(date, models)
                .map_err(|_| Status::InvalidInput)?
        }
    } else {
        let options = v["options"]
            .as_array()
            .filter(|a| !a.is_empty() && a.len() <= 2048)
            .ok_or(Status::InvalidInput)?;
        options
            .iter()
            .map(|row| {
                Ok(Definition {
                    instrument: number(&row["instrument"])?,
                    underlying: number(&row["underlying"])?,
                    strike: decimal(&row["strike"])?,
                    call: row["call"].as_bool().ok_or(Status::InvalidInput)?,
                    required: row["required"].as_bool().ok_or(Status::InvalidInput)?,
                    blueprint: Blueprint {
                        contract: contract(&row["contract"])?,
                        calendar: calendar.as_ref().ok_or(Status::InvalidInput)?.clone(),
                        publications: publications.clone(),
                        conversion: conversion.clone(),
                        maximum_lead,
                    },
                })
            })
            .collect::<Result<Vec<_>, Status>>()?
    };
    let source = instance
        .treasury
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .clone()
        .ok_or(Status::InvalidInput)?;
    let clock = instance.reference_clock()?;
    let statistics = super::scope_pricing::statistics(&v["statistics"])?;
    Ok((
        Arc::new(ReviewedScope {
            manifests: instance.manifests.clone(),
            references: Arc::new(References::new(source, clock)),
            definitions,
            production: v["production"].as_bool().ok_or(Status::InvalidInput)?,
            production_approved: v["production_approved"]
                .as_bool()
                .ok_or(Status::InvalidInput)?,
            review_evidence: if v["review_evidence"].is_null() {
                None
            } else {
                Some(text(&v["review_evidence"])?)
            },
            policy_version: text(&v["policy_version"])?,
            selection_ms: number(&v["selection_ms"])?,
            iv_ms: number(&v["iv_ms"])?,
            risk_ms: number(&v["risk_ms"])?,
            maximum_contracts: usize::try_from(number(&v["maximum_contracts"])?)
                .map_err(|_| Status::InvalidInput)?,
            batch: usize::try_from(number(&v["batch"])?).map_err(|_| Status::InvalidInput)?,
            statistics,
        }),
        policy,
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::messaging::codec;
    use crate::{
        ffi::{
            live::ifm_market_data_clock_v1, reference::ifm_market_data_reference_configure_v1,
            runtime::*,
        },
        option_chain::context::Context,
        reference::{
            decimal,
            treasury_curve::{Curve, Point},
            treasury_policy::Observation,
        },
    };
    use base64::Engine;
    struct Runtime(u64);
    impl Runtime {
        fn new(at: i64) -> Self {
            let mut handle = 0;
            // SAFETY: Exclusive aligned handle; configuration bytes are bounded
            // and remain readable until the synchronous configure call returns.
            unsafe {
                assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 1, &mut handle), 0);
                assert_eq!(ifm_market_data_clock_v1(handle, at, 0), 0);
                let config = br#"{"endpoint":"http://127.0.0.1:1/curve","dns":["127.0.0.1:1"]}"#;
                assert_eq!(
                    ifm_market_data_reference_configure_v1(
                        handle,
                        config.as_ptr(),
                        config.len() as u32
                    ),
                    0
                );
            }
            Self(handle)
        }
    }
    impl Drop for Runtime {
        fn drop(&mut self) {
            assert_eq!(ifm_market_data_runtime_stop_v1(self.0), 0);
            assert_eq!(ifm_market_data_runtime_release_v1(self.0), 0);
        }
    }
    fn rows() -> Value {
        serde_json::from_str(include_str!(
            "../../qualification/ContextWireBridge/contexts.json"
        ))
        .unwrap()
    }
    #[test]
    fn actual_csharp_raw_adapter_preserves_complete_context_and_digest() {
        let rows = rows();
        let runtime = Runtime::new(rows[0]["at"].as_i64().unwrap());
        let instance = instance(runtime.0).unwrap();
        for row in rows.as_array().unwrap() {
            let reviewed = parse(&row["raw"], &instance).unwrap();
            let definition = &reviewed.definitions[0];
            let at = row["at"].as_i64().unwrap();
            let expected = base64::engine::general_purpose::STANDARD
                .decode(row["contract"].as_str().unwrap())
                .unwrap();
            assert_eq!(
                codec::encode(&definition.blueprint.contract.value()).unwrap(),
                expected,
                "{}",
                row["name"]
            );
            let observation = Observation::official(Curve {
                date: time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap(),
                retrieved: at - 600_000_000,
                rates: vec![
                    Point {
                        months: 1,
                        percentage: decimal::parse("4.0000").unwrap(),
                    },
                    Point {
                        months: 2,
                        percentage: decimal::parse("4.10").unwrap(),
                    },
                    Point {
                        months: 3,
                        percentage: decimal::parse("4.2").unwrap(),
                    },
                ],
            });
            let b = &definition.blueprint;
            let context = Context::prepare(
                b.contract.clone(),
                b.calendar.clone(),
                &b.publications,
                &b.conversion,
                Some(&observation),
                [
                    0xcc, 0xdd, 0xee, 0xff, 0xaa, 0xbb, 0x88, 0x99, 0x77, 0x66, 0x55, 0x44, 0x33,
                    0x22, 0x11, 0,
                ],
                b.contract.engine(),
                at,
                b.maximum_lead,
            )
            .unwrap();
            assert_eq!(
                context.digest().unwrap(),
                row["digest"].as_str().unwrap(),
                "{}",
                row["name"]
            );
        }
    }
    #[test]
    fn malformed_raw_imports_do_not_start_source_acquisition_or_truncate_inputs() {
        let rows = rows();
        let runtime = Runtime::new(rows[0]["at"].as_i64().unwrap());
        let instance = instance(runtime.0).unwrap();
        let original = &rows[0]["raw"];
        for mutate in 0..13 {
            let mut raw = original.clone();
            match mutate {
                0 => raw["publication"]["from_offset"] = 1.into(),
                1 => raw["publication"]["entries"][0]["offset"] = 60.into(),
                2 => raw["calendar"]["timezone"] = "Unknown/Zone".into(),
                3 => raw["calendar"]["dates"] = serde_json::json!([1, 1]),
                4 => raw["options"] = serde_json::json!([]),
                5 => {
                    raw["options"][0]["contract"][4] = u64::from(u32::MAX).saturating_add(1).into()
                }
                6 => raw["options"][0]["contract"][12] = serde_json::json!([1]),
                7 => raw["options"][0]["contract"][12][0] = i64::MAX.into(),
                8 => raw["calendar"]["from"] = i64::MAX.into(),
                9 => raw["options"][0]["strike"][3] = u32::MAX.into(),
                10 => raw["options"][0]["contract"][16] = "x".repeat(16385).into(),
                11 => raw["maximum_lead"] = 2001.into(),
                _ => raw["calendar"]["dates"] = Value::Array(vec![0.into(); 2001]),
            }
            assert!(parse(&raw, &instance).is_err(), "mutation {mutate}");
        }
        let mut raw = original.clone();
        raw["options"][0]["contract"][12][1] = 60.into();
        let imported = parse(&raw, &instance).unwrap();
        assert!(
            imported.definitions[0]
                .blueprint
                .contract
                .qualify(rows[0]["at"].as_i64().unwrap(), 0)
                .is_err(),
            "non-UTC convention identity must survive import until qualification rejects it"
        );
    }
}
