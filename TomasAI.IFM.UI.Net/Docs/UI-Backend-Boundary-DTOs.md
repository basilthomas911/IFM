# UI backend boundary DTO inventory

UIR-12 removes backend execution, transport, and listener ownership from `UI.Net.Models` and removes generic model resolution from ViewModels. Some rich domain value graphs remain intentional method-boundary DTOs in ViewModels because the desktop editors submit those same typed aggregates back through command services. They are not service locators, transport clients, event consumers, or classes owned by `UI.Net.Models`.

The architecture suite locks this exception list. A new `*ReadModel` use fails unless this document and the matching assertion are deliberately reviewed together.

| Workflow | Boundary DTO families | ViewModel consumers |
| --- | --- | --- |
| Application/status | economic calendar, market outlook, status log, futures contracts | `IFMAppViewModel`, `MarketEconomicCalendarViewModel`, `StatusConsoleViewModel` |
| Fund | fund, order, order trade, transaction, P/L report | Fund editor and transaction ViewModels |
| Market Data | futures contracts/options, curves, EOD/bar/tick data, signals/status | Market Data editor and row ViewModels |
| Analytics/operations | ITI signals and strategy snapshots | `FuturesItiSignalEventRow` and Strategy Operations workflow |
| Trade | option trade/legs, positions, limits, plans, fills, history and distribution jobs | Trade, End-of-Day, confirmation, and Iron Condor ViewModels |

These types are permitted only as synchronous or asynchronous values crossing a typed service boundary and as the editable aggregate currently owned by a workflow ViewModel. Services own every backend API call and subscription. If a future screen needs a smaller stable display contract, add a UI-owned record under `UI.Net.Models` and map it in Services rather than expanding this inventory.

