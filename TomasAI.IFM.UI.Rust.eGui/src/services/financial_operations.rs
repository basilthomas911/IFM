//! Durable financial operation orchestration. Queue acknowledgement is not a
//! committed ledger receipt. Reconciliation always precedes a replay of the
//! original request; an uncertain operation never receives a replacement ID.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    persistence::{financial_journal, financial_json, financial_request, journal::Journal},
    services::business_context::BusinessContext,
};
use rmpv::Value;
use serde_json::Value as Json;
use std::{collections::BTreeMap, path::PathBuf};
#[derive(Clone)]
pub struct OperationStore {
    pub directory: PathBuf,
    pub configuration: bool,
}
impl OperationStore {
    fn command(&self) -> &'static str {
        if self.configuration {
            "ConfigureLedgerCommand"
        } else {
            "PostFundTransactionCommand"
        }
    }
    async fn save(&self, value: Json) -> Result<Json, String> {
        let path = self.directory.clone();
        let command = self.command();
        tokio::task::spawn_blocking(move || {
            let registry = Registry::compiled()?;
            let id =
                uuid::Uuid::parse_str(value["Request"]["OperationId"].as_str().unwrap_or_default())
                    .map_err(|_| "Invalid retained financial operation")?;
            Journal::new(path).save(
                id,
                &value,
                &|v| financial_journal::validate(&registry, command, v),
                &|| false,
            )
        })
        .await
        .map_err(|e| e.to_string())?
    }
    pub async fn load(&self, id: uuid::Uuid) -> Result<Json, String> {
        let path = self.directory.clone();
        let command = self.command();
        tokio::task::spawn_blocking(move || {
            let registry = Registry::compiled()?;
            Journal::new(path)
                .load(id, &|v| financial_journal::validate(&registry, command, v))?
                .ok_or("Pending financial operation was not found".into())
        })
        .await
        .map_err(|e| e.to_string())?
    }
    pub async fn submit(
        &self,
        backend: &Backend,
        registry: &Registry,
        pending: Json,
        deadline: tokio::time::Instant,
    ) -> Result<Json, String> {
        financial_journal::validate(registry, self.command(), &pending)?;
        let id = uuid::Uuid::parse_str(
            pending["Request"]["OperationId"]
                .as_str()
                .unwrap_or_default(),
        )
        .map_err(|_| "Invalid operation identity")?;
        self.save(pending).await?;
        self.recover(backend, registry, id, deadline).await
    }
    pub async fn recover(
        &self,
        backend: &Backend,
        registry: &Registry,
        id: uuid::Uuid,
        deadline: tokio::time::Instant,
    ) -> Result<Json, String> {
        let mut pending = self.load(id).await?;
        if matches!(pending["Phase"].as_i64(), Some(2 | 3)) {
            return Ok(pending);
        }
        let result = async {
            if let Some(reconciled) = self
                .reconcile(backend, registry, &pending, deadline)
                .await?
            {
                return self.save(reconciled).await;
            }
            pending["Phase"] = 1.into();
            pending["Message"] =
                "Awaiting committed financial outcome. This operation will retain its identity."
                    .into();
            pending = self.save(pending.clone()).await?;
            let request = financial_request::rehydrate(registry, self.command(), &pending)?;
            let (actor, verb) = registry.route(&format!(
                "TomasAI.IFM.Domain.Portfolio.Shared.Financial.{}",
                self.command()
            ))?;
            let portfolio = pending["Request"]["PortfolioId"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid financial Portfolio scope")?;
            let subject = ActorSubject {
                kind: ActorKind::Command,
                name: actor.into(),
                verb: verb.into(),
                entity: portfolio.to_string(),
            };
            let declared = crate::services::strategy::member(
                registry,
                &format!(
                    "TomasAI.IFM.Domain.Portfolio.Shared.Financial.{}",
                    self.command()
                ),
                &request,
                "Subject",
            )?;
            if declared != subject.wire() {
                return Err(
                    "Integrity: persisted financial route does not match its Portfolio".into(),
                );
            }
            let accepted = backend
                .request_until(&subject, &request, deadline, None)
                .await
                .and_then(|reply| {
                    service_result(&reply)
                        .map(|_| ())
                        .map_err(|e| e.to_string())
                });
            for _ in 0..10 {
                if let Some(reconciled) = self
                    .reconcile(backend, registry, &pending, deadline)
                    .await?
                {
                    return self.save(reconciled).await;
                }
                if accepted.is_err() {
                    break;
                }
                tokio::time::timeout_at(
                    deadline,
                    tokio::time::sleep(std::time::Duration::from_millis(100)),
                )
                .await
                .map_err(|_| "Financial outcome deadline expired")?;
            }
            pending["Message"] = if let Err(error) = accepted {
                format!("Outcome not confirmed: {error}. Check the original operation.")
            } else {
                "Outcome unknown; check this original operation again.".into()
            }
            .into();
            self.save(pending.clone()).await
        }
        .await;
        match result {
            Ok(value) => Ok(value),
            Err(error) if error.starts_with("Integrity:") => Err(error),
            Err(error) => {
                pending["Phase"] = 1.into();
                pending["Message"] = format!("Outcome unknown: {error}").into();
                self.save(pending).await
            }
        }
    }
    async fn reconcile(
        &self,
        backend: &Backend,
        registry: &Registry,
        pending: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<Option<Json>, String> {
        let request = &pending["Request"];
        let portfolio = request["PortfolioId"]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or("Invalid financial scope")?;
        let context = tokio::time::timeout_at(deadline, BusinessContext::load(backend))
            .await
            .map_err(|_| "Financial receipt deadline expired")??;
        let subject = ActorSubject::query(
            "GeneralLedgerQuery",
            "GetPostingReceipt",
            &portfolio.to_string(),
        );
        let access = registry.value_from_json(
            "TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialAccess",
            &request["Access"],
        )?;
        let body = registry.encode(
            "TomasAI.IFM.Domain.Portfolio.Shared.Financial.GetPostingReceiptQuery",
            &BTreeMap::from([
                ("Subject".into(), subject.wire()),
                ("EntityId".into(), Value::Array(vec![portfolio.into()])),
                (
                    "Scope".into(),
                    Value::Array(vec![
                        portfolio.into(),
                        if self.configuration {
                            Value::Nil
                        } else {
                            request["Body"]["FundId"]
                                .as_i64()
                                .ok_or("Missing financial Fund scope")?
                                .into()
                        },
                        access,
                    ]),
                ),
                (
                    "OperationId".into(),
                    request["OperationId"]
                        .as_str()
                        .ok_or("Missing financial operation identity")?
                        .into(),
                ),
                (
                    "CorrelationId".into(),
                    uuid::Uuid::new_v4().to_string().into(),
                ),
                ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ]),
        )?;
        let reply = backend
            .request_until(&subject, &body, deadline, None)
            .await?;
        let read = service_result(&reply)?
            .as_array()
            .filter(|v| v.len() >= 4)
            .ok_or("Invalid financial outcome read")?;
        let status = read[0].as_i64().ok_or("Invalid financial outcome status")?;
        if status == 1 {
            let outcome_kind =
                "TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialOperationOutcome";
            let member = if self.configuration {
                "Configuration"
            } else {
                "Posting"
            };
            let completion =
                crate::services::strategy::member(registry, outcome_kind, &read[1], member)?;
            if completion.is_nil() {
                return Err("Integrity: financial receipt contains the wrong outcome type".into());
            }
            let kind = format!(
                "TomasAI.IFM.Domain.Portfolio.Shared.Financial.{}",
                if self.configuration {
                    "LedgerConfigurationCompletedEvent"
                } else {
                    "LedgerPostingCompletedEvent"
                }
            );
            let model = registry.project(&kind, &completion)?;
            if model["OperationId"] != request["OperationId"]
                || model["InputHash"] != request["InputSha256"]
                || model["Receipt"]["BookId"] != request["Body"]["BookId"]
                || if self.configuration {
                    model["Receipt"]["Action"]
                        != registry.project(
                            &format!(
                                "TomasAI.IFM.Domain.Portfolio.Shared.Financial.{}",
                                self.command()
                            ),
                            &financial_request::rehydrate(registry, self.command(), pending)?,
                        )?["Body"]["Action"]
                } else {
                    model["Receipt"]["FundId"] != request["Body"]["FundId"]
                }
            {
                return Err(
                    "Integrity: returned financial receipt does not match the original request"
                        .into(),
                );
            }
            let mut reconciled = pending.clone();
            reconciled["Phase"] = 2.into();
            reconciled["Completion"] = financial_json::stored_json(registry, &kind, &completion)?;
            reconciled["Message"] = "Committed. History may still be updating.".into();
            return Ok(Some(reconciled));
        }
        if status != 2 {
            return Err("Financial receipt service is unavailable".into());
        }
        if !read[1].is_nil() {
            return Err("Integrity: NotFound financial receipt contains a value".into());
        }
        let observed = timestamp(&read[3])?;
        let expires = chrono::DateTime::parse_from_rfc3339(
            request["ExpiresAtUtc"]
                .as_str()
                .ok_or("Missing financial expiry")?,
        )
        .map_err(|_| "Invalid financial expiry")?
        .to_utc();
        if observed >= expires {
            let mut reconciled = pending.clone();
            reconciled["Phase"] = 3.into();
            reconciled["Message"]=if self.configuration{"The request expired; the authoritative receipt query confirms no configuration change."}else{"The request expired; the authoritative receipt query confirms no posting."}.into();
            return Ok(Some(reconciled));
        }
        Ok(None)
    }
}
