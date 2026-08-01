# Application Blackboard

`IBlackboardService` groups cache models by their owning domain. Callers access a
domain root first and then the cache model, for example:

```csharp
blackboard.MarketDataSecurities.FuturesContract
blackboard.MarketDataFeed.FuturesEodData
blackboard.MarketDataAnalytics.FuturesRsiSignal
blackboard.Trade.OptionTrade
blackboard.EventSourcing.EventProjectorState
```

The root properties are `Application`, `EventSourcing`, `Fund`, `MarketData`,
`MarketDataAnalytics`, `MarketDataFeed`, `MarketDataSecurities`, `Reference`, and
`Trade`.

Flat cache-model properties remain as obsolete forwarding aliases for one
compatibility window. They return the same instances owned by the domain roots and
do not introduce different Redis keys or cached values. New code must use the
domain-root API.

`MarketDataFeed.FuturesOptionTickData` and
`MarketDataFeed.FuturesOptionTickPriceData` intentionally reference the same cache
model because both names address the same Redis namespace. The streaming-parameter
property uses `FuturesOptionTickDataStreamingParameterModel`; it is distinct from
`StreamingRequestIdModel`.
