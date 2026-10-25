//! Fund configuration drafts use current backend catalog identities. Scope and
//! audit fields are supplied by the owner, never accepted from editable cells.
use super::editors::{defaults, editor, enum_choices, input};
use super::*;
use crate::services::portfolio_commands as commands;
use crate::services::{
    business_context::BusinessContext, fund_selections::Selections, parameters::overlay_type,
};
fn split(value: &str) -> Vec<String> {
    value
        .split(',')
        .map(str::trim)
        .filter(|v| !v.is_empty())
        .map(str::to_string)
        .collect()
}
fn patch(
    registry: &Registry,
    name: &str,
    wire: &Value,
    fields: &serde_json::Map<String, Json>,
    allowed: &[&str],
) -> Result<Value, String> {
    let shape = registry.describe(&kind(name))?;
    let mut changes = BTreeMap::new();
    for key in allowed {
        let Some(value) = fields.get(*key).and_then(Json::as_str) else {
            continue;
        };
        let value = value.trim();
        let ty = &shape
            .members
            .iter()
            .find(|(name, _, _)| name == key)
            .ok_or("Unknown editable Fund field")?
            .2;
        let input = match ty.as_str() {
            "System.Decimal" => {
                ExactDecimal::parse(value)?;
                value.into()
            }
            "System.Int32" | "System.Int64" => {
                ExactDecimal::parse(value)?;
                let whole = value
                    .split('.')
                    .next()
                    .unwrap_or_default()
                    .parse::<i64>()
                    .map_err(|_| "Integer field overflow")?;
                if ty == "System.Int32" && i32::try_from(whole).is_err() {
                    return Err("Integer field overflow".into());
                }
                whole.into()
            }
            "System.String" => value.into(),
            "System.String[]" => json!(split(value)),
            "System.Boolean" => {
                if value.eq_ignore_ascii_case("true") {
                    true.into()
                } else if value.eq_ignore_ascii_case("false") {
                    false.into()
                } else {
                    return Err("Enter True or False".into());
                }
            }
            _ if registry.describe(ty).is_ok_and(|s| s.is_enum) => {
                registry.enum_value(ty, value)?;
                value.into()
            }
            _ => return Err(format!("Unsupported Fund input {key}")),
        };
        changes.insert((*key).into(), input);
    }
    registry.patch(&kind(name), wire, &changes)
}
fn csv(row: &Json, name: &str) -> Result<String, String> {
    Ok(row[name]
        .as_array()
        .ok_or("Invalid Fund selection array")?
        .iter()
        .map(text)
        .collect::<Vec<_>>()
        .join(","))
}
fn choice(value: &str, label: &str, enabled: bool) -> Json {
    json!({"value":value,"label":label,"enabled":enabled})
}
fn deployment_label(registry: &Registry, deployment: &Json) -> Result<String, String> {
    let enum_name = |kind: &str, value: &Json| -> Result<String, String> {
        if let Some(text) = value.as_str() {
            return Ok(text.into());
        }
        let number = value.as_i64().ok_or("Invalid catalog enum")?;
        registry
            .enum_names(kind)?
            .into_iter()
            .find(|name| registry.enum_value(kind, name) == Ok(number))
            .ok_or("Invalid catalog enum".into())
    };
    Ok(format!(
        "{} [{} / {} / {} {}] v{} {}",
        text(&deployment["Name"]),
        text(&deployment["Symbol"]),
        text(&deployment["Currency"]),
        enum_name(
            "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
            &deployment["TimeFrame"]
        )?,
        text(&deployment["Exchange"]),
        text(&deployment["Key"]["Version"]),
        enum_name(
            "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogLifecycleStatus",
            &deployment["Status"]
        )?
    ))
}
fn number(key: &str, caption: &str, value: Json, min: f64, max: f64, decimals: u8) -> Json {
    let mut field = input(key, caption, value, "number", true, None);
    field["minimum"] = min.into();
    field["maximum"] = max.into();
    field["decimals"] = decimals.into();
    field
}
impl PortfolioController {
    pub(super) async fn save_fund_editor(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        intent: &Json,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let (model, original) = self.editing.clone().ok_or("Missing Fund working copy")?;
        let fields = intent["fields"].as_object().ok_or("Missing Fund input")?;
        let parent = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        let portfolio = parent["PortfolioId"]
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|id| *id > 0)
            .ok_or("Invalid Portfolio identity")?;
        if view != "new-fund" && !self.data.is_current_fund(registry)? {
            return Err("Select the latest Fund version".into());
        }
        let fund = self
            .data
            .fund
            .as_ref()
            .map(|v| registry.project(&kind("FundMandateReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        if view == "assignment" {
            return self
                .save_assignment(backend, registry, intent, context, &parent, &fund, deadline)
                .await;
        }
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid Fund request identity")?;
        let mut value = match view {
            "new-fund" | "version-fund" => patch(
                registry,
                &model,
                &original,
                fields,
                &[
                    "Name",
                    "TradingYear",
                    "OperatingState",
                    "DecisionHorizon",
                    "Objective",
                    "UnderlyingUniverse",
                    "EligibleAssetTypes",
                    "PermittedDirections",
                    "PermittedConditions",
                ],
            )?,
            "allocation" => patch(
                registry,
                &model,
                &original,
                fields,
                &[
                    "TargetWeight",
                    "MinimumWeight",
                    "MaximumWeight",
                    "AllocatedCapital",
                    "Currency",
                    "SourcePolicyVersion",
                ],
            )?,
            "envelope" => patch(
                registry,
                &model,
                &original,
                fields,
                &[
                    "CapacityState",
                    "Currency",
                    "AllocatedCapital",
                    "AvailableCapital",
                    "MaximumRiskPerTrade",
                    "MaximumAggregateRisk",
                    "MaximumMargin",
                    "MaximumGrossNotional",
                    "MaximumContracts",
                    "MaximumOpenPositions",
                    "RemainingLossBudget",
                ],
            )?,
            _ => return Err("Unsupported Fund save".into()),
        };
        let mut changes = BTreeMap::new();
        let name = if matches!(view, "new-fund" | "version-fund") {
            let row = registry.project(&kind(&model), &value)?;
            let strings = |key: &str| -> Result<Vec<String>, String> {
                row[key]
                    .as_array()
                    .ok_or("Invalid Fund selection array")?
                    .iter()
                    .map(|v| {
                        v.as_str()
                            .map(str::to_string)
                            .ok_or("Invalid Fund selection".into())
                    })
                    .collect()
            };
            self.selections
                .as_ref()
                .ok_or("Load current Fund choices")?
                .validate(
                    registry,
                    &strings("UnderlyingUniverse")?,
                    &strings("EligibleAssetTypes")?,
                    &strings("PermittedDirections")?,
                    &strings("PermittedConditions")?,
                )?;
            if !matches!(
                row["DecisionHorizon"].as_str(),
                Some("Daily" | "Weekly" | "Monthly")
            ) {
                return Err("Select Daily, Weekly or Monthly".into());
            }
            let families = split(field(intent, "families"));
            let mut selected = vec![];
            let mut codes = vec![];
            let mut seen = std::collections::BTreeSet::new();
            for family in families {
                if !seen.insert(family.clone()) {
                    return Err("Duplicate strategy deployment selection".into());
                }
                let deployment = self
                    .deployments
                    .iter()
                    .find(|d| {
                        family
                            == format!("{}@{}", text(&d["Key"]["Id"]), text(&d["Key"]["Version"]))
                    })
                    .ok_or("Remove unavailable strategy deployments before saving")?;
                let key=registry.value_from_json("TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey",&json!({"Kind":"Deployment","Id":deployment["Key"]["Id"],"Version":deployment["Key"]["Version"]}))?;
                selected.push(registry.encode(
                    "TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference",
                    &BTreeMap::from([("CatalogDeployment".into(), key)]),
                )?);
                let code = text(&deployment["SystemKey"]);
                if !codes.contains(&code) {
                    codes.push(code);
                }
            }
            changes.extend(BTreeMap::from([
                ("SchemaVersion".into(), 3.into()),
                (
                    "PermittedTradeStrategyFamilies".into(),
                    Value::Array(selected),
                ),
                (
                    "PermittedTradeFamilies".into(),
                    Value::Array(codes.into_iter().map(Value::from).collect()),
                ),
            ]));
            if view == "new-fund" {
                changes.insert(
                    "OperatingState".into(),
                    registry
                        .enum_value(
                            &format!("{}.Contracts.FundOperatingState", api::ROOT),
                            "Draft",
                        )?
                        .into(),
                );
                "CreateFundMandateCommand"
            } else {
                "AddFundMandateVersionCommand"
            }
        } else {
            changes.extend(BTreeMap::from([
                ("PortfolioId".into(), portfolio.into()),
                (
                    "PortfolioVersion".into(),
                    parent["PortfolioVersion"]
                        .as_i64()
                        .ok_or("Missing Portfolio version")?
                        .into(),
                ),
                (
                    "FundId".into(),
                    fund["FundId"].as_i64().ok_or("Select a Fund")?.into(),
                ),
                (
                    "FundMandateVersion".into(),
                    fund["FundMandateVersion"]
                        .as_i64()
                        .ok_or("Missing Fund version")?
                        .into(),
                ),
                (
                    "SourcePolicyId".into(),
                    parent["ActivePolicyId"]
                        .as_i64()
                        .ok_or("Missing policy identity")?
                        .into(),
                ),
                ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                (
                    "CreatedBy".into(),
                    std::env::var("USERNAME").unwrap_or_default().into(),
                ),
            ]));
            let row = registry.project(&kind(&model), &value)?;
            changes.insert(
                "Currency".into(),
                text(&row["Currency"]).trim().to_uppercase().into(),
            );
            if view == "allocation" {
                let prior = self
                    .data
                    .allocation
                    .as_ref()
                    .map(|v| registry.project(&kind(&model), v))
                    .transpose()?
                    .unwrap_or(Json::Null);
                changes.insert(
                    "AllocationVersion".into(),
                    prior["AllocationVersion"]
                        .as_i64()
                        .unwrap_or_default()
                        .checked_add(1)
                        .ok_or("Allocation version overflow")?
                        .into(),
                );
                "DelegateFundAllocationCommand"
            } else {
                let prior = self
                    .data
                    .envelope
                    .as_ref()
                    .map(|v| registry.project(&kind(&model), v))
                    .transpose()?
                    .unwrap_or(Json::Null);
                let days = field(intent, "days")
                    .parse::<i64>()
                    .ok()
                    .filter(|v| (1..=3650).contains(v))
                    .ok_or("Effective Days must be between 1 and 3650")?;
                let expires = context
                    .as_of_utc
                    .checked_add_signed(chrono::Duration::days(days))
                    .ok_or("Envelope expiry overflow")?;
                let id = prior["EnvelopeId"]
                    .as_str()
                    .and_then(|v| uuid::Uuid::parse_str(v).ok())
                    .filter(|v| !v.is_nil())
                    .unwrap_or_else(uuid::Uuid::new_v4);
                changes.extend(BTreeMap::from([
                    ("EnvelopeId".into(), id.to_string().into()),
                    (
                        "EnvelopeVersion".into(),
                        prior["EnvelopeVersion"]
                            .as_i64()
                            .unwrap_or_default()
                            .checked_add(1)
                            .ok_or("Envelope version overflow")?
                            .into(),
                    ),
                    (
                        "SourcePolicyVersion".into(),
                        parent["ActivePolicyVersion"]
                            .as_i64()
                            .ok_or("Missing policy version")?
                            .into(),
                    ),
                    ("ExpiresAtUtc".into(), timestamp_wire(expires)),
                ]));
                "DelegateFundRiskEnvelopeCommand"
            }
        };
        value = overlay_type(registry, &kind(&model), &value, &changes)?;
        let row = registry.project(&kind(&model), &value)?;
        let fund_id = row["FundId"]
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|v| *v > 0)
            .ok_or("Invalid Fund identity")?;
        self.editing = Some((model, value.clone()));
        let child = matches!(view, "new-fund" | "version-fund").then_some(fund_id);
        let mut command_id = request;
        let mut payload = BTreeMap::from([(
            if view == "allocation" {
                "Allocation"
            } else if view == "envelope" {
                "Envelope"
            } else {
                "Mandate"
            }
            .into(),
            value.clone(),
        )]);
        if view == "new-fund" {
            let key = self
                .edit_key
                .ok_or("Missing prepared Fund create identity")?;
            command_id = api::create_command_id(key, &value)?;
            payload.insert("IdempotencyKey".into(), key.to_string().into());
        } else {
            payload.insert(
                if view == "version-fund" {
                    "ExpectedVersion"
                } else {
                    "ExpectedPortfolioVersion"
                }
                .into(),
                if view == "version-fund" {
                    self.data.fund_revision
                } else {
                    self.data.portfolio_revision
                }
                .into(),
            );
        }
        let prepared = commands::prepare(
            registry, context, name, portfolio, child, command_id, request, payload,
        )?;
        self.write_in_flight = true;
        let result = commands::submit(backend, &prepared, deadline).await;
        self.write_in_flight = false;
        result.map_err(|error| {
            if error.starts_with("OutcomeUnknown:") {
                self.requires_reload = true;
            }
            error
        })?;
        // Creating a mandate and attaching it are two explicit backend commands.
        // A failed attachment is retained independently; never create it twice.
        if view == "new-fund" {
            let attach = commands::prepare(
                registry,
                context,
                "AddFundToPortfolioCommand",
                portfolio,
                None,
                uuid::Uuid::new_v4(),
                request,
                BTreeMap::from([
                    (
                        "FundId".into(),
                        Value::Array(vec![portfolio.into(), fund_id.into()]),
                    ),
                    (
                        "ExpectedPortfolioVersion".into(),
                        self.data.portfolio_revision.into(),
                    ),
                ]),
            )?;
            self.pending_attachment = Some(attach.clone());
            self.attachment_rejected = false;
            self.write_in_flight = true;
            let result = commands::submit(backend, &attach, deadline).await;
            self.write_in_flight = false;
            if let Err(error) = result {
                self.attachment_rejected = !error.starts_with("OutcomeUnknown:");
                self.requires_reload = true;
                if let Some(dialog) = self.dialogs.last_mut() {
                    dialog["buttons"] = json!(["Retry attachment"]);
                }
                return Err(format!(
                    "OutcomeUnknown: Fund mandate was created, but attachment needs reconciliation: {error}"
                ));
            }
            self.pending_attachment = None;
        }
        self.accepted_write = true;
        self.dialogs.pop();
        self.editing = None;
        self.edit_key = None;
        self.data
            .refresh(backend, registry, &self.data.filter.clone(), deadline)
            .await?;
        self.load_policies(backend, registry, deadline).await?;
        self.load_metrics(backend, registry, deadline).await?;
        self.message = self.data.status.clone();
        Ok("Fund command committed".into())
    }
    pub(super) async fn open_fund_editor(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<Json, String> {
        let parent = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        if view != "new-fund" && !self.data.is_current_fund(registry)? {
            return Err(
                "Select the latest Fund version before changing its configuration or state".into(),
            );
        }
        let fund = self
            .data
            .fund
            .as_ref()
            .map(|v| registry.project(&kind("FundMandateReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        match view {
            "new-fund" | "version-fund" => {
                self.deployments =
                    crate::services::portfolio_reference::deployments(backend, registry, deadline)
                        .await?;
                self.selections = Some(Selections::load(backend, registry, deadline).await?);
                let mut changes = BTreeMap::from([
                    ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                    ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                    (
                        "CreatedBy".into(),
                        std::env::var("USERNAME").unwrap_or_default().into(),
                    ),
                    ("EffectiveUntilUtc".into(), Value::Nil),
                ]);
                let original = if view == "new-fund" {
                    let wire = api::query(
                        backend,
                        registry,
                        context,
                        "AllocatePortfolioBusinessIdQuery",
                        "Fund",
                        BTreeMap::from([(
                            "Kind".into(),
                            registry
                                .enum_value(
                                    &format!(
                                        "{}.ServiceApi.PortfolioBusinessIdentityKind",
                                        api::ROOT
                                    ),
                                    "Fund",
                                )?
                                .into(),
                        )]),
                        deadline,
                    )
                    .await?;
                    let allocated = registry.project(
                        &format!("{}.ServiceApi.PortfolioBusinessIdAllocation", api::ROOT),
                        &wire,
                    )?;
                    let id = allocated["Value"]
                        .as_i64()
                        .filter(|id| *id > 0 && *id <= i32::MAX as i64)
                        .ok_or("Invalid allocated Fund identity")?;
                    if allocated["Kind"] != "Fund" {
                        return Err("Allocated identity has wrong business kind".into());
                    }
                    use chrono::Datelike;
                    changes.extend(BTreeMap::from([
                        (
                            "PortfolioId".into(),
                            parent["PortfolioId"]
                                .as_i64()
                                .ok_or("Invalid Portfolio identity")?
                                .into(),
                        ),
                        ("FundId".into(), id.into()),
                        ("FundCode".into(), format!("FUND-{id}").into()),
                        ("FundMandateVersion".into(), 1.into()),
                        ("TradingYear".into(), context.as_of_utc.year().into()),
                        (
                            "OperatingState".into(),
                            registry
                                .enum_value(
                                    &format!("{}.Contracts.FundOperatingState", api::ROOT),
                                    "Draft",
                                )?
                                .into(),
                        ),
                        ("DecisionHorizon".into(), "Daily".into()),
                    ]));
                    defaults("FundMandateReadModel")?
                } else {
                    let version = fund["FundMandateVersion"]
                        .as_i64()
                        .and_then(|v| v.checked_add(1))
                        .ok_or("Fund mandate version overflow")?;
                    changes.insert("FundMandateVersion".into(), version.into());
                    self.data.fund.clone().ok_or("Select a Fund")?
                };
                let value =
                    overlay_type(registry, &kind("FundMandateReadModel"), &original, &changes)?;
                let row = registry.project(&kind("FundMandateReadModel"), &value)?;
                let mut family_choices = self
                    .deployments
                    .iter()
                    .map(|d| {
                        Ok(choice(
                            &format!("{}@{}", text(&d["Key"]["Id"]), text(&d["Key"]["Version"])),
                            &deployment_label(registry, d)?,
                            true,
                        ))
                    })
                    .collect::<Result<Vec<_>, String>>()?;
                let refs = row["PermittedTradeStrategyFamilies"]
                    .as_array()
                    .ok_or("Invalid Fund permissions")?;
                let mut selected = vec![];
                for reference in refs {
                    if let Some(deployment) = self.deployments.iter().find(|d| {
                        policy::same_deployment(&d["Key"], &reference["CatalogDeployment"])
                    }) {
                        selected.push(format!(
                            "{}@{}",
                            text(&deployment["Key"]["Id"]),
                            text(&deployment["Key"]["Version"])
                        ));
                    } else {
                        let key = format!("unavailable:{}", family_choices.len());
                        let id = if reference["CatalogDeployment"].is_null() {
                            text(&reference["TradeStrategyFamilyId"])
                        } else {
                            text(&reference["CatalogDeployment"]["Id"])
                        };
                        family_choices.push(choice(
                            &key,
                            &format!("Unavailable: {id} — uncheck to remove"),
                            true,
                        ));
                        selected.push(key);
                    }
                }
                if refs.is_empty() {
                    for legacy in row["PermittedTradeFamilies"]
                        .as_array()
                        .ok_or("Invalid legacy permissions")?
                    {
                        let key = format!("unavailable:legacy:{}", text(legacy));
                        family_choices.push(choice(
                            &key,
                            &format!("Unavailable: {} — uncheck to remove", text(legacy)),
                            true,
                        ));
                        selected.push(key);
                    }
                }
                let selections = self.selections.as_ref().ok_or("Missing Fund choices")?;
                let multi =
                    |key: &str, caption: &str, mut choices: Vec<Json>| -> Result<Json, String> {
                        let values = row[key].as_array().ok_or("Invalid Fund choices")?;
                        for value in values {
                            let text = text(value);
                            if !choices.iter().any(|c| c["value"] == text) {
                                choices.push(choice(&text, &format!("Unavailable: {text}"), false));
                            }
                        }
                        Ok(input(
                            key,
                            caption,
                            csv(&row, key)?,
                            "multi",
                            true,
                            Some(choices),
                        ))
                    };
                let fields = vec![
                    input(
                        "FundId",
                        "Fund ID",
                        row["FundId"].clone(),
                        "text",
                        false,
                        None,
                    ),
                    input("Name", "Name", row["Name"].clone(), "text", true, None),
                    number(
                        "TradingYear",
                        "Trading Year",
                        row["TradingYear"].clone(),
                        2000.,
                        2200.,
                        0,
                    ),
                    input(
                        "OperatingState",
                        "Operating State",
                        row["OperatingState"].clone(),
                        "choice",
                        view != "new-fund",
                        Some(enum_choices(
                            registry,
                            &format!("{}.Contracts.FundOperatingState", api::ROOT),
                        )?),
                    ),
                    input(
                        "DecisionHorizon",
                        "Decision Horizon",
                        row["DecisionHorizon"].clone(),
                        "choice",
                        true,
                        Some(
                            ["Daily", "Weekly", "Monthly"]
                                .into_iter()
                                .map(|s| choice(s, s, true))
                                .collect(),
                        ),
                    ),
                    input(
                        "Objective",
                        "Objective",
                        row["Objective"].clone(),
                        "text",
                        true,
                        None,
                    ),
                    multi(
                        "UnderlyingUniverse",
                        "Underlyings",
                        selections
                            .underlyings
                            .iter()
                            .map(|s| choice(s, s, true))
                            .collect(),
                    )?,
                    multi(
                        "EligibleAssetTypes",
                        "Asset Types",
                        selections.choices(registry, "AssetTypes")?,
                    )?,
                    multi(
                        "PermittedDirections",
                        "Directions",
                        selections.choices(registry, "Directions")?,
                    )?,
                    multi(
                        "PermittedConditions",
                        "Market Conditions",
                        selections.choices(registry, "MarketConditions")?,
                    )?,
                    input(
                        "families",
                        "Permitted Strategy Deployments",
                        selected.join(","),
                        "checklist",
                        true,
                        Some(family_choices),
                    ),
                ];
                self.editing = Some(("FundMandateReadModel".into(), value));
                self.edit_key = Some(uuid::Uuid::new_v4());
                Ok(editor(
                    view,
                    if view == "new-fund" {
                        "Create Fund"
                    } else {
                        "Change Fund"
                    },
                    780,
                    560,
                    225,
                    fields,
                    "Save",
                ))
            }
            "allocation" => {
                let value = if let Some(value) = &self.data.allocation {
                    value.clone()
                } else {
                    overlay_type(
                        registry,
                        &kind("FundAllocationReadModel"),
                        &defaults("FundAllocationReadModel")?,
                        &BTreeMap::from([
                            ("TargetWeight".into(), "0.5".into()),
                            ("MaximumWeight".into(), "1".into()),
                            (
                                "Currency".into(),
                                parent["BaseCurrency"]
                                    .as_str()
                                    .ok_or("Missing Portfolio currency")?
                                    .into(),
                            ),
                            (
                                "SourcePolicyVersion".into(),
                                parent["ActivePolicyVersion"]
                                    .as_i64()
                                    .unwrap_or_default()
                                    .max(1)
                                    .into(),
                            ),
                        ]),
                    )?
                };
                let row = registry.project(&kind("FundAllocationReadModel"), &value)?;
                let fields = vec![
                    number(
                        "TargetWeight",
                        "Target Weight",
                        row["TargetWeight"].clone(),
                        0.,
                        1.,
                        4,
                    ),
                    number(
                        "MinimumWeight",
                        "Minimum Weight",
                        row["MinimumWeight"].clone(),
                        0.,
                        1.,
                        4,
                    ),
                    number(
                        "MaximumWeight",
                        "Maximum Weight",
                        row["MaximumWeight"].clone(),
                        0.,
                        1.,
                        4,
                    ),
                    number(
                        "AllocatedCapital",
                        "Allocated Capital",
                        row["AllocatedCapital"].clone(),
                        0.,
                        1e12,
                        2,
                    ),
                    input(
                        "Currency",
                        "Currency",
                        row["Currency"].clone(),
                        "text",
                        true,
                        None,
                    ),
                    number(
                        "SourcePolicyVersion",
                        "Source Policy Version",
                        row["SourcePolicyVersion"].clone(),
                        1.,
                        i64::MAX as f64,
                        0,
                    ),
                ];
                self.editing = Some(("FundAllocationReadModel".into(), value));
                Ok(editor(
                    view,
                    "Fund Allocation",
                    640,
                    280,
                    225,
                    fields,
                    "Save",
                ))
            }
            "envelope" => {
                let value = if let Some(value) = &self.data.envelope {
                    value.clone()
                } else {
                    overlay_type(
                        registry,
                        &kind("FundRiskEnvelopeReadModel"),
                        &defaults("FundRiskEnvelopeReadModel")?,
                        &BTreeMap::from([
                            (
                                "CapacityState".into(),
                                registry
                                    .enum_value(
                                        &format!("{}.Contracts.FundCapacityState", api::ROOT),
                                        "Available",
                                    )?
                                    .into(),
                            ),
                            (
                                "Currency".into(),
                                parent["BaseCurrency"]
                                    .as_str()
                                    .ok_or("Missing Portfolio currency")?
                                    .into(),
                            ),
                        ]),
                    )?
                };
                let row = registry.project(&kind("FundRiskEnvelopeReadModel"), &value)?;
                let mut fields = vec![
                    input(
                        "CapacityState",
                        "Capacity State",
                        row["CapacityState"].clone(),
                        "choice",
                        true,
                        Some(enum_choices(
                            registry,
                            &format!("{}.Contracts.FundCapacityState", api::ROOT),
                        )?),
                    ),
                    input(
                        "Currency",
                        "Currency",
                        row["Currency"].clone(),
                        "text",
                        true,
                        None,
                    ),
                ];
                for (key, label) in [
                    ("AllocatedCapital", "Allocated Capital"),
                    ("AvailableCapital", "Available Capital"),
                    ("MaximumRiskPerTrade", "Maximum Risk / Trade"),
                    ("MaximumAggregateRisk", "Maximum Aggregate Risk"),
                    ("MaximumMargin", "Maximum Margin"),
                    ("MaximumGrossNotional", "Maximum Gross Notional"),
                    ("MaximumContracts", "Maximum Contracts"),
                    ("MaximumOpenPositions", "Maximum Open Positions"),
                    ("RemainingLossBudget", "Remaining Loss Budget"),
                ] {
                    let count = matches!(key, "MaximumContracts" | "MaximumOpenPositions");
                    fields.push(number(
                        key,
                        label,
                        row[key].clone(),
                        0.,
                        if count { i32::MAX as f64 } else { 1e12 },
                        if count { 0 } else { 2 },
                    ));
                }
                fields.push(number("days", "Effective Days", 30.into(), 1., 3650., 0));
                self.editing = Some(("FundRiskEnvelopeReadModel".into(), value));
                Ok(editor(
                    view,
                    "Fund Risk Envelope",
                    780,
                    450,
                    225,
                    fields,
                    "Save",
                ))
            }
            "assignment" => {
                self.deployments =
                    crate::services::portfolio_reference::deployments(backend, registry, deadline)
                        .await?;
                if self.deployments.is_empty() {
                    return Err("No strategy deployments are configured. Configure a deployment in Reference Data before assigning it to a Fund".into());
                }
                let permissions = fund["PermittedTradeStrategyFamilies"]
                    .as_array()
                    .ok_or("Invalid Fund permissions")?;
                let choices = self
                    .deployments
                    .iter()
                    .filter(|d| {
                        permissions
                            .iter()
                            .any(|p| policy::same_deployment(&p["CatalogDeployment"], &d["Key"]))
                    })
                    .map(|d| {
                        Ok(choice(
                            &format!("{}@{}", text(&d["Key"]["Id"]), text(&d["Key"]["Version"])),
                            &deployment_label(registry, d)?,
                            true,
                        ))
                    })
                    .collect::<Result<Vec<_>, String>>()?;
                let mut dialog = editor(
                    view,
                    "Trade Template Assignment",
                    780,
                    450,
                    225,
                    vec![
                        input(
                            "Enabled",
                            "Enable assignment",
                            "False",
                            "check",
                            false,
                            None,
                        ),
                        input("TradeTemplateId", "Deployment ID", "", "text", false, None),
                        input(
                            "TradeTemplateVersion",
                            "Template Version",
                            "1",
                            "number",
                            false,
                            None,
                        ),
                        input(
                            "DecisionHorizon",
                            "Decision Horizon",
                            fund["DecisionHorizon"].clone(),
                            "choice",
                            false,
                            Some(
                                ["Daily", "Weekly", "Monthly"]
                                    .into_iter()
                                    .map(|s| choice(s, s, true))
                                    .collect(),
                            ),
                        ),
                        input(
                            "UnderlyingUniverse",
                            "Underlyings (CSV)",
                            fund["UnderlyingUniverse"]
                                .as_array()
                                .ok_or("Invalid Fund underlyings")?
                                .iter()
                                .map(text)
                                .collect::<Vec<_>>()
                                .join(", "),
                            "text",
                            false,
                            None,
                        ),
                        input(
                            "AssetType",
                            "Asset Type",
                            fund["EligibleAssetTypes"]
                                .as_array()
                                .and_then(|v| v.first())
                                .cloned()
                                .unwrap_or(Json::Null),
                            "text",
                            false,
                            None,
                        ),
                        input(
                            "deployment",
                            "Trade Family",
                            "",
                            "choice",
                            true,
                            Some(choices.clone()),
                        ),
                        number("Priority", "Priority", 0.into(), 0., i32::MAX as f64, 0),
                        input(
                            "TradeSelectionHintProfileId",
                            "Selection Hint Profile ID",
                            "",
                            "text",
                            false,
                            None,
                        ),
                        input(
                            "TradeSelectionHintProfileVersion",
                            "Hint Profile Version",
                            "0",
                            "number",
                            false,
                            None,
                        ),
                        input(
                            "OrderCompositionProfileId",
                            "Composition Profile ID",
                            "",
                            "text",
                            false,
                            None,
                        ),
                        input(
                            "OrderCompositionProfileVersion",
                            "Composition Version",
                            "0",
                            "number",
                            false,
                            None,
                        ),
                    ],
                    "Save",
                );
                if choices.len() == 1 {
                    self.populate_assignment(
                        registry,
                        &fund,
                        &mut dialog,
                        choices[0]["value"]
                            .as_str()
                            .ok_or("Invalid deployment choice")?,
                    )?;
                } else if choices.is_empty() {
                    dialog["status"]="No active catalog family is permitted by this Fund. Update its mandate before assigning a strategy.".into();
                }
                self.editing = Some((
                    "FundTradeTemplateAssignmentReadModel".into(),
                    defaults("FundTradeTemplateAssignmentReadModel")?,
                ));
                Ok(dialog)
            }
            _ => Err("Unsupported Fund editor".into()),
        }
    }
    fn populate_assignment(
        &self,
        registry: &Registry,
        fund: &Json,
        dialog: &mut Json,
        key: &str,
    ) -> Result<(), String> {
        let d = self
            .deployments
            .iter()
            .find(|d| key == format!("{}@{}", text(&d["Key"]["Id"]), text(&d["Key"]["Version"])))
            .ok_or("Select a current deployment")?;
        if !fund["PermittedTradeStrategyFamilies"]
            .as_array()
            .ok_or("Invalid Fund permissions")?
            .iter()
            .any(|p| policy::same_deployment(&p["CatalogDeployment"], &d["Key"]))
        {
            return Err("Deployment is not permitted by the Fund".into());
        }
        let parameters = d["PipelineParameters"]
            .as_array()
            .ok_or("Invalid deployment parameters")?;
        let parameter = |name: &str| -> Result<Option<&Json>, String> {
            let kind = registry.enum_value(
                "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind",
                name,
            )?;
            let rows = parameters
                .iter()
                .filter(|p| p["Kind"] == kind || p["Kind"] == name)
                .collect::<Vec<_>>();
            if rows.len() > 1 {
                return Err("Duplicate deployment pipeline role".into());
            }
            Ok(rows.first().copied())
        };
        let hint = parameter("TradeSelection")?;
        let composition = parameter("OrderComposition")?;
        let horizon = registry
            .enum_names("TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType")?
            .into_iter()
            .find(|name| {
                registry
                    .enum_value(
                        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                        name,
                    )
                    .is_ok_and(|value| d["Horizon"] == value || d["Horizon"] == *name)
            })
            .ok_or("Invalid deployment horizon")?;
        let nil = Json::Null;
        let hint = hint.unwrap_or(&nil);
        let composition = composition.unwrap_or(&nil);
        let values = BTreeMap::from([
            ("deployment", key.to_string()),
            ("TradeTemplateId", text(&d["Key"]["Id"])),
            ("TradeTemplateVersion", text(&d["Key"]["Version"])),
            ("DecisionHorizon", horizon),
            (
                "UnderlyingUniverse",
                d["Products"]
                    .as_array()
                    .ok_or("Invalid deployment products")?
                    .iter()
                    .map(|p| text(&p["Symbol"]))
                    .fold(Vec::<String>::new(), |mut values, symbol| {
                        if !values.contains(&symbol) {
                            values.push(symbol);
                        }
                        values
                    })
                    .join(","),
            ),
            (
                "AssetType",
                d["InstrumentClasses"]
                    .as_array()
                    .ok_or("Invalid deployment asset classes")?
                    .iter()
                    .find(|asset| {
                        fund["EligibleAssetTypes"]
                            .as_array()
                            .is_some_and(|rows| rows.contains(asset))
                    })
                    .map(text)
                    .unwrap_or_default(),
            ),
            ("TradeSelectionHintProfileId", text(&hint["Id"])),
            (
                "TradeSelectionHintProfileVersion",
                hint["Version"].as_i64().unwrap_or_default().to_string(),
            ),
            ("OrderCompositionProfileId", text(&composition["Id"])),
            (
                "OrderCompositionProfileVersion",
                composition["Version"]
                    .as_i64()
                    .unwrap_or_default()
                    .to_string(),
            ),
            ("Enabled", "false".to_string()),
        ]);
        let published = registry.enum_value(
            "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogLifecycleStatus",
            "Published",
        )?;
        for field in dialog["fields"]
            .as_array_mut()
            .ok_or("Invalid assignment dialog")?
        {
            let key = field["key"].as_str().unwrap_or_default().to_string();
            if let Some(value) = values.get(key.as_str()) {
                field["value"] = value.clone().into();
            }
            if key == "Enabled" {
                field["enabled"] =
                    (d["Status"] == published && !hint.is_null() && !composition.is_null()).into();
            }
        }
        Ok(())
    }
    pub(super) fn choose_assignment(
        &mut self,
        registry: &Registry,
        intent: &Json,
    ) -> Result<String, String> {
        let mut dialog = self
            .dialogs
            .last()
            .cloned()
            .filter(|d| d["key"] == "assignment")
            .ok_or("Open an assignment editor")?;
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?;
        self.populate_assignment(registry, &fund, &mut dialog, field(intent, "deployment"))?;
        *self.dialogs.last_mut().ok_or("Assignment editor closed")? = dialog;
        Ok("Deployment selected".into())
    }
    pub(super) async fn retry_fund_attachment(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let mut prepared = self
            .pending_attachment
            .clone()
            .ok_or("No Fund attachment is pending")?;
        if self.attachment_rejected {
            // A definitive rejection permits a new operation using a fresh revision.
            // An ambiguous attempt always reuses its original bytes and command ID.
            let context = BusinessContext::load(backend).await?;
            let portfolio = prepared
                .subject
                .entity
                .parse::<i32>()
                .map_err(|_| "Invalid retained attachment scope")?;
            let revision = api::query(
                backend,
                registry,
                &context,
                "GetPortfolioRevisionQuery",
                &portfolio.to_string(),
                BTreeMap::from([("PortfolioId".into(), portfolio.into())]),
                deadline,
            )
            .await?;
            let revision = registry.project(
                &format!("{}.ServiceApi.PortfolioAggregateRevision", api::ROOT),
                &revision,
            )?;
            if revision["PortfolioId"] != portfolio {
                return Err("Attachment revision crossed Portfolio scope".into());
            }
            let fund = crate::services::strategy::member(
                registry,
                &format!("{}.Commands.AddFundToPortfolioCommand", api::ROOT),
                &prepared.body,
                "FundId",
            )?;
            let correlation =
                uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
                    .map_err(|_| "Invalid retry identity")?;
            prepared = commands::prepare(
                registry,
                &context,
                "AddFundToPortfolioCommand",
                portfolio,
                None,
                uuid::Uuid::new_v4(),
                correlation,
                BTreeMap::from([
                    ("FundId".into(), fund),
                    (
                        "ExpectedPortfolioVersion".into(),
                        revision["Revision"]
                            .as_i64()
                            .filter(|v| *v > 0)
                            .ok_or("Missing attachment revision")?
                            .into(),
                    ),
                ]),
            )?;
            self.pending_attachment = Some(prepared.clone());
            self.attachment_rejected = false;
        }
        self.write_in_flight = true;
        let result = commands::submit(backend, &prepared, deadline).await;
        self.write_in_flight = false;
        if let Err(error) = result {
            self.attachment_rejected = !error.starts_with("OutcomeUnknown:");
            self.requires_reload = true;
            return Err(format!(
                "OutcomeUnknown: Fund mandate is already created; attachment remains unresolved: {error}"
            ));
        }
        self.accepted_write = true;
        self.pending_attachment = None;
        self.requires_reload = false;
        self.dialogs.clear();
        self.editing = None;
        self.edit_key = None;
        self.data
            .refresh(backend, registry, &self.data.filter.clone(), deadline)
            .await?;
        self.load_policies(backend, registry, deadline).await?;
        self.load_metrics(backend, registry, deadline).await?;
        self.message.clear();
        Ok("Fund attachment committed".into())
    }
    async fn save_assignment(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        context: &BusinessContext,
        parent: &Json,
        fund: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let mut dialog = self
            .dialogs
            .last()
            .cloned()
            .filter(|d| d["key"] == "assignment")
            .ok_or("Open an assignment editor")?;
        self.populate_assignment(registry, fund, &mut dialog, field(intent, "deployment"))?;
        let values = dialog["fields"]
            .as_array()
            .ok_or("Invalid assignment fields")?
            .iter()
            .map(|f| (text(&f["key"]), text(&f["value"])))
            .collect::<BTreeMap<_, _>>();
        let deployment = self
            .deployments
            .iter()
            .find(|d| {
                field(intent, "deployment")
                    == format!("{}@{}", text(&d["Key"]["Id"]), text(&d["Key"]["Version"]))
            })
            .ok_or("Select a permitted deployment")?;
        let enabled = match field(intent, "Enabled") {
            v if v.eq_ignore_ascii_case("true") => true,
            v if v.eq_ignore_ascii_case("false") => false,
            _ => return Err("Enter True or False for Enabled".into()),
        };
        if enabled
            && !dialog["fields"]
                .as_array()
                .unwrap()
                .iter()
                .any(|f| f["key"] == "Enabled" && f["enabled"] == true)
        {
            return Err("Only a published deployment with selection and composition parameters can be enabled".into());
        }
        let portfolio = parent["PortfolioId"]
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|v| *v > 0)
            .ok_or("Invalid Portfolio identity")?;
        let child = fund["FundId"]
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|v| *v > 0)
            .ok_or("Invalid Fund identity")?;
        let key=registry.value_from_json("TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey",&json!({"Kind":"Deployment","Id":deployment["Key"]["Id"],"Version":deployment["Key"]["Version"]}))?;
        let reference = registry.encode(
            "TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference",
            &BTreeMap::from([("CatalogDeployment".into(), key)]),
        )?;
        let required = |key: &str| {
            values
                .get(key)
                .map(String::as_str)
                .ok_or_else(|| format!("Missing derived assignment field {key}"))
        };
        let profile_id = |key: &str| -> Result<Value, String> {
            let text = required(key)?;
            if text.is_empty() {
                Ok(uuid::Uuid::nil().to_string().into())
            } else {
                Ok(uuid::Uuid::parse_str(text)
                    .map_err(|_| "Invalid deployment profile identity")?
                    .to_string()
                    .into())
            }
        };
        let integer = |key: &str| -> Result<Value, String> {
            required(key)?
                .parse::<i64>()
                .map(Value::from)
                .map_err(|_| "Invalid deployment profile version".into())
        };
        let priority = field(intent, "Priority")
            .split('.')
            .next()
            .unwrap_or_default()
            .parse::<i32>()
            .map_err(|_| "Invalid assignment priority")?;
        let value = overlay_type(
            registry,
            &kind("FundTradeTemplateAssignmentReadModel"),
            &defaults("FundTradeTemplateAssignmentReadModel")?,
            &BTreeMap::from([
                ("SchemaVersion".into(), 3.into()),
                ("PortfolioId".into(), portfolio.into()),
                (
                    "PortfolioVersion".into(),
                    parent["PortfolioVersion"]
                        .as_i64()
                        .ok_or("Missing Portfolio version")?
                        .into(),
                ),
                ("FundId".into(), child.into()),
                (
                    "FundMandateVersion".into(),
                    fund["FundMandateVersion"]
                        .as_i64()
                        .ok_or("Missing Fund version")?
                        .into(),
                ),
                (
                    "AssignmentVersion".into(),
                    self.data
                        .fund_revision
                        .checked_add(1)
                        .filter(|v| *v > 1)
                        .ok_or("Reload Fund revision")?
                        .into(),
                ),
                (
                    "TradeTemplateId".into(),
                    required("TradeTemplateId")?.into(),
                ),
                (
                    "TradeTemplateVersion".into(),
                    integer("TradeTemplateVersion")?,
                ),
                ("Enabled".into(), enabled.into()),
                (
                    "DecisionHorizon".into(),
                    required("DecisionHorizon")?.into(),
                ),
                (
                    "UnderlyingUniverse".into(),
                    Value::Array(
                        split(required("UnderlyingUniverse")?)
                            .into_iter()
                            .map(Value::from)
                            .collect(),
                    ),
                ),
                ("AssetType".into(), required("AssetType")?.into()),
                ("TradeFamily".into(), text(&deployment["SystemKey"]).into()),
                ("Priority".into(), priority.into()),
                ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                (
                    "TradeSelectionHintProfileId".into(),
                    profile_id("TradeSelectionHintProfileId")?,
                ),
                (
                    "TradeSelectionHintProfileVersion".into(),
                    integer("TradeSelectionHintProfileVersion")?,
                ),
                (
                    "OrderCompositionProfileId".into(),
                    profile_id("OrderCompositionProfileId")?,
                ),
                (
                    "OrderCompositionProfileVersion".into(),
                    integer("OrderCompositionProfileVersion")?,
                ),
                ("TradeStrategyFamily".into(), reference),
                ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                (
                    "CreatedBy".into(),
                    std::env::var("USERNAME").unwrap_or_default().into(),
                ),
            ]),
        )?;
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid assignment request identity")?;
        let prepared = commands::prepare(
            registry,
            context,
            "AssignTradeTemplateCommand",
            portfolio,
            Some(child),
            request,
            request,
            BTreeMap::from([
                ("Assignment".into(), value),
                ("ExpectedVersion".into(), self.data.fund_revision.into()),
            ]),
        )?;
        self.write_in_flight = true;
        let result = commands::submit(backend, &prepared, deadline).await;
        self.write_in_flight = false;
        result.map_err(|error| {
            if error.starts_with("OutcomeUnknown:") {
                self.requires_reload = true;
            }
            error
        })?;
        self.accepted_write = true;
        self.dialogs.pop();
        self.editing = None;
        self.edit_key = None;
        self.data
            .refresh(backend, registry, &self.data.filter.clone(), deadline)
            .await?;
        self.load_policies(backend, registry, deadline).await?;
        self.load_metrics(backend, registry, deadline).await?;
        self.message = self.data.status.clone();
        Ok("Assignment command committed".into())
    }
}
