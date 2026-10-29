//! Bounded delivery after native accumulation. Only options quotes are replaceable;
//! authoritative records retain their ordering and fail visibly on exhaustion.
use super::live::{Output, OutputSource};
use super::records::Observation;
use std::collections::{BTreeMap, VecDeque};

struct Pending {
    serial: u64,
    value: Output,
}
#[derive(Default)]
struct Slot {
    previous: Option<usize>,
    next: Option<usize>,
    value: Option<Pending>,
}
pub(super) struct LaneOutput {
    authoritative: VecDeque<Pending>,
    indices: BTreeMap<u32, usize>,
    quotes: Vec<Slot>,
    head: Option<usize>,
    tail: Option<usize>,
    serial: u64,
}
impl LaneOutput {
    pub(super) fn new() -> Self {
        Self {
            authoritative: VecDeque::new(),
            indices: BTreeMap::new(),
            quotes: Vec::new(),
            head: None,
            tail: None,
            serial: 0,
        }
    }
    pub(super) fn configure(&mut self, instruments: impl Iterator<Item = u32>) {
        self.clear();
        // LiveData creates three lane descriptors, but a physical options scope
        // activates only one. Reserve the writer buffer only for the active lane.
        self.authoritative.reserve(8192);
        self.indices = instruments
            .enumerate()
            .map(|(index, id)| (id, index))
            .collect();
        self.quotes = (0..self.indices.len()).map(|_| Slot::default()).collect();
    }
    pub(super) fn clear(&mut self) {
        self.authoritative.clear();
        for slot in &mut self.quotes {
            *slot = Slot::default();
        }
        self.head = None;
        self.tail = None;
        self.serial = 0;
    }
    pub(super) fn available(&self) -> usize {
        8192 - self.authoritative.len()
    }
    fn detach(&mut self, index: usize) {
        let previous = self.quotes[index].previous;
        let next = self.quotes[index].next;
        if let Some(previous) = previous {
            self.quotes[previous].next = next;
        } else {
            self.head = next;
        }
        if let Some(next) = next {
            self.quotes[next].previous = previous;
        } else {
            self.tail = previous;
        }
        self.quotes[index].previous = None;
        self.quotes[index].next = None;
    }
    pub(super) fn push_back(&mut self, value: Output) -> Result<(), String> {
        let index = match value.source {
            OutputSource::Observation(Observation::Quote(quote)) => {
                self.indices.get(&quote.header.instrument).copied()
            }
            _ => None,
        };
        if index.is_none() && self.available() == 0 {
            return Err("Authoritative application output capacity exhausted".into());
        }
        let serial = self
            .serial
            .checked_add(1)
            .ok_or("Native output sequence exhausted")?;
        self.serial = serial;
        let pending = Pending { serial, value };
        if let Some(index) = index {
            if self.quotes[index].value.is_some() {
                self.detach(index);
            }
            self.quotes[index].previous = self.tail;
            if let Some(tail) = self.tail {
                self.quotes[tail].next = Some(index);
            } else {
                self.head = Some(index);
            }
            self.tail = Some(index);
            self.quotes[index].value = Some(pending);
        } else {
            self.authoritative.push_back(pending);
        }
        Ok(())
    }
    pub(super) fn pop_front(&mut self) -> Option<Output> {
        if let Some(head) = self.head {
            let quote = self.quotes[head].value.as_ref()?;
            if self
                .authoritative
                .front()
                .is_none_or(|first| quote.serial < first.serial)
            {
                self.detach(head);
                return self.quotes[head].value.take().map(|pending| pending.value);
            }
        }
        self.authoritative.pop_front().map(|pending| pending.value)
    }
    pub(super) fn front(&self) -> Option<&Output> {
        if let Some(head) = self.head {
            let quote = self.quotes[head].value.as_ref()?;
            if self
                .authoritative
                .front()
                .is_none_or(|first| quote.serial < first.serial)
            {
                return Some(&quote.value);
            }
        }
        self.authoritative.front().map(|pending| &pending.value)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::dataset::{
        book::PublicationState,
        records::{Header, Quote},
        transport::Lane,
    };
    fn output(id: u32, sequence: u32, quote: bool) -> Output {
        let header = Header {
            instrument: id,
            publisher: 0,
            flags: 0,
            event_ns: 0,
            received_ns: 0,
            sequence,
            schema: 0,
        };
        Output {
            observed: 0,
            lane: Lane::OptionsGlbx,
            generation: 1,
            source: if quote {
                OutputSource::Observation(Observation::Quote(Quote {
                    header,
                    bid: 1,
                    ask: 2,
                    bid_size: 1,
                    ask_size: 1,
                    bid_count: 1,
                    ask_count: 1,
                }))
            } else {
                OutputSource::ReplayComplete(sequence)
            },
            state: PublicationState::default(),
            trade: None,
        }
    }
    #[test]
    fn latest_quotes_keep_retained_order_without_losing_controls() {
        let mut queue = LaneOutput::new();
        queue.configure([1, 2].into_iter());
        queue.push_back(output(1, 1, true)).unwrap();
        queue.push_back(output(0, 2, false)).unwrap();
        queue.push_back(output(2, 3, true)).unwrap();
        for seq in 4..100_000 {
            queue.push_back(output(1, seq, true)).unwrap();
        }
        assert!(matches!(
            queue.pop_front().unwrap().source,
            OutputSource::ReplayComplete(2)
        ));
        for expected in [3, 99_999] {
            match queue.pop_front().unwrap().source {
                OutputSource::Observation(Observation::Quote(q)) => {
                    assert_eq!(q.header.sequence, expected)
                }
                _ => panic!("wrong source"),
            }
        }
        assert!(queue.pop_front().is_none());
        assert_eq!(queue.available(), 8192);
    }
    #[test]
    fn authoritative_capacity_reset_and_futures_fifo() {
        let mut queue = LaneOutput::new();
        for seq in 0..8192 {
            queue.push_back(output(1, seq, true)).unwrap();
        }
        assert!(queue.push_back(output(1, 8192, true)).is_err());
        for seq in 0..8192 {
            match queue.pop_front().unwrap().source {
                OutputSource::Observation(Observation::Quote(q)) => {
                    assert_eq!(q.header.sequence, seq)
                }
                _ => panic!("wrong source"),
            }
        }
        queue.configure([1].into_iter());
        queue.push_back(output(1, 1, true)).unwrap();
        queue.clear();
        assert!(queue.pop_front().is_none());
        queue.push_back(output(1, 2, true)).unwrap();
        assert!(queue.pop_front().is_some());
    }
}
