//! Fund editor choices come from published lookup/product APIs. Labels never
//! become identities; unavailable stored values remain visible but unselectable.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::{BTreeMap, BTreeSet};
#[derive(Clone, Default)]
pub struct Selections {
    pub underlyings: Vec<String>,
    pub groups: BTreeMap<String, Vec<Json>>,
}
fn selectable(registry: &Registry, row: &Json) -> bool {
    if row["IsEnabled"] != true {
        return false;
    }
    let value = row["InternalValue"].as_str().unwrap_or_default();
    match row["GroupName"].as_str().unwrap_or_default() {
        "AssetTypes" => matches!(value, "Futures" | "FuturesOption"),
        "Directions" | "MarketConditions" => {
            let name = if row["GroupName"] == "Directions" {
                "MarketConditionDirection"
            } else {
                "MarketConditionType"
            };
            value!="Undefined"&&registry.enum_value(&format!("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.{name}"),value).is_ok()
        }
        _ => false,
    }
}
impl Selections {
    pub async fn load(
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<Self, String> {
        let mut output = Self::default();
        for group in ["AssetTypes", "Directions", "MarketConditions"] {
            let subject = ActorSubject::query("ReferenceQuery", "GetLookupDefinitions", "none");
            let body = Value::Array(vec![
                subject.wire(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                group.into(),
            ]);
            let reply = backend
                .request_until(&subject, &body, deadline, None)
                .await?;
            let rows = registry.project(
                "TomasAI.IFM.Domain.Reference.Shared.Lookups.LookupDefinitionReadModel[]",
                service_result(&reply)?,
            )?;
            let mut rows = rows
                .as_array()
                .filter(|v| !v.is_empty() && v.len() <= 4096)
                .cloned()
                .ok_or(format!(
                    "{group}: lookup definitions are missing or invalid"
                ))?;
            let mut ids = BTreeSet::new();
            for row in &rows {
                if row["Id"].as_i64().is_none_or(|id| id <= 0)
                    || row["GroupName"] != group
                    || row["InternalValue"]
                        .as_str()
                        .is_none_or(|v| v.trim().is_empty())
                    || row["DisplayName"]
                        .as_str()
                        .is_none_or(|v| v.trim().is_empty())
                    || !ids.insert(
                        row["InternalValue"]
                            .as_str()
                            .unwrap_or_default()
                            .to_string(),
                    )
                {
                    return Err(format!(
                        "{group}: lookup definitions are missing or invalid"
                    ));
                }
            }
            rows.sort_by_key(|row| (row["DisplayOrder"].as_i64(), row["Id"].as_i64()));
            output.groups.insert(group.into(), rows);
        }
        let mut symbols = BTreeSet::new();
        for family in [1, 2] {
            let subject = ActorSubject::query("ReferenceQuery", "GetTradeStrategySymbols", "none");
            let body = Value::Array(vec![
                subject.wire(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                family.into(),
            ]);
            let reply = backend
                .request_until(&subject, &body, deadline, None)
                .await?;
            let rows = registry.project(
                "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]",
                service_result(&reply)?,
            )?;
            for row in rows
                .as_array()
                .filter(|v| v.len() <= 100_000)
                .ok_or("Invalid Databento product list")?
            {
                let symbol = row["Symbol"]
                    .as_str()
                    .filter(|v| !v.trim().is_empty())
                    .ok_or("Invalid Databento product metadata")?;
                for key in ["Exchange", "Currency"] {
                    if row[key].as_str().is_none_or(|v| v.trim().is_empty()) {
                        return Err("Invalid Databento product metadata".into());
                    }
                }
                let currency = row["Currency"].as_str().unwrap_or_default();
                let exchange = row["Exchange"].as_str().unwrap_or_default();
                if symbol != symbol.trim()
                    || exchange != exchange.trim()
                    || currency.len() != 3
                    || !currency.bytes().all(|v| v.is_ascii_uppercase())
                    || row["Description"]
                        .as_str()
                        .is_none_or(|v| v.trim().is_empty())
                {
                    return Err("Invalid Databento product metadata".into());
                }
                if row["Id"].as_i64().is_none_or(|id| id <= 0) {
                    return Err("Invalid Databento product metadata".into());
                }
                symbols.insert(symbol.to_string());
            }
        }
        if symbols.is_empty() {
            return Err("No current Databento underlying symbols are loaded. Refresh instrument definitions first".into());
        }
        output.underlyings = symbols.into_iter().collect();
        output
            .underlyings
            .sort_by(|a, b| a.encode_utf16().cmp(b.encode_utf16()));
        Ok(output)
    }
    pub fn choices(&self, registry: &Registry, group: &str) -> Result<Vec<Json>, String> {
        Ok(self.groups.get(group).ok_or("Missing Fund lookup group")?.iter().map(|row|serde_json::json!({"value":row["InternalValue"],"label":row["DisplayName"],"enabled":selectable(registry,row)})).collect())
    }
    pub fn validate(
        &self,
        registry: &Registry,
        underlyings: &[String],
        assets: &[String],
        directions: &[String],
        conditions: &[String],
    ) -> Result<(), String> {
        for (label, values, available) in [
            ("Underlyings", underlyings, self.underlyings.clone()),
            ("Asset Types", assets, self.allowed(registry, "AssetTypes")?),
            (
                "Directions",
                directions,
                self.allowed(registry, "Directions")?,
            ),
            (
                "Market Conditions",
                conditions,
                self.allowed(registry, "MarketConditions")?,
            ),
        ] {
            let mut seen = BTreeSet::new();
            if values
                .iter()
                .any(|v| !available.contains(v) || !seen.insert(v))
            {
                return Err(format!(
                    "{label} contains unavailable or duplicate selections. Select values from the current list"
                ));
            }
        }
        Ok(())
    }
    fn allowed(&self, registry: &Registry, group: &str) -> Result<Vec<String>, String> {
        Ok(self
            .groups
            .get(group)
            .ok_or("Missing Fund lookup group")?
            .iter()
            .filter(|row| selectable(registry, row))
            .map(|row| {
                row["InternalValue"]
                    .as_str()
                    .unwrap_or_default()
                    .to_string()
            })
            .collect())
    }
}
