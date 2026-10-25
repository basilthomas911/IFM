//! Native submission journal. Financial bytes are retained before publishing;
//! accepted identities are retained before lifecycle handoff. This directory is
//! separate from the managed ledger journals because it has a distinct schema.
use super::order_submission::Operation;
use crate::{
    backend::{codec, contracts::*, schema::Registry},
    persistence::journal::Journal,
};
use serde_json::{Value as Json, json};
use sha2::{Digest, Sha256};
use std::path::PathBuf;
fn directory() -> Result<PathBuf, String> {
    if let Ok(path) = std::env::var("IFM_STANDALONE_ORDER_JOURNAL") {
        return Ok(PathBuf::from(path));
    }
    std::env::var_os("LOCALAPPDATA")
        .map(|p| PathBuf::from(p).join("TomasAI/IFM/RustNative/OrderSubmissions"))
        .ok_or("Per-user submission journal directory is unavailable".into())
}
fn bytes(value: &Json) -> Result<Vec<u8>, String> {
    value["CommandWire"]
        .as_array()
        .filter(|v| v.len() <= 1024 * 1024)
        .ok_or("Invalid submission wire journal")?
        .iter()
        .map(|v| {
            v.as_u64()
                .and_then(|n| u8::try_from(n).ok())
                .ok_or("Invalid submission byte".into())
        })
        .collect()
}
fn validate(value: &Json) -> Result<(), String> {
    if value["Schema"] != 1
        || value["Request"]["OperationId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .is_none_or(|id| id.is_nil())
    {
        return Err("Invalid submission journal identity".into());
    }
    let bytes = bytes(value)?;
    if value["WireSha256"] != format!("{:x}", Sha256::digest(&bytes)) {
        return Err("Submission journal bytes do not match their integrity hash".into());
    }
    let wire = codec::decode(&bytes)?;
    static REGISTRY: std::sync::OnceLock<Result<Registry, String>> = std::sync::OnceLock::new();
    let r = REGISTRY
        .get_or_init(Registry::compiled)
        .as_ref()
        .map_err(Clone::clone)?;
    let kind = super::order_submission::journal_kind(&wire)?;
    let closing = kind.ends_with("EvaluatePortfolioCloseOrderCompositionCommand");
    let doc = super::parameters::document(&r, &kind, &wire, true)?;
    if super::order_submission::request_hash(&r, &wire)? != doc["InputSha256"] {
        return Err("Submission request semantic hash differs".into());
    }
    let id = uuid::Uuid::parse_str(
        doc["OperationId"]
            .as_str()
            .ok_or("Missing submission operation")?,
    )
    .map_err(|_| "Invalid submission operation")?;
    let portfolio = doc["PortfolioId"]
        .as_i64()
        .filter(|n| *n > 0)
        .ok_or("Invalid submission Portfolio")?;
    let subject = ActorSubject {
        kind: ActorKind::Function,
        name: if closing {
            "PortfolioCloseOrderCompositionFunction"
        } else {
            "PortfolioOrderCompositionFunction"
        }
        .into(),
        verb: if closing { "EvaluateClose" } else { "Evaluate" }.into(),
        entity: format!("{portfolio}.{}", id.simple()),
    };
    if super::strategy::member(&r, &kind, &wire, "Subject")? != subject.wire()
        || doc["CommandId"] != doc["OperationId"]
        || doc["EntityId"]["OperationId"] != doc["OperationId"]
        || doc["EntityId"]["PortfolioId"] != doc["PortfolioId"]
    {
        return Err("Submission command route/correlation differs".into());
    }
    if doc["OperationId"] != value["Request"]["OperationId"]
        || doc["InputSha256"] != value["Request"]["InputSha256"]
        || doc["PortfolioId"] != value["Request"]["PortfolioId"]
    {
        return Err("Submission journal command scope differs".into());
    }
    Ok(())
}
pub async fn save(operation: &Operation, phase: u8, message: &str) -> Result<(), String> {
    let directory = directory()?;
    let id = operation.id;
    let wire = codec::encode(&operation.body)?;
    let value = json!({"Schema":1,"Request":{"OperationId":id.to_string(),"InputSha256":operation.hash,"PortfolioId":operation.portfolio},"WireSha256":format!("{:x}",Sha256::digest(&wire)),"CommandWire":wire,"Phase":phase,"Message":message,"AcceptedOrders":operation.accepted,"Completion":operation.completed,"Dispatched":operation.dispatched,"Channel":operation.channel,"Confirmed":operation.confirmed});
    tokio::task::spawn_blocking(move || {
        Journal::new(directory)
            .save(id, &value, &validate, &|| false)
            .map(|_| ())
    })
    .await
    .map_err(|e| e.to_string())?
}
pub async fn pending() -> Result<Vec<Json>, String> {
    let directory = directory()?;
    tokio::task::spawn_blocking(move || {
        Journal::new(directory).list(&validate).map(|rows| {
            rows.into_iter()
                .filter(|v| {
                    v["Phase"] == 1
                        || v["Phase"] == 2
                            && !v["AcceptedOrders"].as_array().is_none_or(|a| a.is_empty())
                })
                .collect()
        })
    })
    .await
    .map_err(|e| e.to_string())?
}

pub fn restore(registry: &Registry, value: &Json) -> Result<Operation, String> {
    validate(value)?;
    let body = codec::decode(&bytes(value)?)?;
    let kind = super::order_submission::journal_kind(&body)?;
    let closing = kind.ends_with("EvaluatePortfolioCloseOrderCompositionCommand");
    let doc = super::parameters::document(registry, &kind, &body, true)?;
    let id = uuid::Uuid::parse_str(
        doc["OperationId"]
            .as_str()
            .ok_or("Missing saved submission identity")?,
    )
    .map_err(|_| "Invalid saved submission identity")?;
    let portfolio = doc["PortfolioId"]
        .as_i64()
        .and_then(|n| i32::try_from(n).ok())
        .filter(|n| *n > 0)
        .ok_or("Invalid saved Portfolio scope")?;
    let composition = uuid::Uuid::parse_str(
        doc["Body"]["CompositionId"]
            .as_str()
            .ok_or("Missing saved composition")?,
    )
    .map_err(|_| "Invalid saved composition")?;
    let channel = value["Channel"]
        .as_str()
        .filter(|s| ["Broker", "Manual"].contains(s))
        .ok_or("Invalid retained execution channel")?
        .to_string();
    let accepted = value["AcceptedOrders"]
        .as_array()
        .ok_or("Invalid retained accepted orders")?
        .clone();
    if accepted
        .iter()
        .any(|order| order["Id"]["PortfolioId"] != portfolio)
    {
        return Err("Retained accepted order crossed Portfolio scope".into());
    }
    let dispatched = value["Dispatched"]
        .as_u64()
        .and_then(|n| usize::try_from(n).ok())
        .filter(|n| *n <= accepted.len())
        .ok_or("Invalid retained dispatch cursor")?;
    let subject = ActorSubject {
        kind: ActorKind::Function,
        name: if closing {
            "PortfolioCloseOrderCompositionFunction"
        } else {
            "PortfolioOrderCompositionFunction"
        }
        .into(),
        verb: if closing { "EvaluateClose" } else { "Evaluate" }.into(),
        entity: format!("{portfolio}.{}", id.simple()),
    };
    Ok(Operation {
        id,
        portfolio,
        composition,
        subject,
        body,
        hash: doc["InputSha256"]
            .as_str()
            .ok_or("Missing retained request hash")?
            .into(),
        attempted: true,
        accepted,
        completed: if value["Completion"].is_null() {
            None
        } else {
            Some(value["Completion"].clone())
        },
        dispatched,
        channel,
        confirmed: value["Confirmed"] == true,
    })
}
