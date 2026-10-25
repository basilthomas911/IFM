//! Configuration requests bind to exact backend account/rule/period versions.
//! Preparation grants no authority: every submitted request is revalidated and
//! committed by the existing ledger actors before the UI accepts its receipt.
use crate::{
    backend::{contracts::*, identity::financial_hash, schema::Registry},
    persistence::financial_json,
    services::{
        business_context::BusinessContext, financial, financial_preparation as prep,
        parameters::overlay_type,
    },
};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
mod rule_edit;
pub use rule_edit::edit_rule;
fn field<'a>(fields: &'a serde_json::Map<String, Json>, name: &str) -> &'a str {
    fields.get(name).and_then(Json::as_str).unwrap_or_default()
}
fn reason(fields: &serde_json::Map<String, Json>) -> Result<&str, String> {
    let text = field(fields, "reason").trim();
    if text.is_empty() || text.encode_utf16().count() > 1024 {
        Err("A current Portfolio configuration and audit reason of at most 1024 characters are required".into())
    } else {
        Ok(text)
    }
}
fn member(registry: &Registry, kind: &str, value: &Value, name: &str) -> Result<Value, String> {
    crate::services::strategy::member(registry, &prep::kind(kind), value, name)
}
fn selected(
    registry: &Registry,
    config: &Value,
    collection: &str,
    ty: &str,
    key: &str,
    id: &Json,
) -> Result<Value, String> {
    let rows = member(registry, "FinancialLedgerConfiguration", config, collection)?;
    let values = rows
        .as_array()
        .ok_or("Invalid financial configuration collection")?
        .iter()
        .filter(|v| {
            registry
                .project(&prep::kind(ty), v)
                .is_ok_and(|row| row["State"] == "Active" && row["Definition"][key] == *id)
        })
        .collect::<Vec<_>>();
    if values.len() != 1 {
        return Err("Select an active exact account or posting rule".into());
    }
    member(registry, ty, values[0], "Definition")
}
fn same_amount(a: &Json, b: &Json) -> bool {
    let canonical = |v: &Json| {
        v.as_str().map(|v| {
            let v = if v.contains('.') {
                v.trim_end_matches('0').trim_end_matches('.')
            } else {
                v
            };
            if v == "-0" {
                "0".to_string()
            } else {
                v.to_string()
            }
        })
    };
    canonical(a).is_some_and(|a| Some(a) == canonical(b))
}
pub fn control(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i32,
    read: &financial::Read,
    action: &str,
    fields: &serde_json::Map<String, Json>,
) -> Result<Json, String> {
    let config = read.found()?;
    let row = registry.project(&prep::kind("FinancialLedgerConfiguration"), config)?;
    let mut changes = BTreeMap::from([
        (
            "Action".into(),
            registry
                .enum_value(&prep::kind("LedgerConfigurationAction"), action)?
                .into(),
        ),
        (
            "BookId".into(),
            row["BookId"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Missing configured book identity")?
                .into(),
        ),
        ("Reason".into(), reason(fields)?.into()),
        (
            "SourceCut".into(),
            format!("LedgerSnapshot:{portfolio}:{}", read.financial_revision).into(),
        ),
    ]);
    match action {
        "Reconcile" => {}
        "QualifyDevelopmentBook" => {
            let evidence = &row["LatestReconciliation"];
            if row["DevelopmentQualificationBook"].is_null()
                || evidence.is_null()
                || evidence["Differences"]
                    .as_array()
                    .is_none_or(|v| !v.is_empty())
                || !same_amount(&evidence["Debits"], &evidence["Credits"])
            {
                return Err("Record development opening capital and reconcile the unqualified book before qualification".into());
            }
            changes.insert(
                "Book".into(),
                member(
                    registry,
                    "FinancialLedgerConfiguration",
                    config,
                    "DevelopmentQualificationBook",
                )?,
            );
            changes.insert(
                "ReconciliationId".into(),
                evidence["ReconciliationId"]
                    .as_str()
                    .ok_or("Missing reconciliation identity")?
                    .into(),
            );
            changes.insert(
                "SourceCut".into(),
                evidence["SourceCut"]
                    .as_str()
                    .ok_or("Missing reconciliation cut")?
                    .into(),
            );
        }
        "OpenPeriod" => {
            let start = crate::backend::date_input::parse(field(fields, "start"))?;
            let end = crate::backend::date_input::parse(field(fields, "end"))?;
            if end < start
                || row["Periods"]
                    .as_array()
                    .ok_or("Invalid configured periods")?
                    .iter()
                    .any(|p| {
                        p["StartDate"]
                            .as_str()
                            .is_some_and(|p| p <= end.to_string().as_str())
                            && p["EndDate"]
                                .as_str()
                                .is_some_and(|p| p >= start.to_string().as_str())
                    })
            {
                return Err(
                    "The new period must have valid dates and not overlap a configured period"
                        .into(),
                );
            }
            changes.extend(BTreeMap::from([
                ("PeriodId".into(), uuid::Uuid::new_v4().to_string().into()),
                ("PeriodStart".into(), date_only_wire(start)),
                ("PeriodEnd".into(), date_only_wire(end)),
            ]));
        }
        "ClosePeriod" | "ReopenPeriod" => {
            let period = row["Periods"]
                .as_array()
                .ok_or("Invalid configured periods")?
                .iter()
                .find(|p| p["PeriodId"] == field(fields, "period"))
                .ok_or("Select a configured period")?;
            if period["State"]
                != if action == "ClosePeriod" {
                    "Open"
                } else {
                    "Closed"
                }
            {
                return Err("The selected period is not in the required state".into());
            }
            changes.insert(
                "PeriodId".into(),
                period["PeriodId"]
                    .as_str()
                    .ok_or("Missing period identity")?
                    .into(),
            );
            changes.insert(
                "ExpectedVersion".into(),
                period["Version"]
                    .as_i64()
                    .filter(|v| *v > 0)
                    .ok_or("Missing period revision")?
                    .into(),
            );
            for (source, target) in [("StartDate", "PeriodStart"), ("EndDate", "PeriodEnd")] {
                let day = chrono::NaiveDate::parse_from_str(
                    period[source]
                        .as_str()
                        .ok_or("Missing configured period date")?,
                    "%Y-%m-%d",
                )
                .map_err(|_| "Invalid configured period date")?;
                changes.insert(target.into(), date_only_wire(day));
            }
            if action == "ClosePeriod" {
                let evidence = &row["LatestReconciliation"];
                if evidence.is_null()
                    || evidence["Differences"]
                        .as_array()
                        .is_none_or(|v| !v.is_empty())
                    || !same_amount(&evidence["Debits"], &evidence["Credits"])
                {
                    return Err("Run a successful reconciliation before closing a period".into());
                }
                changes.insert(
                    "ReconciliationId".into(),
                    evidence["ReconciliationId"]
                        .as_str()
                        .ok_or("Missing reconciliation identity")?
                        .into(),
                );
                changes.insert(
                    "SourceCut".into(),
                    evidence["SourceCut"]
                        .as_str()
                        .ok_or("Missing reconciliation cut")?
                        .into(),
                );
            }
        }
        "RetireAccount" => {
            let id = field(fields, "account")
                .parse::<i32>()
                .map_err(|_| "Select a configured account")?;
            let definition = selected(
                registry,
                config,
                "Accounts",
                "FinancialConfiguredAccount",
                "AccountId",
                &id.into(),
            )?;
            let row = registry.project(&prep::kind("LedgerAccountDefinition"), &definition)?;
            changes.insert(
                "ExpectedVersion".into(),
                row["Version"]
                    .as_i64()
                    .ok_or("Missing account revision")?
                    .into(),
            );
            changes.insert("Accounts".into(), Value::Array(vec![definition]));
        }
        "RetirePostingRule" => {
            let id = uuid::Uuid::parse_str(field(fields, "rule"))
                .map_err(|_| "Select a configured rule")?;
            let definition = selected(
                registry,
                config,
                "Rules",
                "FinancialConfiguredRule",
                "RuleId",
                &id.to_string().into(),
            )?;
            let row = registry.project(&prep::kind("LedgerPostingRule"), &definition)?;
            changes.insert(
                "ExpectedVersion".into(),
                row["Version"]
                    .as_i64()
                    .ok_or("Missing rule revision")?
                    .into(),
            );
            changes.insert("Rules".into(), Value::Array(vec![definition]));
        }
        _ => return Err("This ledger control is not supported by this editor".into()),
    }
    let body = overlay_type(
        registry,
        &prep::kind("LedgerConfigurationRequest"),
        &prep::defaults("LedgerConfigurationRequest")?,
        &changes,
    )?;
    prep::command(
        registry,
        context,
        true,
        portfolio,
        read.financial_revision,
        body,
    )
}
pub fn backend_draft(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i32,
    revision: i64,
    draft: &Value,
    reason_text: &str,
    authority: bool,
) -> Result<Json, String> {
    let row = registry.project(&prep::kind("LedgerConfigurationRequest"), draft)?;
    let reason_text = reason_text.trim();
    if reason_text.is_empty()
        || reason_text.encode_utf16().count() > 1024
        || row["Book"]["PortfolioId"] != portfolio
    {
        return Err(
            "A prepared configuration for this Portfolio and audit reason are required".into(),
        );
    }
    if authority {
        if row["Action"] != "RefreshAuthority" {
            return Err("Prepare current financial authority first".into());
        }
        for fund in row["Book"]["Funds"]
            .as_array()
            .ok_or("Invalid authority Funds")?
        {
            for deployment in fund["Deployments"]
                .as_array()
                .ok_or("Invalid authority deployments")?
            {
                let expires = chrono::DateTime::parse_from_rfc3339(
                    deployment["Reference"]["ValidUntilUtc"]
                        .as_str()
                        .ok_or("Missing authority expiry")?,
                )
                .map_err(|_| "Invalid authority expiry")?;
                if expires.to_utc() <= context.as_of_utc {
                    return Err("Prepare unexpired financial authority again".into());
                }
            }
        }
    } else if row["Action"] != "CreateBook"
        || row["Book"]["MigrationQualified"] != false
        || row["Book"]["Environment"] != "Emulator"
        || row["Book"]["BookId"] != row["BookId"]
        || row["Book"]["Funds"]
            .as_array()
            .is_none_or(|v| v.is_empty() || v.iter().any(|f| f["CanSpend"] != false))
    {
        return Err("Prepare a development book with new spending disabled".into());
    }
    let body = overlay_type(
        registry,
        &prep::kind("LedgerConfigurationRequest"),
        draft,
        &BTreeMap::from([("Reason".into(), reason_text.into())]),
    )?;
    prep::command(
        registry,
        context,
        true,
        portfolio,
        if authority { revision } else { 0 },
        body,
    )
}
pub fn edit_account(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i32,
    read: &financial::Read,
    account: i32,
    fields: &serde_json::Map<String, Json>,
) -> Result<Json, String> {
    let config = read.found()?;
    let original = selected(
        registry,
        config,
        "Accounts",
        "FinancialConfiguredAccount",
        "AccountId",
        &account.into(),
    )?;
    let row = registry.project(&prep::kind("LedgerAccountDefinition"), &original)?;
    let version = row["Version"]
        .as_i64()
        .filter(|v| *v > 0)
        .ok_or("Missing account version")?;
    let normal = field(fields, "normal");
    if !matches!(normal, "Debit" | "Credit") {
        return Err("Select a normal side".into());
    }
    let dimension = if field(fields, "dimension").eq_ignore_ascii_case("true") {
        true
    } else if field(fields, "dimension").eq_ignore_ascii_case("false") {
        false
    } else {
        return Err("Choose the Fund dimension requirement".into());
    };
    let mut next = overlay_type(
        registry,
        &prep::kind("LedgerAccountDefinition"),
        &original,
        &BTreeMap::from([
            (
                "Version".into(),
                version
                    .checked_add(1)
                    .ok_or("Account version overflow")?
                    .into(),
            ),
            (
                "NormalSide".into(),
                registry
                    .enum_value(&prep::kind("PostingSide"), normal)?
                    .into(),
            ),
            ("FundDimensionRequired".into(), dimension.into()),
            ("ContentHash".into(), "".into()),
        ]),
    )?;
    let hash = financial_hash(&financial_json::stored_json(
        registry,
        &prep::kind("LedgerAccountDefinition"),
        &next,
    )?)?;
    next = overlay_type(
        registry,
        &prep::kind("LedgerAccountDefinition"),
        &next,
        &BTreeMap::from([("ContentHash".into(), hash.into())]),
    )?;
    let config = registry.project(&prep::kind("FinancialLedgerConfiguration"), config)?;
    let body = overlay_type(
        registry,
        &prep::kind("LedgerConfigurationRequest"),
        &prep::defaults("LedgerConfigurationRequest")?,
        &BTreeMap::from([
            (
                "Action".into(),
                registry
                    .enum_value(
                        &prep::kind("LedgerConfigurationAction"),
                        "AddAccountVersion",
                    )?
                    .into(),
            ),
            (
                "BookId".into(),
                config["BookId"]
                    .as_i64()
                    .ok_or("Missing book identity")?
                    .into(),
            ),
            ("ExpectedVersion".into(), version.into()),
            ("Accounts".into(), Value::Array(vec![next])),
            ("Reason".into(), reason(fields)?.into()),
        ]),
    )?;
    prep::command(
        registry,
        context,
        true,
        portfolio,
        read.financial_revision,
        body,
    )
}
