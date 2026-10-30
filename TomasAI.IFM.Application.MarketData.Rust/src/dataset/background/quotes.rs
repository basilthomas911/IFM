//! Optional background quote acquisition. Its failure never retires a monitored
//! options session. Only timestamped copied observations cross this boundary.
use super::*;
use crate::latest::{QuoteValue, Tick};

#[derive(Default)]
pub(super) struct State {
    current: Option<Observer>,
}
struct Observer {
    stop: CancellationToken,
    registrations: Vec<Registration>,
    quotes: Arc<Mutex<BTreeMap<String, Tick<QuoteValue>>>>,
}
impl Drop for Observer {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
impl State {
    pub(super) fn prices(
        &mut self,
        source: super::super::live::Start,
        date: time::Date,
        clock: Arc<Clock>,
        parent: Arc<Child>,
        diagnostic: impl Fn(&[String]) -> Result<(), String> + Send + Sync + 'static,
        changed: watch::Sender<u64>,
    ) -> Result<BTreeMap<String, crate::option_chain::qualification::Decimal>, String> {
        let replace = self.current.as_ref().is_none_or(|current| {
            current.stop.is_cancelled() || current.registrations != source.registrations
        });
        if replace {
            let stop = parent.cancellation().child_token();
            let quotes = Arc::new(Mutex::new(BTreeMap::new()));
            let observer = Observer {
                stop: stop.clone(),
                registrations: source.registrations.clone(),
                quotes: quotes.clone(),
            };
            let task_parent = parent.clone();
            let task_clock = clock.clone();
            parent.spawn_joined(move |_| async move {
                // Separate provider evidence from the main options resource group.
                // A quiet observer is healthy and has no market-data deadline.
                loop {
                    if stop.is_cancelled() { break; }
                    let child = match task_parent.owner().child() { Ok(child)=>Arc::new(child), Err(_)=>break };
                    let result = super::super::bootstrap_quotes::observe(
                        source.clone(),date,task_clock.clone(),child.clone(),&stop,Deadline::recovery(),
                        &diagnostic,
                        |next| {
                            let mut known=quotes.lock().map_err(|_|"Background quotes interrupted")?;
                            let now=task_clock.utc_ticks().map_err(|_|"Background quote clock unavailable")?;
                            let available=|quotes:&BTreeMap<String,Tick<QuoteValue>>|quotes.iter().filter_map(|(contract,quote)|super::super::bootstrap_quotes::midpoint(*quote,now).map(|_|contract.clone())).collect::<BTreeSet<_>>();
                            let membership_changed=available(&known)!=available(&next);
                            // Retain every source tick, but only availability
                            // changes wake planning. Price drift uses maintenance.
                            if *known!=next {
                                *known=next;
                                if membership_changed {changed.send_modify(|n|*n=n.wrapping_add(1));}
                            }
                            Ok(())
                        },
                    ).await;
                    drop(child);
                    if stop.is_cancelled() { break; }
                    // Provider/setup failure belongs to this optional observer.
                    // Never create a dataset recovery incident or reuse stale values.
                    if result.is_err() {
                        if let Ok(mut known)=quotes.lock() { known.clear(); }
                        changed.send_modify(|n|*n=n.wrapping_add(1));
                    }
                    tokio::select! { biased; ()=stop.cancelled()=>break, ()=tokio::time::sleep(Duration::from_secs(5))=>{} }
                }
            }).map_err(|_|"Background quote observer admission failed")?;
            self.current = Some(observer);
        }
        let now = clock
            .utc_ticks()
            .map_err(|_| "Background quote clock unavailable")?;
        Ok(self.current
            .as_ref()
            .ok_or("Background quote observer unavailable")?
            .quotes
            .lock()
            .map_err(|_| "Background quotes interrupted")?
            .iter()
            .filter_map(|(contract, quote)| {
                super::super::bootstrap_quotes::midpoint(*quote, now)
                    .map(|price| (contract.clone(), price))
            })
            .collect())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        dataset::{
            lanes::Gateway,
            live::{Evidence, Start},
        },
        dns::Resolver,
        runtime::owner::Owner,
    };
    use tokio::net::TcpListener;

    #[tokio::test]
    async fn stalled_optional_quote_setup_returns_immediately_reuses_observer_and_joins_on_stop() {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let root = Arc::new(Owner::default());
        let parent = Arc::new(root.child().unwrap());
        let clock = Arc::new(Clock::new(17_931_024_000_000_000, false).unwrap());
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        let source = Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: vec![SubscriptionSpec {
                    schema: Schema::Mbp1,
                    symbology: SType::InstrumentId,
                    symbols: vec!["42".into()],
                    replay_start_ns: None,
                }],
            },
            registrations: vec![Registration {
                instrument: 42,
                publisher: 3,
                contract: "ES20261218".into(),
                source: "42".into(),
                asset: Asset::Futures,
                tick: None,
                vwap: false,
                required: true,
            }],
            key: "x".repeat(32),
            gateway: Gateway {
                host: address.ip().to_string(),
                port: address.port(),
                resolver: Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
            },
            evidence: Evidence::Observations,
        };
        let changed = watch::channel(0).0;
        let mut state = State::default();
        let started = std::time::Instant::now();
        assert!(
            state
                .prices(
                    source.clone(),
                    date,
                    clock.clone(),
                    parent.clone(),
                    |_| Ok(()),
                    changed.clone()
                )
                .unwrap()
                .is_empty()
        );
        assert!(
            started.elapsed() < Duration::from_millis(100),
            "Optional provider IO blocked the caller"
        );
        let (socket, _) = tokio::time::timeout(Duration::from_secs(2), listener.accept())
            .await
            .unwrap()
            .unwrap();
        assert!(
            state
                .prices(source, date, clock, parent.clone(), |_| Ok(()), changed)
                .unwrap()
                .is_empty()
        );
        assert!(
            tokio::time::timeout(Duration::from_millis(50), listener.accept())
                .await
                .is_err(),
            "Unchanged dependencies started a second observer"
        );
        assert!(
            parent.provider_state().borrow().is_none(),
            "Optional setup changed primary provider evidence"
        );
        drop(state);
        tokio::time::timeout(
            Duration::from_secs(2),
            parent.wait_nested(&CancellationToken::new(), Deadline::recovery()),
        )
        .await
        .unwrap()
        .unwrap();
        drop(socket);
        drop(parent);
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        assert!(root.disposable().unwrap());
    }
}
