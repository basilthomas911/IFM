# Order Composition live evidence register v1.0

Updated: 2026-09-08. Status: **strict native live quotes passed; full priced live path not yet qualified**. The [implementation record](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) records software and storage verification separately.

## Resolved machine blockers

The user explicitly approved starting Windows Time and synchronizing this computer. Windows Time was started and resynchronization succeeded at 07:26:15 local time, using time.windows.com. Subsequent sampling still measured roughly 455 ms of lag. An elevated, bounded correction used the median of five NTP samples after checking their spread and rejecting adjustments exceeding two seconds. The correction completed at 07:36:57; immediate residual offset was approximately 16 ms. A later 08:03 sample showed the computer approximately 112–116 ms ahead, so the one-time repair is not a guarantee of permanent clock accuracy. The service remained running. Source timestamps and non-future/freshness checks were not relaxed.

Both native backends now reserve a bounded process working set before locking ring memory. The reservation accounts for concurrent rings and restores the previous working-set limits after the last unlock when those limits still match the reservation. No machine security-policy change or new privilege grant was made. Opt-in tests exercised two concurrent required 8 MiB locks: C++ CTest **1 passed**, Rust **7 passed**.

After these fixes, the authenticated native DataBento test passed under **StrictProduction**, using actual ESU6 definitions and the E2B September 8 expiry. It observed fresh, non-future, two-sided option quotes without weakening the 0..5-second age check. Evidence: `.test-results/ocp/ocp-strict-live-clock-corrected.trx`, **1 passed**. This supersedes the earlier failed clock/memory-lock attempts; it does not establish a fully priced snapshot or long-running recovery qualification.

## Evidence and remaining qualification

| Requirement | Verified evidence / remaining work |
| --- | --- |
| European series | CME's [Tuesday/Thursday FAQ](https://www.cmegroup.com/articles/faqs/e-mini-s-p-500-tuesday-and-thursday-options-frequently-asked-questions.html) identifies E1B..E5B and E1D..E5D as European, with deliverable ES futures, $50 multiplier and 4 p.m. ET termination. Root ES alone does not identify exercise style. |
| Exact mappings and ticks | Reviewed `option_pricing_convention` rows remain unpublished. Exact DataBento IDs, expiry, underlying and premium-dependent tick rules must be mapped. A constant 0.25 tick fixture does not qualify every ES option premium. |
| Product calendar | CME's [trading hours](https://www.cmegroup.com/trading-hours.html) and [2026 Labor Day settlement schedule](https://www.cmegroup.com/tools-information/holiday-calendar/files/2026/labor-day-holiday-settlement-times-2026.pdf) provide exchange evidence. Complete versioned calendar, expiry and day-count mappings have not been published. Synthetic weekday calendars remain test fixtures. |
| FMP access | Both existing opt-in Treasury and economic-calendar endpoint tests passed (`ocp-live-fmp.trx`). Access is established. |
| FMP source and publication convention | The [Treasury documentation](https://site.financialmodelingprep.com/developer/docs/stable/treasury-rates) is now accessible. The [official FAQ](https://site.financialmodelingprep.com/faqs?code=data) confirms daily Treasury data. Neither establishes the source series identity, nominal-semiannual convention or an exact publication cutoff. The [cycle-times page](https://site.financialmodelingprep.com/developer/docs/cycle-times) lists economic-calendar refresh but no Treasury timing. Provider evidence is still required; the user has been asked whether it is available. |
| Rate conversion | The implemented CMT proxy conversion is explicit and tested. Treasury's [official FAQ](https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions) explains Treasury conventions; it does not prove that FMP supplies that exact series. No convention or SLA was inferred from matching sample values. |
| Durable ownership/recovery | Concrete committed workflow/option-order/option-position source adapters, projection receipts, persisted reconstruction plans, startup reconciliation, durable context refresh and discovery-release receipts are implemented. Real PostgreSQL lifecycle/replay, real process replacement/rollover and a joined PostgreSQL-source/delivery/worker-pricing/replacement/closure test pass for two/four legs. The joined test uses controlled UTC feed transport and direct worker calls, so it is not live process recovery or soak. |
| Full live capture | DataBento → supervised worker → Black-76 → immutable snapshot → durable ownership/replacement still requires a single qualified run with published real reference inputs, plus the prescribed recovery/soak checks. |

## Exact remaining closure work

1. Obtain evidence identifying FMP's source series/yield convention and establish an explicit, versioned publication-admission policy.
2. Publish reviewed exact provider/product/tick/calendar/day-count mappings and the corresponding market-data policy version.
3. Run the complete live priced snapshot and committed handoff/replacement/rollover qualification with those inputs; record actual observations and failures.

The API's existing live-enablement guards remain in place. No broker connection or order submission was made. The future composer builders and broker/emulator order lifecycle are separate OC gates. Overall prerequisite/live completion must not be claimed while the rows above remain open.

