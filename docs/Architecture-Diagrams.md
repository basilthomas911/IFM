# TomasAI IFM - Architecture Diagrams

This document contains Mermaid diagrams for the TomasAI Investment Fund Manager architecture.
Use a Markdown viewer or export tool that supports Mermaid (e.g., VS Code with Mermaid extension, Typora, or Pandoc with mermaid-filter) to render these diagrams.

---

## 1. High-Level System Architecture

```mermaid
flowchart TB
    subgraph Clients["Client Layer"]
        UI[Web UI<br/>Razor Pages]
        API_Client[REST API<br/>Clients]
        External[External<br/>Systems]
    end

    subgraph Gateway["API Gateway Layer"]
        Query_API[Query API<br/>Server]
        Command_API[Command API<br/>Server]
        Event_API[Event API<br/>Server]
    end

    subgraph Messaging["Message Broker"]
        NATS[NATS JetStream]
    end

    subgraph Actors["Actor Layer"]
        Query_Actors[Query Actors]
        Command_Actors[Command Actors]
        Event_Actors[Event Actors<br/>Denormalizers]
    end

    subgraph Storage["Storage Layer"]
        PostgreSQL[(PostgreSQL<br/>Event Store)]
        ScyllaDB[(ScyllaDB<br/>Read Models)]
        Redis[(Redis<br/>Cache)]
    end

    UI --> Query_API
    UI --> Command_API
    API_Client --> Query_API
    API_Client --> Command_API
    External --> Event_API

    Query_API --> NATS
    Command_API --> NATS
    Event_API --> NATS

    NATS --> Query_Actors
    NATS --> Command_Actors
    NATS --> Event_Actors

    Command_Actors --> PostgreSQL
    Event_Actors --> ScyllaDB
    Query_Actors --> ScyllaDB
    Query_Actors --> Redis
```

---

## 2. Actor Type Hierarchy

```mermaid
classDiagram
    class IActor {
        <<interface>>
        +ActorMailboxId Id
        +IActorMailbox Mailbox
        +bool IsRunning
        +StartAsync(supervisor)
        +StopAsync()
    }

    class IQueryActor~TActor~ {
        <<interface>>
        +IQuery Query
    }

    class ICommandActor~TActor~ {
        <<interface>>
    }

    class BaseQueryActor~TActor~ {
        <<abstract>>
        #ILogger Logger
        #ParseMessage(context, message)
        #ReceiveAsync(context, state, query)
        #OnStartup(context)
        #OnShutdown(context)
    }

    class BaseEventSourceCommandActor~TActor~ {
        <<abstract>>
        #ILogger Logger
        #ParseMessage(context, message)
        #ReceiveAsync(context, state, command)
        #ValidateCommand(context, state, command)
        #OnStartup(context)
        #OnShutdown(context)
    }

    class FuturesContractQueryActor {
        +ActorName: "FuturesContractQuery"
        #ParseMessage()
        #ReceiveAsync()
    }

    class FuturesContractCommandActor {
        +ActorName: "FuturesContractCommand"
        #ParseMessage()
        #ReceiveAsync()
    }

    IActor <|-- IQueryActor
    IActor <|-- ICommandActor
    IQueryActor <|.. BaseQueryActor
    ICommandActor <|.. BaseEventSourceCommandActor
    BaseQueryActor <|-- FuturesContractQueryActor
    BaseEventSourceCommandActor <|-- FuturesContractCommandActor
```

---

## 3. Command Flow Sequence

```mermaid
sequenceDiagram
    participant Client
    participant CommandAPI as Command API
    participant NATS as NATS JetStream
    participant CommandActor as Command Actor
    participant EventStore as PostgreSQL<br/>(Event Store)
    participant Denormalizer as Event Denormalizer
    participant ReadDB as ScyllaDB<br/>(Read Model)

    Client->>CommandAPI: POST AddFuturesContractCommand
    CommandAPI->>NATS: Publish to Command.FuturesContract.Add.{id}
    NATS->>CommandActor: Deliver Message
    
    CommandActor->>CommandActor: ParseMessage()
    CommandActor->>EventStore: Log Command
    CommandActor->>EventStore: Load Event Stream
    EventStore-->>CommandActor: Historical Events
    CommandActor->>CommandActor: Replay Events ? State
    CommandActor->>CommandActor: Validate Command
    CommandActor->>CommandActor: Execute Handler
    CommandActor->>CommandActor: Generate Domain Event
    CommandActor->>EventStore: Persist Event
    CommandActor->>NATS: Publish FuturesContractAddedEvent
    CommandActor-->>Client: ServiceResult<Guid>
    
    NATS->>Denormalizer: Deliver Event
    Denormalizer->>ReadDB: Update Read Model
```

---

## 4. Query Flow Sequence

```mermaid
sequenceDiagram
    participant Client
    participant QueryAPI as Query API
    participant NATS as NATS JetStream
    participant QueryActor as Query Actor
    participant ReadDB as ScyllaDB<br/>(Read Model)
    participant Cache as Redis Cache

    Client->>QueryAPI: GET /api/marketdata/futures/{id}
    QueryAPI->>NATS: Request Query.FuturesContract.Get.{id}
    NATS->>QueryActor: Deliver Message
    
    QueryActor->>QueryActor: ParseMessage()
    QueryActor->>Cache: Check Cache
    
    alt Cache Hit
        Cache-->>QueryActor: Cached ViewModel
    else Cache Miss
        QueryActor->>ReadDB: Query Read Model
        ReadDB-->>QueryActor: Data
        QueryActor->>QueryActor: Map to ViewModel
        QueryActor->>Cache: Store in Cache
    end
    
    QueryActor-->>NATS: Reply with ViewModel
    NATS-->>QueryAPI: Response
    QueryAPI-->>Client: ServiceResult<ViewModel>
```

---

## 5. Event Sourcing State Management

```mermaid
stateDiagram-v2
    [*] --> Empty: Create State
    
    Empty --> Loaded: LoadState()
    Loaded --> Loaded: ReplayEvents()
    
    state Loaded {
        [*] --> ReadingEvents
        ReadingEvents --> ApplyingEvent: For each event
        ApplyingEvent --> ApplyingEvent: Apply(event)
        ApplyingEvent --> StateRebuilt: All events applied
    }
    
    StateRebuilt --> Processing: ReceiveAsync()
    Processing --> Updated: Command Handler
    Updated --> Updated: Apply(newEvent)
    Updated --> Persisted: SaveState()
    Persisted --> [*]
```

---

## 6. Actor Supervisor Architecture

```mermaid
flowchart TB
    subgraph Supervisor["IActorSupervisor"]
        direction TB
        Registry["Actor Registry<br/>IImmutableDictionary&lt;ActorMailboxId, IActor&gt;"]
        ThreadStates["Thread States<br/>IImmutableDictionary&lt;ActorThreadId, IActorState&gt;"]
        Producers["Producers<br/>IImmutableDictionary&lt;ActorMailboxId, IActorProducer&gt;"]
        Consumers["Consumers<br/>IImmutableDictionary&lt;ActorType, IActorConsumer&gt;"]
        Container["DI Container<br/>IContainerInstance"]
    end

    subgraph Actors["Managed Actors"]
        CmdActor1[FuturesContract<br/>CommandActor]
        CmdActor2[YieldCurveRate<br/>CommandActor]
        QryActor1[FuturesContract<br/>QueryActor]
        QryActor2[YieldCurveRate<br/>QueryActor]
    end

    subgraph Messaging["NATS Integration"]
        Producer1[NatsActorProducer]
        Producer2[NatsActorProducer]
        Consumer1[NatsActorConsumer<br/>Commands]
        Consumer2[NatsActorConsumer<br/>Queries]
    end

    Registry --> CmdActor1
    Registry --> CmdActor2
    Registry --> QryActor1
    Registry --> QryActor2

    Producers --> Producer1
    Producers --> Producer2
    Consumers --> Consumer1
    Consumers --> Consumer2

    Consumer1 --> CmdActor1
    Consumer1 --> CmdActor2
    Consumer2 --> QryActor1
    Consumer2 --> QryActor2
```

---

## 7. CQRS Data Flow

```mermaid
flowchart LR
    subgraph Write["Write Side"]
        Command[Command]
        CommandHandler[Command<br/>Handler]
        DomainEvent[Domain<br/>Event]
        EventStore[(Event<br/>Store)]
    end

    subgraph Sync["Synchronization"]
        EventBus[Event Bus<br/>NATS]
        Denormalizer[Event<br/>Denormalizer]
    end

    subgraph Read["Read Side"]
        ReadModel[(Read<br/>Model)]
        QueryHandler[Query<br/>Handler]
        Query[Query]
        ViewModel[View<br/>Model]
    end

    Command --> CommandHandler
    CommandHandler --> DomainEvent
    DomainEvent --> EventStore
    DomainEvent --> EventBus
    EventBus --> Denormalizer
    Denormalizer --> ReadModel
    Query --> QueryHandler
    QueryHandler --> ReadModel
    ReadModel --> ViewModel
```

---

## 8. Bounded Contexts

```mermaid
flowchart TB
    subgraph Securities["Securities Context"]
        FC[Futures Contracts]
        OC[Option Contracts]
    end

    subgraph MarketData["MarketData Context"]
        EOD[EOD Data]
        YC[Yield Curves]
        TD[Trading Days]
    end

    subgraph Trade["Trade Context"]
        Orders[Orders]
        Positions[Positions]
        Strategies[Strategies]
    end

    subgraph Fund["Fund Context"]
        Balance[Fund Balance]
        Trans[Transactions]
        NAV[NAV]
    end

    subgraph Reference["Reference Context"]
        Symbols[Symbols]
        Exchanges[Exchanges]
    end

    Securities --> MarketData
    MarketData --> Trade
    Trade --> Fund
    Reference --> Securities
    Reference --> Trade
```

---

## 9. Deployment Architecture

```mermaid
flowchart TB
    subgraph LB["Load Balancer"]
        HAProxy[HAProxy/NGINX]
    end

    subgraph API["API Nodes"]
        API1[API Node 1]
        API2[API Node 2]
        API3[API Node 3]
    end

    subgraph NATS_Cluster["NATS Cluster"]
        NATS1[NATS Node 1]
        NATS2[NATS Node 2]
        NATS3[NATS Node 3]
    end

    subgraph Workers["Actor Workers"]
        CmdWorker[Command<br/>Workers]
        QryWorker[Query<br/>Workers]
        EvtWorker[Event<br/>Workers]
    end

    subgraph Data["Data Layer"]
        PG_Primary[(PostgreSQL<br/>Primary)]
        PG_Replica[(PostgreSQL<br/>Replica)]
        Scylla1[(ScyllaDB<br/>Node 1)]
        Scylla2[(ScyllaDB<br/>Node 2)]
        Scylla3[(ScyllaDB<br/>Node 3)]
        Redis1[(Redis<br/>Primary)]
        Redis2[(Redis<br/>Replica)]
    end

    HAProxy --> API1
    HAProxy --> API2
    HAProxy --> API3

    API1 --> NATS1
    API2 --> NATS2
    API3 --> NATS3

    NATS1 <--> NATS2
    NATS2 <--> NATS3
    NATS1 <--> NATS3

    NATS_Cluster --> CmdWorker
    NATS_Cluster --> QryWorker
    NATS_Cluster --> EvtWorker

    CmdWorker --> PG_Primary
    PG_Primary --> PG_Replica
    
    EvtWorker --> Scylla1
    QryWorker --> Scylla1
    Scylla1 <--> Scylla2
    Scylla2 <--> Scylla3

    QryWorker --> Redis1
    Redis1 --> Redis2
```

---

## 10. Message Subject Naming Convention

```mermaid
flowchart LR
    subgraph Subject["NATS Subject Structure"]
        direction LR
        ActorType["ActorType<br/>(Command/Query/Event)"]
        ActorName["ActorName<br/>(FuturesContractCommand)"]
        Verb["Verb<br/>(Add/Change/Remove/Get)"]
        EntityId["EntityId<br/>(ES-2025-06)"]
    end

    ActorType --> |"."| ActorName --> |"."| Verb --> |"."| EntityId

    subgraph Examples["Examples"]
        Ex1["Command.FuturesContractCommand.Add.ES-2025-06"]
        Ex2["Query.FuturesContractQuery.Get.ES-2025-06"]
        Ex3["Event.FuturesContractDenormalizer.Added.ES-2025-06"]
    end
```

---

## PDF Export Instructions

To export this document to PDF with rendered diagrams:

### Option 1: VS Code + Markdown PDF Extension
1. Install "Markdown PDF" extension in VS Code
2. Install "Markdown Preview Mermaid Support" extension
3. Open this file and use Ctrl+Shift+P ? "Markdown PDF: Export (pdf)"

### Option 2: Typora
1. Open this file in Typora
2. File ? Export ? PDF

### Option 3: Pandoc with Mermaid Filter
```bash
npm install -g @mermaid-js/mermaid-cli
pandoc Architecture-Diagrams.md -o Architecture-Diagrams.pdf --filter mermaid-filter
```

### Option 4: Mermaid Live Editor
1. Visit https://mermaid.live
2. Copy each diagram code block
3. Export as PNG/SVG
4. Combine into PDF document

---

*Document generated for TomasAI Investment Fund Manager v25.10*
