# Architecture Documentation

## Table of Contents
- [Overview](#overview)
- [Architecture Patterns](#architecture-patterns)
- [Component Design](#component-design)
- [Data Flow](#data-flow)
- [Design Decisions](#design-decisions)
- [Security Considerations](#security-considerations)
- [Scalability & Performance](#scalability--performance)

## Overview

The Payment Gateway API follows a layered architecture with clear separation of concerns, implementing industry-standard patterns for maintainability, testability, and extensibility.

### High-Level Architecture

```
┌─────────────────┐
│   API Client    │
└────────┬────────┘
         │ HTTP/HTTPS
         ▼
┌─────────────────┐
│  Controllers    │  ← API Layer (HTTP endpoints)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Services      │  ← Business Logic Layer
│  (BankClient)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Repositories   │  ← Data Access Layer
│ (PaymentsRepo)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Data Store    │  ← In-Memory Storage
│  (List<T>)      │
└─────────────────┘
```

## Architecture Patterns

### 1. Layered Architecture

The application is organized into distinct layers with well-defined responsibilities:

#### **API Layer** (`Controllers/`)
- **Responsibility**: HTTP request/response handling, routing
- **Components**: `PaymentsController`
- **Concerns**: Input validation, HTTP status codes, response formatting
- **Pattern**: RESTful API design

```csharp
[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    // Handles HTTP concerns only
    // Delegates business logic to services
}
```

#### **Business Logic Layer** (`Services/`)
- **Responsibility**: Business rules, external integrations
- **Components**: `BankClient`, `IBankClient`
- **Concerns**: Bank communication, error handling, retry logic

```csharp
public class BankClient : IBankClient
{
    // Handles bank API communication
    // Implements error handling and logging
}
```

#### **Data Access Layer** (`Repositories/`)
- **Responsibility**: Data persistence and retrieval
- **Components**: `PaymentsRepository`
- **Concerns**: CRUD operations, data consistency

```csharp
public class PaymentsRepository
{
    // Abstracts storage mechanism
    // Provides thread-safe operations
}
```

### 2. Repository Pattern

The Repository Pattern abstracts data access logic:

**Benefits**:
- Separation of concerns
- Easy to swap storage implementations
- Simplified testing (mock repository)
- Centralized data access logic

**Implementation**:
```csharp
public class PaymentsRepository
{
    public readonly List<PostPaymentResponse> Payments = [];

    public void Add(PostPaymentResponse payment) { }
    public PostPaymentResponse? Get(Guid id) { }
}
```

### 3. Dependency Injection (DI)

All dependencies are injected through constructors, following the Dependency Inversion Principle:

**Configuration** (`Program.cs`):
```csharp
// Register dependencies
builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri(config["BankApiUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

**Benefits**:
- Loose coupling
- Easy testing (inject mocks)
- Lifecycle management
- Configuration flexibility

### 4. Data Transfer Objects (DTO)

Separate models for different layers prevent over-exposure and provide flexibility:

```
┌──────────────────────┐
│  PostPaymentRequest  │  ← API Input (with validation)
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ BankPaymentRequest   │  ← Bank API format
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ BankPaymentResponse  │  ← Bank API response
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ PostPaymentResponse  │  ← Stored & returned to client
└──────────────────────┘
```

## Component Design

### Controllers

**PaymentsController**
```csharp
[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;

    // POST /api/payments - Process payment
    // GET /api/payments/{id} - Retrieve payment
}
```

**Design Principles**:
- Single Responsibility: Each endpoint handles one operation
- Thin Controllers: Logic delegated to services
- Proper HTTP semantics: Correct status codes and responses

### Services

**BankClient**
```csharp
public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankClient> _logger;

    public async Task<BankPaymentResponse?> ProcessPaymentAsync(
        BankPaymentRequest request)
    {
        // HTTP communication
        // Error handling
        // Logging
    }
}
```

**Design Principles**:
- Interface segregation: `IBankClient` for testability
- Single responsibility: Only handles bank communication
- Robust error handling: Returns null on failures
- Comprehensive logging: All operations logged

### Repositories

**PaymentsRepository**
```csharp
public class PaymentsRepository
{
    public readonly List<PostPaymentResponse> Payments = [];

    public void Add(PostPaymentResponse payment)
    {
        Payments.Add(payment);
    }

    public PostPaymentResponse? Get(Guid id)
    {
        return Payments.FirstOrDefault(p => p.Id == id);
    }

    public PostPaymentResponse? GetByIdempotencyKey(string idempotencyKey)
    {
        return Payments.FirstOrDefault(p => p.IdempotencyKey == idempotencyKey);
    }
}
```

**Design Principles**:
- Simple in-memory storage for demonstration
- Easily replaceable with database implementation
- Thread-safe operations (List<T> is thread-safe for reads)
- Returns null for not found (explicit handling)
- **Idempotency support**: Lookup by idempotency key for duplicate detection

### Validation

**Custom Validation Attribute**
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class FutureExpiryDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        // Uses reflection to access multiple properties
        // Validates expiry date is in the future
    }
}
```

**Built-in Validation**
```csharp
[FutureExpiryDate]
public class PostPaymentRequest
{
    [Required]
    [RegularExpression(@"^\d{14,19}$")]
    public string CardNumber { get; set; }

    [Range(1, 12)]
    public int ExpiryMonth { get; set; }

    // ... other properties with validation
}
```

**Design Principles**:
- Declarative validation: Easy to read and maintain
- Fail fast: Validation before business logic
- Clear error messages: User-friendly feedback
- Multi-layer validation: Request + custom attributes

### Helpers

**CardNumberExtensions**
```csharp
public static class CardNumberExtensions
{
    public static string ExtractLastFourDigits(this string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 4)
            return string.Empty;

        var lastFour = cardNumber[^4..];
        return lastFour.All(char.IsDigit) ? lastFour : string.Empty;
    }
}
```

**Design Principles**:
- Extension methods for clean syntax
- Defensive programming: Null checks
- Single responsibility: One specific task
- Reusable: Used across multiple components

## Data Flow

### Payment Processing Flow

```
1. Client sends POST /api/payments
   - Optional: Idempotency-Key header
        ↓
2. Controller: Check idempotency key
   - If key provided → PaymentsRepository.GetByIdempotencyKey()
   - If existing payment found → Return cached response (SKIP rest)
        ↓
3. Model Binding & Validation
   - Data Annotations validated
   - FutureExpiryDate validated
        ↓
4a. If Invalid → Controller
   - Create rejected payment with idempotency key
   - Store in repository
   - Return 400 BadRequest

4b. If Valid → Controller
        ↓
5. Transform to BankPaymentRequest
        ↓
6. BankClient.ProcessPaymentAsync()
   - HTTP call to bank API
   - Error handling
   - Return BankPaymentResponse or null
        ↓
7a. If bank returns null → Controller
   - Return 503 Service Unavailable

7b. If bank succeeds → Controller
   - Map to PostPaymentResponse
   - Set status (Authorized/Declined)
   - Extract last 4 card digits
   - Generate new GUID
   - Set idempotency key if provided
        ↓
8. PaymentsRepository.Add()
   - Store payment with idempotency key
        ↓
9. Return 200 OK with payment details
```

### Payment Retrieval Flow

```
1. Client sends GET /api/payments/{id}
        ↓
2. Controller validates GUID format
        ↓
3. PaymentsRepository.Get(id)
        ↓
4a. If not found → Controller
   - Return 404 Not Found

4b. If found → Controller
   - Map to GetPaymentResponse
   - Return 200 OK
```

## Design Decisions

### 1. In-Memory Storage

**Decision**: Use `List<PostPaymentResponse>` for storage

**Rationale**:
- Simple demonstration
- No external dependencies
- Fast for testing
- Easy to swap with real database

**Trade-offs**:
- Data lost on restart
- Not scalable
- No concurrent access protection

**Production Alternative**: Replace with Entity Framework Core + SQL Server/PostgreSQL

### 2. Disabled Automatic Model Validation

**Decision**: Set `SuppressModelStateInvalidFilter = true`

**Rationale**:
- Allows custom handling of validation failures
- Enables storing rejected payments for audit
- Provides better error responses

**Implementation**:
```csharp
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });
```

### 3. Card Number Storage

**Decision**: Store only last 4 digits as string

**Rationale**:
- PCI DSS compliance consideration
- Reduces security risk
- Sufficient for user identification
- Cannot be used for fraudulent transactions

**Implementation**:
```csharp
CardNumberLastFour = request.CardNumber.ExtractLastFourDigits()
```

### 4. Three Payment Statuses

**Decision**: Authorized, Declined, Rejected

**Rationale**:
- **Authorized**: Bank approved - clear success state
- **Declined**: Bank rejected - legitimate attempt, bank decision
- **Rejected**: Validation failed - audit trail for analysis

**Benefits**:
- Analytics on validation failures
- Fraud detection patterns
- User experience improvements

### 5. Nullable Return Types

**Decision**: Use nullable references (`PostPaymentResponse?`)

**Rationale**:
- Explicit handling of not-found cases
- Compiler warnings for null safety
- Better than throwing exceptions
- Clear intent in API

### 6. HttpClient through DI

**Decision**: Register `HttpClient` via `AddHttpClient<T>`

**Rationale**:
- Prevents socket exhaustion
- Proper disposal management
- Connection pooling
- Easy to configure timeout/base URL

### 7. Synchronous Repository Methods

**Decision**: Repository methods are synchronous

**Rationale**:
- In-memory operations are instant
- No I/O blocking
- Simpler code
- Easy to make async when adding database

### 8. Idempotency Support

**Decision**: Implement idempotency via optional `Idempotency-Key` header

**Rationale**:
- **Prevents Duplicate Payments**: Network retries won't charge customers multiple times
- **Industry Standard**: Follows patterns used by Stripe, PayPal, and other payment providers
- **Safe Retries**: Clients can safely retry failed requests
- **Backward Compatible**: Optional header doesn't break existing integrations

**Implementation Details**:

```csharp
public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
    [FromBody] PostPaymentRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
{
    // Check for existing payment with same idempotency key
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var existingPayment = _paymentsRepository.GetByIdempotencyKey(idempotencyKey);
        if (existingPayment != null)
        {
            return Ok(existingPayment); // Return cached response
        }
    }

    // Process payment normally and store with idempotency key
    // ...
}
```

**Key Design Choices**:

1. **Optional Header**: No breaking changes for existing clients
2. **Pre-Validation Check**: Idempotency checked before validation to return exact previous response
3. **All Statuses Supported**: Works for Authorized, Declined, and Rejected payments
4. **String Key**: Flexible - accepts UUIDs, client-generated keys, or any unique string
5. **Repository Lookup**: Single source of truth for idempotency key storage

**Benefits**:
- Zero duplicate charges from network issues
- Client-side retry safety
- Audit trail maintained (idempotency key stored with payment)
- No additional infrastructure needed (uses existing repository)

**Trade-offs**:
- In-memory implementation: Keys lost on restart (production would use persistent storage)
- No key expiration: Keys stored indefinitely (production would implement TTL)
- No conflict detection: Same key with different payment data returns original (by design)

**Production Considerations**:
- Add database index on `IdempotencyKey` for performance
- Implement key expiration (e.g., 24 hours)
- Consider distributed locking for high-concurrency scenarios
- Add key conflict validation (same key, different data) with 409 Conflict response

## Security Considerations

### Input Validation

1. **Multi-Layer Validation**
   - Data Annotations (Required, Range, RegularExpression)
   - Custom Attributes (FutureExpiryDate)
   - Controller-level validation

2. **Injection Prevention**
   - Parameterized queries (when database added)
   - No dynamic SQL
   - Input sanitization

### Data Protection

1. **Card Number Masking**
   - Only last 4 digits stored
   - Original number never persisted
   - Logged with masking

2. **No Sensitive Data in Logs**
   - CVV never logged
   - Full card numbers never logged
   - Error messages sanitized

### Communication Security

1. **HTTPS Enforcement**
   - Redirect HTTP to HTTPS
   - Secure bank communication

2. **Request Validation**
   - Model binding validation
   - Anti-forgery tokens (for browser clients)

## Scalability & Performance

### Current Limitations

1. **In-Memory Storage**
   - Limited to single instance
   - No data persistence
   - Memory constraints

2. **No Caching**
   - Every GET reads from repository
   - Could benefit from distributed cache

3. **No Rate Limiting**
   - Vulnerable to abuse
   - Could overwhelm bank API

### Production Enhancements

1. **Database Storage**
   ```csharp
   // Replace in-memory with EF Core
   services.AddDbContext<PaymentDbContext>(options =>
       options.UseSqlServer(connectionString));
   ```

2. **Distributed Caching**
   ```csharp
   services.AddStackExchangeRedisCache(options =>
   {
       options.Configuration = redisConnection;
   });
   ```

3. **Rate Limiting**
   ```csharp
   services.AddRateLimiter(options =>
   {
       options.AddFixedWindowLimiter("fixed", opt =>
       {
           opt.Window = TimeSpan.FromMinutes(1);
           opt.PermitLimit = 100;
       });
   });
   ```

4. **Horizontal Scaling**
   - Stateless design allows multiple instances
   - Load balancer distribution
   - Shared database/cache

5. **Asynchronous Processing**
   - Queue payment requests
   - Background workers
   - Event-driven architecture

## Future Considerations

### Authentication & Authorization

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { });

[Authorize(Roles = "Merchant")]
public class PaymentsController : Controller
```

### Event Sourcing

```csharp
public class PaymentEvent
{
    public Guid PaymentId { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string Data { get; set; }
}
```

### Circuit Breaker Pattern

```csharp
services.AddHttpClient<IBankClient, BankClient>()
    .AddPolicyHandler(GetCircuitBreakerPolicy());
```

### Idempotency

```csharp
[HttpPost]
public async Task<ActionResult> PostPayment(
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
    [FromBody] PostPaymentRequest request)
```

---

**Last Updated**: 2025-01-08
