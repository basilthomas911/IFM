//! Canonical Portfolio ledger reads. The backend owns balances, performance
//! metrics and authority; the UI keeps status/revision separate from values.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared.Financial";
#[derive(Clone, Copy)]
pub enum AccessScope {
    MetricsReader,
    LedgerEditor,
}
#[derive(Clone)]
pub struct Read {
    pub status: i64,
    pub value: Option<Value>,
    pub financial_revision: i64,
    pub observed_at: chrono::DateTime<chrono::Utc>,
}
impl Read {
    pub fn found(&self) -> Result<&Value, String> {
        if self.status != 1 {
            return Err(if self.status == 2 {
                "Financial record was not found"
            } else {
                "Financial data is unavailable"
            }
            .into());
        }
        self.value
            .as_ref()
            .ok_or("Found financial read has no value".into())
    }
}
pub async fn query(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    portfolio: i32,
    fund: Option<i32>,
    parameters: Value,
    permissions: AccessScope,
    deadline: tokio::time::Instant,
) -> Result<Read, String> {
    if portfolio <= 0 || fund.is_some_and(|v| v <= 0) {
        return Err("Select a valid financial Portfolio/Fund scope".into());
    }
    let actor = match name {
        "PrepareFinancialAuthorityQuery"
        | "PrepareFinancialBookQuery"
        | "GetFinancialLedgerConfigurationQuery"
        | "GetFinancialPostingConfigurationQuery"
        | "GetFundRiskAuthorizationQuery"
        | "GetPostingReceiptQuery"
        | "GetJournalQuery"
        | "GetAccountBalancesQuery"
        | "GetTrialBalanceQuery"
        | "GetFundPerformanceMetricsQuery"
        | "GetFundTransactionsPageQuery"
        | "GetReconciliationQuery" => "GeneralLedgerQuery",
        "GetFinancialAdmissionSnapshotQuery"
        | "GetCapacityReservationQuery"
        | "GetCapacityUsageQuery"
        | "GetFundReservationsPageQuery" => "CapacityReservationQuery",
        _ => return Err("Unsupported Portfolio financial query".into()),
    };
    let kind = format!("{ROOT}.{name}");
    let (declared, verb) = registry.route(&kind)?;
    if actor != declared {
        return Err("Financial query route differs from compiled contract".into());
    }
    let subject = ActorSubject::query(actor, verb, &portfolio.to_string());
    let roles: &[&str] = match permissions {
        AccessScope::MetricsReader => &["LedgerRead"],
        AccessScope::LedgerEditor => &[
            "LedgerRead",
            "LedgerPost",
            "LedgerReverse",
            "LedgerConfigure",
            "LedgerPeriodReopen",
            "LedgerImport",
        ],
    };
    let access = Value::Array(vec![
        std::env::var("USERNAME").unwrap_or_default().into(),
        Value::Array(roles.iter().map(|v| (*v).into()).collect()),
        Value::Array(vec![portfolio.into()]),
    ]);
    let scope = Value::Array(vec![
        portfolio.into(),
        fund.map(Value::from).unwrap_or(Value::Nil),
        access,
    ]);
    let mut fields = BTreeMap::from([
        ("Subject".into(), subject.wire()),
        ("EntityId".into(), Value::Array(vec![portfolio.into()])),
        ("Scope".into(), scope),
        (
            "CorrelationId".into(),
            uuid::Uuid::new_v4().to_string().into(),
        ),
        ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
    ]);
    let shape = registry.describe(&kind)?;
    if shape
        .members
        .iter()
        .any(|(name, _, _)| name == "Parameters")
    {
        fields.insert("Parameters".into(), parameters);
    } else {
        // Earlier financial query contracts flattened request fields into permanent
        // numeric slots. Their Parameters accessor is ignored by MessagePack.
        let request_kind = format!(
            "{ROOT}.{}Request",
            name.strip_suffix("Query")
                .ok_or("Invalid financial query name")?
        );
        for (member, _, _) in registry.describe(&request_kind)?.members {
            if fields.contains_key(&member)
                || !shape.members.iter().any(|(name, _, _)| name == &member)
            {
                return Err(format!("Unsupported financial request mapping {member}"));
            }
            fields.insert(
                member.clone(),
                crate::services::strategy::member(registry, &request_kind, &parameters, &member)?,
            );
        }
    }
    let body = registry.encode(&kind, &fields)?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    let read = service_result(&reply)?
        .as_array()
        .filter(|v| v.len() >= 4)
        .ok_or("Invalid FinancialRead envelope")?;
    let status = read[0]
        .as_i64()
        .filter(|v| (1..=3).contains(v))
        .ok_or("Unknown financial read status")?;
    let revision = read[2]
        .as_i64()
        .filter(|v| *v >= 0)
        .ok_or("Invalid financial revision")?;
    let observed_at = timestamp(&read[3])?;
    if status == 1 && read[1].is_nil() {
        return Err("Found financial read has no value".into());
    }
    if status != 1 && !read[1].is_nil() {
        return Err("Unavailable financial read unexpectedly contains values".into());
    }
    Ok(Read {
        status,
        value: if read[1].is_nil() {
            None
        } else {
            Some(read[1].clone())
        },
        financial_revision: revision,
        observed_at,
    })
}
/// Fractions and monetary values are display-only here. Preserve the backend's
/// CalculationBasis report; the Sharpe/drawdown definitions are not recomputed.
pub fn metrics_context(
    registry: &Registry,
    wire: &Value,
) -> Result<BTreeMap<String, String>, String> {
    let row = registry.project(&format!("{ROOT}.FundPerformanceMetrics"), wire)?;
    let mut fields = BTreeMap::new();
    if row["HasHistory"] != true {
        return Ok(fields);
    }
    fields.insert(
        "metricsBasis".into(),
        row["CalculationBasis"]
            .as_str()
            .ok_or("Missing metrics calculation basis")?
            .into(),
    );
    for (index, (key, percent, grouped)) in [
        ("WinRate", true, false),
        ("AverageProfit", false, true),
        ("LossRate", true, false),
        ("AverageLoss", false, true),
        ("WinLossRatio", false, false),
        ("ActualSharpeRatio", false, false),
        ("PnlAmount", false, true),
        ("PnlPercent", true, false),
        ("TradeCommission", false, true),
        ("MaximumDrawdownPercent", true, false),
    ]
    .into_iter()
    .enumerate()
    {
        let value = if row[key].is_null() && key == "MaximumDrawdownPercent" {
            "N/A".into()
        } else {
            let raw = row[key].as_str().ok_or("Missing exact financial metric")?;
            let value = crate::services::outlook::decimal_fixed(raw, 2, percent)?;
            if grouped { group_amount(&value) } else { value }
        };
        fields.insert(format!("metric{index}"), value);
    }
    Ok(fields)
}
fn group_amount(value: &str) -> String {
    let (integer, fraction) = value.split_once('.').unwrap_or((value, ""));
    let negative = integer.starts_with('-');
    let integer = integer.trim_start_matches('-');
    let mut output = String::new();
    if negative {
        output.push('-');
    }
    for (index, ch) in integer.chars().enumerate() {
        if index > 0 && (integer.len() - index) % 3 == 0 {
            output.push(',');
        }
        output.push(ch);
    }
    if !fraction.is_empty() {
        output.push('.');
        output.push_str(fraction);
    }
    output
}
