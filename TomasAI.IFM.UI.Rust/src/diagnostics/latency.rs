//! Opt-in bounded publication-to-render timing.
//!
//! `IFM_UI_LATENCY_OUTPUT` enables a shared sample buffer capped at 10,000 rows.
//! The render-pass callback records CPU timing and counts samples dropped at capacity;
//! it does not measure GPU completion or display presentation. File output happens
//! when `finish` is called, not in the paint callback. A short mutex protects samples
//! shared by the application and render callback.

//! Opt-in bounded receipt/publication-to-render-command timing. No file I/O on the paint thread.
use crate::publication_transfer::{Stamp, utc_ticks};
use eframe::{egui, egui_wgpu};
use std::sync::{Arc, Mutex};
/// Bounded latency rows, overflow count, and per-kind last-source timestamps.
#[derive(Default)]
struct Samples {
    /// Seven-column timing samples: kind, sequence, source ticks, produced ticks, received ticks, paint ticks, and fresh-source flag.
    rows: Vec<[i64; 7]>,
    /// Timing samples omitted after the bounded buffer reaches capacity.
    dropped: u64,
    /// Most recently measured source timestamp for each publication kind.
    last_source: [i64; 9],
}
/// Optional shared CPU render-timing recorder and its shutdown output path.
#[derive(Clone)]
pub struct LatencyTrace {
    /// Shared mutex-protected latency samples.
    inner: Arc<Mutex<Samples>>,
    /// Output file selected by IFM_UI_LATENCY_OUTPUT; written only when timing collection finishes.
    path: std::path::PathBuf,
}
impl LatencyTrace {
    /// Enable bounded timing collection only when IFM_UI_LATENCY_OUTPUT names an output file.
    pub fn from_env() -> Option<Self> {
        std::env::var_os("IFM_UI_LATENCY_OUTPUT").map(|path| Self {
            inner: Arc::new(Mutex::new(Samples {
                rows: Vec::with_capacity(10000),
                dropped: 0,
                last_source: [0; 9],
            })),
            path: path.into(),
        })
    }
    /// Schedule a bounded CPU render-pass timing callback for changed publication stamps; file output is deferred until finish.
    pub fn paint(&self, ctx: &egui::Context, stamps: [Option<Stamp>; 9]) {
        if stamps.iter().all(Option::is_none) {
            return;
        }
        ctx.layer_painter(egui::LayerId::new(
            egui::Order::Foreground,
            egui::Id::new("publication-latency"),
        ))
        .add(egui_wgpu::Callback::new_paint_callback(
            ctx.screen_rect(),
            Paint {
                trace: self.clone(),
                stamps,
            },
        ));
    }
    /// Write the collected CPU render-timing report and dropped-sample count to the configured path. I/O failures are intentionally nonfatal.
    pub fn finish(&self) {
        let samples = self.inner.lock().unwrap();
        let report = serde_json::json!({"measurement":"CPU render-pass callback; excludes GPU completion and display presentation","columns":["kind","sequence","quoteReceiptUtcTicks","publicationFirstObservedUtcTicks","rustReceiptUtcTicks","paintUtcTicks","freshQuote"],"samples":samples.rows,"droppedAtCapacity":samples.dropped});
        if let Ok(bytes) = serde_json::to_vec_pretty(&report) {
            let _ = std::fs::write(&self.path, bytes);
        }
    }
}
/// Wgpu render callback carrying the timing recorder and the frame's changed-publication stamps.
struct Paint {
    /// Shared bounded latency recorder retained by the render callback.
    trace: LatencyTrace,
    /// Changed publication stamps associated with this frame.
    stamps: [Option<Stamp>; 9],
}
impl egui_wgpu::CallbackTrait for Paint {
    /// Record at most 10,000 CPU render-callback samples and count dropped samples; this measures neither GPU completion nor presentation.
    fn paint(
        &self,
        _: egui::PaintCallbackInfo,
        _: &mut wgpu::RenderPass<'static>,
        _: &egui_wgpu::CallbackResources,
    ) {
        let now = utc_ticks();
        let mut samples = self.trace.inner.lock().unwrap();
        for stamp in self.stamps.iter().flatten() {
            if samples.rows.len() < 10000 {
                let fresh = stamp.source_ticks > 0
                    && samples.last_source[stamp.kind as usize] != stamp.source_ticks;
                samples.last_source[stamp.kind as usize] = stamp.source_ticks;
                samples.rows.push([
                    stamp.kind as i64,
                    stamp.sequence as i64,
                    stamp.source_ticks,
                    stamp.produced_ticks,
                    stamp.received_ticks,
                    now,
                    fresh as i64,
                ]);
            } else {
                samples.dropped += 1;
            }
        }
    }
}
