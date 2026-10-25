//! Typed integrity adapter for the existing per-user .NET journals. An uncertain
//! request is reloaded verbatim; neither restarting a view nor retrying a read
//! creates a replacement financial operation ID.
use crate::backend::{identity::financial_hash, schema::Registry};
use serde_json::{Value as Json, json};
fn utc(value: &Json) -> Result<String, String> {
    let text = value
        .as_str()
        .ok_or("Missing financial request timestamp")?;
    let date = chrono::DateTime::parse_from_rfc3339(text)
        .map(|v| v.to_utc())
        .or_else(|_| {
            chrono::NaiveDateTime::parse_from_str(text, "%Y-%m-%dT%H:%M:%S%.f").map(|v| v.and_utc())
        })
        .map_err(|_| "Invalid financial request timestamp")?;
    let value = crate::backend::contracts::utc_roundtrip(date);
    let (whole, fraction) = value
        .trim_end_matches('Z')
        .split_once('.')
        .ok_or("Invalid financial timestamp precision")?;
    let fraction = fraction.trim_end_matches('0');
    Ok(if fraction.is_empty() {
        format!("{whole}Z")
    } else {
        format!("{whole}.{fraction}Z")
    })
}
fn normalized(registry: &Registry, kind: &str, value: &Json) -> Result<Json, String> {
    if value.is_null() {
        return Ok(Json::Null);
    }
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|s| s.strip_suffix(']'))
        .unwrap_or(kind);
    if kind == "System.DateTime" {
        return Ok(Json::String(utc(value)?));
    }
    if kind == "System.Guid" {
        return uuid::Uuid::parse_str(value.as_str().ok_or("Invalid financial GUID")?)
            .map(|v| Json::String(v.to_string()))
            .map_err(|_| "Invalid financial GUID".into());
    }
    if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
        kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
            .and_then(|v| v.strip_suffix(']'))
    }) {
        return value
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid financial journal array")?
            .iter()
            .map(|v| normalized(registry, inner, v))
            .collect::<Result<Vec<_>, _>>()
            .map(Json::Array);
    }
    if let Ok(shape) = registry.describe(kind) {
        if shape.is_enum {
            return Ok(value.clone());
        }
        let values = value
            .as_object()
            .ok_or("Invalid financial journal object")?;
        let mut output = serde_json::Map::new();
        for (name, value) in values {
            let value = if let Some((_, _, kind)) = shape.members.iter().find(|(n, _, _)| n == name)
            {
                normalized(registry, kind, value)?
            } else {
                value.clone()
            };
            output.insert(name.clone(), value);
        }
        return Ok(Json::Object(output));
    }
    Ok(value.clone())
}
pub fn request_hash(registry: &Registry, command: &str, request: &Json) -> Result<String, String> {
    if !matches!(
        command,
        "PostFundTransactionCommand" | "ConfigureLedgerCommand"
    ) {
        return Err("Unsupported financial journal request".into());
    }
    // Resolve the body type from compiled metadata so its published type name can
    // evolve without guessing from the command's display label.
    let shape = registry.describe(&format!(
        "TomasAI.IFM.Domain.Portfolio.Shared.Financial.{command}"
    ))?;
    let body_kind = &shape
        .members
        .iter()
        .find(|(name, _, _)| name == "Body")
        .ok_or("Missing financial request body contract")?
        .2;
    let operation = uuid::Uuid::parse_str(request["OperationId"].as_str().unwrap_or_default())
        .map_err(|_| "Invalid persisted operation identity")?;
    if operation.is_nil() {
        return Err("Missing persisted operation identity".into());
    }
    financial_hash(
        &json!({"PortfolioId":request["PortfolioId"],"OperationId":operation.to_string(),"ExpectedFinancialRevision":request["ExpectedFinancialRevision"],"RequestedAtUtc":utc(&request["RequestedAtUtc"] )?,"ExpiresAtUtc":utc(&request["ExpiresAtUtc"] )?,"Body":normalized(registry,body_kind,&request["Body"] )?,"Principal":request["Access"]["Principal"]}),
    )
}
pub fn validate(registry: &Registry, command: &str, value: &Json) -> Result<(), String> {
    let request = &value["Request"];
    let operation = uuid::Uuid::parse_str(request["OperationId"].as_str().unwrap_or_default())
        .ok()
        .filter(|v| !v.is_nil())
        .ok_or("Pending financial request integrity failed")?;
    if request["CommandId"]
        .as_str()
        .and_then(|s| uuid::Uuid::parse_str(s).ok())
        != Some(operation)
        || request["InputSha256"] != request_hash(registry, command, request)?
    {
        return Err("Pending financial request integrity failed".into());
    }
    let phase = value["Phase"]
        .as_u64()
        .filter(|v| *v <= 3)
        .ok_or("Invalid pending financial phase")?;
    if request["PortfolioId"].as_i64().is_none_or(|v| v <= 0)
        || !request["Body"].is_object()
        || request["Access"]["Principal"]
            .as_str()
            .is_none_or(|v| v.is_empty())
    {
        return Err("Invalid pending financial scope".into());
    }
    if phase == 2
        && (value["Completion"]["OperationId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            != Some(operation)
            || value["Completion"]["InputHash"] != request["InputSha256"])
    {
        return Err("Financial completion does not match the persisted request".into());
    }
    Ok(())
}
pub fn current_user_directory(configuration: bool) -> Result<std::path::PathBuf, String> {
    let base = std::env::var_os("LOCALAPPDATA")
        .filter(|v| !v.is_empty())
        .ok_or("Local application data directory is unavailable")?;
    Ok(std::path::PathBuf::from(base)
        .join("IFM")
        .join("Portfolio")
        .join(if configuration {
            "PendingFinancialConfigurations"
        } else {
            "PendingFinancialOperations"
        }))
}
