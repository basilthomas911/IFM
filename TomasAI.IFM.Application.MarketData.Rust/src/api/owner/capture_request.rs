//! Bounded raw command projection for coherent snapshots. Fields are copied from
//! the public request; all date, capacity and financial validation is native.
use crate::option_chain::composition::{
    capture::{Failure, Request},
    model::Future,
};
use crate::reference::decimal;
use serde_json::Value;
use std::collections::BTreeSet;
/// Copied native capture command, with no managed source or pricing callback.
pub struct Command {
    /// Event-driven readiness wait; absent preserves immediate-query semantics.
    pub wait: bool,
    /// Complete public snapshot request.
    pub request: Request,
    /// Raw outright-futures reference definitions, ignored by option captures.
    pub futures: Vec<Future>,
    /// Preserves the existing rejection of non-UTC future definitions.
    pub non_utc_definition: bool,
}
fn invalid() -> Failure {
    Failure {
        code: "InvalidSnapshotRequest".into(),
        input: "Snapshot".into(),
    }
}
fn text(value: &Value, maximum: usize) -> Result<String, Failure> {
    value
        .as_str()
        .filter(|s| s.len() <= maximum)
        .map(str::to_owned)
        .ok_or_else(invalid)
}
fn number(value: &Value) -> Result<i64, Failure> {
    value.as_i64().ok_or_else(invalid)
}
fn boolean(value: &Value) -> Result<bool, Failure> {
    value.as_bool().ok_or_else(invalid)
}
fn guid(value: &Value) -> Result<[u8; 16], Failure> {
    let fields = value
        .as_array()
        .filter(|a| a.len() == 16)
        .ok_or_else(invalid)?;
    let mut bytes = [0; 16];
    for (target, source) in bytes.iter_mut().zip(fields) {
        *target = u8::try_from(number(source)?).map_err(|_| invalid())?;
    }
    Ok(bytes)
}
impl Command {
    /// Parses a copied JSON command. Malformed public input is a typed snapshot
    /// failure and cannot mutate dataset ownership or start recovery.
    ///
    /// # Errors
    /// Invalid shape, scalar representation, UTC input or bounded capacity.
    pub fn from_value(value: &Value) -> Result<Self, Failure> {
        if number(&value["at_offset"])? != 0 || number(&value["until_offset"])? != 0 {
            return Err(invalid());
        }
        let options = boolean(&value["options"])?;
        if options && value["dataset"].as_str() != Some("GLBX.MDP3") {
            return Err(Failure {
                code: "Recovering".into(),
                input: "GenerationId".into(),
            });
        }
        let risk = value["risk"]
            .as_array()
            .filter(|a| a.len() <= 2048)
            .ok_or_else(invalid)?
            .iter()
            .map(|v| text(v, 1024))
            .collect::<Result<BTreeSet<_>, _>>()?;
        let request = Request {
            id: guid(&value["id"])?,
            scope: text(&value["scope"], 1024)?,
            generation: guid(&value["generation"])?,
            horizon: text(&value["horizon"], 128)?,
            at: number(&value["at"])?,
            until: number(&value["until"])?,
            maximum: usize::try_from(number(&value["maximum"])?).map_err(|_| invalid())?,
            age: u32::try_from(number(&value["age"])?).map_err(|_| invalid())?,
            skew: u32::try_from(number(&value["skew"])?).map_err(|_| invalid())?,
            options,
            selection: boolean(&value["selection"])?,
            allow_missing: boolean(&value["allow_missing"])?,
            risk,
        };
        let mut futures = Vec::new();
        let mut non_utc_definition = false;
        if !options {
            for definition in value["futures"]
                .as_array()
                .filter(|a| a.len() <= 2048)
                .ok_or_else(invalid)?
            {
                let fields = definition
                    .as_array()
                    .filter(|a| a.len() == 10)
                    .ok_or_else(invalid)?;
                non_utc_definition |= number(&fields[6])? != 0;
                futures.push(Future {
                    contract: text(&fields[0], 1024)?,
                    root: text(&fields[1], 60)?,
                    dataset: text(&fields[2], 64)?,
                    exchange: text(&fields[3], 64)?,
                    currency: text(&fields[4], 16)?,
                    last_trading: number(&fields[5])?,
                    multiplier: decimal::parse(fields[7].as_str().ok_or_else(invalid)?)
                        .ok_or_else(invalid)?,
                    tick: decimal::parse(fields[8].as_str().ok_or_else(invalid)?)
                        .ok_or_else(invalid)?,
                    digest: text(&fields[9], 4096)?,
                });
            }
        }
        Ok(Self {
            wait: match value.get("wait") {
                None => false,
                Some(value) => boolean(value)?,
            },
            request,
            futures,
            non_utc_definition,
        })
    }
}
