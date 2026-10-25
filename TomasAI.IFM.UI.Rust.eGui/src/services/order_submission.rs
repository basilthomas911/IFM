//! One retained Portfolio operation and deterministic lifecycle stages. The
//! adapter never invents economics or creates a new identity after a lost reply.
use super::{
    business_context::BusinessContext,
    parameters,
    portfolio_commands::{self, Prepared},
};
use crate::backend::{
    contracts::*, identity::financial_hash, schema::Registry, transport::Backend,
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition";
const TRADE: &str = "TomasAI.IFM.Domain.Trade.Shared";
pub struct Operation {
    pub id: uuid::Uuid,
    pub portfolio: i32,
    pub composition: uuid::Uuid,
    pub subject: ActorSubject,
    pub body: Value,
    pub hash: String,
    pub attempted: bool,
    pub accepted: Vec<Json>,
    pub completed: Option<Json>,
    pub dispatched: usize,
    pub channel: String,
    pub confirmed: bool,
}
fn semantic_candidate(
    registry: &Registry,
    candidate: &Value,
    closing: bool,
) -> Result<Json, String> {
    let mut value = super::inspection_display::semantic_document(
        registry,
        &format!(
            "{ROOT}.{}",
            if closing {
                "PortfolioCloseOrderCandidate"
            } else {
                "PortfolioOrderCandidate"
            }
        ),
        candidate,
    )?;
    // These computed validity getters participate in the C# canonical JSON hash,
    // although they are deliberately absent from the MessagePack contract.
    if closing {
        let valid = |v: &Json| {
            ["PortfolioId", "FundId", "OrderId", "TradeId"]
                .iter()
                .all(|key| v[*key].as_i64().is_some_and(|n| n > 0))
                && v["PositionId"]
                    .as_str()
                    .and_then(|s| uuid::Uuid::parse_str(s).ok())
                    .is_some_and(|id| !id.is_nil())
        };
        let position = valid(&value["Position"]["Id"]);
        let reference = valid(&value["WorkflowId"]["Position"]);
        let workflow = reference
            && value["WorkflowId"]["ExitDecisionId"]
                .as_str()
                .and_then(|s| uuid::Uuid::parse_str(s).ok())
                .is_some_and(|id| !id.is_nil())
            && value["WorkflowId"]["ValueDate"]
                .as_str()
                .is_some_and(|date| date != "0001-01-01");
        value["Position"]["Id"]["IsValid"] = position.into();
        value["WorkflowId"]["Position"]["IsValid"] = reference.into();
        value["WorkflowId"]["IsValid"] = workflow.into();
    }
    // FinancialCanonicalHash omits default execution options and explicitly null
    // optional decision evidence, exactly as the backend serializer does.
    let object = value.as_object_mut().ok_or("Invalid candidate document")?;
    for (key, default) in [("TimeInForce", "Day"), ("AlgorithmPace", "Normal")] {
        if object.get(key).is_some_and(|v| v == default) {
            object.remove(key);
        }
    }
    for key in ["DecisionEvidence", "SetupTrade"] {
        if object.get(key).is_some_and(Json::is_null) {
            object.remove(key);
        }
    }
    Ok(value)
}
impl Operation {
    pub fn prepare(
        registry: &Registry,
        context: &BusinessContext,
        portfolio: i32,
        id: uuid::Uuid,
        candidate: Value,
    ) -> Result<Self, String> {
        Self::prepare_kind(registry, context, portfolio, id, candidate, false)
    }
    pub fn command_kind(&self) -> String {
        format!(
            "{ROOT}.{}",
            if self.is_closing() {
                "EvaluatePortfolioCloseOrderCompositionCommand"
            } else {
                "EvaluatePortfolioOrderCompositionCommand"
            }
        )
    }
    pub fn is_closing(&self) -> bool {
        self.subject.name == "PortfolioCloseOrderCompositionFunction"
    }
    pub fn prepare_close(
        registry: &Registry,
        context: &BusinessContext,
        portfolio: i32,
        id: uuid::Uuid,
        candidate: Value,
    ) -> Result<Self, String> {
        Self::prepare_kind(registry, context, portfolio, id, candidate, true)
    }
    fn prepare_kind(
        registry: &Registry,
        context: &BusinessContext,
        portfolio: i32,
        id: uuid::Uuid,
        candidate: Value,
        closing: bool,
    ) -> Result<Self, String> {
        let named = parameters::document(
            registry,
            &format!(
                "{ROOT}.{}",
                if closing {
                    "PortfolioCloseOrderCandidate"
                } else {
                    "PortfolioOrderCandidate"
                }
            ),
            &candidate,
            true,
        )?;
        let composition = uuid::Uuid::parse_str(
            named["CompositionId"]
                .as_str()
                .ok_or("Missing composition identity")?,
        )
        .map_err(|_| "Invalid composition identity")?;
        let expires = chrono::DateTime::parse_from_rfc3339(
            named["ValidUntilUtc"]
                .as_str()
                .ok_or("Missing candidate expiry")?,
        )
        .map_err(|_| "Invalid candidate expiry")?
        .to_utc();
        if portfolio <= 0
            || id.is_nil()
            || composition.is_nil()
            || named["PositionType"] != if closing { "Closing" } else { "Opening" }
            || expires <= context.as_of_utc
            || named["ValueDate"] != context.value_date.to_string()
        {
            return Err(
                "Prepared order expired or crossed its API trading date; prepare it again".into(),
            );
        }
        let principal = format!("Desktop:{}", std::env::var("USERNAME").unwrap_or_default());
        let instant = normalized_instant;
        let hash = financial_hash(
            &json!({"PortfolioId":portfolio,"OperationId":id.to_string(),"ExpectedFinancialRevision":0,"RequestedAtUtc":instant(context.as_of_utc),"ExpiresAtUtc":instant(expires),"Body":semantic_candidate(registry,&candidate,closing)?,"Principal":principal}),
        )?;
        let subject = ActorSubject {
            kind: ActorKind::Function,
            name: if closing {
                "PortfolioCloseOrderCompositionFunction"
            } else {
                "PortfolioOrderCompositionFunction"
            }
            .into(),
            verb: if closing { "EvaluateClose" } else { "Evaluate" }.into(),
            entity: format!("{portfolio}.{}", id.simple()),
        };
        let kind = format!(
            "{ROOT}.{}",
            if closing {
                "EvaluatePortfolioCloseOrderCompositionCommand"
            } else {
                "EvaluatePortfolioOrderCompositionCommand"
            }
        );
        let body = registry.encode(
            &kind,
            &BTreeMap::from([
                ("SchemaVersion".into(), 1.into()),
                ("CommandId".into(), id.to_string().into()),
                ("Subject".into(), subject.wire()),
                (
                    "EntityId".into(),
                    Value::Array(vec![portfolio.into(), id.to_string().into()]),
                ),
                (
                    "ErrorCode".into(),
                    if closing { 34131 } else { 34130 }.into(),
                ),
                (
                    "RouteTo".into(),
                    registry
                        .enum_value(
                            "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
                            "PortfolioOrderCompositionBoundedContext",
                        )?
                        .into(),
                ),
                ("OperationId".into(), id.to_string().into()),
                ("PortfolioId".into(), portfolio.into()),
                ("CorrelationId".into(), id.to_string().into()),
                ("CausationId".into(), composition.to_string().into()),
                ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
                ("ExpiresAtUtc".into(), timestamp_wire(expires)),
                ("ExpectedFinancialRevision".into(), 0.into()),
                ("Body".into(), candidate),
                ("InputSha256".into(), hash.clone().into()),
                (
                    "Access".into(),
                    Value::Array(vec![
                        principal.into(),
                        Value::Array(vec![
                            if closing {
                                "OrderCompositionClose"
                            } else {
                                "OrderCompositionEvaluate"
                            }
                            .into(),
                        ]),
                        Value::Array(vec![portfolio.into()]),
                    ]),
                ),
            ]),
        )?;
        Ok(Self {
            id,
            portfolio,
            composition,
            subject,
            body,
            hash,
            attempted: false,
            accepted: vec![],
            completed: None,
            dispatched: 0,
            channel: "Broker".into(),
            confirmed: false,
        })
    }
    pub async fn evaluate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if self.completed.is_some() {
            return Ok(());
        }
        if self.attempted {
            return Err("OutcomeUnknown: Portfolio submission is unconfirmed; reconcile its retained identity before another submission".into());
        }
        self.attempted = true;
        super::order_submission_store::save(self, 1, "Awaiting Portfolio completion").await?;
        self.request_terminal(backend, registry, deadline).await
    }
    /// Portfolio's function state and transaction store key completed results by
    /// operation ID plus hash. Explicit reconciliation reuses these exact bytes;
    /// it cannot allocate a replacement composition or regenerate its expiry.
    pub async fn reconcile(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if self.completed.is_some() {
            return Ok(());
        }
        super::order_submission_store::save(
            self,
            1,
            "Reconciling the original Portfolio operation",
        )
        .await?;
        self.request_terminal(backend, registry, deadline).await
    }
    async fn request_terminal(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let response = backend
            .request_until(&self.subject, &self.body, deadline, None)
            .await
            .map_err(|e| format!("OutcomeUnknown: {e}"))?;
        self.decode_terminal(registry, &response).map_err(|error| {
            if error.starts_with("Portfolio declined the request:")
                || error.starts_with("OutcomeUnknown:")
            {
                error
            } else {
                format!("OutcomeUnknown: {error}")
            }
        })
    }
    fn decode_terminal(&mut self, registry: &Registry, response: &Value) -> Result<(), String> {
        let closing = self.is_closing();
        let value = service_result(&response).map_err(|error| {
            if response
                .as_array()
                .and_then(|p| p.first())
                .and_then(Value::as_bool)
                == Some(false)
            {
                format!("Portfolio declined the request: {error}")
            } else {
                format!("OutcomeUnknown: {error}")
            }
        })?;
        let parts = value
            .as_array()
            .filter(|v| v.len() >= 2)
            .ok_or("OutcomeUnknown: invalid Portfolio terminal result")?;
        if !parts[1].is_nil() {
            if !parts[0].is_nil() {
                return Err("OutcomeUnknown: Portfolio returned two terminal values".into());
            }
            let failed = parameters::document(
                registry,
                &format!(
                    "{ROOT}.{}",
                    if closing {
                        "PortfolioCloseOrderCompositionFailedEvent"
                    } else {
                        "PortfolioOrderCompositionFailedEvent"
                    }
                ),
                &parts[1],
                true,
            )?;
            if failed["CommandId"] != self.id.to_string()
                || failed["EntityId"]["PortfolioId"] != self.portfolio
                || failed["EntityId"]["OperationId"] != self.id.to_string()
            {
                return Err("OutcomeUnknown: Portfolio failure identity differs".into());
            }
            return Err(format!(
                "Portfolio declined the request: {}",
                failed["ErrorMessage"].as_str().unwrap_or("unknown error")
            ));
        }
        let completed = parameters::document(
            registry,
            &format!(
                "{ROOT}.{}",
                if closing {
                    "PortfolioCloseOrderCompositionCompletedEvent"
                } else {
                    "PortfolioOrderCompositionCompletedEvent"
                }
            ),
            &parts[0],
            true,
        )?;
        if completed["OperationId"] != self.id.to_string()
            || completed["PortfolioId"] != self.portfolio
            || completed["Receipt"]["CompositionId"] != self.composition.to_string()
            || completed["InputHash"] != self.hash
        {
            return Err("OutcomeUnknown: Portfolio completion identity differs from the retained submission".into());
        }
        if closing {
            let request = parameters::document(registry, &self.command_kind(), &self.body, true)?;
            if completed["Receipt"]["WorkflowId"] != request["Body"]["WorkflowId"]
                || completed["Receipt"]["PortfolioId"] != self.portfolio
            {
                return Err("OutcomeUnknown: Closing receipt crossed its retained workflow".into());
            }
        }
        let close_orders = if closing {
            if completed["Receipt"]["TradeOrder"].is_null() {
                vec![]
            } else {
                vec![completed["Receipt"]["TradeOrder"].clone()]
            }
        } else {
            vec![]
        };
        let instructions = if closing {
            &close_orders
        } else {
            completed["Receipt"]["TradeOrders"]
                .as_array()
                .ok_or("OutcomeUnknown: Missing accepted orders")?
        };
        let status = &completed["Receipt"]["Status"];
        if (status
            == if closing {
                "ExecuteTradeOrder"
            } else {
                "ExecuteTradeOrders"
            })
            != !instructions.is_empty()
        {
            return Err("OutcomeUnknown: Portfolio returned inconsistent accepted orders".into());
        }
        let orders = instructions
            .iter()
            .map(|value| instruction(registry, value, self.portfolio))
            .collect::<Result<Vec<_>, _>>()?;
        if closing {
            let request = parameters::document(registry, &self.command_kind(), &self.body, true)?;
            for instruction in instructions {
                if instruction["PositionType"] != "Closing"
                    || instruction["TargetPosition"] != request["Body"]["Position"]["Id"]
                {
                    return Err(
                        "OutcomeUnknown: Accepted close targeted a different position".into(),
                    );
                }
            }
        }
        self.accepted = orders;
        self.completed = Some(completed);
        Ok(())
    }
    pub async fn dispatch(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        _context: &BusinessContext,
        channel: &str,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !["Broker", "Manual"].contains(&channel) {
            return Err("Invalid execution channel".into());
        }
        let event = uuid::Uuid::parse_str(
            self.completed
                .as_ref()
                .ok_or("Portfolio has not completed")?["Id"]
                .as_str()
                .ok_or("Missing Portfolio event identity")?,
        )
        .map_err(|_| "Invalid Portfolio event identity")?;
        while self.dispatched < self.accepted.len() {
            let order = &self.accepted[self.dispatched];
            let id = &order["Id"];
            let key = format!("{}.{}.{}", id["PortfolioId"], id["FundId"], id["OrderId"]);
            let seed = stable(&[&event.simple().to_string(), &key]);
            let entity = parameters::from_document(registry, &format!("{TRADE}.TradeOrderId"), id)?;
            for (stage, command, verb) in [
                ("create", "CreateTradeOrderCommand", "CreateTradeOrder"),
                ("approve", "ApproveTradeOrderCommand", "ApproveTradeOrder"),
                ("ready", "ReadyTradeOrderCommand", "ReadyTradeOrder"),
                (
                    "bind",
                    "BindTradeOrderExecutionCommand",
                    "BindTradeOrderExecution",
                ),
            ] {
                let command_id = stable(&[&seed.simple().to_string(), stage]);
                let kind = format!("{TRADE}.Order.{command}"); // These three commands inherit Actor as a static property, which is not
                // a wire member. This closed table follows TradeOrderActorNames.Command.
                let subject = ActorSubject {
                    kind: ActorKind::Command,
                    name: "TradeOrderCommand".into(),
                    verb: verb.into(),
                    entity: key.clone(),
                };
                let mut fields = BTreeMap::from([
                    ("CommandId".into(), command_id.to_string().into()),
                    ("Subject".into(), subject.wire()),
                    ("PostEvents".into(), true.into()),
                    ("EntityId".into(), entity.clone()),
                ]);
                if stage == "create" {
                    fields.insert(
                        "Order".into(),
                        parameters::from_document(
                            registry,
                            &format!("{TRADE}.TradeOrderDefinition"),
                            order,
                        )?,
                    );
                }
                if stage == "bind" {
                    fields.insert(
                        "ExecutionAttemptId".into(),
                        stable(&[&seed.simple().to_string(), "execution-attempt"])
                            .to_string()
                            .into(),
                    );
                    fields.insert(
                        "ExecutionChannel".into(),
                        registry
                            .enum_value(&format!("{TRADE}.ExecutionChannel"), channel)?
                            .into(),
                    );
                    fields.insert(
                        "EffectiveAtUtc".into(),
                        super::strategy::member(
                            registry,
                            &self.command_kind(),
                            &self.body,
                            "RequestedAtUtc",
                        )?
                        .clone(),
                    );
                }
                portfolio_commands::submit(
                    backend,
                    &Prepared {
                        command_id,
                        subject,
                        body: registry.encode(&kind, &fields)?,
                    },
                    deadline,
                )
                .await?;
            }
            self.dispatched += 1;
            super::order_submission_store::save(self, 1, "Accepted order lifecycle dispatched")
                .await?;
        }
        Ok(())
    }
}
pub fn stable(values: &[&str]) -> uuid::Uuid {
    use sha2::{Digest, Sha256};
    let bytes = Sha256::digest(values.join("|").as_bytes());
    let mut id = [0u8; 16];
    id.copy_from_slice(&bytes[..16]);
    uuid::Uuid::from_bytes_le(id)
}
pub fn instruction(registry: &Registry, value: &Json, portfolio: i32) -> Result<Json, String> {
    if value["Id"]["PortfolioId"] != portfolio
        || ["PortfolioId", "FundId", "OrderId"]
            .iter()
            .any(|key| value["Id"][*key].as_i64().is_none_or(|n| n <= 0))
        || !["Opening", "Closing"].contains(&value["PositionType"].as_str().unwrap_or_default())
    {
        return Err("OutcomeUnknown: Portfolio returned an invalid accepted order".into());
    }
    let shape = registry.describe(&format!("{TRADE}.TradeOrderDefinition"))?;
    let mut result = serde_json::Map::new();
    for (name, _, _) in shape.members {
        if let Some(value) = value.get(&name) {
            result.insert(name, value.clone());
        }
    }
    result.insert("SchemaVersion".into(), 4.into());
    if let Some(target) = value.get("TargetPosition") {
        if !target.is_null() {
            let id = target;
            result.insert("TargetPositionId".into(),json!({"Trade":{"PortfolioId":id["PortfolioId"],"FundId":id["FundId"],"OrderId":id["OrderId"],"TradeId":id["TradeId"]},"PositionId":id["PositionId"]}));
        }
    }
    let components = value["Components"]
        .as_array()
        .ok_or("Invalid accepted components")?
        .iter()
        .map(|c| {
            let mut component = c.clone();
            if let Some(legs) = component["Legs"].as_array_mut() {
                for leg in legs {
                    leg["LegacyMarketInstrumentId"] = 0.into();
                }
            }
            component
        })
        .collect::<Vec<_>>();
    result.insert("Components".into(), components.into());
    // Round-trip fills every default published field without adding display getters.
    let wire = parameters::from_document(
        registry,
        &format!("{TRADE}.TradeOrderDefinition"),
        &Json::Object(result),
    )?;
    parameters::document(
        registry,
        &format!("{TRADE}.TradeOrderDefinition"),
        &wire,
        true,
    )
}

fn normalized_instant(v: chrono::DateTime<chrono::Utc>) -> String {
    let value = utc_roundtrip(v);
    let (whole, fraction) = value.trim_end_matches('Z').split_once('.').unwrap();
    let fraction = fraction.trim_end_matches('0');
    if fraction.is_empty() {
        format!("{whole}Z")
    } else {
        format!("{whole}.{fraction}Z")
    }
}
pub fn request_hash(registry: &Registry, body: &Value) -> Result<String, String> {
    let kind = journal_kind(body)?;
    let closing = kind.ends_with("EvaluatePortfolioCloseOrderCompositionCommand");
    let member = |name| super::strategy::member(registry, &kind, body, name);
    let candidate = member("Body")?;
    let access = parameters::document(
        registry,
        "TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialAccess",
        &member("Access")?,
        true,
    )?;
    financial_hash(
        &json!({"PortfolioId":member("PortfolioId")?.as_i64(),"OperationId":member("OperationId")?.as_str(),"ExpectedFinancialRevision":member("ExpectedFinancialRevision")?.as_i64(),"RequestedAtUtc":normalized_instant(timestamp(&member("RequestedAtUtc")?)?),"ExpiresAtUtc":normalized_instant(timestamp(&member("ExpiresAtUtc")?)?),"Body":semantic_candidate(registry,&candidate,closing)?,"Principal":access["Principal"]}),
    )
}

/// Decode only the closed submission envelope family. Journal data cannot choose
/// an arbitrary actor or deserialize as a different financial command.
pub fn journal_kind(body: &Value) -> Result<String, String> {
    let subject = body
        .as_array()
        .and_then(|v| v.get(2))
        .and_then(Value::as_array)
        .ok_or("Invalid submission subject")?;
    let actor = subject
        .get(1)
        .and_then(Value::as_str)
        .ok_or("Missing submission actor")?;
    match actor {
        "PortfolioOrderCompositionFunction" => {
            Ok(format!("{ROOT}.EvaluatePortfolioOrderCompositionCommand"))
        }
        "PortfolioCloseOrderCompositionFunction" => Ok(format!(
            "{ROOT}.EvaluatePortfolioCloseOrderCompositionCommand"
        )),
        _ => Err("Unsupported submission journal actor".into()),
    }
}
