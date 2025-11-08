# End-to-End (E2E) Testing Guide

This document explains how to run and maintain the E2E test suite for the Payment Gateway API.

## Overview

The E2E tests validate the complete payment processing flow by:
- Running the actual Payment Gateway API in a Docker container
- Using the real Bank Simulator (Mountebank)
- Making HTTP requests to test real-world scenarios
- Verifying all three payment statuses: Authorized, Declined, Rejected

## Prerequisites

- Docker and Docker Compose installed
- No services running on ports 2525, 8080, or 5000

## Quick Start

### Windows (PowerShell)
```powershell
.\run-e2e-tests.ps1
```

### Linux/Mac (Bash)
```bash
chmod +x run-e2e-tests.sh
./run-e2e-tests.sh
```

## Test Results

Each E2E test run automatically saves detailed results in timestamped directories under `test-results/`:

```
test-results/
├── 2025-01-15_14-30-25/
│   ├── e2e-results.trx       # Machine-readable XML format (for CI/CD)
│   └── e2e-results.html      # Human-readable HTML report
├── 2025-01-15_15-45-10/
│   ├── e2e-results.trx
│   └── e2e-results.html
...
```

### Test Result Files

- **TRX File**: Visual Studio Test Results format
  - Can be viewed in Visual Studio, Rider, or VS Code
  - Contains detailed test execution data, timings, and error messages
  - Suitable for CI/CD integration

- **HTML File**: Web-based test report
  - Open directly in any web browser
  - Color-coded pass/fail indicators
  - Stack traces for failed tests
  - Easy to share with team members

### Test Summary

After each test run, the script displays a summary:

```
================================================================
                    TEST RESULTS SUMMARY
================================================================

Total Tests: 11
Passed: 11
Failed: 0
Duration: 2.06s

Detailed Results:
  TRX Report: test-results/2025-01-15_14-30-25/e2e-results.trx
  HTML Report: test-results/2025-01-15_14-30-25/e2e-results.html

================================================================
```

## Manual Testing

### 1. Start Services
```bash
docker-compose -f docker-compose.test.yml up --build
```

### 2. Run Tests (in another terminal)
```bash
# Run all E2E tests
dotnet test --filter "Category=E2E"

# Run specific test
dotnet test --filter "FullyQualifiedName~PostPayment_WithValidCard_ReturnsAuthorized"
```

### 3. Stop Services
```bash
docker-compose -f docker-compose.test.yml down -v
```

## Test Architecture

```
┌─────────────────┐
│  Test Runner    │  ← Runs E2E tests
│  Container      │
└────────┬────────┘
         │ HTTP
         ▼
┌─────────────────┐
│ Payment Gateway │  ← API under test
│    Container    │
└────────┬────────┘
         │ HTTP
         ▼
┌─────────────────┐
│ Bank Simulator  │  ← Mock bank (Mountebank)
│   Container     │
└─────────────────┘
```

## Test Coverage

The E2E test suite covers:

### 1. **Health Checks**
- `HealthCheck_ApiIsRunning` - Verifies API is accessible

### 2. **Authorized Payments**
- `PostPayment_WithValidCard_ReturnsAuthorized` - Valid card returns authorized status
- `PostPayment_MultipleCurrencies_AllSucceed` - Tests USD, GBP, EUR currencies

### 3. **Declined Payments**
- `PostPayment_WithDeclinedCard_ReturnsDeclined` - Bank declines the payment

### 4. **Rejected Payments**
- `PostPayment_WithInvalidCardNumber_ReturnsRejected` - Invalid card format
- `PostPayment_WithExpiredCard_ReturnsRejected` - Expired card date

### 5. **Payment Retrieval**
- `GetPayment_WithValidId_ReturnsPayment` - Retrieve existing payment
- `GetPayment_WithInvalidId_ReturnsNotFound` - Non-existent payment returns 404

### 6. **Edge Cases**
- `PostPayment_WithLeadingZeroCardNumber_PreservesZeros` - Tests "0366" preservation

### 7. **Complete Flows**
- `E2E_CompletePaymentFlow_AuthorizedDeclinedRejected` - Full end-to-end scenario

## Test Cards

Use these test card numbers with the Bank Simulator:

| Card Number        | Result      | Description                    |
|--------------------|-------------|--------------------------------|
| 2222405343248877   | Authorized  | Valid card, payment succeeds   |
| 2222405343248878   | Declined    | Valid card, bank declines      |
| 123                | Rejected    | Invalid format, validation fails |

## Environment Variables

The tests use these environment variables:

| Variable               | Default                    | Description              |
|------------------------|----------------------------|--------------------------|
| PAYMENT_GATEWAY_URL    | http://localhost:5000      | API base URL             |
| BankApiUrl             | http://bank_simulator:8080 | Bank simulator URL       |

## Debugging Failed Tests

### View Container Logs
```bash
# All services
docker-compose -f docker-compose.test.yml logs

# Specific service
docker-compose -f docker-compose.test.yml logs payment_gateway
docker-compose -f docker-compose.test.yml logs bank_simulator
docker-compose -f docker-compose.test.yml logs test_runner
```

### Access Running Container
```bash
# Get shell in payment gateway
docker exec -it payment_gateway_test sh

# Get shell in test runner
docker exec -it test_runner sh
```

### Run Tests Locally (without Docker)
```bash
# Start bank simulator
docker-compose up bank_simulator

# Run API locally
cd src/PaymentGateway.Api
dotnet run

# Run E2E tests
cd test/PaymentGateway.Api.Tests
dotnet test --filter "Category=E2E"
```

## Adding New E2E Tests

1. **Add test to `PaymentGatewayE2ETests.cs`**:
```csharp
[Test]
[Category("E2E")]
public async Task YourNewTest()
{
    // Arrange
    var request = new PostPaymentRequest { /* ... */ };

    // Act
    var response = await _client.PostAsJsonAsync("/api/Payments", request);

    // Assert
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

2. **Run tests**:
```bash
./run-e2e-tests.sh
```

## Troubleshooting

### Port Already in Use
```bash
# Check what's using the ports
netstat -ano | findstr :5000
netstat -ano | findstr :8080

# Stop all Docker containers
docker-compose -f docker-compose.test.yml down -v
```

### Health Check Failing
- Check if services are starting properly: `docker-compose -f docker-compose.test.yml logs`
- Increase health check retries in `docker-compose.test.yml`
- Verify `/health` endpoint is accessible: `curl http://localhost:5000/health`

### Tests Timeout
- Increase HttpClient timeout in test constructor
- Check network connectivity between containers
- Verify bank simulator is responding: `curl http://localhost:8080`

## Performance

Typical E2E test run times:
- **Full suite**: ~30-60 seconds
- **Single test**: ~2-5 seconds
- **Docker build** (first time): ~2-3 minutes
- **Docker build** (cached): ~30 seconds

## Best Practices

1. **Keep tests independent** - Each test should work in isolation
2. **Use unique data** - Generate unique GUIDs/data to avoid conflicts
3. **Clean up** - Tests clean up after themselves
4. **Fast feedback** - Run E2E tests before committing
5. **CI integration** - Run E2E tests on every PR

## Maintenance

### Update Dependencies
```bash
# Rebuild images with latest dependencies
docker-compose -f docker-compose.test.yml build --no-cache
```

### Monitor Test Health
```bash
# Run tests and save results
./run-e2e-tests.sh > test-results.log 2>&1
```
