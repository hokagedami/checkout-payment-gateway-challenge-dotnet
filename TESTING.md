# Testing Documentation

## Table of Contents
- [Overview](#overview)
- [Test Strategy](#test-strategy)
- [Test Structure](#test-structure)
- [Running Tests](#running-tests)
- [Test Categories](#test-categories)
- [TDD Approach](#tdd-approach)
- [Best Practices](#best-practices)

## Overview

The Payment Gateway API has **169 comprehensive tests** achieving full code coverage across all components. The test suite is built using **NUnit 4.0** with **Moq** for mocking dependencies.

### Test Statistics

```
Total Tests:     169
Unit Tests:      103
Integration:     54
E2E Tests:       12
Pass Rate:       100%
Framework:       NUnit 4.0
Mocking:         Moq 4.20
```

### Test Coverage by Component

| Component | Tests | Type | Coverage |
|-----------|-------|------|----------|
| CardNumberExtensions | 22 | Unit | 100% |
| FutureExpiryDateAttribute | 11 | Unit | 100% |
| PostPaymentRequest Validation | 53 | Unit | 100% |
| PaymentsRepository | 11 | Unit | 100% |
| BankClient | 6 | Unit | 100% |
| PaymentsController | 54 | Integration | 100% |
| Payment Gateway E2E | 12 | E2E | 100% |

## Test Strategy

### Testing Pyramid

```
      /\
     /  \
    / E2E\      ← 12 tests (full system)
   /______\
  /        \
 /Integration\  ← 54 tests (API endpoints)
/____________\
/            \
/    Unit     \ ← 103 tests (business logic)
/______________\
```

### Unit Tests
**Focus**: Individual components in isolation

**Characteristics**:
- Fast execution (< 100ms per test)
- No external dependencies
- Mocked collaborators
- Test single responsibility

**Example**:
```csharp
[Test]
public void ExtractLastFourDigits_WithValidCardNumber_ReturnsLastFourDigits()
{
    // Arrange
    var cardNumber = "1234567890123456";

    // Act
    var result = cardNumber.ExtractLastFourDigits();

    // Assert
    Assert.That(result, Is.EqualTo("3456"));
}
```

### Integration Tests
**Focus**: Multiple components working together

**Characteristics**:
- Tests full request/response cycle
- Uses WebApplicationFactory
- Mock external dependencies (bank API)
- Verify end-to-end behavior

**Example**:
```csharp
[Test]
public async Task ProcessPayment_WithValidRequest_ReturnsAuthorized()
{
    // Arrange
    var mockBankClient = new Mock<IBankClient>();
    mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
        .ReturnsAsync(new BankPaymentResponse { Authorized = true });

    var client = CreateTestClient(new PaymentsRepository(), mockBankClient);

    // Act
    var response = await client.PostAsJsonAsync("/api/Payments", request);

    // Assert
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

### End-to-End Tests
**Focus**: Complete system testing with real services

**Characteristics**:
- Tests against running API and Bank Simulator
- No mocks - uses actual HTTP communication
- Runs in Docker containers
- Validates complete workflows
- Tests real-world scenarios

**Example**:
```csharp
[Test]
[Category("E2E")]
public async Task PostPayment_WithValidCard_ReturnsAuthorized()
{
    // Arrange
    var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    var request = new PostPaymentRequest
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2026,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/Payments", request);
    var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

    // Assert
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Authorized));
}
```

## Test Structure

### Test Organization

```
test/PaymentGateway.Api.Tests/
├── Controllers/
│   ├── PaymentsControllerTestBase.cs       # Shared test infrastructure
│   ├── GetPaymentTests.cs                  # 5 GET endpoint tests
│   ├── PostPaymentTests.cs                 # 13 POST success tests
│   ├── PaymentValidationTests.cs           # 6 validation tests
│   ├── RejectedPaymentTests.cs             # 10 rejected payment tests
│   └── IdempotencyTests.cs                 # 6 idempotency tests
├── E2E/
│   └── PaymentGatewayE2ETests.cs           # 12 E2E tests
├── Helpers/
│   └── CardNumberExtensionsTests.cs        # 22 unit tests
├── Validation/
│   └── FutureExpiryDateAttributeTests.cs   # 11 unit tests
├── Models/
│   └── PostPaymentRequestValidationTests.cs # 53 unit tests
├── Services/
│   └── BankClientTests.cs                  # 6 unit tests
├── Repositories/
│   └── PaymentsRepositoryTests.cs          # 10 unit tests
└── Usings.cs                               # Global test using
```

### Controller Test Organization

Controller tests are split into feature-focused files for better maintainability:

- **PaymentsControllerTestBase.cs**: Shared test infrastructure
  - `CreateTestClient()` - Creates HTTP client with mocked dependencies
  - `CreateInMemoryContext()` - Creates EF Core InMemory database
  - `CreateValidPaymentRequest()` - Factory for valid test requests
  - Setup/TearDown lifecycle methods

- **GetPaymentTests.cs**: Payment retrieval tests (5 tests)
  - Successful retrieval
  - 404 for missing payments
  - Correct payment details
  - Declined payment status
  - Rejected payment retrieval

- **PostPaymentTests.cs**: Successful payment processing (13 tests)
  - Authorized and declined responses
  - Payment storage verification
  - Multiple currencies support
  - Different card lengths (14-19 digits)
  - CVV formats (3 and 4 digits)
  - Amount ranges (min/max)
  - Last 4 digits extraction

- **PaymentValidationTests.cs**: Validation failures (6 tests)
  - Invalid card numbers
  - Invalid expiry months
  - Expired cards
  - Invalid currencies
  - Invalid CVVs
  - Invalid amounts

- **RejectedPaymentTests.cs**: Rejected payment storage (10 tests)
  - Validation failures create rejected payments
  - Rejected payments stored with correct status
  - Edge cases (null, short, alphabetic card numbers)
  - Rejected payments don't call bank

- **IdempotencyTests.cs**: Duplicate prevention (6 tests)
  - Same key returns same payment
  - Different keys create different payments
  - No key allows duplicates
  - Idempotency for all payment statuses (authorized, declined, rejected)

### Test Naming Convention

Format: `MethodName_Scenario_ExpectedBehavior`

Examples:
```csharp
ProcessPayment_WithValidRequest_ReturnsAuthorized()
ProcessPayment_WithExpiredCard_ReturnsBadRequest()
ExtractLastFourDigits_WithNullCardNumber_ReturnsEmptyString()
Get_WithInvalidId_ReturnsNull()
```

### Test Structure (AAA Pattern)

All tests follow the Arrange-Act-Assert pattern:

```csharp
[Test]
public async Task TestName()
{
    // Arrange - Set up test data and dependencies
    var mockBankClient = new Mock<IBankClient>();
    var repository = new PaymentsRepository();
    var client = CreateTestClient(repository, mockBankClient);

    // Act - Execute the operation being tested
    var response = await client.PostAsJsonAsync("/api/Payments", request);

    // Assert - Verify the outcome
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test file
dotnet test --filter "FullyQualifiedName~PaymentsControllerTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~ProcessPayment_WithValidRequest"

# Run tests by category
dotnet test --filter "Category=E2E"      # E2E only
dotnet test --filter "Category!=E2E"     # Exclude E2E (unit + integration)

# Generate coverage report (with coverlet)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Visual Studio / Rider

1. **Test Explorer**: View → Test Explorer
2. **Run All**: Click "Run All" in Test Explorer
3. **Run Single**: Right-click test → Run
4. **Debug**: Right-click test → Debug
5. **Coverage**: Tests → Analyze Code Coverage

### Watch Mode (Continuous Testing)

```bash
dotnet watch test
```

Tests automatically run when files change.

## Test Categories

### 1. Idempotency Tests (12 tests total)

**Purpose**: Verify duplicate payment prevention across all test levels

#### Unit Tests (4 tests - PaymentsRepository)
- `GetByIdempotencyKey_WithValidKey_ReturnsPayment`
- `GetByIdempotencyKey_WithInvalidKey_ReturnsNull`
- `GetByIdempotencyKey_WithNullIdempotencyKey_ReturnsNull`
- `GetByIdempotencyKey_WithMultiplePayments_ReturnsCorrectOne`

**Example**:
```csharp
[Test]
public void GetByIdempotencyKey_WithValidKey_ReturnsPayment()
{
    // Arrange
    var repository = new PaymentsRepository();
    var idempotencyKey = "test-key-123";
    var payment = new PostPaymentResponse
    {
        Id = Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        IdempotencyKey = idempotencyKey
    };
    repository.Add(payment);

    // Act
    var result = repository.GetByIdempotencyKey(idempotencyKey);

    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.IdempotencyKey, Is.EqualTo(idempotencyKey));
}
```

#### Integration Tests (6 tests - PaymentsController)
- `ProcessPayment_WithIdempotencyKey_ReturnsSamePaymentOnRetry`
- `ProcessPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments`
- `ProcessPayment_WithoutIdempotencyKey_CreatesMultiplePayments`
- `ProcessPayment_WithIdempotencyKey_ReturnsRejectedPaymentOnRetry`
- `ProcessPayment_WithIdempotencyKey_ReturnsDeclinedPaymentOnRetry`

**Example**:
```csharp
[Test]
public async Task ProcessPayment_WithIdempotencyKey_ReturnsSamePaymentOnRetry()
{
    // Arrange
    var mockBankClient = new Mock<IBankClient>();
    mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
        .ReturnsAsync(new BankPaymentResponse { Authorized = true });

    var paymentsRepository = new PaymentsRepository();
    var client = CreateTestClient(paymentsRepository, mockBankClient);
    var idempotencyKey = "test-key-123";

    client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

    // Act - First request
    var firstResponse = await client.PostAsJsonAsync("/api/Payments", request);
    var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

    // Act - Retry with same key
    var secondResponse = await client.PostAsJsonAsync("/api/Payments", request);
    var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

    // Assert - Same payment returned
    Assert.That(secondPayment.Id, Is.EqualTo(firstPayment.Id));
    Assert.That(paymentsRepository.Payments.Count, Is.EqualTo(1)); // Only one payment stored

    // Bank called only once
    mockBankClient.Verify(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Once);
}
```

#### E2E Tests (2 tests - Full System)
- `PostPayment_WithIdempotencyKey_PreventsDuplicatePayments`
- `PostPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments`

**Example**:
```csharp
[Test]
[Category("E2E")]
public async Task PostPayment_WithIdempotencyKey_PreventsDuplicatePayments()
{
    // Arrange
    var idempotencyKey = $"e2e-test-{Guid.NewGuid()}";
    _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

    // Act - First request
    var firstResponse = await _client.PostAsJsonAsync("/api/Payments", request);
    var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

    // Act - Retry (network failure simulation)
    var secondResponse = await _client.PostAsJsonAsync("/api/Payments", request);
    var secondPayment = await secondResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

    // Assert - Same payment ID returned, no duplicate charge
    Assert.That(secondPayment.Id, Is.EqualTo(firstPayment.Id));
    Assert.That(secondPayment.IdempotencyKey, Is.EqualTo(idempotencyKey));
}
```

**Key Test Scenarios**:
- Duplicate detection for authorized payments
- Duplicate detection for declined payments
- Duplicate detection for rejected payments
- Different keys create different payments
- No key provided allows duplicates (backward compatibility)

### 2. End-to-End Tests (12 tests)

**File**: `E2E/PaymentGatewayE2ETests.cs`

**Coverage**:
- Health check endpoint
- Authorized payment processing
- Declined payment processing
- Rejected payments (invalid card, expired card)
- Payment retrieval (success and not found)
- Multiple currencies support
- Leading zero preservation
- Complete payment workflows
- Idempotency duplicate prevention (2 tests)

**Running E2E Tests**:
```bash
# Using Docker (recommended)
docker-compose -f docker-compose.test.yml up --abort-on-container-exit

# Local (requires services running)
dotnet test --filter "Category=E2E"
```

**Example Test**:
```csharp
[Test]
[Category("E2E")]
public async Task E2E_CompletePaymentFlow_AuthorizedDeclinedRejected()
{
    // 1. Authorized payment
    var authResponse = await _client.PostAsJsonAsync("/api/Payments", authorizedRequest);
    Assert.That(authResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // 2. Declined payment
    var decResponse = await _client.PostAsJsonAsync("/api/Payments", declinedRequest);
    Assert.That(decResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // 3. Rejected payment
    var rejResponse = await _client.PostAsJsonAsync("/api/Payments", rejectedRequest);
    Assert.That(rejResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    // 4. Verify retrievals
    var authGetResponse = await _client.GetAsync($"/api/Payments/{authPayment.Id}");
    Assert.That(authGetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

**Note**: E2E tests require Docker and running services. See [E2E-README.md](./E2E-README.md) for detailed setup instructions.

### 3. CardNumberExtensions Tests (22 tests)

**File**: `Helpers/CardNumberExtensionsTests.cs`

**Coverage**:
- Valid card numbers (14-19 digits)
- Null/empty/whitespace handling
- Short strings (< 4 characters)
- Non-numeric characters
- Leading zeros preservation
- Formatted card numbers (with spaces/dashes)

**Example Test**:
```csharp
[TestCase("12345678901234")]   // 14 digits
[TestCase("123456789012345")]  // 15 digits
[TestCase("1234567890123456")] // 16 digits
public void ExtractLastFourDigits_WithValidLength_ReturnsLastFour(string cardNumber)
{
    var result = cardNumber.ExtractLastFourDigits();
    Assert.That(result, Is.EqualTo(cardNumber[^4..]));
}
```

### 3. FutureExpiryDate Validation Tests (11 tests)

**File**: `Validation/FutureExpiryDateAttributeTests.cs`

**Coverage**:
- Future dates (success)
- Past dates (failure)
- Current month/year (success)
- Edge cases (December, January transitions)
- Missing properties (failure)

**Example Test**:
```csharp
[Test]
public void Validate_WithFutureDate_ReturnsSuccess()
{
    var request = new TestModel { ExpiryMonth = 12, ExpiryYear = 2026 };
    var attribute = new FutureExpiryDateAttribute();

    var result = attribute.GetValidationResult(request, context);

    Assert.That(result, Is.EqualTo(ValidationResult.Success));
}
```

### 4. Request Validation Tests (53 tests)

**File**: `Models/PostPaymentRequestValidationTests.cs`

**Coverage**:
- Card number validation (15 tests)
- Expiry month validation (7 tests)
- Currency validation (11 tests)
- Amount validation (8 tests)
- CVV validation (11 tests)
- Multiple errors (1 test)

**Example Test**:
```csharp
[TestCase("USD")]
[TestCase("GBP")]
[TestCase("EUR")]
public void Validate_WithValidCurrency_ReturnsSuccess(string currency)
{
    var request = CreateValidRequest();
    request.Currency = currency;

    var results = ValidateModel(request);

    Assert.That(results, Is.Empty);
}
```

### 5. Repository Tests (7 tests)

**File**: `Repositories/PaymentsRepositoryTests.cs`

**Coverage**:
- Add payment successfully
- Get by valid ID
- Get by invalid ID (returns null)
- Multiple add operations
- Initial state (empty)
- Count increments

**Example Test**:
```csharp
[Test]
public void Get_WithInvalidId_ReturnsNull()
{
    var repository = new PaymentsRepository();

    var result = repository.Get(Guid.NewGuid());

    Assert.That(result, Is.Null);
}
```

### 6. BankClient Tests (6 tests)

**File**: `Services/BankClientTests.cs`

**Coverage**:
- Successful authorized response
- Declined response
- 400 Bad Request (returns null)
- 503 Service Unavailable (returns null)
- Network exception (returns null)
- Correct request format

**Example Test**:
```csharp
[Test]
public async Task ProcessPaymentAsync_WithException_ReturnsNull()
{
    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("Network error"));

    var result = await bankClient.ProcessPaymentAsync(request);

    Assert.That(result, Is.Null);
}
```

### 7. Controller Integration Tests (40 tests across 5 files)

**Files**: Split by feature for better organization and maintainability

#### **GetPaymentTests.cs** (5 tests)
- `RetrievesAPaymentSuccessfully`
- `Returns404IfPaymentNotFound`
- `GetPayment_ReturnsCorrectPaymentDetails`
- `GetPayment_WithDeclinedPayment_ReturnsCorrectStatus`
- `RejectedPayment_CanBeRetrievedById`

#### **PostPaymentTests.cs** (13 tests)
- `ProcessPayment_WithValidRequest_ReturnsAuthorized`
- `ProcessPayment_WithValidRequest_ReturnsDeclined`
- `ProcessPayment_WhenBankReturnsNull_Returns503`
- `ProcessPayment_StoresPaymentInRepository`
- `ProcessPayment_WithMultipleCurrencies_AllSucceed`
- `ProcessPayment_WithDifferentCardLengths_AllSucceed`
- `ProcessPayment_With3DigitCvv_Succeeds`
- `ProcessPayment_With4DigitCvv_Succeeds`
- `ProcessPayment_ExtractsCorrectLastFourDigits`
- `ProcessPayment_WithMinimumAmount_Succeeds`
- `ProcessPayment_WithLargeAmount_Succeeds`

#### **PaymentValidationTests.cs** (6 tests)
- `ProcessPayment_WithInvalidCardNumber_ReturnsBadRequest` (3 test cases)
- `ProcessPayment_WithInvalidExpiryMonth_ReturnsBadRequest` (2 test cases)
- `ProcessPayment_WithExpiredCard_ReturnsBadRequest`
- `ProcessPayment_WithInvalidCurrency_ReturnsBadRequest` (3 test cases)
- `ProcessPayment_WithInvalidCvv_ReturnsBadRequest` (3 test cases)
- `ProcessPayment_WithInvalidAmount_ReturnsBadRequest`

#### **RejectedPaymentTests.cs** (10 tests)
- `ProcessPayment_WithInvalidCardNumber_CreatesRejectedPayment`
- `ProcessPayment_WithExpiredCard_CreatesRejectedPayment`
- `ProcessPayment_WithInvalidCurrency_CreatesRejectedPayment`
- `ProcessPayment_WithInvalidCvv_CreatesRejectedPayment`
- `ProcessPayment_WithInvalidAmount_CreatesRejectedPayment`
- `ProcessPayment_WithMultipleValidationErrors_CreatesRejectedPayment`
- `ProcessPayment_WithNullCardNumber_HandlesGracefully`
- `ProcessPayment_WithShortCardNumber_ExtractsAvailableDigits`
- `ProcessPayment_WithAlphabeticCardNumber_HandlesGracefully`
- `ProcessPayment_RejectedPayments_DoNotCallBank`

#### **IdempotencyTests.cs** (6 tests)
- `ProcessPayment_WithIdempotencyKey_ReturnsSamePaymentOnRetry`
- `ProcessPayment_WithDifferentIdempotencyKeys_CreatesMultiplePayments`
- `ProcessPayment_WithoutIdempotencyKey_CreatesMultiplePayments`
- `ProcessPayment_WithIdempotencyKey_ReturnsRejectedPaymentOnRetry`
- `ProcessPayment_WithIdempotencyKey_ReturnsDeclinedPaymentOnRetry`

#### **PaymentsControllerTestBase.cs** - Shared Infrastructure

All controller test classes inherit from this base class:

```csharp
public abstract class PaymentsControllerTestBase
{
    protected Random Random = null!;
    protected WebApplicationFactory<Program> Factory = null!;

    [SetUp]
    public void SetUp()
    {
        Random = new Random();
        Factory = new WebApplicationFactory<Program>();
    }

    [TearDown]
    public void TearDown()
    {
        Factory?.Dispose();
    }

    protected (HttpClient client, PaymentGatewayDbContext context) CreateTestClient(
        Mock<IBankClient>? mockBankClient = null)
    {
        // Creates test client with DI overrides
        // Returns tuple with client and shared context
    }

    protected static PostPaymentRequest CreateValidPaymentRequest()
    {
        // Creates valid payment request
    }
}
```

**Benefits of Split Structure**:
- **Better Organization**: Tests grouped by feature area
- **Improved Maintainability**: Smaller files (~100-300 lines each)
- **Easier Navigation**: Find tests quickly by feature
- **Clear Separation**: GET, POST success, validation, rejection, and idempotency
- **Reduced Merge Conflicts**: Changes isolated to specific feature files

## TDD Approach

This project was built using **Test-Driven Development (TDD)** following the Red-Green-Refactor cycle.

### Red-Green-Refactor Cycle

```
1. RED    → Write failing test
2. GREEN  → Write minimal code to pass
3. REFACTOR → Improve code quality
4. REPEAT
```

### TDD Example: CardNumberExtensions

#### 1. RED - Write Failing Test
```csharp
[Test]
public void ExtractLastFourDigits_WithValidCardNumber_ReturnsLastFourDigits()
{
    var cardNumber = "1234567890123456";
    var result = cardNumber.ExtractLastFourDigits();  // Doesn't exist yet
    Assert.That(result, Is.EqualTo("3456"));
}
// Test fails - Method doesn't exist
```

#### 2. GREEN - Minimal Implementation
```csharp
public static string ExtractLastFourDigits(this string? cardNumber)
{
    if (cardNumber == null || cardNumber.Length < 4)
        return string.Empty;

    return cardNumber[^4..];
}
// Test passes
```

#### 3. REFACTOR - Add More Tests & Improve
```csharp
[TestCase(null)]
[TestCase("")]
[TestCase("123")]
public void ExtractLastFourDigits_WithInvalidInput_ReturnsEmpty(string cardNumber)
{
    var result = cardNumber.ExtractLastFourDigits();
    Assert.That(result, Is.EqualTo(string.Empty));
}

// Refactor to handle all cases
public static string ExtractLastFourDigits(this string? cardNumber)
{
    if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 4)
        return string.Empty;

    var lastFour = cardNumber[^4..];
    return lastFour.All(char.IsDigit) ? lastFour : string.Empty;
}
// All tests pass
```

### TDD Benefits Realized

1. **Design Validation**: Tests validate API design before implementation
2. **Regression Prevention**: 147 tests catch breaking changes
3. **Documentation**: Tests serve as usage examples
4. **Confidence**: Refactoring is safe with full test coverage
5. **Bug Prevention**: Edge cases identified early

## Best Practices

### 1. Test Independence

Each test is independent and doesn't rely on other tests:

```csharp
[SetUp]
public void SetUp()
{
    // Fresh instances for each test
    _repository = new PaymentsRepository();
    _factory = new WebApplicationFactory<Program>();
}
```

### 2. Descriptive Test Names

Names clearly describe scenario and expected outcome:

```csharp
ProcessPayment_WithExpiredCard_ReturnsBadRequest()  // Clear
Test1()                                             // Unclear
```

### 3. Single Assertion Focus

Each test verifies one logical concept:

```csharp
// Good - Tests one thing
[Test]
public void ProcessPayment_WithValidRequest_Returns200()
{
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}

// Avoid - Tests multiple unrelated things
[Test]
public void ProcessPayment_ValidatesEverything()
{
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    Assert.That(repository.Count, Is.EqualTo(5));
}
```

### 4. Use Test Helpers

Reduce duplication with helper methods:

```csharp
private HttpClient CreateTestClient(...)  // Reusable setup
private PostPaymentRequest CreateValidPaymentRequest()  // Default test data
```

### 5. Test Both Happy and Sad Paths

```csharp
// Happy path
ProcessPayment_WithValidRequest_ReturnsAuthorized()

// Sad paths
ProcessPayment_WithInvalidCard_ReturnsBadRequest()
ProcessPayment_WithExpiredCard_ReturnsBadRequest()
ProcessPayment_WhenBankUnavailable_Returns503()
```

### 6. Mocking External Dependencies

Always mock external systems:

```csharp
var mockBankClient = new Mock<IBankClient>();
mockBankClient.Setup(x => x.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
    .ReturnsAsync(new BankPaymentResponse { Authorized = true });
```

### 7. Constraint-Based Assertions

Use NUnit's constraint model for better error messages:

```csharp
// Good - Clear constraint
Assert.That(result, Is.EqualTo("3456"));
Assert.That(collection, Has.Count.EqualTo(1));
Assert.That(value, Is.Not.Null);

// Avoid - Classic assertions
Assert.AreEqual("3456", result);
Assert.AreEqual(1, collection.Count);
Assert.IsNotNull(value);
```

### 8. TestCase for Parameterized Tests

Use TestCase for testing multiple inputs:

```csharp
[TestCase("USD")]
[TestCase("GBP")]
[TestCase("EUR")]
public void Validate_WithValidCurrency_ReturnsSuccess(string currency)
{
    // Test runs 3 times with different currencies
}
```

## Continuous Testing

### Watch Mode

```bash
dotnet watch test
```

Automatically runs tests when files change - instant feedback!

### Pre-Commit Hook

Add a git pre-commit hook to run tests:

```bash
#!/bin/sh
dotnet test
if [ $? -ne 0 ]; then
    echo "Tests failed. Commit aborted."
    exit 1
fi
```

## Test Metrics

### Current Metrics

| Metric | Value | Target |
|--------|-------|--------|
| Test Count | 158 | 100+ |
| Unit Tests | 99 | - |
| Integration Tests | 48 | - |
| E2E Tests | 11 | - |
| Pass Rate | 100% | 100% |
| Code Coverage | ~100% | >90% |
| Avg Test Time (Unit/Integration) | <10ms | <100ms |
| Total Test Time (Unit/Integration) | ~2s | <5s |
| E2E Test Time (Docker) | ~30-45s | <60s |

### Quality Indicators

- All tests passing
- No flaky tests
- Fast execution
- Clear naming
- Good organization

---

**Last Updated**: 2025-01-08
