//! Opt-in bounded receipt/publication-to-render-command timing. No file I/O on the paint thread.
use crate::publication_transfer::{Stamp, utc_ticks};
use eframe::{egui, egui_wgpu};
use std::sync::{Arc, Mutex};
#[derive(Default)]
struct Samples {
    rows: Vec<[i64; 7]>,
    dropped: u64,
    last_source: [i64; 9],
}
#[derive(Clone)]
pub struct LatencyTrace {
    inner: Arc<Mutex<Samples>>,
    path: std::path::PathBuf,
}
impl LatencyTrace {
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
    pub fn finish(&self) {
        let samples = self.inner.lock().unwrap();
        let report = serde_json::json!({"measurement":"CPU render-pass callback; excludes GPU completion and display presentation","columns":["kind","sequence","quoteReceiptUtcTicks","publicationFirstObservedUtcTicks","rustReceiptUtcTicks","paintUtcTicks","freshQuote"],"samples":samples.rows,"droppedAtCapacity":samples.dropped});
        if let Ok(bytes) = serde_json::to_vec_pretty(&report) {
            let _ = std::fs::write(&self.path, bytes);
        }
    }
}
struct Paint {
    trace: LatencyTrace,
    stamps: [Option<Stamp>; 9],
}
impl egui_wgpu::CallbackTrait for Paint {
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
