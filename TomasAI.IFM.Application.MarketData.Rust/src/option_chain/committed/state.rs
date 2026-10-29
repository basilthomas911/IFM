//! Native immutable journal directory. A refresh is copied business intent, not
//! a provider lease. Duplicate callers join one cancellable source read; no lock
//! is held while awaiting storage and recovery reuses the last accepted book.
use super::Book;
use crate::runtime::Deadline;
use std::{
    future::Future,
    sync::{Arc, Mutex},
};
use tokio::sync::watch;
use tokio_util::sync::CancellationToken;
#[derive(Default)]
struct State {
    requested: u64,
    applied: u64,
    book: Option<Arc<Book>>,
    acknowledged: Option<[u8; 32]>,
    deadline: Option<Deadline>,
}
/// Native copied-fact authority. It owns no dataset child, task, SDK handle,
/// source socket or managed callback and can safely survive a physical reset.
pub struct Directory {
    loading: std::sync::atomic::AtomicBool,
    authority: String,
    state: Mutex<State>,
    changed: watch::Sender<u64>,
    closed: CancellationToken,
}
struct Reading<'a>(&'a Directory);
impl Drop for Reading<'_> {
    fn drop(&mut self) {
        self.0
            .loading
            .store(false, std::sync::atomic::Ordering::Release);
        self.0.notify();
    }
}
impl Directory {
    /// Starts one requested source capture. The read itself belongs to the dataset
    /// resource parent, using that episode's original deadline.
    ///
    /// # Errors
    /// Noncanonical/oversized business namespace.
    pub fn new(authority: &str) -> Result<Self, String> {
        if authority.trim().is_empty()
            || authority != authority.trim()
            || authority.encode_utf16().count() > 128
            || authority.chars().any(char::is_control)
        {
            return Err("Invalid committed authority namespace".into());
        }
        let (changed, _) = watch::channel(0);
        Ok(Self {
            loading: std::sync::atomic::AtomicBool::new(false),
            authority: authority.into(),
            state: Mutex::new(State {
                requested: 1,
                ..Default::default()
            }),
            changed,
            closed: CancellationToken::new(),
        })
    }
    fn notify(&self) {
        self.changed.send_modify(|v| *v = v.wrapping_add(1));
    }
    /// Requests a read after this explicit handoff. Each monotonic request
    /// fences reads begun before a later committed change. No recovery timer
    /// is created or reset.
    ///
    /// # Errors
    /// Closed/interrupted directory or exhausted monotonic request ordinal.
    pub fn refresh(&self) -> Result<u64, String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        if self.closed.is_cancelled() {
            return Err("Committed directory ended".into());
        }
        if state.applied == state.requested
            && state
                .book
                .as_ref()
                .is_some_and(|book| state.acknowledged == Some(book.signature))
        {
            state.deadline = None;
        }
        state.deadline.get_or_insert_with(Deadline::recovery);
        state.requested = state
            .requested
            .checked_add(1)
            .ok_or("Committed refresh ordinal exhausted")?;
        let revision = state.requested;
        drop(state);
        self.notify();
        Ok(revision)
    }
    /// Native event subscription, independent of quote consumers or C# callers.
    pub fn changes(&self) -> watch::Receiver<u64> {
        self.changed.subscribe()
    }
    /// First pending handoff's original deadline. Duplicate commands never reset it.
    ///
    /// # Errors
    /// Interrupted copied business state.
    pub fn pending_deadline(&self) -> Result<Option<Deadline>, String> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?
            .deadline)
    }
    /// Reuses an actual supervised replacement episode after terminal failure.
    /// Only the native factory passes this budget; maintenance cannot grant one.
    ///
    /// # Errors
    /// Interrupted copied control state.
    pub fn rebase_pending(&self, incoming: Deadline) -> Result<(), String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        if state.deadline.is_some_and(|old| old.remaining().is_zero())
            && !incoming.remaining().is_zero()
        {
            state.deadline = Some(incoming);
        }
        Ok(())
    }
    /// Last accepted facts, including while a newer read is pending. These copied
    /// facts permit physical source reconstruction without awaiting C# storage.
    ///
    /// # Errors
    /// Interrupted native copied-state directory.
    pub fn accepted(&self) -> Result<Option<Arc<Book>>, String> {
        if self.closed.is_cancelled() {
            return Ok(None);
        }
        Ok(self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?
            .book
            .clone())
    }
    /// Whether the latest explicit refresh has been accepted; this is independent
    /// of physical source readiness and must also be checked before acknowledging
    /// a committed handoff to a caller.
    ///
    /// # Errors
    /// Interrupted native copied-state directory.
    pub fn current(&self) -> Result<bool, String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        Ok(!self.closed.is_cancelled() && state.applied == state.requested && state.book.is_some())
    }
    /// Whether this immutable business snapshot has already been delivered.
    /// Physical recovery reuses this fact and never waits on C# acknowledgement.
    ///
    /// # Errors
    /// Interrupted copied state.
    pub fn acknowledged(&self, book: &Book) -> Result<bool, String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        Ok(state.acknowledged == Some(book.signature))
    }
    /// Records successful conditional business delivery only for the exact
    /// current accepted snapshot. A later handoff cannot inherit an old receipt.
    ///
    /// # Errors
    /// Superseded/closed/interrupted copied state.
    pub fn acknowledge(&self, book: &Book) -> Result<(), String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        if self.closed.is_cancelled()
            || state.applied != state.requested
            || state
                .book
                .as_ref()
                .is_none_or(|current| current.signature != book.signature)
        {
            return Err("Committed acknowledgement was superseded".into());
        }
        state.acknowledged = Some(book.signature);
        state.deadline = None;
        drop(state);
        self.notify();
        Ok(())
    }
    /// Ends the business directory. A late storage result is fenced even if its
    /// source ignored cancellation; no old snapshot can resurrect ownership.
    ///
    /// # Errors
    /// Interrupted copied-state directory.
    pub fn close(&self) -> Result<(), String> {
        self.closed.cancel();
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        state.book = None;
        state.acknowledged = None;
        state.deadline = None;
        drop(state);
        self.notify();
        Ok(())
    }
    /// Loads one complete snapshot. Only one caller polls the source future. Other
    /// callers wait on native change notifications, sharing their original budgets.
    /// Cancellation drops the source future and releases read admission via RAII.
    ///
    /// # Errors
    /// Source validation, stale/conflicting revision, cancellation/closure or the
    /// original absolute dataset deadline. Failed reads never replace accepted facts.
    pub async fn load<F, Fut>(
        &self,
        source: F,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Arc<Book>, String>
    where
        F: FnOnce() -> Fut,
        Fut: Future<Output = Result<Book, String>>,
    {
        let mut changes = self.changed.subscribe();
        let ticket = loop {
            changes.borrow_and_update();
            if stop.is_cancelled() || self.closed.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Committed capture cancelled/original deadline expired".into());
            }
            let action = {
                let state = self
                    .state
                    .lock()
                    .map_err(|_| "Committed directory interrupted")?;
                if state.applied == state.requested
                    && let Some(book) = &state.book
                {
                    return Ok(book.clone());
                }
                if self.loading.load(std::sync::atomic::Ordering::Acquire) {
                    None
                } else {
                    self.loading
                        .store(true, std::sync::atomic::Ordering::Release);
                    Some(state.requested)
                }
            };
            if let Some(ticket) = action {
                break ticket;
            }
            deadline.run(stop,async{tokio::select!{biased;()=self.closed.cancelled()=>Err("Committed directory ended"),result=changes.changed()=>result.map_err(|_|"Committed directory ended")}}).await.map_err(|_|"Committed capture cancelled/original deadline expired")??;
        };
        let _reading = Reading(self);
        let superseded = async {
            loop {
                changes
                    .changed()
                    .await
                    .map_err(|_| "Committed directory ended")?;
                let state = self
                    .state
                    .lock()
                    .map_err(|_| "Committed directory interrupted")?;
                if state.requested != ticket {
                    return Ok::<(), &str>(());
                }
            }
        };
        let book=deadline.run(stop,async{tokio::select!{biased;()=self.closed.cancelled()=>Err("Committed directory ended".into()),result=superseded=>{result?;Err("Committed source was superseded by a newer handoff".into())},book=source()=>book}}).await.map_err(|_|"Committed capture cancelled/original deadline expired")??;
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Committed directory interrupted")?;
        if self.closed.is_cancelled()
            || stop.is_cancelled()
            || deadline.remaining().is_zero()
            || state.requested != ticket
        {
            return Err("Committed capture retired before acceptance".into());
        }
        if book.authority != self.authority {
            return Err("Committed authority namespace changed".into());
        }
        if let Some(prior) = state.book.as_ref() {
            book.validate_successor(prior)?;
        }
        let book = Arc::new(book);
        state.book = Some(book.clone());
        state.applied = ticket;
        if state.acknowledged == Some(book.signature) {
            state.deadline = None;
        }
        Ok(book)
    }
}
