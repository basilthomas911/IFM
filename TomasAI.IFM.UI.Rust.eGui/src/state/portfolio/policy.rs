//! Risk-policy editing and commands. Policy amounts are backend-validated DTO
//! inputs, never a locally calculated spending/risk authority.
use super::*;
use crate::services::{
    business_context::BusinessContext, parameters::overlay_type, portfolio_commands as commands,
};
pub(super) fn same_deployment(a: &Json, b: &Json) -> bool {
    let valid = |v: &Json| v["Kind"] == 7 || v["Kind"] == "Deployment";
    valid(a)
        && valid(b)
        && a["Id"]
            .as_str()
            .and_then(|v| uuid::Uuid::parse_str(v).ok())
            .is_some_and(|id| {
                !id.is_nil()
                    && b["Id"].as_str().and_then(|v| uuid::Uuid::parse_str(v).ok()) == Some(id)
            })
        && a["Version"] == b["Version"]
}
fn positive(row: &Json, key: &str) -> Result<i32, String> {
    i32::try_from(
        row[key]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or(format!("Invalid {key}"))?,
    )
    .map_err(|_| format!("{key} overflow"))
}
fn integer(value: &str) -> Result<i32, String> {
    ExactDecimal::parse(value)?;
    let whole = value.split('.').next().unwrap_or(value);
    whole
        .parse::<i64>()
        .ok()
        .and_then(|v| i32::try_from(v).ok())
        .ok_or("Integer input exceeds its published range".into())
}
fn patch_fields(
    registry: &Registry,
    kind: &str,
    wire: &Value,
    fields: &serde_json::Map<String, Json>,
    prefix: &str,
    allowed: &[&str],
) -> Result<Value, String> {
    let shape = registry.describe(kind)?;
    let mut changes = BTreeMap::new();
    for name in allowed {
        let Some(text) = fields
            .get(&format!("{prefix}{name}"))
            .and_then(Json::as_str)
        else {
            continue;
        };
        let text = text.trim();
        let (_, _, ty) = shape
            .members
            .iter()
            .find(|(n, _, _)| n == name)
            .ok_or("Missing policy input contract")?;
        let value = match ty.as_str() {
            "System.Decimal" => {
                ExactDecimal::parse(text)?;
                text.into()
            }
            "System.Int32" => integer(text)?.into(),
            "System.Boolean" => {
                if text.eq_ignore_ascii_case("true") {
                    true.into()
                } else if text.eq_ignore_ascii_case("false") {
                    false.into()
                } else {
                    return Err(format!("Enter True or False for {name}"));
                }
            }
            "System.String" => text.into(),
            _ => return Err(format!("Unsupported editable policy input {name}")),
        };
        changes.insert((*name).to_string(), value);
    }
    overlay_type(registry, kind, wire, &changes)
}
impl PortfolioController {
    fn selected_policy_wire(&self, registry: &Registry) -> Result<Value, String> {
        let parent = self.data.portfolio.as_ref().ok_or("Select a Portfolio")?;
        let parent = registry.project(&kind("PortfolioReadModel"), parent)?;
        let rows = self
            .policies
            .iter()
            .map(|wire| {
                Ok((
                    wire,
                    registry.project(&kind("PortfolioFinancialPolicyReadModel"), wire)?,
                ))
            })
            .collect::<Result<Vec<_>, String>>()?;
        let selected = rows
            .iter()
            .find(|(_, row)| {
                self.selected_policy.as_ref().is_some_and(|key| {
                    *key == format!("{}@{}", text(&row["PolicyId"]), text(&row["PolicyVersion"]))
                })
            })
            .or_else(|| {
                rows.iter().find(|(_, row)| {
                    row["PolicyId"] == parent["ActivePolicyId"]
                        && row["PolicyVersion"] == parent["ActivePolicyVersion"]
                })
            })
            .or(rows.first());
        selected
            .map(|(wire, _)| (*wire).clone())
            .ok_or("Select a policy".into())
    }
    fn new_limits(&self, registry: &Registry, prior: Option<&Value>) -> Result<Value, String> {
        let old = prior
            .map(|v| {
                crate::services::strategy::member(
                    registry,
                    &kind("PortfolioFinancialPolicyReadModel"),
                    v,
                    "TradeFamilyLimits",
                )
            })
            .transpose()?;
        let old = old
            .as_ref()
            .and_then(Value::as_array)
            .cloned()
            .unwrap_or_default();
        let mut limits = vec![];
        for choice in &self.deployments {
            let key = &choice["Key"];
            if let Some(found) = old.iter().find(|v| {
                registry
                    .project(&kind("TradeFamilyRiskLimitReadModel"), v)
                    .is_ok_and(|row| same_deployment(&row["CatalogDeployment"], key))
            }) {
                limits.push(found.clone());
                continue;
            }
            let reference=registry.encode("TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey",&BTreeMap::from([("Kind".into(),registry.enum_value("TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind","Deployment")?.into()),("Id".into(),key["Id"].as_str().ok_or("Missing deployment identity")?.into()),("Version".into(),key["Version"].as_i64().filter(|v|*v>0).ok_or("Invalid deployment version")?.into())]))?;
            limits.push(registry.encode(
                &kind("TradeFamilyRiskLimitReadModel"),
                &BTreeMap::from([("CatalogDeployment".into(), reference)]),
            )?);
        }
        Ok(Value::Array(limits))
    }
    async fn policy_submit(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        name: &str,
        policy: i32,
        request: uuid::Uuid,
        fields: BTreeMap<String, Value>,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let parent = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        let prepared = commands::prepare(
            registry,
            context,
            name,
            positive(&parent, "PortfolioId")?,
            Some(policy),
            request,
            request,
            fields,
        )?;
        self.write_in_flight = true;
        let submitted = commands::submit(backend, &prepared, deadline).await;
        self.write_in_flight = false;
        match submitted {
            Err(error) => {
                if error.starts_with("OutcomeUnknown:") {
                    self.requires_reload = true;
                }
                return Err(error);
            }
            Ok(_) => {
                self.accepted_write = true;
                self.editing_policy = None;
                self.policy_dirty = false;
                self.policy_key = None;
            }
        }
        // Assignment changes the parent authority as well as the child policy.
        // Read that parent before rebuilding the inline policy heading.
        let refreshed = async {
            if name == "ActivateAndAssignPortfolioFinancialPolicyCommand" {
                self.data
                    .refresh(backend, registry, &self.data.filter.clone(), deadline)
                    .await?;
            }
            self.load_policies(backend, registry, deadline).await
        }
        .await;
        match refreshed {
            Ok(()) => {
                self.message.clear();
                Ok("Policy command committed".into())
            }
            Err(error) => {
                self.message = format!("Policy command committed; reload to refresh: {error}");
                Ok(self.message.clone())
            }
        }
    }
    pub(super) async fn policy_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let action = field(intent, "action");
        if self
            .dialogs
            .last()
            .is_some_and(|d| d.key == "discard-policy")
        {
            if !matches!(action, "Yes" | "No") {
                return Err("Confirm or cancel discarding the policy working copy".into());
            }
            self.dialogs.pop();
            if action == "Yes" {
                self.editing_policy = None;
                self.policy_key = None;
                self.policy_dirty = false;
                self.dialogs.pop();
            }
            return Ok("Policy discard choice applied".into());
        }
        let fields = intent["fields"]
            .as_object()
            .ok_or("Invalid policy fields")?;
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid policy request identity")?;
        let context = BusinessContext::load(backend).await?;
        self.data.context = Some(context.clone());
        let parent = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        let portfolio = positive(&parent, "PortfolioId")?;
        match action {
            "Cancel" => {
                self.editing_policy = None;
                self.policy_dirty = false;
                self.policy_key = None;
                self.message.clear();
            }
            "New Policy" => {
                if self.editing_policy.is_some() {
                    return Err("Save or cancel the policy working copy first".into());
                }
                let value = api::query(
                    backend,
                    registry,
                    &context,
                    "AllocatePortfolioBusinessIdQuery",
                    "Policy",
                    BTreeMap::from([(
                        "Kind".into(),
                        registry
                            .enum_value(
                                &format!("{}.ServiceApi.PortfolioBusinessIdentityKind", api::ROOT),
                                "Policy",
                            )?
                            .into(),
                    )]),
                    deadline,
                )
                .await?;
                let allocation = registry.project(
                    &format!("{}.ServiceApi.PortfolioBusinessIdAllocation", api::ROOT),
                    &value,
                )?;
                if allocation["Kind"] != "Policy" {
                    return Err("Allocated identity has the wrong business kind".into());
                }
                let id = positive(&allocation, "Value")?;
                let policy = registry.encode(
                    &kind("PortfolioFinancialPolicyReadModel"),
                    &BTreeMap::from([
                        ("PortfolioId".into(), portfolio.into()),
                        ("PolicyId".into(), id.into()),
                        ("PolicyVersion".into(), 1.into()),
                        ("SchemaVersion".into(), 3.into()),
                        ("Name".into(), "".into()),
                        (
                            "OperatingState".into(),
                            registry
                                .enum_value(
                                    &format!(
                                        "{}.Contracts.PortfolioFinancialPolicyState",
                                        api::ROOT
                                    ),
                                    "Draft",
                                )?
                                .into(),
                        ),
                        (
                            "BaseCurrency".into(),
                            parent["BaseCurrency"]
                                .as_str()
                                .ok_or("Missing Portfolio currency")?
                                .into(),
                        ),
                        ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                        ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                        (
                            "CreatedBy".into(),
                            std::env::var("USERNAME").unwrap_or_default().into(),
                        ),
                        ("SupersededBy".into(), "".into()),
                        ("TradeFamilyLimits".into(), self.new_limits(registry, None)?),
                    ]),
                )?;
                self.editing_policy = Some(policy);
                self.policy_key = Some(uuid::Uuid::new_v4());
                self.policy_version = false;
                self.policy_dirty = false;
                self.message.clear();
            }
            "New Version" | "Change Policy..." => {
                if self.editing_policy.is_some() {
                    return Err("Save or cancel the policy working copy first".into());
                }
                let selected = self.selected_policy_wire(registry)?;
                let row =
                    registry.project(&kind("PortfolioFinancialPolicyReadModel"), &selected)?;
                let version = row["PolicyVersion"]
                    .as_i64()
                    .filter(|v| *v > 0)
                    .and_then(|v| v.checked_add(1))
                    .ok_or("Policy version overflow")?;
                let value = overlay_type(
                    registry,
                    &kind("PortfolioFinancialPolicyReadModel"),
                    &selected,
                    &BTreeMap::from([
                        ("PolicyVersion".into(), version.into()),
                        ("SchemaVersion".into(), 3.into()),
                        (
                            "OperatingState".into(),
                            registry
                                .enum_value(
                                    &format!(
                                        "{}.Contracts.PortfolioFinancialPolicyState",
                                        api::ROOT
                                    ),
                                    "Draft",
                                )?
                                .into(),
                        ),
                        ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                        ("EffectiveUntilUtc".into(), Value::Nil),
                        ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                        (
                            "CreatedBy".into(),
                            std::env::var("USERNAME").unwrap_or_default().into(),
                        ),
                        ("SupersededOnUtc".into(), Value::Nil),
                        ("SupersededBy".into(), "".into()),
                        (
                            "TradeFamilyLimits".into(),
                            self.new_limits(registry, Some(&selected))?,
                        ),
                    ]),
                )?;
                self.editing_policy = Some(value);
                self.policy_version = true;
                self.policy_key = None;
                self.policy_dirty = false;
                self.message.clear();
            }
            "Save" => {
                self.policy_dirty = true;
                let original = self
                    .editing_policy
                    .as_ref()
                    .ok_or("No policy draft is being edited")?;
                let mut value = patch_fields(
                    registry,
                    &kind("PortfolioFinancialPolicyReadModel"),
                    original,
                    fields,
                    "",
                    &[
                        "Name",
                        "CapitalBase",
                        "ProtectedReserve",
                        "MaximumDeployableCapital",
                        "MaximumRiskPerTrade",
                        "MaximumAggregateRisk",
                        "MaximumMargin",
                        "MaximumGrossNotional",
                        "MaximumOpenPositions",
                        "MaximumDrawdownAmount",
                    ],
                )?;
                let limits = crate::services::strategy::member(
                    registry,
                    &kind("PortfolioFinancialPolicyReadModel"),
                    &value,
                    "TradeFamilyLimits",
                )?;
                let limits = limits
                    .as_array()
                    .ok_or("Invalid policy family limit list")?
                    .iter()
                    .enumerate()
                    .map(|(index, wire)| {
                        patch_fields(
                            registry,
                            &kind("TradeFamilyRiskLimitReadModel"),
                            wire,
                            fields,
                            &format!("limit.{index}."),
                            &[
                                "Enabled",
                                "MaximumRiskPerTrade",
                                "MaximumAggregateRisk",
                                "MaximumMargin",
                                "MaximumGrossNotional",
                                "MaximumOpenPositions",
                            ],
                        )
                    })
                    .collect::<Result<Vec<_>, _>>()?;
                value = overlay_type(
                    registry,
                    &kind("PortfolioFinancialPolicyReadModel"),
                    &value,
                    &BTreeMap::from([
                        (
                            "BaseCurrency".into(),
                            parent["BaseCurrency"]
                                .as_str()
                                .ok_or("Missing Portfolio currency")?
                                .into(),
                        ),
                        ("TradeFamilyLimits".into(), Value::Array(limits)),
                    ]),
                )?;
                let row = registry.project(&kind("PortfolioFinancialPolicyReadModel"), &value)?;
                let id = positive(&row, "PolicyId")?;
                self.editing_policy = Some(value.clone());
                let mut fields = BTreeMap::from([("Policy".into(), value)]);
                let name = if self.policy_version {
                    let selected = registry.project(
                        &kind("PortfolioFinancialPolicyReadModel"),
                        &self.selected_policy_wire(registry)?,
                    )?;
                    fields.insert(
                        // Historical wire name; the API passes the aggregate revision.
                        "ExpectedVersion".into(),
                        selected["AggregateRevision"]
                            .as_i64()
                            .filter(|v| *v > 0)
                            .ok_or("Reload policy revision before saving")?
                            .into(),
                    );
                    "AddPortfolioFinancialPolicyVersionCommand"
                } else {
                    fields.insert(
                        "IdempotencyKey".into(),
                        self.policy_key
                            .ok_or("Missing prepared policy operation identity")?
                            .to_string()
                            .into(),
                    );
                    "CreatePortfolioFinancialPolicyCommand"
                };
                return self
                    .policy_submit(
                        backend, registry, &context, name, id, request, fields, deadline,
                    )
                    .await;
            }
            "Retire" | "Delete Draft" | "Activate && Assign" => {
                if self.editing_policy.is_some() {
                    return Err("Save or cancel the working copy first".into());
                }
                let row = registry.project(
                    &kind("PortfolioFinancialPolicyReadModel"),
                    &self.selected_policy_wire(registry)?,
                )?;
                let id = positive(&row, "PolicyId")?;
                let revision = row["AggregateRevision"]
                    .as_i64()
                    .filter(|v| *v > 0)
                    .ok_or("Reload policy revision before changing its state")?;
                let version = row["PolicyVersion"]
                    .as_i64()
                    .filter(|v| *v > 0)
                    .ok_or("Invalid policy version")?;
                let mut fields = BTreeMap::new();
                let name = match action {
                    "Retire" => {
                        fields.insert("PolicyVersion".into(), version.into());
                        fields.insert("ExpectedRevision".into(), revision.into());
                        fields.insert("Reason".into(), "Operator retirement".into());
                        "RetirePortfolioFinancialPolicyCommand"
                    }
                    "Delete Draft" => {
                        if row["OperatingState"] != "Draft" {
                            return Err("Only a Draft policy can be deleted".into());
                        }
                        fields.insert("ExpectedRevision".into(), revision.into());
                        fields.insert("Reason".into(), "Operator deleted unused Draft".into());
                        "DeleteDraftPortfolioFinancialPolicyCommand"
                    }
                    _ => {
                        if row["OperatingState"] != "Draft" {
                            return Err("Select a Draft policy".into());
                        }
                        let current = api::query(
                            backend,
                            registry,
                            &context,
                            "GetPortfolioRevisionQuery",
                            &portfolio.to_string(),
                            BTreeMap::from([("PortfolioId".into(), portfolio.into())]),
                            deadline,
                        )
                        .await?;
                        let current = registry.project(
                            &format!("{}.ServiceApi.PortfolioAggregateRevision", api::ROOT),
                            &current,
                        )?;
                        if current["PortfolioId"] != portfolio {
                            return Err("Portfolio revision crossed policy scope".into());
                        }
                        // Named contract fields prevent positional PolicyVersion/AggregateRevision
                        // inversion. The hybrid discrepancy is tracked separately in acceptance.
                        fields.insert("PolicyVersion".into(), version.into());
                        fields.insert("ExpectedPolicyRevision".into(), revision.into());
                        fields.insert(
                            "ExpectedPortfolioRevision".into(),
                            current["Revision"]
                                .as_i64()
                                .filter(|v| *v > 0)
                                .ok_or("Missing Portfolio revision")?
                                .into(),
                        );
                        "ActivateAndAssignPortfolioFinancialPolicyCommand"
                    }
                };
                return self
                    .policy_submit(
                        backend, registry, &context, name, id, request, fields, deadline,
                    )
                    .await;
            }
            _ => return Err("Unsupported Portfolio policy action".into()),
        }
        Ok("Policy working copy updated".into())
    }
}
