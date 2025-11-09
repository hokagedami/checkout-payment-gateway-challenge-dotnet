# API Reference

## Table of Contents
- [Overview](#overview)
- [Base URL](#base-url)
- [Authentication](#authentication)
- [Idempotency](#idempotency)
- [Endpoints](#endpoints)
  - [POST /api/Payments](#post-apipayments)
  - [GET /api/Payments/{id}](#get-apipaymentsid)
- [Request Models](#request-models)
- [Response Models](#response-models)
- [Error Handling](#error-handling)
- [Status Codes](#status-codes)
- [Examples](#examples)

## Overview

The Payment Gateway API provides RESTful endpoints for processing payments and retrieving payment information. All requests and responses use JSON format.

**API Version**: 1.0
**Content-Type**: `application/json`
**Protocol**: HTTPS (HTTP redirects to HTTPS)

## Base URL

```
Development: https://localhost:5001
Production:  https://api.paymentgateway.com
```

## Authentication

**Current Version**: No authentication required (demonstration purposes)

## Idempotency

The Payment Gateway API supports idempotency to prevent duplicate payments.

### Idempotency-Key Header

**Header Name**: `Idempotency-Key`
**Value**: String (recommended: UUID v4)
**Required**: No (optional)
**Max Length**: 255 characters

### How It Works

1. **First Request**: When a payment request includes an idempotency key, the API processes it normally and associates the key with the payment response
2. **Duplicate Request**: If the same idempotency key is used again, the API returns the cached response without re-processing the payment or calling the bank
3. **No Key Provided**: Requests without an idempotency key are treated as unique and always processed

### Example Usage

```bash
curl -X POST https://localhost:5001/api/Payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "cardNumber": "2222405343248877",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "GBP",
    "amount": 1000,
    "cvv": "123"
  }'
```

### Response with Idempotency Key

All payment responses include the `idempotencyKey` field if one was provided:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000,
  "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000"
}
```

### Best Practices

1. **Generate Keys Client-Side**: Create idempotency keys before making the request
2. **Use UUIDs**: Recommended to use UUID v4 for uniqueness
3. **Reuse for Retries**: Use the same key for all retries of the same logical operation
4. **Don't Reuse Across Operations**: Each unique payment should have a different key
5. **Store Keys**: Keep track of idempotency keys to enable safe retries

### Idempotency Guarantees

- Works for all payment statuses: Authorized, Declined, and Rejected
- Prevents duplicate bank charges
- Returns the exact same response for duplicate requests
- No limit on retry window (keys are stored indefinitely in this implementation)

## Endpoints

### POST /api/Payments

Process a new payment request through the banking partner.

#### Request

**Method**: `POST`
**URL**: `/api/Payments`
**Content-Type**: `application/json`

**Headers** (optional):
- `Idempotency-Key`: String (UUID recommended) - Prevents duplicate payments

**Body Schema**:
```json
{
  "cardNumber": "string (14-19 digits, required)",
  "expiryMonth": "integer (1-12, required)",
  "expiryYear": "integer (YYYY, required, must be future)",
  "currency": "string (3 chars, required, USD|GBP|EUR)",
  "amount": "integer (1-2147483647, required)",
  "cvv": "string (3-4 digits, required)"
}
```

#### Success Response (200 OK)

**Condition**: Payment authorized by bank

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

**Condition**: Payment declined by bank

```json
{
  "id": "7b92c3d1-4a5e-4f6b-8c9d-1e2f3a4b5c6d",
  "status": "Declined",
  "cardNumberLastFour": "8878",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "USD",
  "amount": 5000
}
```

#### Validation Error Response (400 Bad Request)

**Condition**: Request validation failed

```json
{
  "id": "9c8d7e6f-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "status": "Rejected",
  "cardNumberLastFour": "1234",
  "expiryMonth": 6,
  "expiryYear": 2020,
  "currency": "GBP",
  "amount": 100,
  "errors": {
    "ExpiryYear": ["The expiry date must be in the future"]
  }
}
```

**Common Validation Errors**:
```json
{
  "errors": {
    "CardNumber": [
      "The CardNumber field is required.",
      "Card number must be between 14 and 19 digits"
    ],
    "ExpiryMonth": [
      "The field ExpiryMonth must be between 1 and 12."
    ],
    "ExpiryYear": [
      "The expiry date must be in the future"
    ],
    "Currency": [
      "Currency must be exactly 3 characters",
      "Supported currencies are USD, GBP, EUR"
    ],
    "Amount": [
      "The field Amount must be between 1 and 2147483647."
    ],
    "Cvv": [
      "CVV must be 3 or 4 digits"
    ]
  }
}
```

#### Error Response (503 Service Unavailable)

**Condition**: Bank simulator is unavailable

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "The banking service is currently unavailable. Please try again later."
}
```

#### cURL Example

```bash
curl -X POST https://localhost:5001/api/Payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248877",
    "expiryMonth": 12,
    "expiryYear": 2026,
    "currency": "GBP",
    "amount": 1000,
    "cvv": "123"
  }'
```

---

### GET /api/Payments/{id}

Retrieve details of a previously processed payment.

#### Request

**Method**: `GET`
**URL**: `/api/Payments/{id}`
**Parameters**:
- `id` (path, required): GUID of the payment (e.g., `3fa85f64-5717-4562-b3fc-2c963f66afa6`)

#### Success Response (200 OK)

**Condition**: Payment found

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

**Status Values**:
- `Authorized`: Payment approved by bank
- `Declined`: Payment rejected by bank
- `Rejected`: Payment failed validation (never sent to bank)

#### Error Response (404 Not Found)

**Condition**: Payment ID not found

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

#### cURL Example

```bash
curl -X GET https://localhost:5001/api/Payments/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

---

## Request Models

### PostPaymentRequest

| Field | Type | Required | Validation | Description |
|-------|------|----------|------------|-------------|
| cardNumber | string | Yes | Regex: `^\d{14,19}$` | Card number (14-19 digits, no spaces) |
| expiryMonth | integer | Yes | Range: 1-12 | Card expiry month |
| expiryYear | integer | Yes | Future date | Card expiry year (YYYY format) |
| currency | string | Yes | Length: 3, Values: USD\|GBP\|EUR | Payment currency (uppercase) |
| amount | integer | Yes | Range: 1-2147483647 | Amount in smallest currency unit (cents/pence) |
| cvv | string | Yes | Regex: `^\d{3,4}$` | Card verification value (3-4 digits) |

**Example**:
```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 4,
  "expiryYear": 2027,
  "currency": "EUR",
  "amount": 2500,
  "cvv": "456"
}
```

**Validation Rules**:

1. **Card Number**:
   - Must be 14-19 numeric digits
   - No spaces, dashes, or other formatting
   - Examples:
     - `2222405343248877` (16 digits - Visa/Mastercard)
     - `378282246310005` (15 digits - Amex)
     - `36227206271667` (14 digits - Diners)
     - `1234` (too short)
     - `2222-4053-4324-8877` (contains dashes)

2. **Expiry Date**:
   - Month must be 1-12
   - Combined month/year must be in the future
   - Current month/year is valid
   - Examples (assuming today is Jan 2025):
     - Month: 1, Year: 2025 (current month)
     - Month: 2, Year: 2025 (next month)
     - Month: 12, Year: 2026 (future)
     - Month: 12, Year: 2024 (past)
     - Month: 13, Year: 2025 (invalid month)

3. **Currency**:
   - Must be exactly 3 uppercase characters
   - Must be one of: USD, GBP, EUR
   - Examples:
     - `USD`
     - `GBP`
     - `EUR`
     - `usd` (lowercase)
     - `CAD` (not supported)
     - `US` (too short)

4. **Amount**:
   - Integer representing smallest currency unit
   - £10.00 = 1000 (pence)
   - $25.50 = 2550 (cents)
   - Must be between 1 and 2,147,483,647
   - Examples:
     - `100` (£1.00 or $1.00)
     - `1` (£0.01 or $0.01)
     - `999999` (£9,999.99 or $9,999.99)
     - `0` (too low)
     - `-100` (negative not allowed)

5. **CVV**:
   - Must be 3 or 4 numeric digits
   - 3 digits for Visa/Mastercard/Discover
   - 4 digits for American Express
   - Examples:
     - `123` (3 digits)
     - `4567` (4 digits)
     - `12` (too short)
     - `abc` (not numeric)

---

## Response Models

### PostPaymentResponse

| Field | Type | Description |
|-------|------|-------------|
| id | string (GUID) | Unique payment identifier |
| status | string | Payment status: `Authorized`, `Declined`, or `Rejected` |
| cardNumberLastFour | string | Last 4 digits of card number |
| expiryMonth | integer | Card expiry month (1-12) |
| expiryYear | integer | Card expiry year (YYYY) |
| currency | string | Payment currency (USD/GBP/EUR) |
| amount | integer | Amount in smallest currency unit |

**Example (Authorized)**:
```json
{
  "id": "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 3,
  "expiryYear": 2028,
  "currency": "USD",
  "amount": 15000
}
```

### GetPaymentResponse

Identical structure to `PostPaymentResponse`.

| Field | Type | Description |
|-------|------|-------------|
| id | string (GUID) | Unique payment identifier |
| status | string | Payment status: `Authorized`, `Declined`, or `Rejected` |
| cardNumberLastFour | string | Last 4 digits of card number |
| expiryMonth | integer | Card expiry month (1-12) |
| expiryYear | integer | Card expiry year (YYYY) |
| currency | string | Payment currency (USD/GBP/EUR) |
| amount | integer | Amount in smallest currency unit |

---

## Error Handling

### Error Response Format

All errors follow RFC 7807 Problem Details format:

```json
{
  "type": "string (URI reference)",
  "title": "string (short summary)",
  "status": "integer (HTTP status code)",
  "detail": "string (explanation)",
  "errors": {
    "FieldName": ["error message 1", "error message 2"]
  }
}
```

### Common Error Scenarios

#### 1. Missing Required Field

**Request**:
```json
{
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000,
  "cvv": "123"
}
```

**Response (400)**:
```json
{
  "id": "generated-guid",
  "status": "Rejected",
  "errors": {
    "CardNumber": ["The CardNumber field is required."]
  }
}
```

#### 2. Invalid Card Number Format

**Request**:
```json
{
  "cardNumber": "1234",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000,
  "cvv": "123"
}
```

**Response (400)**:
```json
{
  "id": "generated-guid",
  "status": "Rejected",
  "cardNumberLastFour": "1234",
  "errors": {
    "CardNumber": ["Card number must be between 14 and 19 digits"]
  }
}
```

#### 3. Expired Card

**Request**:
```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 1,
  "expiryYear": 2020,
  "currency": "GBP",
  "amount": 1000,
  "cvv": "123"
}
```

**Response (400)**:
```json
{
  "id": "generated-guid",
  "status": "Rejected",
  "cardNumberLastFour": "8877",
  "errors": {
    "ExpiryYear": ["The expiry date must be in the future"]
  }
}
```

#### 4. Multiple Validation Errors

**Request**:
```json
{
  "cardNumber": "123",
  "expiryMonth": 15,
  "expiryYear": 2020,
  "currency": "INVALID",
  "amount": -100,
  "cvv": "12345"
}
```

**Response (400)**:
```json
{
  "id": "generated-guid",
  "status": "Rejected",
  "errors": {
    "CardNumber": ["Card number must be between 14 and 19 digits"],
    "ExpiryMonth": ["The field ExpiryMonth must be between 1 and 12."],
    "ExpiryYear": ["The expiry date must be in the future"],
    "Currency": ["Currency must be exactly 3 characters"],
    "Amount": ["The field Amount must be between 1 and 2147483647."],
    "Cvv": ["CVV must be 3 or 4 digits"]
  }
}
```

#### 5. Bank Service Unavailable

**Request**: Valid payment request

**Response (503)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "The banking service is currently unavailable. Please try again later."
}
```

---

## Status Codes

| Code | Status | Description |
|------|--------|-------------|
| 200 | OK | Payment processed successfully (authorized or declined) |
| 400 | Bad Request | Validation failed - payment rejected |
| 404 | Not Found | Payment ID not found |
| 503 | Service Unavailable | Bank simulator is unavailable |

**Note**: Both authorized and declined payments return 200 OK. Check the `status` field in the response to determine the outcome.

---

## Examples

### Example 1: Successful Authorized Payment

**Request**:
```bash
POST /api/Payments
Content-Type: application/json

{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 5000,
  "cvv": "123"
}
```

**Response (200 OK)**:
```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 5000
}
```

**Explanation**: Card ending in odd digit (7) → Bank authorizes

---

### Example 2: Declined Payment

**Request**:
```bash
POST /api/Payments
Content-Type: application/json

{
  "cardNumber": "2222405343248878",
  "expiryMonth": 6,
  "expiryYear": 2027,
  "currency": "USD",
  "amount": 2500,
  "cvv": "456"
}
```

**Response (200 OK)**:
```json
{
  "id": "c9bf9e57-1685-4c89-bafb-ff5af830be8a",
  "status": "Declined",
  "cardNumberLastFour": "8878",
  "expiryMonth": 6,
  "expiryYear": 2027,
  "currency": "USD",
  "amount": 2500
}
```

**Explanation**: Card ending in even digit (8) → Bank declines

---

### Example 3: Rejected Payment (Invalid Currency)

**Request**:
```bash
POST /api/Payments
Content-Type: application/json

{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "CAD",
  "amount": 1000,
  "cvv": "123"
}
```

**Response (400 Bad Request)**:
```json
{
  "id": "2e9a8c7b-6d5e-4f3a-9b8c-1d0e9f8a7b6c",
  "status": "Rejected",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "CAD",
  "amount": 1000,
  "errors": {
    "Currency": ["Supported currencies are USD, GBP, EUR"]
  }
}
```

**Explanation**: CAD is not a supported currency → Validation fails → Payment rejected

---

### Example 4: Retrieve Payment

**Request**:
```bash
GET /api/Payments/f47ac10b-58cc-4372-a567-0e02b2c3d479
```

**Response (200 OK)**:
```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 5000
}
```

---

### Example 5: Retrieve Non-Existent Payment

**Request**:
```bash
GET /api/Payments/00000000-0000-0000-0000-000000000000
```

**Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

---

### Example 6: Multiple Payments Workflow

```bash
# 1. Process first payment (authorized)
POST /api/Payments
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "currency": "GBP",
  "amount": 1000,
  "cvv": "123"
}
# Response: { "id": "abc-123", "status": "Authorized", ... }

# 2. Process second payment (declined)
POST /api/Payments
{
  "cardNumber": "2222405343248878",
  "expiryMonth": 3,
  "expiryYear": 2027,
  "currency": "USD",
  "amount": 2000,
  "cvv": "456"
}
# Response: { "id": "def-456", "status": "Declined", ... }

# 3. Retrieve first payment
GET /api/Payments/abc-123
# Response: { "id": "abc-123", "status": "Authorized", ... }

# 4. Retrieve second payment
GET /api/Payments/def-456
# Response: { "id": "def-456", "status": "Declined", ... }
```

---

## Rate Limiting

**Current Version**: No rate limiting implemented

**Production Recommendation**: Implement rate limiting to prevent abuse:
- 100 requests per minute per IP address
- 429 Too Many Requests response when exceeded
- Retry-After header indicating wait time

