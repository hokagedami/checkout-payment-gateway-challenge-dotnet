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

The Payment Gateway API has **147 comprehensive tests** achieving full code coverage across all components. The test suite is built using **NUnit 4.0** with **Moq** for mocking dependencies.

### Test Statistics

```
Total Tests:     147
Unit Tests:      99
Integration:     48
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
| PaymentsRepository | 7 | Unit | 100% |
| BankClient | 6 | Unit | 100% |
| PaymentsController | 48 | Integration | 100% |

## Test Strategy

### Testing Pyramid

```
      /\
     /  \
    / E2E\      ← 0 tests (infrastructure ready)
   /______\
  /        \
 /Integration\  ← 48 tests (API endpoints)
/____________\
/            \
/    Unit     \ ← 99 tests (business logic)
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

## Test Structure

### Test Organization

```
test/PaymentGateway.Api.Tests/
├── Controllers/
│   └── PaymentsControllerTests.cs          # 48 integration tests
├── Helpers/
│   └── CardNumberExtensionsTests.cs        # 22 unit tests
├── Validation/
│   └── FutureExpiryDateAttributeTests.cs   # 11 unit tests
├── Models/
│   └── PostPaymentRequestValidationTests.cs # 53 unit tests
├── Services/
│   └── BankClientTests.cs                  # 6 unit tests
├── Repositories/
│   └── PaymentsRepositoryTests.cs          # 7 unit tests
└── Usings.cs                               # Global test using
```

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

# Run tests by category (when E2E tests added)
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category!=E2E"  # Exclude E2E

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

### 1. CardNumberExtensions Tests (22 tests)

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

### 2. FutureExpiryDate Validation Tests (11 tests)

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

### 3. Request Validation Tests (53 tests)

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

### 4. Repository Tests (7 tests)

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

### 5. BankClient Tests (6 tests)

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

### 6. Controller Integration Tests (48 tests)

**File**: `Controllers/PaymentsControllerTests.cs`

**Coverage**:
- **GET endpoint** (5 tests):
  - Successful retrieval
  - 404 for missing payment
  - Correct payment details
  - Declined payment status
  - Retrieve after POST

- **POST endpoint - Success** (19 tests):
  - Authorized response
  - Declined response
  - Storage in repository
  - Multiple currencies
  - Different card lengths
  - 3 and 4 digit CVV
  - Card masking
  - Min/max amounts

- **POST endpoint - Validation** (8 tests):
  - Invalid card number
  - Invalid expiry month
  - Expired card
  - Invalid currency
  - Invalid CVV
  - Invalid amount

- **POST endpoint - Errors** (1 test):
  - Bank unavailable (503)

- **Rejected Payments** (11 tests):
  - Invalid card creates rejected
  - Expired card creates rejected
  - Invalid currency creates rejected
  - Invalid CVV creates rejected
  - Invalid amount creates rejected
  - Multiple errors creates rejected
  - Rejected can be retrieved
  - Null card number handling
  - Short card number handling
  - Alphabetic card handling
  - Rejected don't call bank

**SetUp/TearDown**:
```csharp
[SetUp]
public void SetUp()
{
    _random = new Random();
    _factory = new WebApplicationFactory<Program>();
}

[TearDown]
public void TearDown()
{
    _factory?.Dispose();
}
```

**Helper Methods**:
```csharp
private HttpClient CreateTestClient(
    PaymentsRepository? paymentsRepository = null,
    Mock<IBankClient>? mockBankClient = null)
{
    // Creates test client with DI overrides
}

private static PostPaymentRequest CreateValidPaymentRequest()
{
    // Creates valid payment request
}
```

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
| Test Count | 147 | 100+ |
| Pass Rate | 100% | 100% |
| Code Coverage | ~100% | >90% |
| Avg Test Time | <10ms | <100ms |
| Total Test Time | ~1s | <5s |

### Quality Indicators

- All tests passing
- No flaky tests
- Fast execution
- Clear naming
- Good organization

---

**Last Updated**: 2025-01-08
