//! Exact posting-rule version edits; account choices are taken from the same
//! backend ledger snapshot as the rule being replaced.
use super::*;
fn binding(registry: &Registry, config: &Json, text: &str) -> Result<Value, String> {
    if text.is_empty() {
        return Ok(Value::Nil);
    }
    let (id, version) = text
        .split_once('@')
        .ok_or("Select an exact account version")?;
    let id = id
        .parse::<i32>()
        .ok()
        .filter(|v| *v > 0)
        .ok_or("Invalid account identity")?;
    let version = version
        .parse::<i64>()
        .ok()
        .filter(|v| *v > 0)
        .ok_or("Invalid account version")?;
    if !config["Accounts"]
        .as_array()
        .ok_or("Invalid configured accounts")?
        .iter()
        .any(|a| {
            a["State"] == "Active"
                && a["Definition"]["AccountId"] == id
                && a["Definition"]["Version"] == version
        })
    {
        return Err("Select active exact account versions".into());
    }
    registry.encode(
        &prep::kind("LedgerAccountBinding"),
        &BTreeMap::from([
            ("AccountId".into(), id.into()),
            ("Version".into(), version.into()),
        ]),
    )
}
pub fn edit_rule(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: i32,
    read: &financial::Read,
    rule: uuid::Uuid,
    fields: &serde_json::Map<String, Json>,
) -> Result<Json, String> {
    let config = read.found()?;
    let original = selected(
        registry,
        config,
        "Rules",
        "FinancialConfiguredRule",
        "RuleId",
        &rule.to_string().into(),
    )?;
    let source = registry.project(&prep::kind("LedgerPostingRule"), &original)?;
    let version = source["Version"]
        .as_i64()
        .filter(|v| *v > 0)
        .ok_or("Missing rule version")?;
    let config = registry.project(&prep::kind("FinancialLedgerConfiguration"), config)?;
    let day = crate::backend::date_input::parse(field(fields, "effective"))?;
    if !config["Periods"]
        .as_array()
        .ok_or("Invalid configured periods")?
        .iter()
        .any(|p| {
            p["State"] == "Open"
                && p["StartDate"]
                    .as_str()
                    .is_some_and(|start| start <= day.to_string().as_str())
                && p["EndDate"]
                    .as_str()
                    .is_some_and(|end| end >= day.to_string().as_str())
        })
    {
        return Err("Choose an effective date in an open period".into());
    }
    let debit = binding(registry, &config, field(fields, "debit"))?;
    let credit = binding(registry, &config, field(fields, "credit"))?;
    if debit.is_nil() || credit.is_nil() {
        return Err("Select debit and credit account versions".into());
    }
    let asset = binding(registry, &config, field(fields, "asset"))?;
    let pnl = binding(registry, &config, field(fields, "pnl"))?;
    if asset.is_nil() != pnl.is_nil() {
        return Err("Valuation asset and unrealized P&L accounts must be selected together".into());
    }
    let confirmed = if field(fields, "confirmed").eq_ignore_ascii_case("true") {
        true
    } else if field(fields, "confirmed").eq_ignore_ascii_case("false") {
        false
    } else {
        return Err("Choose the confirmed movement requirement".into());
    };
    let mut next = overlay_type(
        registry,
        &prep::kind("LedgerPostingRule"),
        &original,
        &BTreeMap::from([
            (
                "Version".into(),
                version
                    .checked_add(1)
                    .and_then(|v| i32::try_from(v).ok())
                    .ok_or("Rule version overflow")?
                    .into(),
            ),
            ("Debit".into(), debit),
            ("Credit".into(), credit),
            ("RequiresConfirmedMovement".into(), confirmed.into()),
            ("ValuationAsset".into(), asset),
            ("UnrealizedPnl".into(), pnl),
            ("ContentHash".into(), "".into()),
        ]),
    )?;
    let hash = financial_hash(&financial_json::stored_json(
        registry,
        &prep::kind("LedgerPostingRule"),
        &next,
    )?)?;
    next = overlay_type(
        registry,
        &prep::kind("LedgerPostingRule"),
        &next,
        &BTreeMap::from([("ContentHash".into(), hash.into())]),
    )?;
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
                        "AddPostingRuleVersion",
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
            ("Rules".into(), Value::Array(vec![next])),
            ("PeriodStart".into(), date_only_wire(day)),
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
