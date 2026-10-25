//! Direct-key database recovery API adapter. These requests deliberately do not
//! use the ordinary actor registry route: C# exposes instance Verb properties.
//! The UI only requests work; backup execution and authorization stay on server.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use rmpv::Value;
use serde_json::json;
use std::collections::BTreeMap;

pub const ROOT: &str = "TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup";
pub fn kind(section: &str, name: &str) -> String {
    format!("{ROOT}.{section}.{name}")
}
pub fn wrapper(value: impl Into<Value>) -> Value {
    Value::Array(vec![value.into()])
}

/// Closed query surface reachable from the existing Rust backup screen.
pub fn query_verb(name: &str) -> Result<&'static str, String> {
    Ok(match name {
        "GetDatabaseProtectionSetsQuery" => "GetProtectionSets",
        "ListDatabaseBackupOperationsQuery" => "ListBackupOperations",
        "GetDatabaseBackupSetupQuery" => "GetBackupSetup",
        "GetLatestVerifiedDatabaseBackupQuery" => "GetLatestVerifiedBackup",
        "GetLatestRestoreTestedDatabaseBackupQuery" => "GetLatestRestoreTestedBackup",
        "GetDatabaseBackupLogQuery" => "GetBackupLog",
        "GetDatabaseBackupPolicyQuery" => "GetPolicy",
        "GetDatabaseBackupServiceHealthQuery" => "GetServiceHealth",
        _ => return Err("Unsupported backup query".into()),
    })
}
pub fn command_route(name: &str) -> Result<(&'static str, i64), String> {
    Ok(match name {
        "RequestDatabaseBackupCommand" => ("RequestBackup", 9101),
        "CancelDatabaseBackupCommand" => ("CancelBackup", 9102),
        "RequestDatabaseRestoreDrillCommand" => ("RequestRestoreDrill", 9107),
        "UpdateDatabaseBackupPolicyCommand" => ("UpdatePolicy", 9108),
        _ => return Err("Unsupported backup command".into()),
    })
}
/// Caller metadata is not a credential. Backend roles and policy checks remain
/// authoritative. All business timestamps come from the API session clock.
pub fn envelope(
    registry: &Registry,
    context: &BusinessContext,
    request: uuid::Uuid,
) -> Result<Value, String> {
    if request.is_nil() {
        return Err("Missing backup request identity".into());
    }
    let caller = std::env::var("IFM_UI_OPERATOR")
        .or_else(|_| std::env::var("USERNAME"))
        .map_err(|_| "Configure IFM_UI_OPERATOR for database administration")?;
    let environment = std::env::var("IFM_ENVIRONMENT").unwrap_or_else(|_| {
        if std::env::var("DOTNET_ENVIRONMENT").as_deref() == Ok("Development") {
            "development"
        } else {
            "paper-trading"
        }
        .into()
    });
    for v in [&caller, &environment] {
        if v.trim().is_empty() || v.encode_utf16().count() > 256 || v.chars().any(char::is_control)
        {
            return Err("Invalid database request caller or environment".into());
        }
    }
    registry.value_from_json(&kind("Contracts", "DatabaseRequestEnvelope"), &json!({
        "ContractVersion":1,"RequestId":request.to_string(),"CallerIdentity":caller,
        "AuthorizationReference":"interactive-ui","CallerRoles":["DatabaseRecoveryOperator"],
        "Origin":"UI","CorrelationId":request.to_string(),"CausationId":uuid::Uuid::nil().to_string(),
        "EnvironmentIdentity":environment,"CreatedUtc":context.as_of_utc.to_rfc3339()
    }))
}
pub fn source(registry: &Registry, name: &str) -> Result<Value, String> {
    if !matches!(name, "AwsCloud" | "LocalWorkstation") {
        return Err("Select a concrete backup source".into());
    }
    registry.value_from_json(&kind("Contracts", "BackupSource"), &json!(name))
}
/// The caller owns one deadline for an entire action, including all follow-up
/// reads. No nested default timeout can extend its budget.
pub async fn query(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    source_name: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let request = uuid::Uuid::new_v4();
    let subject = ActorSubject::query(
        "DatabaseBackupQuery",
        query_verb(name)?,
        &request.simple().to_string(),
    );
    fields.insert("Subject".into(), subject.wire());
    fields.insert("EntityId".into(), wrapper(request.to_string()));
    fields.insert("Request".into(), envelope(registry, context, request)?);
    fields.insert("Source".into(), source(registry, source_name)?);
    fields.entry("PageSize".into()).or_insert(50.into());
    fields
        .entry("ContinuationIdentity".into())
        .or_insert("".into());
    let body = registry.encode(&kind("Queries", name), &fields)?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    Ok(service_result(&reply)?.clone())
}
/// Preserve C# initializer defaults, especially empty strings/arrays and
/// PostEvents=true. An accepted backup is not a completed backup.
pub fn command_body(
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    source_name: &str,
    request: uuid::Uuid,
    entity: uuid::Uuid,
    mut fields: BTreeMap<String, Value>,
) -> Result<(ActorSubject, Value), String> {
    if entity.is_nil() {
        return Err("Missing backup operation identity".into());
    }
    let (verb, error) = command_route(name)?;
    let subject = ActorSubject {
        kind: ActorKind::Command,
        name: "DatabaseBackupCommand".into(),
        verb: verb.into(),
        entity: entity.simple().to_string(),
    };
    fields.insert("CommandId".into(), request.to_string().into());
    fields.insert("EntityId".into(), wrapper(entity.to_string()));
    fields.insert("Subject".into(), subject.wire());
    fields.insert("Request".into(), envelope(registry, context, request)?);
    fields.insert("Source".into(), source(registry, source_name)?);
    fields.insert("PostEvents".into(), true.into());
    fields.insert("ErrorCode".into(), error.into());
    fields.insert(
        "RouteTo".into(),
        registry.value_from_json(
            "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
            &json!("SystemAdminBoundedContext"),
        )?,
    );
    fields
        .entry("RequiredDestinations".into())
        .or_insert(Value::Array(vec![]));
    for key in [
        "SafeReason",
        "ApprovalIdentity",
        "ApprovalReference",
        "DisposableTargetProfile",
        "ValidationProfile",
        "LegalHoldReference",
    ] {
        fields.entry(key.into()).or_insert("".into());
    }
    // DateTimeOffset's CLR default is year 1 UTC, not nil or Unix epoch.
    fields.entry("EvaluationBoundaryUtc".into()).or_insert(
        registry.value_from_json("System.DateTimeOffset", &json!("0001-01-01T00:00:00+00:00"))?,
    );
    Ok((subject, registry.encode(&kind("Commands", name), &fields)?))
}
pub async fn command(
    backend: &Backend,
    subject: &ActorSubject,
    body: &Value,
    deadline: tokio::time::Instant,
) -> Result<(), String> {
    // Publication can have reached the server even if the reply is lost. Never
    // retry a write automatically, or confuse a lost receipt with rejection.
    let reply = backend
        .request_until(subject, body, deadline, None)
        .await
        .map_err(|e| format!("OutcomeUnknown: {e}; reconcile the operation before retrying"))?;
    match service_result(&reply) {
        Ok(value)
            if value
                .as_array()
                .and_then(|v| v.first())
                .and_then(Value::as_str)
                .is_some_and(|v| uuid::Uuid::parse_str(v).is_ok_and(|id| !id.is_nil())) =>
        {
            Ok(())
        }
        Ok(_) => Err("OutcomeUnknown: malformed backup acceptance receipt".into()),
        Err(error)
            if reply
                .as_array()
                .and_then(|v| v.first())
                .and_then(Value::as_bool)
                == Some(false) =>
        {
            Err(error)
        }
        Err(error) => Err(format!("OutcomeUnknown: {error}")),
    }
}
