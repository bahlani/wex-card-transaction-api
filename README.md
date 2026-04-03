# Card Transaction API

A C# / ASP.NET Core 8 Web API for managing credit cards and purchase transactions with currency conversion via the [Treasury Reporting Rates of Exchange API](https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/treasury-reporting-rates-of-exchange).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (optional — only needed if you want to run via container)

## Quick Start

### Option 1: Run with .NET SDK (recommended)

```bash
# Restore dependencies
dotnet restore

# Run the API
dotnet run --project src/CardTransactionApi

# Open http://localhost:5198/swagger to explore the API
```

### Option 2: Run with Docker

```bash
docker compose up --build

# Open http://localhost:8080/swagger to explore the API
```

## Run Tests

```bash
dotnet test
```

Tests include:
- **Integration tests** — full HTTP request/response tests using `WebApplicationFactory` with an in-memory database
- **Unit tests** — isolated tests for the exchange rate service using fake HTTP handlers

## API Endpoints

### 1. Create a Card

```
POST /api/cards
Content-Type: application/json

{ "creditLimit": 5000.00 }
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4-...",
  "creditLimit": 5000.00
}
```

### 2. Store a Purchase Transaction

```
POST /api/cards/{cardId}/transactions
Content-Type: application/json

{
  "description": "Grocery Store",
  "transactionDate": "2024-06-15",
  "amount": 42.50
}
```

**Response (201 Created):**
```json
{
  "id": "e5f6g7h8-...",
  "cardId": "a1b2c3d4-...",
  "description": "Grocery Store",
  "transactionDate": "2024-06-15T00:00:00",
  "amount": 42.50
}
```

### 3. Retrieve a Transaction (with optional currency conversion)

```
GET /api/transactions/{id}?currency=Canada-Dollar
```

**Response (200 OK):**
```json
{
  "id": "e5f6g7h8-...",
  "description": "Grocery Store",
  "transactionDate": "2024-06-15T00:00:00",
  "originalAmount": 42.50,
  "exchangeRate": 1.360,
  "convertedAmount": 57.80,
  "currency": "Canada-Dollar"
}
```

Currency conversion uses the exchange rate on or before the transaction date, within the prior 6 months. Returns an error if no rate is available.

### 4. Retrieve Card Balance (with optional currency conversion)

```
GET /api/cards/{cardId}/balance?currency=Canada-Dollar
```

**Response (200 OK):**
```json
{
  "cardId": "a1b2c3d4-...",
  "creditLimit": 5000.00,
  "totalSpent": 42.50,
  "availableBalance": 4957.50,
  "currency": "Canada-Dollar",
  "exchangeRate": 1.360,
  "convertedCreditLimit": 6800.00,
  "convertedTotalSpent": 57.80,
  "convertedAvailableBalance": 6742.20
}
```

Uses the latest available exchange rate from the Treasury API.

### 5. List Available Currencies

```
GET /api/currencies
```

**Response (200 OK):**
```json
[
  "Canada-Dollar",
  "Euro Zone-Euro",
  "Japan-Yen",
  "Mexico-Peso",
  "United Kingdom-Pound"
]
```

Returns the list of valid `currency` values you can use with the other endpoints. Data is sourced live from the Treasury API.

## Error Handling

All error responses follow a consistent format:

```json
{
  "errorCode": "ERROR_CODE",
  "error": "Human-readable error message."
}
```

| Error Code | HTTP Status | Description |
|---|---|---|
| `CARD_NOT_FOUND` | 404 | Card ID does not exist |
| `TRANSACTION_NOT_FOUND` | 404 | Transaction ID does not exist |
| `INSUFFICIENT_BALANCE` | 400 | Transaction amount exceeds available credit |
| `EXCHANGE_RATE_NOT_FOUND` | 400 | No exchange rate available for the requested currency/date |
| `CURRENCY_CONVERSION_UNAVAILABLE` | 502 | External exchange rate service is temporarily unavailable |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

Model validation errors (e.g., missing required fields, invalid amounts) return a standard 400 response with field-level details.

## Architecture

```
src/CardTransactionApi/
├── Controllers/          # API endpoints
│   ├── CardsController.cs
│   ├── CurrenciesController.cs
│   └── TransactionsController.cs
├── Data/
│   └── AppDbContext.cs   # EF Core database context
├── Dtos/                 # Request/response models
├── Models/               # Domain entities (Card, Transaction)
├── Services/
│   ├── IExchangeRateService.cs
│   └── ExchangeRateService.cs  # Treasury API client
└── Program.cs            # App startup and DI configuration

tests/CardTransactionApi.Tests/
├── Integration/          # End-to-end API tests
└── Unit/                 # Service-level unit tests
```

## Design Decisions & Trade-offs

- **SQLite over PostgreSQL/SQL Server** — chose a zero-install, file-based database so the project runs immediately with no external dependencies. The database auto-creates on first run, keeping the setup to a single `dotnet run`. One trade-off: SQLite's EF Core provider doesn't support `SUM` on `decimal` columns, so balance calculations fetch the amounts column and sum client-side with full decimal precision. At scale, I'd switch to PostgreSQL or SQL Server for better concurrency, native decimal aggregation, and production support.

- **Single-project layered architecture over full Clean Architecture** — the domain is small (2 entities, 4 endpoints), so splitting into separate Domain, Application, Infrastructure, and API projects would add overhead without meaningful benefit. Controllers, services, models, and DTOs are still separated by folder. For a larger codebase, I'd move to a multi-project Clean Architecture or Vertical Slice approach to enforce dependency rules at compile time.

- **Direct DbContext usage over Repository pattern** — EF Core's `DbContext` already implements Unit of Work, and `DbSet<T>` acts as a repository. Adding another abstraction layer on top would be redundant at this scale. The trade-off is that controllers are harder to unit test in isolation, which I mitigate by using integration tests via `WebApplicationFactory`. In a larger project, a thin repository layer would help with testability and decoupling.

- **Typed HttpClient (IHttpClientFactory) over raw HttpClient** — `AddHttpClient<IExchangeRateService, ExchangeRateService>()` gives proper connection pooling, DNS rotation, and lifetime management out of the box. A raw `new HttpClient()` risks socket exhaustion. Alternatives like Refit would reduce boilerplate but add a dependency for a single external call. I'd also add Polly resilience policies (retry, circuit breaker) for production use.

- **Integration tests with WebApplicationFactory over unit tests alone** — integration tests exercise the full ASP.NET Core pipeline (routing, model binding, validation, serialization), catching bugs that isolated unit tests miss. The in-memory database keeps tests fast, and mocking `IExchangeRateService` avoids flaky external API calls. A real-world project would add contract tests and use Testcontainers for database-level testing.

- **No authentication** — intentionally omitted to keep the scope focused on the core domain logic. In production, I'd add JWT bearer authentication and request validation filters. A global exception handler is included to prevent stack trace leakage and ensure consistent error responses.

- **Credit limit enforcement on transactions** — the spec defines available balance as credit limit minus transactions but doesn't explicitly prevent overspending. I chose to reject transactions that exceed the available balance as a sensible credit card constraint. This is a design decision, not a spec requirement.

- **No date restrictions on transactions** — the spec doesn't constrain transaction dates, so any valid date is accepted. The 6-month exchange rate lookback window in Requirement #3 naturally handles old transactions — if no rate is available, the conversion returns an error while the transaction itself is still stored.

## Currency Names

The `currency` query parameter accepts the `country_currency_desc` value from the Treasury API, in `Country-Currency` format. Call `GET /api/currencies` to see the full list of supported values. Common examples:

- `Canada-Dollar`
- `United Kingdom-Pound`
- `Japan-Yen`
- `Euro Zone-Euro`
- `Mexico-Peso`
