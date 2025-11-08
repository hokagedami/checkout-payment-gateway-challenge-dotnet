# Payment Gateway API

A production-ready payment gateway API built with ASP.NET Core 8.0, implementing secure payment processing with comprehensive validation, audit trails, and robust error handling.

## Overview

This Payment Gateway API allows merchants to process payments by integrating with a banking partner. It provides:

- **Payment Processing**: Submit payment requests and receive authorization/decline responses
- **Payment Retrieval**: Query historical payment information
- **Comprehensive Validation**: Multi-layer validation with detailed error messages
- **Audit Trail**: All payment attempts (authorized, declined, and rejected) are stored
- **Card Security**: Card numbers are masked, storing only the last 4 digits

## Features

### Core Functionality
- Process payment requests with full validation
- Retrieve payment details by ID
- Support for multiple currencies (USD, GBP, EUR)
- Support for 14-19 digit card numbers
- 3 or 4 digit CVV support

### Payment Status
- **Authorized**: Payment approved by the bank
- **Declined**: Payment rejected by the bank
- **Rejected**: Payment failed validation before reaching the bank

### Security & Compliance
- Card number masking (only last 4 digits stored)
- Input validation and sanitization
- Custom validation attributes for expiry dates
- No sensitive data in logs

### Quality Assurance
- **147 comprehensive tests** (100% passing)
  - 99 unit tests
  - 48 integration tests
- NUnit test framework with [SetUp]/[TearDown] lifecycle
- Test-Driven Development (TDD) approach
- Full code coverage

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for bank simulator)

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
# Run all tests
dotnet test

# Run only unit/integration tests (exclude E2E)
dotnet test --filter "Category!=E2E"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### 4. Access Swagger UI

Navigate to `https://localhost:5001/swagger` to explore the API interactively.

## API Endpoints

### POST /api/payments
Process a new payment request

**Request Body:**
```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000,
  "cvv": "123"
}
```

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000
}
```

### GET /api/payments/{id}
Retrieve payment details by ID

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000
}
```

**Not Found Response (404):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

## Project Structure

```
src/
  PaymentGateway.Api/
    Controllers/          # API endpoints
    Models/              # Request/Response DTOs
      Bank/              # Bank integration models
      Requests/          # API request models
      Responses/         # API response models
    Services/            # Business logic & external services
    Repositories/        # Data access layer
    Validation/          # Custom validation attributes
    Helpers/             # Utility classes
    Enums/              # Enumerations

test/
  PaymentGateway.Api.Tests/
    Controllers/         # Integration tests
    Services/            # Unit tests for services
    Repositories/        # Unit tests for repositories
    Validation/          # Unit tests for validators
    Helpers/            # Unit tests for helpers
    Models/             # Unit tests for model validation

imposters/              # Bank simulator configuration
docker-compose.yml      # Docker setup for bank simulator
```

## Validation Rules

### Card Number
- **Required**: Yes
- **Format**: 14-19 numeric digits
- **Example**: `2222405343248877`

### Expiry Month
- **Required**: Yes
- **Range**: 1-12
- **Example**: `12`

### Expiry Year
- **Required**: Yes
- **Validation**: Must be in the future (combined with month)
- **Example**: `2026`

### Currency
- **Required**: Yes
- **Supported Values**: `USD`, `GBP`, `EUR`
- **Format**: Exactly 3 uppercase characters

### Amount
- **Required**: Yes
- **Range**: 1 to 2,147,483,647 (smallest currency unit)
- **Example**: `1000` (£10.00 or $10.00)

### CVV
- **Required**: Yes
- **Format**: 3-4 numeric digits
- **Example**: `123` or `1234`

## Bank Simulator Behavior

The mock bank determines authorization based on the card number:
- **Last digit is odd** → Authorized
- **Last digit is even** → Declined

Examples:
- `2222405343248877` (ends in 7) → **Authorized**
- `2222405343248878` (ends in 8) → **Declined**

## Configuration

### appsettings.json
```json
{
  "BankApiUrl": "http://localhost:8080",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Further Documentation

- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Technical architecture and design decisions
- **[TESTING.md](./TESTING.md)** - Comprehensive testing guide and TDD approach
- **[API.md](./API.md)** - Detailed API reference with examples

## Technology Stack

- **Framework**: .NET 8.0
- **Web Framework**: ASP.NET Core
- **Testing**: NUnit 4.0, Moq
- **API Documentation**: Swagger/OpenAPI
- **Bank Simulator**: Mountebank (Docker)

## Key Design Decisions

1. **In-Memory Storage**: Simple repository pattern for demonstration purposes
2. **Custom Validation**: Attribute-based validation with FutureExpiryDateAttribute
3. **Card Masking**: Only last 4 digits stored for PCI compliance consideration
4. **Rejected Payment Audit**: Failed validations are stored for analytics
5. **Disabled Auto-Validation**: Custom handling to store rejected payments

## License

This is a technical challenge submission.

## Authors

Developed as part of the Payment Gateway technical challenge.

---

**Note**: This is a demonstration project. For production use, consider:
- Persistent database storage
- Authentication & authorization
- Rate limiting
- Encryption at rest
- PCI DSS compliance
- Distributed caching
- Event sourcing for audit trail
