//! Conditional business outbox acknowledgement after actual native realization.
//! Raw storage calls are cancellable descendants, never managed lease owners.
use super::{Book, Directory, Discovery};
use crate::runtime::{Deadline, owner::Child};
use serde_json::Value;
use std::{collections::BTreeSet, sync::Arc};
use tokio_util::sync::CancellationToken;
impl Directory {
    /// Delivers existing durable outbox rows only after the caller has proved
    /// complete source realization. Accepted delivery facts survive physical reset,
    /// so a stalled C# store cannot become a prerequisite of reconstruction.
    ///
    /// # Errors
    /// Changed journal, malformed data, incomplete/uncertain writes, excessive
    /// backlog or the original dataset cancellation/deadline.
    pub async fn deliver(
        &self,
        discovery: &Discovery,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.deliver_for_physical(discovery, parent, stop, deadline, None)
            .await
            .map(|_| ())
    }
    /// Conditionally acknowledges only the physical recipe actually realized by
    /// a native resource group. A concurrent changed recipe is never certified.
    ///
    /// # Errors
    /// Same source, persistence, bounded-capacity and original-deadline failures
    /// as delivery. A changed physical recipe returns false without any write.
    pub async fn deliver_for_physical(
        &self,
        discovery: &Discovery,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
        physical: Option<[u8; 32]>,
    ) -> Result<bool, String> {
        let book = self
            .accepted()?
            .ok_or("Committed delivery lacks accepted facts")?;
        if let Some(physical) = physical
            && book.physical_signature()? != physical
        {
            return Ok(false);
        }
        if self.acknowledged(&book)? {
            return Ok(true);
        }
        let mut seen = BTreeSet::new();
        for _ in 0..=100 {
            let reply = read(
                discovery,
                parent.clone(),
                &book,
                "pending",
                &[],
                stop,
                deadline,
            )
            .await?;
            if !current(&reply, &book)? {
                self.refresh()?;
                return Err("Committed journal changed before delivery acknowledgement".into());
            }
            let rows = reply["rows"]
                .as_array()
                .filter(|rows| rows.len() <= 100)
                .ok_or("Invalid committed outbox page")?;
            if rows.is_empty() {
                self.acknowledge(&book)?;
                return Ok(true);
            }
            let mut ids = Vec::new();
            for row in rows {
                let row = row
                    .as_array()
                    .filter(|row| row.len() == 2)
                    .ok_or("Malformed committed outbox row")?;
                super::guid(&row[0])?;
                let id = row[0].as_str().ok_or("Invalid committed outbox identity")?;
                let revision = row[1]
                    .as_i64()
                    .filter(|revision| *revision >= 0 && *revision <= book.revision)
                    .ok_or("Committed outbox belongs to a newer journal")?;
                if !seen.insert((id.to_owned(), revision)) {
                    return Err("Committed acknowledgement made no progress".into());
                }
                ids.push(id.to_owned());
            }
            let reply = read(
                discovery,
                parent.clone(),
                &book,
                "ack",
                &ids,
                stop,
                deadline,
            )
            .await?;
            if !current(&reply, &book)? {
                self.refresh()?;
                return Err("Committed journal changed during delivery acknowledgement".into());
            }
        }
        Err("Committed outbox exceeds bounded delivery capacity".into())
    }
}
fn current(reply: &Value, book: &Book) -> Result<bool, String> {
    let accepted = reply["accepted"]
        .as_bool()
        .ok_or("Invalid committed outbox receipt")?;
    Ok(accepted
        && reply["revision"].as_i64() == Some(book.revision)
        && reply["digest"]
            .as_str()
            .is_some_and(|digest| digest.eq_ignore_ascii_case(&book.journal_digest)))
}
async fn read(
    discovery: &Discovery,
    parent: Arc<Child>,
    book: &Book,
    action: &str,
    ids: &[String],
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Value, String> {
    let args=serde_json::to_vec(&serde_json::json!({"action":action,"authority":book.authority,"dataset":"GLBX.MDP3","revision":book.revision,"digest":book.journal_digest,"ids":ids})).map_err(|_|"Invalid committed delivery command")?;
    let bytes = crate::messaging::raw_io::read(
        discovery.broker.clone(),
        discovery.scope.clone(),
        parent,
        9,
        &args,
        stop,
        deadline,
    )
    .await?;
    if bytes.len() > 16384 {
        return Err("Committed outbox receipt exceeds capacity".into());
    }
    serde_json::from_slice(&bytes).map_err(|_| "Malformed committed outbox receipt".into())
}
