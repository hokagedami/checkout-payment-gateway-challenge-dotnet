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

#### Observability
- **Structured Logging**: Serilog with console and file sinks
- **Async/Await Throughout**: Non-blocking operations

### Quality Assurance
- **76 comprehensive tests**
  - 26 unit tests (Services: 6, Repositories: 9, Validation: 7, Helpers: 8, Models: 2)
  - 39 integration tests (Controllers - feature-focused files)
  - 11 end-to-end tests (covering core functionality)
- NUnit test framework with [SetUp]/[TearDown] lifecycle
- Test-Driven Development (TDD) approach
- Docker-based E2E testing with real services (SQL Server, Bank API)
- **E2E Pass Rate**: 100% (11/11 tests passing)

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

The API will be available at `https://localhost:5001` (or check console output)

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

### 4. Access Swagger UI

Navigate to `https://localhost:5001/swagger` to explore the API interactively.

**Note**: Swagger UI includes API Key authentication support. Use one of these keys:
- `test-api-key-1`
- `test-api-key-2`
- `merchant-demo-key`

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

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "USD",
  "amount": 100
}
```

**Response (400 Bad Request - Rejected):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CardNumber": ["Card number must be between 14-19 digits"]
  },
  "payment": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Rejected"
  }
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
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "USD",
  "amount": 100
}
```

**Response (404 Not Found):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

## Idempotency

The API supports idempotent payment processing using the `Idempotency-Key` header:

```bash
curl -X POST https://localhost:5001/api/payments \
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
- **Retry with Same Key**: Returns the existing payment response without calling the bank
- **Different Request, Same Key**: Returns 409 Conflict (different payment data)

**Best Practices:**
- Use a unique key per logical payment attempt (e.g., UUID or order ID)
- Store keys on the client side before making the request
- Reuse the same key for all retries of the same logical payment
- Do not reuse keys across different payment attempts

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
    Models/                          # Request/Response DTOs
      Bank/                          # Bank integration models
        BankPaymentRequest.cs
        BankPaymentResponse.cs
      Entities/                      # Database entities
        Payment.cs
      Requests/                      # API request models
        PostPaymentRequest.cs
      Responses/                     # API response models
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
      BankClient.cs
      IBankClient.cs
    Validation/                      # Custom validation attributes
      FutureExpiryDateAttribute.cs

test/
  PaymentGateway.Api.Tests/
    Controllers/                     # Integration tests (39 tests)
      GetPaymentTests.cs             # 5 tests
      IdempotencyTests.cs            # 5 tests
      PaymentsControllerTestBase.cs  # Base class for integration tests
      PaymentValidationTests.cs      # 2 tests
      PostPaymentTests.cs            # 11 tests
      RejectedPaymentTests.cs        # 10 tests
    E2E/                             # End-to-end tests (11 tests)
      PaymentGatewayE2ETests.cs      # 11 tests
    Helpers/                         # Unit tests for helpers (8 tests)
      CardNumberExtensionsTests.cs
    Models/                          # Unit tests for model validation (2 tests)
      PostPaymentRequestValidationTests.cs
    Repositories/                    # Unit tests for repositories (9 tests)
      PaymentsRepositoryTests.cs
    Services/                        # Unit tests for services (6 tests)
      BankClientTests.cs
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
INSTRUCTIONS.md                      # Original challenge instructions
PaymentGateway.sln                   # Visual Studio solution file
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
- **Logging**: Serilog 9.0 (Console + File sinks)

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
1. **Repository Pattern**: Interface-based data access (IPaymentsRepository) for testability
2. **Custom Validation**: Attribute-based validation with FutureExpiryDateAttribute
3. **Card Masking**: Only last 4 digits stored for PCI compliance consideration
4. **Rejected Payment Audit**: Failed validations are stored for analytics
5. **Disabled Auto-Validation**: Custom handling to store rejected payments before returning 400

### Implemented Enhancements
6. **SQL Server Persistence**: Production-grade database instead of in-memory storage
7. **Async/Await Throughout**: All repository and service methods are async
8. **API Key Authentication**: Simple but effective authentication for merchant identification
9. **Idempotency First-Class**: Pre-validation check prevents duplicate processing
10. **Structured Logging**: Serilog provides detailed observability

### Testing Strategy
11. **Test Pyramid**: 26 unit, 39 integration, 11 E2E tests (76 total)
12. **Feature-Focused Tests**: Split monolithic test files for maintainability
13. **Docker-Based E2E**: Real services (SQL Server, Bank API) in containers


## License

This is a technical assessment project.
