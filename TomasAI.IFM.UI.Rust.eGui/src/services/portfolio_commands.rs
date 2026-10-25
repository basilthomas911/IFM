//! Prepared Portfolio administration writes. Controllers must retain the exact
//! prepared request and its outcome before allowing another submission.
use crate::backend::{codec, contracts::*, schema::Registry, transport::Backend};
use crate::services::{business_context::BusinessContext, portfolio};
use rmpv::Value;
use std::collections::BTreeMap;
#[derive(Clone)]
pub struct Prepared {
    pub command_id: uuid::Uuid,
    pub subject: ActorSubject,
    pub body: Value,
}
pub fn template(kind: &str) -> Result<Value, String> {
    let templates: BTreeMap<String, Vec<u8>> = serde_json::from_str(include_str!(
        "../../tests/fixtures/backend/portfolio-request-defaults.json"
    ))
    .map_err(|e| e.to_string())?;
    codec::decode(
        templates
            .get(kind)
            .ok_or("Missing compiled Portfolio initializer")?,
    )
}
pub fn prepare(
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    portfolio_id: i32,
    child_id: Option<i32>,
    command_id: uuid::Uuid,
    correlation_id: uuid::Uuid,
    mut fields: BTreeMap<String, Value>,
) -> Result<Prepared, String> {
    if portfolio_id <= 0
        || child_id.is_some_and(|v| v <= 0)
        || command_id.is_nil()
        || correlation_id.is_nil()
    {
        return Err("Invalid Portfolio request identity".into());
    }
    let (actor, error, requires_child) = match name {
        "CreatePortfolioCommand"
        | "DelegateFundAllocationCommand"
        | "DelegateFundRiskEnvelopeCommand" => ("PortfolioCommand", 34002, false),
        "AddPortfolioVersionCommand" | "AddFundToPortfolioCommand" => {
            ("PortfolioCommand", 34003, false)
        }
        "ChangePortfolioOperatingStateCommand" | "RetirePortfolioCommand" => {
            ("PortfolioCommand", 34005, false)
        }
        "DeleteDraftPortfolioCommand" => ("PortfolioCommand", 34015, false),
        "CreateFundMandateCommand"
        | "AssignTradeTemplateCommand"
        | "CreateManualFundOrderCommand" => ("PortfolioFundCommand", 34002, true),
        "AddFundMandateVersionCommand"
        | "AddManualFundOrderTradeCommand"
        | "RemoveManualFundOrderTradeCommand"
        | "DeleteManualFundOrderCommand" => ("PortfolioFundCommand", 34003, true),
        "ChangeFundOperatingStateCommand" => ("PortfolioFundCommand", 34005, true),
        "CreatePortfolioFinancialPolicyCommand"
        | "AddPortfolioFinancialPolicyVersionCommand"
        | "ActivateAndAssignPortfolioFinancialPolicyCommand"
        | "RetirePortfolioFinancialPolicyCommand"
        | "DeleteDraftPortfolioFinancialPolicyCommand" => {
            ("PortfolioFinancialPolicyCommand", 34002, true)
        }
        _ => return Err("Unsupported Portfolio administration command".into()),
    };
    if requires_child != child_id.is_some() {
        return Err("Portfolio command target shape mismatch".into());
    }
    let kind = format!("{}.Commands.{name}", portfolio::ROOT);
    let (declared, verb) = registry.route(&kind)?;
    if declared != actor {
        return Err("Portfolio command route differs from compiled contract".into());
    }
    let key = child_id
        .map(|v| format!("{portfolio_id}.{v}"))
        .unwrap_or_else(|| portfolio_id.to_string());
    let subject = ActorSubject {
        kind: ActorKind::Command,
        name: actor.into(),
        verb: verb.into(),
        entity: key,
    };
    let mut identity = vec![portfolio_id.into()];
    if let Some(child) = child_id {
        identity.push(child.into());
    }
    fields.insert("EntityId".into(), Value::Array(identity));
    fields.insert("Subject".into(), subject.wire());
    fields.insert("CommandId".into(), command_id.to_string().into());
    fields.insert("CorrelationId".into(), correlation_id.to_string().into());
    fields.insert("RequestedOnUtc".into(), timestamp_wire(context.as_of_utc));
    fields.insert("Access".into(), portfolio::access(true));
    fields.insert("ErrorCode".into(), error.into());
    let body =
        crate::services::parameters::overlay_type(registry, &kind, &template(&kind)?, &fields)?;
    Ok(Prepared {
        command_id,
        subject,
        body,
    })
}
pub async fn submit(
    backend: &Backend,
    prepared: &Prepared,
    deadline: tokio::time::Instant,
) -> Result<uuid::Uuid, String> {
    let reply = backend
        .request_until(&prepared.subject, &prepared.body, deadline, None)
        .await
        .map_err(|e| format!("OutcomeUnknown: Portfolio command may have committed: {e}"))?;
    let value = match service_result(&reply) {
        Ok(value) => value,
        Err(error) => {
            if reply
                .as_array()
                .and_then(|v| v.first())
                .and_then(Value::as_bool)
                == Some(false)
            {
                return Err(error);
            }
            return Err(format!("OutcomeUnknown: {error}"));
        }
    };
    // Command actors return ServiceResult<GuidResult>; the managed API unwraps
    // that layer before its callers see ServiceResult<Guid>. Decode the actor
    // boundary here, while retaining compatibility with an already-unwrapped
    // reply from older endpoints.
    let id=value.as_str().or_else(||value.as_array().filter(|v|v.len()==1).and_then(|v|v[0].as_str())).unwrap_or_default();
    uuid::Uuid::parse_str(id)
        .ok()
        .filter(|v| !v.is_nil() && *v==prepared.command_id)
        .ok_or("OutcomeUnknown: invalid Portfolio command receipt".into())
}
