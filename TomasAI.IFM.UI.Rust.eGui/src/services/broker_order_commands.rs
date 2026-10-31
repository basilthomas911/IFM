//! Working-order mutations carry the API instant and a retained operation ID.
//! Committed events resolve lost replies; arbitrary retransmission is forbidden.
use super::{
    broker_events::Watch,
    broker_order_ui, broker_reads,
    business_context::BusinessContext,
    parameters,
    portfolio_commands::{self, Prepared},
};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::{
    backend_document::request_document,
    trade_contracts::{BrokerOrderDefinition, OrderExecutionDefinition},
};
use rmpv::Value;
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Order.Broker";
pub struct Operation {
    pub prepared: Prepared,
    pub order_key: String,
    pub attempted: bool,
    pub acknowledged: bool,
    entity: Value,
    watch: Option<Watch>,
}
pub fn prepare(
    registry: &Registry,
    context: &BusinessContext,
    order: &BrokerOrderDefinition,
    execution: Option<&OrderExecutionDefinition>,
    pending: bool,
    id: uuid::Uuid,
    price: Option<&ExactDecimal>,
) -> Result<Operation, String> {
    if id.is_nil() || !broker_order_ui::can_mutate(order, execution, pending)? {
        return Err("The selected broker order cannot be changed until it is working with valid bounds and no pending acknowledgement".into());
    }
    if let Some(price) = price {
        broker_order_ui::validate_limit(order, price)?;
    }
    let entity_id = order.id.as_ref().ok_or("Missing broker order identity")?;
    let order_key = broker_reads::typed_identity(entity_id)?;
    let entity = parameters::from_document(
        registry,
        "TomasAI.IFM.Domain.Trade.Shared.BrokerOrderId",
        &request_document(entity_id)?,
    )?;
    let (name, verb) = if price.is_some() {
        (
            "RequestBrokerOrderLimitUpdateCommand",
            "RequestBrokerOrderLimitUpdate",
        )
    } else {
        (
            "RequestBrokerOrderCancelCommand",
            "RequestBrokerOrderCancel",
        )
    };
    let subject = ActorSubject {
        kind: ActorKind::Command,
        name: "BrokerOrderCommand".into(),
        verb: verb.into(),
        entity: order_key.clone(),
    };
    let mut fields = BTreeMap::from([
        ("CommandId".into(), id.to_string().into()),
        ("OperationId".into(), id.to_string().into()),
        ("PostEvents".into(), true.into()),
        ("Subject".into(), subject.wire()),
        ("EntityId".into(), entity.clone()),
        ("EffectiveAtUtc".into(), timestamp_wire(context.as_of_utc)),
    ]);
    if let Some(price) = price {
        fields.insert("NewSignedNetDebitLimit".into(), price.wire());
    }
    Ok(Operation {
        prepared: Prepared {
            command_id: id,
            subject,
            body: registry.encode(&format!("{ROOT}.{name}"), &fields)?,
        },
        order_key,
        attempted: false,
        acknowledged: false,
        entity,
        watch: None,
    })
}
impl Operation {
    pub fn reconcile(&mut self) -> bool {
        if self.watch.as_ref().is_some_and(Watch::committed) {
            self.acknowledged = true;
        }
        self.acknowledged
    }
    pub async fn submit(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if self.reconcile() {
            return Ok(());
        }
        if self.attempted {
            return Err("OutcomeUnknown: broker mutation remains unconfirmed; refresh its original evidence".into());
        }
        self.watch = Some(
            Watch::start(
                backend,
                registry,
                &format!("{ROOT}.BrokerOrderChangedEvent"),
                "BrokerOrderDefinition",
                &format!("{ROOT}.BrokerOrderDefinition"),
                self.prepared.command_id,
                &self.order_key,
                self.entity.clone(),
                deadline,
            )
            .await?,
        );
        let mut receiver = self.watch.as_ref().unwrap().receiver();
        self.attempted = true;
        let reply = portfolio_commands::submit(backend, &self.prepared, deadline);
        tokio::pin!(reply);
        let result = tokio::select! {result=&mut reply=>result.map(|_|()),event=async{receiver.wait_for(|v|v.is_some()).await.is_ok()}=>{if event{Ok(())}else{reply.await.map(|_|())}}};
        result.map_err(|e| e.replace("Portfolio command", "Broker order command"))?;
        self.acknowledged = true;
        Ok(())
    }
}
