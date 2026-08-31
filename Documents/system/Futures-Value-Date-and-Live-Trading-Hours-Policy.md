# Futures Value Date and Live Trading Hours Policy

Status: Approved authoritative policy  
Market timezone: Toronto/New York Eastern Time (`America/New_York`, with daylight-saving conversion)

## 1. Market hours and value-date session

A futures value-date session:

- starts at 18:00 Eastern on the preceding calendar day, Sunday through Thursday;
- ends at 17:00 Eastern on the value date, Monday through Friday;
- includes its 18:00 start boundary and excludes its 17:00 end boundary;
- has no active value date during the weekday 17:00-18:00 maintenance gap or from Friday 17:00 through Sunday 18:00.

At 18:00 Eastern, the active and operational value date must roll immediately to the next weekday value date. For example, at 18:00 on Monday, August 31, 2026, the value date is Tuesday, September 1, 2026.

During a closed interval, the operational/read-only value date remains the most recently completed value date, while the active value date is absent.

Market hours and active value-date hours are the same interval: Sunday at 18:00 through Friday at 17:00 Eastern, excluding the daily 17:00-18:00 market close from Monday through Thursday.

## 2. Live trading hours

Live trading hours are 03:00 inclusive through 16:00 exclusive Eastern, Monday through Friday.

This is the position-entry window. Both opening new positions and closing existing positions are permitted during live trading hours.

## 3. Off-trading hours

Off-trading hours are all active value-date-session hours outside the live trading window. They include:

- 18:00 through 03:00 Eastern for the next value date;
- 16:00 through 17:00 Eastern on the value date.

Off-trading hours have an active value date but must not be represented as live trading hours. Opening a new position is prohibited. Closing, reducing or otherwise exiting an existing position is permitted.

Market-data, broker and order services required to monitor or close existing positions may remain available during off-trading hours because the market is open. Entry-only workflows remain disabled.

## 4. Closed hours

Closed hours have no active value date:

- 17:00 through 18:00 Eastern, Monday through Thursday;
- Friday at 17:00 through Sunday at 18:00 Eastern.

The most recently completed value date remains available as the operational value date for read-only work.

No opening or closing of positions is permitted while the market is closed. Application navigation and stored-data functions remain available.

## 5. Required state model

| State | Active value date | Open position | Close position | Market-data/order access |
|---|---:|---:|---:|---:|
| Live trading | Yes | Allowed | Allowed | Available |
| Off trading | Yes | Prohibited | Allowed | Available as required for monitoring/exits |
| Closed | No | Prohibited | Prohibited | Live market access stopped |

## 6. System conformance requirements

- One shared Eastern-time policy must determine value date and trading state for server, actors, UI and tests.
- The API server is the runtime authority for the current session decision. It must initialize that decision when the API process starts, before dependent market-data startup and before the UI treats the server as ready.
- The authoritative decision must be exposed as one immutable, versioned snapshot containing the market state, operational and active value dates, session boundaries and next transition. UI and actor consumers read that snapshot; they must not independently infer current state from their local clocks.
- The API server may reconstruct this deterministic snapshot from the authoritative Eastern clock and this policy after a restart. Persistence is not required for the current calendar-only policy.
- With production automatic feed startup enabled, each futures feed session starts at market open, 18:00 Eastern Sunday through Thursday, and stops at market close, 17:00 Eastern Monday through Friday.
- If the API server starts or restarts during an open session, production automatic startup must initialize the current value date and start the required feed immediately. If it starts during `Closed`, it retains the last operational value date for read-only use and waits for the next market open.
- The 03:00 and 16:00 boundaries change position-entry permission only. They must not start, restart or stop an otherwise-required market-data feed.
- The UI must refresh the authoritative session while it remains open and react at 03:00, 16:00, 17:00 and 18:00 boundaries without requiring an application restart.
- A value-date rollover must update every value-date-keyed UI model and pipeline service coherently; changing only the displayed date is prohibited.
- `LiveTrading` must mean only the weekday 03:00-16:00 window. It must not be inferred from the broader value-date session.
- `OffTrading` must mean an active value-date/market-hours session outside live trading hours and must enforce close-only position permissions.
- `Closed` must mean there is no active value date.
- Position-entry decisions must use the live-trading window.
- Position-exit decisions must allow both live-trading and off-trading states, but never the closed state.
- Market-data and order-service lifecycle decisions must use market hours and position-monitoring/exit requirements. They must not mistake the end of the position-entry window for market close.
- Application navigation and non-live functionality must remain independent of all three trading states.
- Tests must cover exact boundary instants, Eastern daylight-saving transitions, an application kept open across rollover and coherent rollover of value-date-dependent services.

## 7. Current conformance gap recorded on 2026-08-31

The shared calculator already rolls Monday through Thursday to the following value date at 18:00 Eastern, while the current startup service incorrectly treats the narrower 03:00-16:00 position-entry window as the automatic feed-start window. The running UI reads the market-session snapshot only at startup, so it can retain the previous value date after 18:00. The current session DTO also derives `IsLiveSessionOpen` from the broader value-date session instead of representing market-open, live-trading and off-trading states independently. The active-value-date calculation does not exclude the weekday 17:00-18:00 gap or Friday after 17:00. These are implementation defects relative to this approved policy and require correction.
