use super::{Prepared, portfolio};
use crate::backend::{schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use rmpv::Value;
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;
use tokio::time::Instant;

const ROOT: &str = portfolio::ROOT;

pub(in crate::services) async fn qualify(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    command: &Prepared,
    deadline: Instant,
) -> Result<Prepared, String> {
    let verb = command.subject.verb.as_str();
    if !matches!(
        verb,
        "CreateFundMandate"
            | "AddFundMandateVersion"
            | "ConfigureFundBaseContracts"
            | "AssignTradeTemplate"
            | "ChangeFundOperatingState"
            | "CreatePortfolioFinancialPolicy"
            | "AddPortfolioFinancialPolicyVersion"
            | "ActivateAndAssignPortfolioFinancialPolicy"
            | "CreateManualFundOrder"
    ) {
        return Ok(command.clone());
    }
    let kind = format!("{ROOT}.Commands.{verb}Command");
    let field = |name| registry.wire_field(&kind, name, &command.body).cloned();
    let scope = &command.subject.entity;
    let (parent, child) = scope
        .split_once('.')
        .ok_or("Invalid Portfolio command scope")?;
    let portfolio_id: i32 = parent.parse().map_err(|_| "Invalid Portfolio identity")?;
    let child_id: i32 = child
        .parse()
        .map_err(|_| "Invalid Portfolio child identity")?;
    let access = field("Access")?;
    let mut fields = BTreeMap::new();
    if verb == "CreateManualFundOrder" {
        let (parent_model, revision) =
            active_parent(backend, registry, context, portfolio_id, deadline).await?;
        let parent_version = member(
            registry,
            "ViewModels.PortfolioReadModel",
            "PortfolioVersion",
            &parent_model,
        )?;
        let request = field("Request")?;
        if parent_version
            != member(
                registry,
                "Contracts.CreateManualFundOrderRequest",
                "PortfolioVersion",
                &request,
            )?
        {
            return Err("Manual draft Portfolio version is stale".into());
        }
        let order_kind = registry.enum_value(
            &format!("{ROOT}.ServiceApi.PortfolioBusinessIdentityKind"),
            "Order",
        )?;
        let operation = stable_id(&format!(
            "PortfolioFund.OrderIdentity.v1:{scope}:{}:{order_kind}:0",
            command.command_id.simple()
        ));
        let completed = portfolio::request_preparation(
            backend,
            registry,
            "AllocatePortfolioBusinessIdCommand",
            "PortfolioBusinessIdAllocation",
            operation,
            BTreeMap::from([
                ("Kind".into(), order_kind.into()),
                ("Access".into(), access),
            ]),
            deadline,
        )
        .await?;
        let allocated = member(
            registry,
            "Preparation.PortfolioBusinessIdAllocationCompletedEvent",
            "Value",
            &completed,
        )?;
        let order = member(
            registry,
            "ServiceApi.PortfolioBusinessIdAllocation",
            "Value",
            allocated,
        )?;
        if !order.as_i64().is_some_and(|id| id > 0) {
            return Err("Portfolio returned an invalid manual order identity".into());
        }
        fields.insert(
            "Preparation".into(),
            Value::Array(vec![
                order.clone(),
                Value::Array(vec![]),
                parent_model,
                revision,
            ]),
        );
    } else {
        let mut inputs = BTreeMap::from([("Access".into(), access)]);
        match verb {
            "CreateFundMandate" | "AddFundMandateVersion" | "ConfigureFundBaseContracts" => {
                inputs.insert("Mandate".into(), field("Mandate")?);
            }
            "AssignTradeTemplate" => {
                inputs.insert("Assignment".into(), field("Assignment")?);
            }
            "CreatePortfolioFinancialPolicy" | "AddPortfolioFinancialPolicyVersion" => {
                inputs.insert("Policy".into(), field("Policy")?);
            }
            "ActivateAndAssignPortfolioFinancialPolicy" => {
                let (parent_model, revision) =
                    projected_parent(backend, registry, context, portfolio_id, deadline).await?;
                if revision != field("ExpectedPortfolioRevision")? {
                    return Err("The parent Portfolio revision changed before policy activation".into());
                }
                fields.insert("ParentPortfolio".into(), parent_model);
                let policy = portfolio::query(
                    backend,
                    registry,
                    context,
                    "GetPortfolioFinancialPolicyQuery",
                    child,
                    BTreeMap::from([
                        ("PolicyId".into(), child_id.into()),
                        ("PolicyVersion".into(), field("PolicyVersion")?),
                    ]),
                    deadline,
                )
                .await?;
                if member(
                    registry,
                    "ViewModels.PortfolioFinancialPolicyReadModel",
                    "PortfolioId",
                    &policy,
                )?
                .as_i64()
                    != Some(i64::from(portfolio_id))
                {
                    return Err("Policy preparation crossed Portfolio scope".into());
                }
                inputs.insert("Policy".into(), policy);
                inputs.insert("ActivatesPolicy".into(), true.into());
            }
            _ => {}
        }
        let fund_state = if verb == "ChangeFundOperatingState" {
            Some(field("State")?)
        } else if verb == "AddFundMandateVersion" {
            Some(
                member(
                    registry,
                    "ViewModels.FundMandateReadModel",
                    "OperatingState",
                    &field("Mandate")?,
                )?
                .clone(),
            )
        } else {
            None
        };
        if let Some(state) = fund_state {
            let active =
                registry.enum_value(&format!("{ROOT}.Contracts.FundOperatingState"), "Active")?;
            if state.as_i64() == Some(active) {
                let (parent_model, revision) =
                    active_parent(backend, registry, context, portfolio_id, deadline).await?;
                let parent_version = member(
                    registry,
                    "ViewModels.PortfolioReadModel",
                    "PortfolioVersion",
                    &parent_model,
                )?
                .clone();
                let mut fund_fields = BTreeMap::from([
                    ("PortfolioId".into(), portfolio_id.into()),
                    ("FundId".into(), child_id.into()),
                ]);
                let fund = portfolio::query(
                    backend,
                    registry,
                    context,
                    "GetFundQuery",
                    scope,
                    fund_fields.clone(),
                    deadline,
                )
                .await?;
                let version = member(
                    registry,
                    "ViewModels.FundMandateReadModel",
                    "FundMandateVersion",
                    &fund,
                )?
                .clone();
                fund_fields.insert("MandateVersion".into(), version);
                let assignments = portfolio::query(
                    backend,
                    registry,
                    context,
                    "GetFundTemplateAssignmentsQuery",
                    scope,
                    fund_fields,
                    deadline,
                )
                .await?;
                let mut enabled = Vec::new();
                for assignment in assignments
                    .as_array()
                    .ok_or("Invalid Fund assignment list")?
                {
                    if member(
                        registry,
                        "ViewModels.FundTradeTemplateAssignmentReadModel",
                        "Enabled",
                        assignment,
                    )?
                    .as_bool()
                        == Some(true)
                    {
                        enabled.push(assignment.clone());
                    }
                }
                inputs.insert("ActivationAssignments".into(), Value::Array(enabled));
                fields.insert(
                    "ActivationPreparation".into(),
                    Value::Array(vec![revision, parent_version]),
                );
            } else if verb == "ChangeFundOperatingState" {
                return Ok(command.clone());
            }
        }
        let operation = uuid::Uuid::new_v4();
        inputs.insert(
            "OwnerCommandId".into(),
            command.command_id.to_string().into(),
        );
        portfolio::request_preparation(
            backend,
            registry,
            "ValidatePortfolioReferenceCommand",
            "PortfolioReferenceValidation",
            operation,
            inputs,
            deadline,
        )
        .await?;
        fields.insert(
            "ReferencePreparationId".into(),
            operation.to_string().into(),
        );
    }
    Ok(Prepared {
        body: crate::services::parameters::overlay_type(registry, &kind, &command.body, &fields)?,
        ..command.clone()
    })
}

async fn active_parent(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    portfolio_id: i32,
    deadline: Instant,
) -> Result<(Value, Value), String> {
    let (portfolio, revision) = projected_parent(backend, registry, context, portfolio_id, deadline).await?;
    let active = registry.enum_value(&format!("{ROOT}.Contracts.PortfolioOperatingState"), "Active")?;
    if member(registry, "ViewModels.PortfolioReadModel", "OperatingState", &portfolio)?.as_i64() != Some(active) {
        return Err("The parent Portfolio is not active".into());
    }
    Ok((portfolio, revision))
}

async fn projected_parent(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    portfolio_id: i32,
    deadline: Instant,
) -> Result<(Value, Value), String> {
    let fields = BTreeMap::from([("PortfolioId".into(), portfolio_id.into())]);
    let key = portfolio_id.to_string();
    let revision = || {
        portfolio::query(
            backend,
            registry,
            context,
            "GetPortfolioRevisionQuery",
            &key,
            fields.clone(),
            deadline,
        )
    };
    let before = revision().await?;
    let portfolio = portfolio::query(
        backend,
        registry,
        context,
        "GetPortfolioQuery",
        &key,
        fields.clone(),
        deadline,
    )
    .await?;
    let after = revision().await?;
    let source_revision = member(
        registry,
        "ServiceApi.PortfolioAggregateRevision",
        "Revision",
        &before,
    )?
    .clone();
    if before != after
        || !source_revision.as_i64().is_some_and(|value| value > 0)
        || member(registry, "ViewModels.PortfolioReadModel", "PortfolioId", &portfolio)?.as_i64() != Some(i64::from(portfolio_id))
    {
        return Err("The parent Portfolio changed or is unavailable".into());
    }
    Ok((portfolio, source_revision))
}

fn member<'a>(
    registry: &Registry,
    kind: &str,
    name: &str,
    value: &'a Value,
) -> Result<&'a Value, String> {
    registry.wire_field(&format!("{ROOT}.{kind}"), name, value)
}

fn stable_id(input: &str) -> uuid::Uuid {
    let hash = Sha256::digest(input.as_bytes());
    uuid::Uuid::from_bytes_le(hash[..16].try_into().unwrap())
}
