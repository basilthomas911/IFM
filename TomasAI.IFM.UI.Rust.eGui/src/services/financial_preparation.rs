//! Version-bound operator requests assembled from backend-selected definitions.
//! The backend remains the authority for balances, limits and posting validity.
use crate::{
    backend::{contracts::*, identity::financial_hash, schema::Registry},
    persistence::{financial_journal, financial_json},
    services::{business_context::BusinessContext, parameters::overlay_type},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared.Financial";
pub fn kind(name: &str) -> String {
    format!("{ROOT}.{name}")
}
pub fn defaults(name: &str) -> Result<Value, String> {
    let values: BTreeMap<String, Vec<u8>> = serde_json::from_str(include_str!(
        "../../tests/fixtures/backend/portfolio-model-defaults.json"
    ))
    .map_err(|e| e.to_string())?;
    crate::backend::codec::decode(
        values
            .get(&kind(name))
            .ok_or("Missing compiled financial initializer")?,
    )
}
pub fn access() -> Value {
    Value::Array(vec![
        std::env::var("USERNAME").unwrap_or_default().into(),
        Value::Array(
            [
                "LedgerRead",
                "LedgerPost",
                "LedgerReverse",
                "LedgerConfigure",
                "LedgerPeriodReopen",
                "LedgerImport",
            ]
            .map(Value::from)
            .to_vec(),
        ),
        Value::Array(vec![]),
    ])
}
pub fn scoped_access(portfolio: i32) -> Value {
    let mut value = access();
    if let Value::Array(v) = &mut value {
        v[2] = Value::Array(vec![portfolio.into()]);
    }
    value
}
pub fn command(
    registry: &Registry,
    context: &BusinessContext,
    configuration: bool,
    portfolio: i32,
    revision: i64,
    body: Value,
) -> Result<Json, String> {
    let operation = uuid::Uuid::new_v4();
    command_with_identity(
        registry,
        context,
        configuration,
        portfolio,
        revision,
        body,
        operation,
    )
}
pub fn command_with_identity(
    registry: &Registry,
    context: &BusinessContext,
    configuration: bool,
    portfolio: i32,
    revision: i64,
    body: Value,
    operation: uuid::Uuid,
) -> Result<Json, String> {
    if portfolio <= 0 || revision < 0 || operation.is_nil() {
        return Err("Invalid financial command scope/revision/identity".into());
    }
    let name = if configuration {
        "ConfigureLedgerCommand"
    } else {
        "PostFundTransactionCommand"
    };
    let (actor, verb) = registry.route(&kind(name))?;
    let subject = ActorSubject {
        kind: ActorKind::Command,
        name: actor.into(),
        verb: verb.into(),
        entity: portfolio.to_string(),
    };
    let expires = context
        .as_of_utc
        .checked_add_signed(chrono::Duration::minutes(2))
        .ok_or("Financial expiry exceeds date range")?;
    use chrono::Datelike;
    if expires.year() > 9999 {
        return Err("Financial expiry exceeds the backend date range".into());
    }
    let request = overlay_type(
        registry,
        &kind(name),
        &crate::services::portfolio_commands::template(&kind(name))?,
        &BTreeMap::from([
            ("CommandId".into(), operation.to_string().into()),
            ("OperationId".into(), operation.to_string().into()),
            ("PortfolioId".into(), portfolio.into()),
            ("EntityId".into(), Value::Array(vec![portfolio.into()])),
            ("Subject".into(), subject.wire()),
            (
                "CorrelationId".into(),
                if configuration {
                    uuid::Uuid::new_v4()
                } else {
                    operation
                }
                .to_string()
                .into(),
            ),
            (
                "CausationId".into(),
                if configuration {
                    uuid::Uuid::new_v4()
                } else {
                    operation
                }
                .to_string()
                .into(),
            ),
            ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ("ExpiresAtUtc".into(), timestamp_wire(expires)),
            ("ExpectedFinancialRevision".into(), revision.into()),
            ("Body".into(), body),
            ("Access".into(), scoped_access(portfolio)),
        ]),
    )?;
    let mut request = financial_json::stored_json(registry, &kind(name), &request)?;
    request["InputSha256"] = financial_journal::request_hash(registry, name, &request)?.into();
    let pending = json!({"Request":request,"Phase":0,"Message":"Prepared.","Completion":null});
    financial_journal::validate(registry, name, &pending)?;
    Ok(pending)
}
pub fn posting(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i32,
    fund: i32,
    revision: i64,
    config: &Value,
    fields: &serde_json::Map<String, Json>,
    related: Option<&Value>,
    related_receipt: Option<&Value>,
) -> Result<Json, String> {
    let config_json = registry.project(&kind("FinancialPostingConfiguration"), config)?;
    if config_json["FundId"] != fund || config_json["PeriodOpen"] != true {
        return Err("An open period and current Fund posting configuration are required".into());
    }
    let field = |name: &str| fields.get(name).and_then(Json::as_str).unwrap_or_default();
    let rules = crate::services::strategy::member(
        registry,
        &kind("FinancialPostingConfiguration"),
        config,
        "Rules",
    )?;
    let rules = rules
        .as_array()
        .ok_or("Invalid configured posting rules")?
        .iter()
        .filter(|v| {
            registry
                .project(&kind("LedgerPostingRule"), v)
                .is_ok_and(|v| v["RuleId"] == field("rule"))
        })
        .collect::<Vec<_>>();
    if rules.len() != 1 {
        return Err("Choose an exact configured posting rule".into());
    }
    let rule = registry.project(&kind("LedgerPostingRule"), rules[0])?;
    let date = |name: &str| {
        crate::backend::date_input::parse(field(name)).map_err(|error| format!("{name}: {error}"))
    };
    let accounting = date("accountingDate")?;
    let value_date = date("valueDate")?;
    if config_json["AccountingDate"] != accounting.to_string() {
        return Err("Reload posting configuration for this accounting date".into());
    }
    let amount = field("amount").trim();
    ExactDecimal::parse(amount)?;
    if amount.starts_with('-')
        || !amount.chars().any(|c| matches!(c, '1'..='9'))
        || amount
            .split_once('.')
            .is_some_and(|(_, f)| f.len() > 2 && f[2..].bytes().any(|v| v != b'0'))
    {
        return Err("Enter a positive amount in whole cents".into());
    }
    let (whole, fraction) = amount
        .split_once('.')
        .map(|(a, b)| (a, Some(b)))
        .unwrap_or((amount, None));
    let whole = whole.trim_start_matches('0');
    let whole = if whole.is_empty() { "0" } else { whole };
    let canonical = if let Some(fraction) = fraction {
        format!("{whole}.{fraction}")
    } else {
        whole.into()
    };
    let decimal = ExactDecimal::parse(&canonical)?;
    let description = field("description");
    let reference = field("reference");
    let confirmed = if field("confirmed").eq_ignore_ascii_case("true") {
        true
    } else if field("confirmed").eq_ignore_ascii_case("false") {
        false
    } else {
        return Err("Confirm the cash movement explicitly".into());
    };
    if description.trim().is_empty()
        || description.encode_utf16().count() > 1000
        || reference.encode_utf16().count() > 256
    {
        return Err("Enter a description of at most 1000 characters and movement reference of at most 256 characters".into());
    }
    if rule["RequiresConfirmedMovement"] == true && (!confirmed || reference.trim().is_empty()) {
        return Err("Confirm the cash movement and supply its reference".into());
    }
    let tx = rule["Kind"].as_str().ok_or("Missing posting kind")?;
    if tx == "OpeningBalance" && config_json["AllowDevelopmentOpeningCapital"] != true {
        return Err("Development opening capital is not available for this book".into());
    }
    let related_row = related
        .map(|v| registry.project(&kind("FinancialTransactionRow"), v))
        .transpose()?
        .unwrap_or(Json::Null);
    let receipt = related_receipt
        .map(|v| registry.project(&kind("LedgerPostingReceipt"), v))
        .transpose()?
        .unwrap_or(Json::Null);
    if matches!(tx, "Reversal" | "WithdrawalSettled" | "WithdrawalCancelled") {
        if related_row["Transaction"]["FundId"] != fund
            || receipt["FundId"] != fund
            || receipt["OperationId"] != related_row["OperationId"]
            || receipt["BookId"] != config_json["BookId"]
        {
            return Err("Select the original transaction and its committed receipt".into());
        }
        if tx == "Reversal" && related_row["JournalId"].as_i64().is_none_or(|v| v <= 0) {
            return Err("Select a posted journal to reverse".into());
        }
        if tx != "Reversal"
            && (related_row["Transaction"]["TransactionKind"] != "WithdrawalRequested"
                || receipt["ObligationId"].is_null())
        {
            return Err("Select the original withdrawal request".into());
        }
    }
    let operation = uuid::Uuid::new_v4();
    let valid_until = context
        .as_of_utc
        .checked_add_signed(chrono::Duration::minutes(2))
        .ok_or("Financial expiry exceeds date range")?;
    use chrono::Datelike;
    if valid_until.year() > 9999 {
        return Err("Financial expiry exceeds the backend date range".into());
    }
    let draft = json!({"Kind":registry.enum_value(&kind("LedgerTransactionKind"),tx)?,"Amount":serde_json::from_str::<Json>(&canonical).map_err(|_|"Invalid financial amount")?,"AccountingDate":accounting.to_string(),"ValueDate":value_date.to_string(),"Description":description,"MovementReference":reference,"MovementConfirmed":confirmed});
    let principal = std::env::var("USERNAME").unwrap_or_default();
    let movement = overlay_type(
        registry,
        &kind("LedgerMovementEvidence"),
        &defaults("LedgerMovementEvidence")?,
        &BTreeMap::from([
            (
                "Status".into(),
                registry
                    .enum_value(
                        &kind("MovementStatus"),
                        if tx != "OpeningBalance" && confirmed {
                            "Confirmed"
                        } else {
                            "Pending"
                        },
                    )?
                    .into(),
            ),
            (
                "SourceReference".into(),
                if tx == "OpeningBalance" {
                    ""
                } else {
                    reference.trim()
                }
                .into(),
            ),
            ("ObservedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ("ReceivedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ("ValidUntilUtc".into(), timestamp_wire(valid_until)),
            (
                "ContentHash".into(),
                financial_hash(&json!({"draft":draft,"Principal":principal}))?.into(),
            ),
        ]),
    )?;
    let source = overlay_type(
        registry,
        &kind("LedgerSourceReference"),
        &defaults("LedgerSourceReference")?,
        &BTreeMap::from([
            (
                "System".into(),
                if tx == "OpeningBalance" {
                    "DevelopmentOpeningCapital"
                } else {
                    "PortfolioOperator"
                }
                .into(),
            ),
            (
                "SourceEntityId".into(),
                format!("{portfolio}.{fund}").into(),
            ),
            ("SourceEventId".into(), operation.to_string().into()),
            ("SourceSequence".into(), 1.into()),
            (
                "SourceContentHash".into(),
                financial_hash(
                    &json!({"draft":draft,"Principal":principal,"operation":operation.to_string()}),
                )?
                .into(),
            ),
            ("OccurredAtUtc".into(), timestamp_wire(context.as_of_utc)),
        ]),
    )?;
    let posting_rule = registry.encode(
        &kind("LedgerPostingRuleReference"),
        &BTreeMap::from([
            (
                "RuleId".into(),
                rule["RuleId"]
                    .as_str()
                    .ok_or("Missing rule identity")?
                    .into(),
            ),
            (
                "Version".into(),
                rule["Version"]
                    .as_i64()
                    .ok_or("Missing rule version")?
                    .into(),
            ),
            (
                "ContentHash".into(),
                rule["ContentHash"]
                    .as_str()
                    .ok_or("Missing rule hash")?
                    .into(),
            ),
        ]),
    )?;
    let body = overlay_type(
        registry,
        &kind("LedgerPostingRequest"),
        &defaults("LedgerPostingRequest")?,
        &BTreeMap::from([
            (
                "BookId".into(),
                config_json["BookId"]
                    .as_i64()
                    .ok_or("Missing book identity")?
                    .into(),
            ),
            ("FundId".into(), fund.into()),
            (
                "TransactionKind".into(),
                registry
                    .enum_value(&kind("LedgerTransactionKind"), tx)?
                    .into(),
            ),
            ("Currency".into(), "USD".into()),
            ("Amount".into(), decimal.wire()),
            ("AccountingDate".into(), date_only_wire(accounting)),
            ("ValueDate".into(), date_only_wire(value_date)),
            ("Description".into(), description.trim().into()),
            (
                "Authority".into(),
                crate::services::strategy::member(
                    registry,
                    &kind("FinancialPostingConfiguration"),
                    config,
                    "Authority",
                )?,
            ),
            ("PostingRule".into(), posting_rule),
            (
                "RelatedJournalId".into(),
                if tx == "Reversal" {
                    related_row["JournalId"]
                        .as_i64()
                        .map(Value::from)
                        .unwrap_or(Value::Nil)
                } else {
                    Value::Nil
                },
            ),
            (
                "RelatedObligationId".into(),
                if matches!(tx, "WithdrawalSettled" | "WithdrawalCancelled") {
                    receipt["ObligationId"]
                        .as_str()
                        .map(Value::from)
                        .unwrap_or(Value::Nil)
                } else {
                    Value::Nil
                },
            ),
            ("MovementEvidence".into(), movement),
            ("Source".into(), source),
        ]),
    )?;
    command_with_identity(
        registry, context, false, portfolio, revision, body, operation,
    )
}
