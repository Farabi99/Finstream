# FinStream - Real-Time Financial Market Data Engine

> A production-ready, real-time market data analytics backend demonstrating Clean Architecture, CQRS pattern, and horizontal scaling via Redis.

---

## Table of Contents

- [Quick Start (Docker)](#quick-start-docker)
- [Quick Start (Manual)](#quick-start-manual)
- [What Is FinStream?](#what-is-finstream)
- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)
- [API Endpoints](#api-endpoints)
- [WebSocket Integration](#websocket-integration)
- [How to Test](#how-to-test)
- [Development](#development)

---

## Quick Start (Docker)

Run **everything** with a single command — no manual setup needed:

```bash
docker-compose up --build
```

That's it! This spins up:
- 🐘 **PostgreSQL** — persistent database (data survives restarts)
- 🔴 **Redis** — cache + pub/sub backplane
- ⚙️ **Processor** — background worker generating and processing market data
- 🌐 **API** — REST API + Swagger UI + WebSocket server

### Open Swagger UI
👉 **http://localhost:5106/swagger**

### Docker Volume & Data Persistence

| Command | Data |
|---------|------|
| `docker-compose down` | ✅ PostgreSQL data is **preserved** (volume `pgdata` kept) |
| `docker-compose down -v` | ❌ PostgreSQL data is **deleted** (volume removed) |
| `docker-compose up --build` | 🔄 Rebuilds images, keeps existing data |

### Useful Docker Commands
```bash
docker-compose up --build -d    # Run in background (detached)
docker-compose logs -f api      # Follow API logs
docker-compose logs -f processor # Follow Processor logs
docker-compose ps               # Check status of all containers
docker-compose down              # Stop everything (data preserved)
docker-compose down -v           # Stop everything AND delete data
```

### Exposing PostgreSQL / Redis to Host Tools
By default, only the API port (5106) is exposed. To connect with pgAdmin, DBeaver, or Redis Insight, uncomment the `ports` section in `docker-compose.yml` for the respective service.

---

## Quick Start (Manual)

If you prefer running without Docker (e.g., for local development):

### 1. Start Redis (Required)
```bash
docker run -d -p 6379:6379 --name finstream-redis redis
```

### 2. Run the Processor (Terminal 1)
```bash
cd FinStream.Processor
dotnet run
```

### 3. Run the API (Terminal 2)
```bash
cd FinStream.API
dotnet run
```

### 4. Open Swagger UI
👉 **http://localhost:5106/swagger**

---

## What Is FinStream?

FinStream is a **real-time financial market data analytics engine** that:

1. **Generates fake market data** (simulates a live stock feed)
2. **Calculates trading indicators** (Moving Averages, Volatility)
3. **Evaluates trading rules** (e.g., "Alert me when price drops 5%")
4. **Stores historical data** for analysis
5. **Streams real-time updates** via WebSocket with horizontal scaling

**Use Cases:**
- Trading bot monitoring dashboards
- Real-time stock price trackers
- Automated alert systems for financial instruments
- Educational tool for learning CQRS and Clean Architecture

---

## Architecture Overview

FinStream uses **CQRS (Command Query Responsibility Segregation)** - a pattern that separates the "write" side (data generation) from the "read" side (data serving).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              FINSTREAM ARCHITECTURE                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                     FINSTREAM.PROCESSOR (Write Side)                 │   │
│   │                                                                      │   │
│   │   ┌──────────────┐    Channel<Tick>    ┌──────────────────────┐     │   │
│   │   │ MarketFeed   │ ─────────────────► │ MetricsProcessor     │     │   │
│   │   │ Service      │    (thread-safe)    │ Service              │     │   │
│   │   │ (generates   │                    │ - Calculate SMA/EMA  │     │   │
│   │   │  fake ticks) │                    │ - Evaluate rules     │     │   │
│   │   └──────────────┘                    │ - Generate signals   │     │   │
│   │                                         └──────────┬───────────┘     │   │
│   │                                                    │                │   │
│   │                            ┌──────────────────────┼──────────────┐  │   │
│   │                            ▼                      ▼              │  │   │
│   │   ┌──────────────┐   ┌───────────┐   ┌──────────────────┐       │  │   │
│   │   │    Redis     │   │  Batch    │   │   SQL Database   │       │  │   │
│   │   │  (cache +    │   │  Writer   │   │  (persistent     │       │  │   │
│   │   │  pub/sub)    │   │  Service  │   │   storage)       │       │  │   │
│   │   └──────────────┘   └───────────┘   └──────────────────┘       │  │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│                          Redis Pub/Sub│Broadcast                             │
│                                    ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                       FINSTREAM.API (Read Side)                      │   │
│   │                                                                      │   │
│   │   ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐     │   │
│   │   │   REST API   │    │  WebSocket   │    │   Redis          │     │   │
│   │   │  Controllers │    │  Manager     │◄───│   Backplane       │     │   │
│   │   │  (read from  │    │  (broadcast  │    │  (receives pub/   │     │   │
│   │   │   cache + DB)│    │   to clients)│    │   sub messages)  │     │   │
│   │   └──────────────┘    └──────────────┘    └──────────────────┘     │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Why Split Into Two Applications?

| Aspect | Combined App | Split (CQRS) |
|--------|--------------|--------------|
| **Performance** | Slower (blocking) | Faster (parallel processing) |
| **Scaling** | Single point | Scale read/write independently |
| **Complexity** | Simple | More components |
| **Reliability** | Single failure point | Partial failures don't block |

---

## Project Structure

```
FinStream/
│
├── Dockerfile                           # Multi-stage build (API + Processor)
├── docker-compose.yml                   # One-command setup for all services
├── .dockerignore                        # Excludes bin/obj from Docker builds
│
├── FinStream.Domain/                    # CORE BUSINESS LOGIC (no dependencies)
│   ├── Entities/                       # Data models
│   │   ├── Instrument.cs               # Stock/ETF entity (Symbol, Name, IsActive)
│   │   ├── MetricSnapshot.cs           # Calculated indicators at a point in time
│   │   ├── SignalEvent.cs              # Alert when a trading rule triggers
│   │   └── SignalRule.cs               # Rule definition (e.g., "Price > 5%")
│   │
│   ├── Interfaces/                     # Contracts (what the data layer must implement)
│   │   ├── IInstrumentRepository.cs    # How to access instruments
│   │   ├── IMetricRepository.cs        # How to access metrics
│   │   ├── ISignalRepository.cs        # How to access signals
│   │   └── IRuleRepository.cs          # How to access rules
│   │
│   └── ValueObjects/                   # Immutable small objects with validation
│       ├── Price.cs                    # Decimal with validation
│       ├── Symbol.cs                   # Ticker symbol wrapper
│       └── Tick.cs                     # Price tick with timestamp
│
├── FinStream.Application/              # APPLICATION LAYER (depends only on Domain)
│   ├── DTOs/                           # Data Transfer Objects (API <-> Internal)
│   │   ├── InstrumentDto.cs            # Instrument for API responses
│   │   ├── MetricDto.cs                # Metrics for API responses
│   │   ├── SignalDto.cs                # Signals for API responses
│   │   └── RuleDto.cs                  # Rules for API responses
│   │
│   ├── Services/                       # Business logic services
│   │   ├── RuleEngine.cs               # Evaluates trading rules against metrics
│   │   ├── MetricsCalculator.cs        # Calculates SMA, EMA, Volatility
│   │   └── SignalEvaluator.cs          # Runs rules and creates signal events
│   │
│   └── Mappers/                        # Converts between Entity and DTO
│       ├── InstrumentMapper.cs        # Instrument ↔ InstrumentDto
│       ├── MetricMapper.cs            # MetricSnapshot ↔ MetricDto
│       ├── SignalMapper.cs            # SignalEvent ↔ SignalDto
│       └── RuleMapper.cs              # SignalRule ↔ RuleDto
│
├── FinStream.Infrastructure/           # INFRASTRUCTURE LAYER (depends on Domain + Application)
│   ├── Data/
│   │   └── AppDbContext.cs            # Entity Framework Core database context
│   │
│   ├── Repositories/                   # Data access implementations
│   │   ├── InstrumentRepository.cs    # Implements IInstrumentRepository
│   │   ├── MetricRepository.cs        # Implements IMetricRepository
│   │   ├── SignalRepository.cs        # Implements ISignalRepository
│   │   └── RuleRepository.cs          # Implements IRuleRepository
│   │
│   ├── Pipeline/                       # Background services (thread-safe data flow)
│   │   ├── MarketFeedService.cs        # Generates fake price ticks (Tick → Channel)
│   │   ├── MetricsProcessorService.cs  # Processes ticks → calculates metrics → checks rules
│   │   ├── BatchDbWriterService.cs    # Batches writes to database (1000 items or 1 sec)
│   │   ├── TickChannel.cs             # ITickChannel implementation (Channel<Tick>)
│   │   └── BatchChannel.cs            # IBatchChannel implementation (Channel<BatchItem>)
│   │
│   └── WebSockets/
│       ├── WebSocketManager.cs        # Manages browser connections, broadcasts messages
│       └── RedisWebSocketBackplane.cs # Bridges Redis Pub/Sub to local WebSocket clients
│
├── FinStream.API/                      # WEB API LAYER (REST + WebSocket)
│   ├── Controllers/                    # HTTP request handlers
│   │   ├── InstrumentsController.cs   # GET/POST/DELETE /api/instruments
│   │   ├── MetricsController.cs       # GET /api/instruments/{symbol}/metrics
│   │   ├── SignalsController.cs       # GET /api/signals
│   │   └── RulesController.cs        # GET/POST/DELETE /api/rules
│   │
│   ├── Program.cs                      # DI configuration, middleware, startup
│   └── appsettings.json               # Configuration (database, Redis connection strings)
│
├── FinStream.Processor/                # BACKGROUND WORKER (runs MarketFeed + Processing)
│   ├── Program.cs                      # DI configuration for background services
│   └── Worker.cs                       # Template for background services (reference only)
│
├── FinStream.Tests/                   # UNIT TESTS
│   ├── RuleEngineTests.cs            # Tests for rule evaluation logic
│   ├── MetricsCalculatorTests.cs     # Tests for indicator calculations
│   ├── SignalEvaluatorTests.cs       # Tests for signal generation
│   └── ControllersTests.cs          # Tests for API controllers
│
├── FinStream.IntegrationTests/        # INTEGRATION TESTS
│   └── MultiNodeWebSocketBroadcastTest.cs  # Tests horizontal scaling via Redis
│
└── README.md                          # This file!
```

---

## Technology Stack

| Component | Technology | Why |
|-----------|------------|-----|
| **Runtime** | .NET 8 | Latest LTS, high performance |
| **Web Framework** | ASP.NET Core | Fast, mature, WebSocket support |
| **Database** | EF Core + InMemory/SQL Server/PostgreSQL | Swappable providers |
| **Cache/PubSub** | Redis (StackExchange.Redis) | Fast caching + horizontal scaling |
| **Testing** | xUnit + Testcontainers | Unit + integration tests |
| **API Docs** | Swagger/OpenAPI | Interactive documentation |
| **Architecture** | Clean Architecture + CQRS | Separation of concerns |

---

## API Endpoints

### REST Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/instruments` | List all tracked stocks |
| `POST` | `/api/instruments` | Add a new stock (e.g., `{ "symbol": "TSLA" }`) |
| `GET` | `/api/instruments/{symbol}` | Get details for a specific stock |
| `DELETE` | `/api/instruments/{symbol}` | Stop tracking a stock |
| `GET` | `/api/instruments/{symbol}/metrics` | Get latest calculated indicators (from Redis) |
| `GET` | `/api/instruments/{symbol}/metrics/history` | Get historical metrics (from DB) |
| `GET` | `/api/rules` | List all trading rules |
| `POST` | `/api/rules` | Create a new rule (e.g., `{ "name": "SPIKE", "conditionType": "PRICECHANGE_PCT_GT", "threshold": "5" }`) |
| `DELETE` | `/api/rules/{id}` | Delete a rule |
| `GET` | `/api/signals` | List all triggered signals |
| `GET` | `/api/signals/{symbol}` | Get signals for a specific stock |
| `GET` | `/health` | Health check endpoint |

### WebSocket Endpoint

| Endpoint | Description |
|----------|-------------|
| `ws://localhost:5106/ws/stream` | Subscribe to all live market updates |
| `ws://localhost:5106/ws/stream?symbol=AAPL` | Subscribe to specific stock only |

**WebSocket Message Format:**
```json
{
  "symbol": "AAPL",
  "timestamp": "2026-05-21T10:30:00Z",
  "price": 213.45,
  "sma": 212.10,
  "ema": 213.01,
  "volatility": 2.5,
  "signals": ["SPIKE"]
}
```

---

## WebSocket Integration

The WebSocket system supports **horizontal scaling** via Redis Pub/Sub:

```
Browser 1 ─┐
Browser 2 ─┼─► API Server 1 ─┐
Browser 3 ─┘                 │
                              ▼
                         Redis Channel
                              │
Browser 4 ─┐                 │
Browser 5 ─┼─► API Server 2 ─┘
Browser 6 ─┘
```

**How it works:**
1. `MetricsProcessorService` publishes messages to Redis channel `finstream:ws:broadcast`
2. All API servers subscribe to this channel
3. Each server's `RedisWebSocketBackplane` forwards messages to local connected browsers
4. Users on ANY server receive the same real-time updates

---

## How to Test

### Unit Tests (Fast, no external dependencies)
```bash
dotnet test FinStream.Tests
# Result: 33 tests passing
```

### Integration Tests (Requires Docker for Redis)
```bash
dotnet test FinStream.IntegrationTests
# Result: 2 tests passing, 1 skipped (WebSocket TestServer limitation)
```

### Manual Testing with Swagger

1. Start Redis: `docker run -d -p 6379:6379 --name finstream-redis redis`
2. Run Processor: `dotnet run --project FinStream.Processor`
3. Run API: `dotnet run --project FinStream.API`
4. Open: **http://localhost:5106/swagger**

Try:
- Click "GET /api/instruments" → "Try it out" → "Execute"
- Create a new rule with different threshold values
- Watch the `/api/signals` endpoint fill up as rules trigger

### WebSocket Browser Test

Open this HTML in a browser to see real-time data:

```html
<!DOCTYPE html>
<html>
<head><title>FinStream WebSocket Test</title></head>
<body>
  <h1>FinStream Real-Time Data</h1>
  <pre id="output"></pre>
  <script>
    const ws = new WebSocket('ws://localhost:5106/ws/stream');
    ws.onmessage = (event) => {
      document.getElementById('output').innerText = JSON.stringify(JSON.parse(event.data), null, 2);
    };
  </script>
</body>
</html>
```

---

## Development

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                         API Layer                            │
│           (Controllers, HTTP, Dependency Injection)         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Application Layer                        │
│            (Services, DTOs, Mappers, Business Logic)         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│              (Entities, Interfaces, Value Objects)          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                       │
│         (EF Core, Repositories, Redis, Channels)              │
└─────────────────────────────────────────────────────────────┘
```

**Dependency Rule:** Each layer only depends on the layer directly below it.

### Key Design Decisions

1. **Channel<T> over ConcurrentQueue + Timer**
   - Prevents the Queue/Timer bug from Test B requirements
   - Native async/await support
   - Built-in backpressure (bounded channels)

2. **Rule Engine Pattern**
   - Mirrors Test A's `AddRule(divisor, label)` approach
   - Dynamic rule loading from database
   - Real-time rule reload via Redis Pub/Sub

3. **Batch Writes**
   - 1000 items OR 1 second interval
   - Bulk inserts are much faster than individual writes
   - Prevents database locks under high load

4. **Redis for CQRS**
   - Write-through cache for latest metrics
   - Pub/Sub for horizontal WebSocket scaling
   - Swappable providers (InMemory → SQL Server → PostgreSQL)

---

## Troubleshooting

### "Redis connection refused"
```bash
# Start Redis with Docker
docker run -d -p 6379:6379 --name finstream-redis redis

# Or check if container is running
docker ps | grep redis
```

### "Port already in use"
```bash
# Check what's using port 5106
netstat -ano | findstr 5106

# Kill the process or use a different port in appsettings.json
```

### "Tests failing"
```bash
# Make sure Docker is running (for integration tests)
docker ps

# Run just the unit tests
dotnet test FinStream.Tests
```

---

## Further Reading

- **CQRS Pattern**: https://martinfowler.com/articles/cqrs.html
- **Clean Architecture**: https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html
- **Channel<T>**: https://docs.microsoft.com/en-us/dotnet/architecture/porting-existing-applications/understanding-high-performance
- **Redis Pub/Sub**: https://redis.io/topics/pubsub

---
