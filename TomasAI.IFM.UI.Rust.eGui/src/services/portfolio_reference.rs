//! Backend-owned deployment choices for Portfolio forms. Paging and local
//! selection preserve published identities; no strategy definition is inferred.
use crate::backend::{schema::Registry, transport::Backend};
use serde_json::{Value as Json, json};
use std::collections::{BTreeMap, BTreeSet};
pub async fn deployments(
    backend: &Backend,
    registry: &Registry,
    deadline: tokio::time::Instant,
) -> Result<Vec<Json>, String> {
    let mut rows = vec![];
    let mut cursor: Option<String> = None;
    let mut seen = BTreeSet::new();
    loop {
        let request = json!({"Operation":3,"Kind":7,"Key":null,"Limit":64,"AfterCode":cursor});
        let page = tokio::time::timeout_at(
            deadline,
            crate::state::strategy_catalog::query(backend, registry, &request),
        )
        .await
        .map_err(|_| "Deployment choices deadline expired")??;
        let items = page["Items"]
            .as_array()
            .filter(|v| v.len() <= 64)
            .ok_or("Invalid deployment choices page")?;
        for value in items {
            let key = &value["Key"];
            let id = uuid::Uuid::parse_str(key["Id"].as_str().unwrap_or_default())
                .ok()
                .filter(|v| !v.is_nil())
                .ok_or("Invalid deployment identity")?;
            let version = key["Version"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid deployment version")?;
            if key["Kind"] != 7 || !seen.insert((id, version)) {
                return Err("Invalid or duplicate ConfigurationDb deployment identities".into());
            }
            rows.push(value.clone());
        }
        if page["NextCode"].is_null() {
            break;
        }
        let next = page["NextCode"]
            .as_str()
            .filter(|v| !v.is_empty())
            .ok_or("Invalid deployment continuation")?;
        if rows.len() >= 4096 || items.is_empty() || cursor.as_deref() == Some(next) {
            return Err("Deployment catalog paging exceeded its limit".into());
        }
        cursor = Some(next.into());
    }
    if rows.len() > 4096 {
        return Err("Deployment catalog paging exceeded its limit".into());
    }
    let retired = registry.enum_value(
        "TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogLifecycleStatus",
        "Retired",
    )?;
    let mut latest: BTreeMap<String, Json> = BTreeMap::new();
    for row in rows {
        let id = row["Key"]["Id"]
            .as_str()
            .ok_or("Missing deployment identity")?
            .to_string();
        if latest
            .get(&id)
            .is_none_or(|old| old["Key"]["Version"].as_i64() < row["Key"]["Version"].as_i64())
        {
            latest.insert(id, row);
        }
    }
    let mut rows = latest
        .into_values()
        .filter(|row| row["Status"] != retired)
        .collect::<Vec<_>>();
    rows.sort_by(|a, b| {
        a["Code"]
            .as_str()
            .unwrap_or_default()
            .encode_utf16()
            .cmp(b["Code"].as_str().unwrap_or_default().encode_utf16())
    });
    Ok(rows)
}
