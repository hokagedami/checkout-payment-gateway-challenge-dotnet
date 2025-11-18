# Payment Gateway Challenge - .NET Implementation

A payment gateway API built with ASP.NET Core 8.0, implementing secure payment processing with comprehensive validation, audit trails, and database persistence.

## Overview

This Payment Gateway API allows merchants to process payments by integrating with a banking partner. It provides:

- **Payment Processing**: Submit payment requests and receive authorization/decline responses
- **Payment Retrieval**: Query historical payment information
- **Idempotency**: Prevent duplicate payments using idempotency keys
- **Comprehensive Validation**: Multi-layer validation with detailed error messages
- **Audit Trail**: All payment attempts (authorized, declined, and rejected) are stored
- **Card Security**: Card numbers are masked, storing only the last 4 digits

## Features

### Core Functionality
- Process payment requests with full validation
- Retrieve payment details by ID
- Idempotency support via Idempotency-Key header
- Support for multiple currencies (USD, GBP, EUR)
- Support for 14-19 digit card numbers
- 3 or 4 digit CVV support

### Payment Status
- **Authorized**: Payment approved by the bank
- **Declined**: Payment rejected by the bank
- **Rejected**: Payment failed validation before reaching the bank

### Implemented Enhancements

#### Security & Authentication
- **API Key Authentication**: X-API-Key header validation for all endpoints
- **Card Number Masking**: Only last 4 digits stored (PCI compliance consideration)
- **Input Validation**: Multi-layer validation with detailed error messages
- **No Sensitive Data in Logs**: CVV and full card numbers never logged

#### Data Persistence
- **SQL Server Integration**: Entity Framework Core 9.0 with async/await
- **Database Migrations**: Automatic schema management on startup
- **Repository Pattern**: Interface-based data access with IPaymentsRepository
- **All Payment Statuses Stored**: Authorized, Declined, and Rejected

#### Observability & Error Handling
- **Structured Logging**: Serilog with console, file, and Seq sinks
- **Log Enrichment**: Includes machine name, thread ID, and log context
- **Optional Seq Integration**: Advanced log querying and visualization at http://localhost:5341
- **Global Exception Handler**: Centralized exception handling with RFC 7807 Problem Details
- **Unified API Responses**: Consistent response format across all endpoints with `ApiResponse<T>` wrapper
- **Async/Await Throughout**: Non-blocking operations

#### Clean Architecture
- **Service Layer**: Business logic encapsulated in `PaymentService`, decoupling controllers from repositories
- **Repository Pattern**: Data access abstracted through `IPaymentsRepository` interface
- **Middleware-Based Validation**: ModelState validation handled by middleware for cleaner controllers
- **Separation of Concerns**: Clear separation between HTTP handling (Controller), business logic (Service), and data access (Repository)
- **DRY Principle**: Reusable response wrappers and helper methods throughout
- **Dependency Injection**: All dependencies injected via constructor for testability

### Quality Assurance
- **212 comprehensive tests**
  - 68 unit tests (Services: 17, Repositories: 9, Validation: 7, Helpers: 8, Models: 12, Middleware: 6, Authentication: 10)
  - 128 integration tests (Controllers - feature-focused files, Middleware validation: 7)
  - 16 end-to-end tests (covering core functionality and edge cases)
- NUnit test framework with [SetUp]/[TearDown] lifecycle
- Test-Driven Development (TDD) approach
- Docker-based E2E testing with real services (SQL Server, Bank API)
- **Unit & Integration Pass Rate**: 100% (196/196 tests passing)
- **E2E Pass Rate**: 100% (16/16 tests passing when services running)

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for bank simulator & SQL Server)

### 1. Start the Bank Simulator

```bash
docker-compose up -d
```

This starts the mock bank API on `http://localhost:8080`

### 2. Run the Application

```bash
dotnet run --project src/PaymentGateway.Api
```

The API will be available at:
- **HTTPS**: `https://localhost:7092`
- **HTTP**: `http://localhost:5067`

**Note**: Check console output for actual ports as they may vary based on your environment.

### 3. Run Tests

```bash
# Run all tests (unit + integration, excludes E2E)
dotnet test --filter "Category!=E2E"

# Run E2E tests (requires Docker)
docker-compose -f docker-compose.test.yml up --abort-on-container-exit

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

**Note**: E2E tests require Docker and services to be running. The suite includes 11 tests covering core payment functionality.

### 4. Test the API

You can test the API using Swagger UI or Postman:

#### Swagger UI
Navigate to `https://localhost:7092/swagger` to explore the API interactively.

**Note**: Swagger UI includes API Key authentication support. Use one of these keys:
- `test-api-key-1`
- `test-api-key-2`
- `merchant-demo-key`

#### Postman Collection
A comprehensive Postman collection is included: `PaymentGateway.postman_collection.json`

**Features:**
- 18 pre-configured requests covering all API scenarios
- Automated test assertions validating unified API response format
- Environment variables for seamless testing
- Complete coverage of payment statuses, authentication, validation, and idempotency

**To use:**
1. Import `PaymentGateway.postman_collection.json` into Postman
2. Update the `baseUrl` variable to match your running API port (default: `https://localhost:7092`)
3. Ensure the API is running
4. Run requests individually or use Collection Runner

The collection includes:
- **Process Payment**: Authorized, Declined, Rejected scenarios
- **Retrieve Payment**: Valid and invalid ID tests
- **Authentication**: API key validation tests
- **Validation**: Card number, expiry, currency, CVV validation
- **Idempotency**: With key, retry same key, and without key scenarios
- **Multi-Currency**: EUR currency support test

## API Endpoints

**Authentication**: All endpoints require an `X-API-Key` header with a valid API key.

### POST /api/payments
Process a new payment request

**Headers:**
```
X-API-Key: test-api-key-1
Content-Type: application/json
Idempotency-Key: <optional-unique-key>
```

**Request Body:**
```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "USD",
  "amount": 100,
  "cvv": "123"
}
```

**Response (200 OK - Authorized):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Authorized",
    "cardNumberLastFour": "8877",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "USD",
    "amount": 100,
    "idempotencyKey": null
  },
  "errors": []
}
```

**Response (400 Bad Request - Rejected):**
```json
{
  "success": false,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Rejected",
    "cardNumberLastFour": "",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "USD",
    "amount": 100,
    "idempotencyKey": null
  },
  "errors": [
    "Card number must be between 14-19 digits"
  ]
}
```

### GET /api/payments/{id}
Retrieve a payment by ID

**Headers:**
```
X-API-Key: test-api-key-1
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Authorized",
    "cardNumberLastFour": "8877",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "USD",
    "amount": 100
  },
  "errors": []
}
```

**Response (404 Not Found):**
```json
{
  "success": false,
  "data": null,
  "errors": [
    "Payment not found"
  ]
}
```

## Unified API Response Format

All API endpoints return responses in a consistent format using the `ApiResponse<T>` wrapper:

```json
{
  "success": true/false,
  "data": { /* response data or null */ },
  "errors": [ /* array of error messages */ ]
}
```

**Key Benefits:**
- **Consistent Structure**: Same response shape for all endpoints (success or failure)
- **Easy Error Handling**: Check `success` field for quick status determination
- **Detailed Errors**: Array of human-readable error messages
- **Type Safety**: Generic `data` field contains strongly-typed response objects

**Examples:**

**Success Response:**
```json
{
  "success": true,
  "data": { "id": "...", "status": "Authorized", ... },
  "errors": []
}
```

**Validation Error (400):**
```json
{
  "success": false,
  "data": { "id": "...", "status": "Rejected", ... },
  "errors": ["Card number must be between 14-19 digits"]
}
```

**Not Found (404):**
```json
{
  "success": false,
  "data": null,
  "errors": ["Payment not found"]
}
```

## Idempotency

The API supports idempotent payment processing using the `Idempotency-Key` header:

```bash
curl -X POST https://localhost:7092/api/payments \
  -H "X-API-Key: test-api-key-1" \
  -H "Idempotency-Key: unique-payment-123" \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248877",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "USD",
    "amount": 100,
    "cvv": "123"
  }'
```

**Idempotency Key Behavior:**
- **First Request**: Processes payment and stores the idempotency key
- **Retry with Same Key**: Returns the existing payment response (200 OK) without calling the bank
- **No Idempotency Key**: Each request creates a new payment (non-idempotent behavior)

**Best Practices:**
- Use a unique key per logical payment attempt (e.g., UUID or order ID)
- Store keys on the client side before making the request
- Reuse the same key for all retries of the same logical payment
- Do not reuse keys across different payment attempts
- Omit the header if you want to allow duplicate payments intentionally

## Project Structure

```
src/
  PaymentGateway.Api/
    appsettings.Development.json      # Development configuration
    appsettings.json                  # Configuration
    Authentication/                   # Authentication handlers
      ApiKeyAuthenticationHandler.cs
      ApiKeyAuthenticationSchemeOptions.cs  # Contains ApiKeyAuthenticationDefaults
    Controllers/                      # API endpoints
      PaymentsController.cs
    Data/                            # Database context
      PaymentGatewayDbContext.cs
    Enums/                           # Enumerations
      PaymentStatus.cs
    Helpers/                         # Utility classes
      CardNumberExtensions.cs
    Middleware/                      # Middleware components
      GlobalExceptionHandler.cs      # Global exception handling with RFC 7807 Problem Details
      ModelStateValidationMiddleware.cs  # Validation filter for clean controllers
    Models/                          # Request/Response DTOs
      Bank/                          # Bank integration models
        BankPaymentRequest.cs
        BankPaymentResponse.cs
      Entities/                      # Database entities
        Payment.cs
      Requests/                      # API request models
        PostPaymentRequest.cs
      Responses/                     # API response models
        ApiResponse.cs               # Unified response wrapper
        GetPaymentResponse.cs
        PostPaymentResponse.cs
    PaymentGateway.Api.csproj        # Project file
    Program.cs                       # Application entry point
    Properties/                      # Launch settings
      launchSettings.json
    Repositories/                    # Data access layer
      IPaymentsRepository.cs
      PaymentsRepository.cs
    Services/                        # Business logic & external services
      BankClient.cs                  # Bank API integration
      IBankClient.cs
      PaymentService.cs              # Business logic layer (decouples controller from repository)
      IPaymentService.cs
    Validation/                      # Custom validation attributes
      FutureExpiryDateAttribute.cs

test/
  PaymentGateway.Api.Tests/
    Authentication/                  # Unit tests for authentication (10 tests)
      ApiKeyAuthenticationHandlerTests.cs
    Controllers/                     # Integration tests (128 tests)
      GetPaymentTests.cs             # 5 tests
      IdempotencyTests.cs            # 5 tests
      PaymentsControllerTestBase.cs  # Base class with ApiResponse helper methods
      PaymentValidationTests.cs      # 11 tests
      PostPaymentTests.cs            # 15 tests
      RejectedPaymentTests.cs        # 10 tests
    E2E/                             # End-to-end tests (16 tests)
      PaymentGatewayE2ETests.cs      # 16 tests - covers core functionality and edge cases
    Helpers/                         # Unit tests for helpers (8 tests)
      CardNumberExtensionsTests.cs
    Middleware/                      # Unit tests for middleware (13 tests)
      GlobalExceptionHandlerTests.cs # 6 tests
      ModelStateValidationFilterTests.cs  # 7 tests
    Models/                          # Unit tests for models (12 tests)
      ApiResponseTests.cs            # 10 tests
      PostPaymentRequestValidationTests.cs  # 2 tests
    Repositories/                    # Unit tests for repositories (9 tests)
      PaymentsRepositoryTests.cs
    Services/                        # Unit tests for services (17 tests)
      BankClientTests.cs             # 6 tests
      PaymentServiceTests.cs         # 11 tests - business logic unit tests
    Usings.cs                        # Global using statements
    Validation/                      # Unit tests for validators (7 tests)
      FutureExpiryDateAttributeTests.cs
    PaymentGateway.Api.Tests.csproj  # Test project file

imposters/                           # Bank simulator configuration
  bank_simulator.ejs

# Root directory files
.dockerignore                        # Docker ignore file
docker-compose.test.yml              # Docker setup for E2E tests (with SQL Server)
docker-compose.yml                   # Docker setup for bank simulator
Dockerfile                           # API container image
Dockerfile.test                      # Test runner container image
PaymentGateway.postman_collection.json  # Postman collection for API testing
PaymentGateway.sln                   # Visual Studio solution file
README.md                            # This file - comprehensive project documentation
run-e2e-tests.ps1                    # E2E test runner script (Windows)
run-e2e-tests.sh                     # E2E test runner script (Linux/Mac)
```

## Validation Rules

### Card Number
- **Required**: Yes
- **Format**: 14-19 numeric digits
- **Example**: `2222405343248877`
- **Notes**: Leading zeros are preserved

### Expiry Month
- **Required**: Yes
- **Format**: Integer 1-12
- **Validation**: Must represent a valid calendar month

### Expiry Year
- **Required**: Yes
- **Format**: Integer (4-digit year)
- **Validation**: Must be in the future (combined month/year)
- **Example**: `2026`

### Currency
- **Required**: Yes
- **Allowed Values**: USD, GBP, EUR
- **Format**: 3-letter ISO currency code
- **Case**: Insensitive

### Amount
- **Required**: Yes
- **Format**: Positive integer
- **Unit**: Smallest currency unit (cents, pence, etc.)
- **Example**: `100` = $1.00 USD

### CVV
- **Required**: Yes
- **Format**: 3 or 4 numeric digits
- **Example**: `123`
- **Security**: Never stored or logged

## Environment Variables

The application can be configured using environment variables or `appsettings.json`:

```bash
# Database
ConnectionStrings__DefaultConnection="Server=localhost;Database=PaymentGatewayDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"

# Authentication - API Keys (comma-separated)
Authentication__ApiKeys__0="test-api-key-1"
Authentication__ApiKeys__1="test-api-key-2"
Authentication__ApiKeys__2="merchant-demo-key"

# Bank API
BankApiUrl="http://localhost:8080"

# Logging
Serilog__MinimumLevel__Default="Information"

# Seq Server (Optional - for structured log viewing)
Serilog__WriteTo__2__Args__serverUrl="http://localhost:5341"
```

### Optional: Seq for Log Visualization

The application is configured to send logs to [Seq](https://datalust.co/seq) for structured log visualization and querying.

**To use Seq:**
1. Install Seq locally or run via Docker:
   ```bash
   docker run -d --name seq -e ACCEPT_EULA=Y -p 5341:80 datalust/seq:latest
   ```
2. Access Seq UI at `http://localhost:5341`
3. View structured logs with filtering, querying, and visualization

**Note**: Seq is optional. The application will continue logging to console and file even if Seq is not available.
```

## Technology Stack

### Core Technologies
- **Framework**: .NET 8.0
- **Web Framework**: ASP.NET Core 8.0
- **Language**: C# 12

### Data & Persistence
- **Database**: SQL Server 2022
- **ORM**: Entity Framework Core 9.0
- **Migrations**: EF Core Migrations (auto-applied on startup)

### Security & Authentication
- **Authentication**: Custom API Key authentication handler
- **Authorization**: ASP.NET Core Authorization

### Logging
- **Logging**: Serilog 9.0 (Console + File + Seq sinks)
- **Structured Logging**: All logs include contextual properties
- **Log Visualization**: Optional Seq integration for advanced log querying

### Testing
- **Framework**: NUnit 4.0
- **Mocking**: Moq 4.20
- **E2E Database**: SQL Server (Docker)
- **E2E Services**: Docker Compose (Bank Simulator, SQL Server)

### Infrastructure
- **API Documentation**: Swagger/OpenAPI
- **Bank Simulator**: Mountebank (Docker)
- **Containerization**: Docker + Docker Compose

## Key Design Decisions

### Core Architecture
1. **Layered Architecture**: Clear separation between Controller (HTTP) → Service (Business Logic) → Repository (Data Access)
2. **Service Layer Pattern**: PaymentService encapsulates business logic, decoupling controller from repository
3. **Repository Pattern**: Interface-based data access (IPaymentsRepository) for testability and flexibility
4. **Dependency Injection**: All dependencies injected via constructor for loose coupling and testability
5. **Custom Validation**: Attribute-based validation with FutureExpiryDateAttribute
6. **Card Masking**: Only last 4 digits stored for PCI compliance consideration
7. **Rejected Payment Audit**: Failed validations are stored for analytics
8. **Middleware-Based Validation**: ModelStateValidationFilter handles validation before controller actions

### API Design & Error Handling
9. **Unified Response Format**: ApiResponse<T> wrapper ensures consistent response structure across all endpoints
10. **Global Exception Handler**: Centralized exception handling implementing RFC 7807 Problem Details
11. **Semantic HTTP Status Codes**: Proper use of 200, 400, 404, 503 with descriptive error messages
12. **Success/Error Flags**: Boolean `success` field for easy client-side handling

### Implemented Enhancements
13. **SQL Server Persistence**: Production-grade database instead of in-memory storage
14. **Async/Await Throughout**: All repository and service methods are async
15. **API Key Authentication**: Simple but effective authentication for merchant identification
16. **Idempotency First-Class**: Pre-validation check prevents duplicate processing
17. **Structured Logging**: Serilog provides detailed observability with contextual properties

### Testing Strategy
18. **Comprehensive Test Coverage**: 68 unit, 128 integration, 16 E2E tests (212 total)
19. **Service Layer Testing**: 11 unit tests for business logic independently of HTTP/database
20. **Feature-Focused Tests**: Split monolithic test files for maintainability
21. **Docker-Based E2E**: Real services (SQL Server, Bank API) in containers
22. **Test Helper Methods**: Reusable ReadApiResponseAsync<T> for consistent test patterns


## License

This is a technical assessment project.
