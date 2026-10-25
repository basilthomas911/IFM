//! Typed Portfolio selection state. Complete reads are staged before replacing
//! the displayed selection, so a failed refresh cannot combine two Funds.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::services::{business_context::BusinessContext, portfolio as api};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
pub fn kind(name: &str) -> String {
    format!("{}.ViewModels.{name}", api::ROOT)
}
fn model(registry: &Registry, name: &str, wire: &Value) -> Result<Json, String> {
    registry.project(&kind(name), wire)
}
pub fn fund_key(row: &Json) -> Result<String, String> {
    crate::backend::identity::canonical_json(&serde_json::json!([
        row["Name"].as_str().ok_or("Missing Fund name")?.to_string(),
        row["FundMandateVersion"]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or("Invalid Fund mandate version")?
            .to_string(),
        row["OperatingState"]
            .as_str()
            .ok_or("Invalid Fund state")?
            .to_string(),
    ]))
}
#[derive(Clone, Default)]
pub struct PortfolioData {
    pub context: Option<BusinessContext>,
    pub portfolios: Vec<Value>,
    pub funds: Vec<Value>,
    pub portfolio: Option<Value>,
    pub fund: Option<Value>,
    pub allocation: Option<Value>,
    pub envelope: Option<Value>,
    pub assignments: Vec<Value>,
    pub portfolio_revision: i64,
    pub fund_revision: i64,
    pub filter: String,
    pub fund_filter: Option<String>,
    pub status: String,
}
impl PortfolioData {
    pub fn visible_funds(&self, registry: &Registry) -> Result<Vec<Value>, String> {
        let mut rows = self
            .funds
            .iter()
            .map(|v| Ok((model(registry, "FundMandateReadModel", v)?, v.clone())))
            .collect::<Result<Vec<_>, String>>()?;
        rows.retain(|(row, _)| {
            self.fund_filter
                .as_ref()
                .is_none_or(|filter| row["OperatingState"] == *filter)
        });
        rows.sort_by(|(a, _), (b, _)| {
            a["Name"]
                .as_str()
                .unwrap_or_default()
                .to_uppercase()
                .encode_utf16()
                .cmp(
                    b["Name"]
                        .as_str()
                        .unwrap_or_default()
                        .to_uppercase()
                        .encode_utf16(),
                )
                .then_with(|| {
                    b["FundMandateVersion"]
                        .as_i64()
                        .cmp(&a["FundMandateVersion"].as_i64())
                })
        });
        Ok(rows.into_iter().map(|(_, wire)| wire).collect())
    }
    pub fn is_current_fund(&self, registry: &Registry) -> Result<bool, String> {
        let Some(selected) = &self.fund else {
            return Ok(false);
        };
        let selected = model(registry, "FundMandateReadModel", selected)?;
        for wire in &self.funds {
            let row = model(registry, "FundMandateReadModel", wire)?;
            if row["FundId"] == selected["FundId"]
                && row["FundMandateVersion"].as_i64() > selected["FundMandateVersion"].as_i64()
            {
                return Ok(false);
            }
        }
        Ok(true)
    }
    fn clear_fund(&mut self) {
        self.fund = None;
        self.allocation = None;
        self.envelope = None;
        self.assignments.clear();
        self.fund_revision = 0;
    }
    async fn page(
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        name: &str,
        key: &str,
        item: &str,
        fields: BTreeMap<String, Value>,
        deadline: tokio::time::Instant,
    ) -> Result<Vec<Value>, String> {
        let value = api::query(backend, registry, context, name, key, fields, deadline).await?;
        let page_kind = format!("{}.ServiceApi.PortfolioPage`1[{}]", api::ROOT, kind(item));
        let page = registry.project(&page_kind, &value)?;
        if page["PageSize"] != 100 {
            return Err("Portfolio page size differs from request".into());
        }
        value
            .as_array()
            .and_then(|v| v.first())
            .and_then(Value::as_array)
            .filter(|v| v.len() <= 100)
            .cloned()
            .ok_or("Invalid Portfolio page".into())
    }
    async fn revision(
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        portfolio: i32,
        fund: Option<i32>,
        deadline: tokio::time::Instant,
    ) -> Result<i64, String> {
        let mut fields = BTreeMap::from([("PortfolioId".into(), portfolio.into())]);
        if let Some(id) = fund {
            fields.insert("FundId".into(), id.into());
        }
        let key = fund
            .map(|id| format!("{portfolio}.{id}"))
            .unwrap_or_else(|| portfolio.to_string());
        let wire = api::query(
            backend,
            registry,
            context,
            if fund.is_some() {
                "GetFundRevisionQuery"
            } else {
                "GetPortfolioRevisionQuery"
            },
            &key,
            fields,
            deadline,
        )
        .await?;
        let row = registry.project(
            &format!("{}.ServiceApi.PortfolioAggregateRevision", api::ROOT),
            &wire,
        )?;
        if row["PortfolioId"] != portfolio
            || fund
                .map(|id| row["FundId"] != id)
                .unwrap_or(!row["FundId"].is_null())
        {
            return Err("Portfolio revision crossed selection scope".into());
        }
        row["Revision"]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or("Portfolio revision is unavailable".into())
    }
    pub async fn refresh(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        filter: &str,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let context = BusinessContext::load(backend).await?;
        let state = registry.enum_value(
            &format!("{}.Contracts.PortfolioOperatingState", api::ROOT),
            filter,
        )?;
        if state == 0 {
            return Err("Select a valid Portfolio state".into());
        }
        let rows = Self::page(
            backend,
            registry,
            &context,
            "GetPortfoliosQuery",
            "page",
            "PortfolioReadModel",
            BTreeMap::from([
                ("State".into(), state.into()),
                ("PageSize".into(), 100.into()),
                ("PageToken".into(), Value::Nil),
            ]),
            deadline,
        )
        .await?;
        let previous = self
            .portfolio
            .as_ref()
            .map(|v| model(registry, "PortfolioReadModel", v))
            .transpose()?;
        let mut seen = std::collections::BTreeSet::new();
        for wire in &rows {
            let row = model(registry, "PortfolioReadModel", wire)?;
            let id = row["PortfolioId"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid Portfolio identity")?;
            if row["OperatingState"] != filter || !seen.insert(id) {
                return Err("Portfolio page has a duplicate identity or wrong state".into());
            }
        }
        let selected = rows
            .iter()
            .find(|v| {
                model(registry, "PortfolioReadModel", v).is_ok_and(|row| {
                    previous
                        .as_ref()
                        .is_some_and(|p| row["PortfolioId"] == p["PortfolioId"])
                })
            })
            .or(rows.first())
            .cloned();
        let mut next = self.clone();
        next.context = Some(context);
        next.filter = filter.into();
        next.portfolios = rows;
        if next.fund_filter.is_none() && self.context.is_none() {
            next.fund_filter = Some("Active".into());
        }
        if let Some(selected) = selected {
            next.select_portfolio_wire(backend, registry, selected, deadline)
                .await?;
        } else {
            next.portfolio = None;
            next.portfolio_revision = 0;
            next.funds.clear();
            next.clear_fund();
            next.status.clear();
        }
        *self = next;
        Ok(())
    }
    pub async fn select_portfolio(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        id: i32,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let selected = self
            .portfolios
            .iter()
            .find(|v| {
                model(registry, "PortfolioReadModel", v).is_ok_and(|row| row["PortfolioId"] == id)
            })
            .cloned()
            .ok_or("Select a current Portfolio row")?;
        let mut next = self.clone();
        next.context = Some(BusinessContext::load(backend).await?);
        next.select_portfolio_wire(backend, registry, selected, deadline)
            .await?;
        *self = next;
        Ok(())
    }
    async fn select_portfolio_wire(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        selected: Value,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let row = model(registry, "PortfolioReadModel", &selected)?;
        let id = i32::try_from(
            row["PortfolioId"]
                .as_i64()
                .ok_or("Missing Portfolio identity")?,
        )
        .map_err(|_| "Portfolio identity overflow")?;
        let context = self.context.as_ref().ok_or("Load API context first")?;
        let funds = Self::page(
            backend,
            registry,
            context,
            "GetFundsQuery",
            &id.to_string(),
            "FundMandateReadModel",
            BTreeMap::from([
                ("PortfolioId".into(), id.into()),
                ("State".into(), Value::Nil),
                ("PageSize".into(), 100.into()),
                ("PageToken".into(), Value::Nil),
            ]),
            deadline,
        )
        .await?;
        let mut identities = std::collections::BTreeSet::new();
        for wire in &funds {
            let fund = model(registry, "FundMandateReadModel", wire)?;
            if fund["PortfolioId"] != id
                || !identities.insert((
                    fund["FundId"].to_string(),
                    fund["FundMandateVersion"].to_string(),
                ))
            {
                return Err("Fund page crossed Portfolio scope or repeated a version".into());
            }
        }
        let revision = Self::revision(backend, registry, context, id, None, deadline).await?;
        let previous = self
            .fund
            .as_ref()
            .map(|v| model(registry, "FundMandateReadModel", v).and_then(|v| fund_key(&v)))
            .transpose()?;
        self.portfolio = Some(selected);
        self.portfolio_revision = revision;
        self.funds = funds;
        self.clear_fund();
        let visible = self.visible_funds(registry)?;
        if let Some(fund) = visible
            .iter()
            .find(|v| {
                model(registry, "FundMandateReadModel", v)
                    .and_then(|v| fund_key(&v))
                    .is_ok_and(|id| Some(id) == previous)
            })
            .or(visible.first())
            .cloned()
        {
            self.select_fund_wire(backend, registry, fund, deadline)
                .await?;
        } else {
            self.status = "No Funds match the selected state.".into();
        }
        Ok(())
    }
    pub async fn select_fund(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        key: &str,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let selected = self
            .visible_funds(registry)?
            .into_iter()
            .find(|v| {
                model(registry, "FundMandateReadModel", v)
                    .and_then(|v| fund_key(&v))
                    .is_ok_and(|v| v == key)
            })
            .ok_or("Select a current Fund row")?;
        let mut next = self.clone();
        next.context = Some(BusinessContext::load(backend).await?);
        next.select_fund_wire(backend, registry, selected, deadline)
            .await?;
        *self = next;
        Ok(())
    }
    async fn select_fund_wire(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        selected: Value,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let row = model(registry, "FundMandateReadModel", &selected)?;
        let portfolio = self.portfolio.as_ref().ok_or("Select a Portfolio")?;
        let parent = model(registry, "PortfolioReadModel", portfolio)?;
        if row["PortfolioId"] != parent["PortfolioId"] {
            return Err("Fund does not belong to selected Portfolio".into());
        }
        let pid = i32::try_from(
            row["PortfolioId"]
                .as_i64()
                .ok_or("Invalid Portfolio identity")?,
        )
        .map_err(|_| "Portfolio identity overflow")?;
        let fid = i32::try_from(row["FundId"].as_i64().ok_or("Invalid Fund identity")?)
            .map_err(|_| "Fund identity overflow")?;
        let context = self.context.as_ref().ok_or("Load API context")?;
        let key = format!("{pid}.{fid}");
        let portfolio_revision =
            Self::revision(backend, registry, context, pid, None, deadline).await?;
        let fund_revision =
            Self::revision(backend, registry, context, pid, Some(fid), deadline).await?;
        let fields = BTreeMap::from([
            ("PortfolioId".into(), pid.into()),
            ("FundId".into(), fid.into()),
        ]);
        let allocation = api::query(
            backend,
            registry,
            context,
            "GetFundAllocationQuery",
            &key,
            fields.clone(),
            deadline,
        )
        .await?;
        let mut envelope_fields = fields.clone();
        envelope_fields.insert("AsOfUtc".into(), timestamp_wire(context.as_of_utc));
        let envelope = api::query(
            backend,
            registry,
            context,
            "GetFundRiskEnvelopeQuery",
            &key,
            envelope_fields,
            deadline,
        )
        .await?;
        for (name, value) in [
            ("FundAllocationReadModel", &allocation),
            ("FundRiskEnvelopeReadModel", &envelope),
        ] {
            if !value.is_nil() {
                let row = model(registry, name, value)?;
                if row["PortfolioId"] != pid || row["FundId"] != fid {
                    return Err("Fund configuration crossed selection scope".into());
                }
            }
        }
        let mut fields = fields;
        fields.insert(
            "MandateVersion".into(),
            row["FundMandateVersion"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid Fund version")?
                .into(),
        );
        let assignments = api::query(
            backend,
            registry,
            context,
            "GetFundTemplateAssignmentsQuery",
            &key,
            fields,
            deadline,
        )
        .await?;
        let assignments = assignments
            .as_array()
            .filter(|v| v.len() <= 10_000)
            .cloned()
            .ok_or("Invalid Fund assignment list")?;
        for value in &assignments {
            let value = model(registry, "FundTradeTemplateAssignmentReadModel", value)?;
            if value["PortfolioId"] != pid
                || value["FundId"] != fid
                || value["FundMandateVersion"] != row["FundMandateVersion"]
            {
                return Err("Fund assignment crossed selection scope or mandate version".into());
            }
        }
        self.portfolio_revision = portfolio_revision;
        self.fund_revision = fund_revision;
        self.fund = Some(selected);
        self.allocation = if allocation.is_nil() {
            None
        } else {
            Some(allocation)
        };
        self.envelope = if envelope.is_nil() {
            None
        } else {
            Some(envelope)
        };
        self.assignments = assignments;
        self.status.clear();
        Ok(())
    }
}
