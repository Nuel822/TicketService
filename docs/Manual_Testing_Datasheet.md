# Ticket Service API — Manual Testing Data Sheet

**Base URL:** `http://localhost:5000` (or as configured in `launchSettings.json`)  
**Content-Type:** `application/json`  
**Error Format:** RFC 7807 `application/problem+json`

---

## Table of Contents

1. [Global Response Reference](#1-global-response-reference)
2. [POST /api/events — Create Event](#2-post-apievents--create-event)
3. [PUT /api/events/{id} — Update Event](#3-put-apieventsid--update-event)
4. [POST /api/events/{eventId}/tickets — Purchase Ticket](#4-post-apieventseventidtickets--purchase-ticket)
5. [Supporting GET Endpoints (Quick Reference)](#5-supporting-get-endpoints-quick-reference)

---

## 1. Global Response Reference

| HTTP Status | Title | Trigger |
|---|---|---|
| `200 OK` | — | Successful read or purchase |
| `201 Created` | — | Event successfully created |
| `204 No Content` | — | Event successfully deleted |
| `404 Not Found` | `Not Found` | Entity with given ID does not exist |
| `409 Conflict` | `Conflict` | Domain rule violation (oversell, invalid state, concurrency) |
| `422 Unprocessable Entity` | `Validation Failed` | FluentValidation failure on request body |
| `499` | — | Client closed the connection (no body) |
| `500 Internal Server Error` | `Internal Server Error` | Unhandled exception |

**Standard 422 Response Shape:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/events",
  "errors": {
    "FieldName": ["Error message 1", "Error message 2"]
  }
}
```

**Standard 404 / 409 Response Shape:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Event with identifier 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx' was not found.",
  "instance": "/api/events/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

---

## 2. POST /api/events — Create Event

**Endpoint:** `POST /api/events`  
**Rate Limit Policy:** `writes`  
**Validation:** FluentValidation (`CreateEventValidator`)

### 2.1 Valid Request — Minimal (Single Tier)

**Test ID:** `CE-01`  
**Description:** Create a basic event with one pricing tier where tier quantity equals total capacity.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "An outdoor music festival.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "name": "General Admission",
      "price": 49.99,
      "quantity": 500
    }
  ]
}
```

**Expected Response:** `201 Created`
```json
{
  "id": "<<generated-guid>>",
  "name": "Summer Music Festival",
  "description": "An outdoor music festival.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 500,
  "availableTickets": 500,
  "pricingTiers": [
    {
      "id": "<<generated-guid>>",
      "name": "General Admission",
      "price": 49.99,
      "totalQuantity": 500,
      "availableQuantity": 500
    }
  ],
  "createdAt": "<<timestamp>>"
}
```

---

### 2.2 Valid Request — Multiple Tiers

**Test ID:** `CE-02`  
**Description:** Create an event with multiple pricing tiers. Sum of tier quantities must equal `totalCapacity`.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Tech Conference 2026",
  "description": "Annual developer conference.",
  "venue": "ExCeL London, Royal Victoria Dock",
  "date": "2026-11-20",
  "time": "09:00:00",
  "totalCapacity": 1000,
  "pricingTiers": [
    {
      "name": "Early Bird",
      "price": 99.00,
      "quantity": 200
    },
    {
      "name": "Standard",
      "price": 149.00,
      "quantity": 600
    },
    {
      "name": "VIP",
      "price": 299.00,
      "quantity": 200
    }
  ]
}
```

**Expected Response:** `201 Created`  
All three tiers returned with generated IDs. `availableTickets` = 1000.

---

### 2.3 Valid Request — Free Event (Price = 0)

**Test ID:** `CE-03`  
**Description:** Tier price of `0` is allowed (free event).

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Community Open Day",
  "description": "Free entry for all.",
  "venue": "Town Hall, Manchester",
  "date": "2026-09-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    {
      "name": "Free Entry",
      "price": 0,
      "quantity": 100
    }
  ]
}
```

**Expected Response:** `201 Created`  
Tier price returned as `0`.

---

### 2.4 Invalid — Missing Required Field: `name`

**Test ID:** `CE-04`  
**Description:** `name` is empty string — should fail validation.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "",
  "description": "Test event.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "12:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "Name": ["Event name is required."]
  }
}
```

---

### 2.5 Invalid — Missing Required Field: `venue`

**Test ID:** `CE-05`  
**Description:** `venue` is empty string.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Test Event",
  "description": "Test.",
  "venue": "",
  "date": "2026-10-01",
  "time": "12:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "Venue": ["Venue is required."]
  }
}
```

---

### 2.6 Invalid — Event Date in the Past

**Test ID:** `CE-06`  
**Description:** `date` is set to a past date.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Past Event",
  "description": "This already happened.",
  "venue": "Old Venue",
  "date": "2020-01-01",
  "time": "10:00:00",
  "totalCapacity": 50,
  "pricingTiers": [
    { "name": "Standard", "price": 20.00, "quantity": 50 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "Date": ["Event date must be in the future."]
  }
}
```

---

### 2.7 Invalid — `totalCapacity` is Zero

**Test ID:** `CE-07`  
**Description:** `totalCapacity` must be greater than zero.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Zero Capacity Event",
  "description": "No one can attend.",
  "venue": "Nowhere",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 0,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 0 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "TotalCapacity": ["Total capacity must be greater than zero."],
    "PricingTiers[0].Quantity": ["Tier quantity must be greater than zero."]
  }
}
```

---

### 2.8 Invalid — No Pricing Tiers

**Test ID:** `CE-08`  
**Description:** `pricingTiers` array is empty.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "No Tiers Event",
  "description": "Missing tiers.",
  "venue": "Some Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": []
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PricingTiers": ["At least one pricing tier is required."]
  }
}
```

---

### 2.9 Invalid — Tier Quantities Do Not Sum to `totalCapacity`

**Test ID:** `CE-09`  
**Description:** Cross-field validation: sum of tier quantities (300) ≠ `totalCapacity` (500).

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Mismatched Capacity Event",
  "description": "Tier quantities don't add up.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    { "name": "Standard", "price": 25.00, "quantity": 200 },
    { "name": "VIP", "price": 75.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "": ["The sum of all pricing tier quantities must equal the total capacity."]
  }
}
```

---

### 2.10 Invalid — Negative Tier Price

**Test ID:** `CE-10`  
**Description:** Tier price is negative (below zero).

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Negative Price Event",
  "description": "Invalid pricing.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": -5.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PricingTiers[0].Price": ["Tier price must be zero or greater."]
  }
}
```

---

### 2.11 Invalid — `name` Exceeds 200 Characters

**Test ID:** `CE-11`  
**Description:** `name` field exceeds maximum length of 200 characters.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "description": "Too long name.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```
*(The `name` value above is 202 characters — 'A' × 202)*

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "Name": ["Event name must not exceed 200 characters."]
  }
}
```

---

### 2.12 Invalid — Multiple Validation Errors Simultaneously

**Test ID:** `CE-12`  
**Description:** Multiple fields fail at once — verifies all errors are returned together.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "",
  "description": "",
  "venue": "",
  "date": "2020-06-01",
  "time": "10:00:00",
  "totalCapacity": 0,
  "pricingTiers": []
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "Name": ["Event name is required."],
    "Venue": ["Venue is required."],
    "Date": ["Event date must be in the future."],
    "TotalCapacity": ["Total capacity must be greater than zero."],
    "PricingTiers": ["At least one pricing tier is required."]
  }
}
```

---

### 2.13 Invalid — Malformed JSON Body

**Test ID:** `CE-13`  
**Description:** Request body is not valid JSON.

**Request:**
```http
POST /api/events
Content-Type: application/json

{ "name": "Bad JSON", "venue": }
```

**Expected Response:** `400 Bad Request` (ASP.NET Core JSON parsing error)

---

### 2.14 Invalid — Tier Name Exceeds 100 Characters

**Test ID:** `CE-14`  
**Description:** Tier `name` exceeds maximum length of 100 characters.

**Request:**
```http
POST /api/events
Content-Type: application/json

{
  "name": "Valid Event",
  "description": "Test.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    {
      "name": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
      "price": 10.00,
      "quantity": 100
    }
  ]
}
```
*(Tier name is 101 characters)*

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PricingTiers[0].Name": ["Tier name must not exceed 100 characters."]
  }
}
```

---

## 3. PUT /api/events/{id} — Update Event

**Endpoint:** `PUT /api/events/{id}`
**Rate Limit Policy:** `writes`
**Validation:** FluentValidation (`UpdateEventValidator`)

> ⚠️ **Important — Request shape differs from GET response shape:**

> To **add** a new tier, omit `existingTierId` entirely (or set it to `null`).
> `existingTierId` is a **lookup-only** key — the tier's database identity cannot be changed.

**Tier quantity rule:** The sum of all tier `quantity` values must **not exceed** `totalCapacity`.
It is allowed to be less — the event manager may expand individual tiers at any time.

> **Pre-condition:** Use the `id` returned from a successful `CE-01` or `CE-02` test.
> Replace `{VALID_EVENT_ID}` with an actual GUID from a created event.
> Replace `{TIER_ID}` with an actual pricing tier GUID from the created event.

---

### 3.1 Valid Request — Update Event Details Only

**Test ID:** `UE-01`
**Description:** Update name, description, venue, date, and time. Re-submit existing tier with its `existingTierId` to preserve it.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival (Updated)",
  "description": "Updated description for the festival.",
  "venue": "Victoria Park, London",
  "date": "2026-08-20",
  "time": "19:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 54.99,
      "quantity": 500
    }
  ]
}
```

**Expected Response:** `200 OK`
```json
{
  "id": "{VALID_EVENT_ID}",
  "name": "Summer Music Festival (Updated)",
  "venue": "Victoria Park, London",
  "date": "2026-08-20",
  "time": "19:00:00",
  "totalCapacity": 500,
  "availableTickets": 500,
  "pricingTiers": [
    {
      "id": "{TIER_ID}",
      "name": "General Admission",
      "price": 54.99,
      "totalQuantity": 500,
      "availableQuantity": 500
    }
  ]
}
```

---

### 3.2 Valid Request — Add a New Pricing Tier

**Test ID:** `UE-02`
**Description:** Add a new tier (no `existingTierId` field) alongside an existing tier. The new tier is created and appended.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival (Updated)",
  "description": "Now with VIP.",
  "venue": "Victoria Park, London",
  "date": "2026-08-20",
  "time": "19:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 49.99,
      "quantity": 400
    },
    {
      "name": "VIP",
      "price": 149.99,
      "quantity": 100
    }
  ]
}
```

**Expected Response:** `200 OK`
Response includes both tiers. The new VIP tier has a freshly generated `id`.

---

### 3.3 Valid Request — Update Existing Tier Price

**Test ID:** `UE-03`
**Description:** Update only the price of an existing tier by supplying its `existingTierId`.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "An outdoor music festival.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 59.99,
      "quantity": 500
    }
  ]
}
```

**Expected Response:** `200 OK`
Tier price updated to `59.99`.

---

### 3.4 Invalid — Event ID Does Not Exist

**Test ID:** `UE-04`  
**Description:** PUT with a valid GUID format that does not correspond to any event.

**Request:**
```http
PUT /api/events/00000000-0000-0000-0000-000000000001
Content-Type: application/json

{
  "name": "Ghost Event",
  "description": "Does not exist.",
  "venue": "Nowhere",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "existingTierId": null, "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `404 Not Found`
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Event with identifier '00000000-0000-0000-0000-000000000001' was not found.",
  "instance": "/api/events/00000000-0000-0000-0000-000000000001"
}
```

---

### 3.5 Invalid — Non-GUID Event ID in URL

**Test ID:** `UE-05`  
**Description:** URL contains a non-GUID string where a GUID is expected.

**Request:**
```http
PUT /api/events/not-a-valid-guid
Content-Type: application/json

{
  "name": "Test",
  "description": "",
  "venue": "Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `400 Bad Request` (route constraint `{id:guid}` rejects the request)

---

### 3.6 Invalid — Reduce Capacity Below Tickets Already Sold

**Test ID:** `UE-06`  
**Description:** After tickets have been purchased for an event, attempt to reduce `totalCapacity` below the number of tickets already sold. This triggers a domain rule violation.

**Pre-condition:** Create an event (CE-01), purchase some tickets (PT-01), then attempt this update.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "Reduced capacity.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 1,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 49.99,
      "quantity": 1
    }
  ]
}
```

**Expected Response:** `409 Conflict`
```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "<<domain rule message about capacity>>",
  "instance": "/api/events/{VALID_EVENT_ID}"
}
```

---

### 3.7 Invalid — Empty Request Body

**Test ID:** `UE-07`
**Description:** PUT with no body at all.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json
```

**Expected Response:** `400 Bad Request`

---

### 3.8 Invalid — Tier Quantities Exceed `totalCapacity`

**Test ID:** `UE-08`
**Description:** Sum of tier quantities (600 + 600 = 1200) exceeds `totalCapacity` (1000). Returns `422`.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "Over-allocated tiers.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 1000,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 49.99,
      "quantity": 600
    },
    {
      "name": "VIP",
      "price": 99.99,
      "quantity": 600
    }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "": ["The sum of all pricing tier quantities must not exceed the total capacity."]
  }
}
```

---

### 3.9 Valid — Tier Quantities Less Than `totalCapacity`

**Test ID:** `UE-09`
**Description:** Sum of tier quantities (300) is less than `totalCapacity` (1000). This is allowed — the event manager may expand tiers later.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "Partially allocated.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 1000,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 49.99,
      "quantity": 300
    }
  ]
}
```

**Expected Response:** `200 OK`
`availableTickets` = 300 (sum of tier available quantities). `totalCapacity` = 1000.

---

### 3.10 Invalid — Wrong Field Name (`totalQuantity` instead of `quantity`)

**Test ID:** `UE-10`
**Description:** Sending `totalQuantity` (the GET response field name) instead of `quantity` (the PUT request field name). The `quantity` field defaults to `0`, which fails the `GreaterThan(0)` validation rule.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Paris Fashion Week",
  "description": "A show of the finest arts",
  "venue": "Paris Hall Arena",
  "date": "2026-07-01",
  "time": "18:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "VIP",
      "price": 99.99,
      "totalQuantity": 500
    }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "PricingTiers[0].Quantity": ["Tier quantity must be greater than zero."]
  }
}
```

> **Note:** `totalQuantity` is silently ignored by the deserialiser. `quantity` defaults to `0`.

---

### 3.11 Invalid — Missing `name` Field

**Test ID:** `UE-11`
**Description:** `name` is empty — fails the `NotEmpty` validation rule.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "",
  "description": "Missing name.",
  "venue": "Test Venue",
  "date": "2026-10-01",
  "time": "10:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "Standard", "price": 10.00, "quantity": 100 }
  ]
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "Name": ["Event name is required."]
  }
}
```

---

### 3.12 Invalid — Reduce Tier Quantity Below Already-Sold Count (Tier-Level Oversell)

**Test ID:** `UE-12`
**Description:** After tickets have been purchased for a specific tier, attempt to reduce that tier's `quantity` below the number already sold. The domain guard in `PricingTier.Update()` throws `OversellException`, which maps to `409 Conflict`.

This is distinct from `UE-06` (event-level capacity reduction). Here the event `totalCapacity` is unchanged — only the individual tier's `quantity` is reduced below its sold count.

**Pre-condition:**
1. Create an event with a "General Admission" tier of quantity 100 (CE-01).
2. Purchase 50 tickets against that tier (PT-01, quantity = 50).
3. Note the tier's `{TIER_ID}` from the GET response.

**Request:**
```http
PUT /api/events/{VALID_EVENT_ID}
Content-Type: application/json

{
  "name": "Summer Music Festival",
  "description": "An outdoor music festival.",
  "venue": "Hyde Park, London",
  "date": "2026-08-15",
  "time": "18:00:00",
  "totalCapacity": 500,
  "pricingTiers": [
    {
      "existingTierId": "{TIER_ID}",
      "name": "General Admission",
      "price": 49.99,
      "quantity": 30
    }
  ]
}
```

> **Why this triggers:** 50 tickets are already sold on this tier. Setting `quantity` to 30 means `30 < 50 sold` — the domain throws `OversellException`.

**Expected Response:** `409 Conflict`
```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot purchase 50 ticket(s) for tier 'General Admission'. Only 30 ticket(s) available.",
  "instance": "/api/events/{VALID_EVENT_ID}"
}
```

> **Contrast with UE-06:** UE-06 tests the event-level capacity guard (`Event.Update()`). UE-12 tests the tier-level guard (`PricingTier.Update()`). Both throw `OversellException` → `409 Conflict`, but the error message references the tier name and sold count.

---

## 4. POST /api/events/{eventId}/tickets — Purchase Ticket

**Endpoint:** `POST /api/events/{eventId}/tickets`  
**Rate Limit Policy:** `purchases`  
**Validation:** FluentValidation (`PurchaseTicketValidator`)  
**Optional Header:** `Idempotency-Key: <string>`

> **Pre-condition:** A valid event must exist. Use `{VALID_EVENT_ID}` and `{TIER_ID}` from a previously created event.

---

### 4.1 Valid Request — Single Ticket Purchase

**Test ID:** `PT-01`  
**Description:** Purchase 1 ticket with all required fields.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Alice Johnson",
  "purchaserEmail": "alice.johnson@example.com",
  "quantity": 1
}
```

**Expected Response:** `200 OK`
```json
{
  "ticketId": "<<generated-guid>>",
  "eventId": "{VALID_EVENT_ID}",
  "pricingTierId": "{TIER_ID}",
  "tierName": "General Admission",
  "purchaserName": "Alice Johnson",
  "purchaserEmail": "alice.johnson@example.com",
  "quantity": 1,
  "unitPrice": 49.99,
  "totalPrice": 49.99,
  "status": 1,
  "purchasedAt": "<<timestamp>>"
}
```
> `status: 1` = `Active`

---

### 4.2 Valid Request — Maximum Quantity (10 Tickets)

**Test ID:** `PT-02`  
**Description:** Purchase the maximum allowed quantity of 10 tickets in a single transaction.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Bob Smith",
  "purchaserEmail": "bob.smith@example.com",
  "quantity": 10
}
```

**Expected Response:** `200 OK`  
`quantity: 10`, `totalPrice` = `unitPrice × 10`.

---

### 4.3 Valid Request — With Idempotency Key

**Test ID:** `PT-03`  
**Description:** Purchase with an `Idempotency-Key` header. Sending the same request twice with the same key should return the same result without creating a duplicate ticket.

**Request (first call):**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json
Idempotency-Key: idem-key-abc-123

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Carol White",
  "purchaserEmail": "carol.white@example.com",
  "quantity": 2
}
```

**Expected Response (first call):** `200 OK` — ticket created.

**Request (second call — identical):**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json
Idempotency-Key: idem-key-abc-123

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Carol White",
  "purchaserEmail": "carol.white@example.com",
  "quantity": 2
}
```

**Expected Response (second call):** `200 OK` — **same `ticketId`** as first call (idempotent replay, no new ticket created).

---

### 4.4 Valid Request — Free Tier (Price = 0)

**Test ID:** `PT-04`  
**Description:** Purchase a ticket for a free event tier.

**Pre-condition:** Create event CE-03 first.

**Request:**
```http
POST /api/events/{FREE_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{FREE_TIER_ID}",
  "purchaserName": "Dave Brown",
  "purchaserEmail": "dave.brown@example.com",
  "quantity": 1
}
```

**Expected Response:** `200 OK`  
`unitPrice: 0`, `totalPrice: 0`.

---

### 4.5 Invalid — Event ID Does Not Exist

**Test ID:** `PT-05`  
**Description:** Purchase for a non-existent event.

**Request:**
```http
POST /api/events/00000000-0000-0000-0000-000000000099/tickets
Content-Type: application/json

{
  "pricingTierId": "00000000-0000-0000-0000-000000000001",
  "purchaserName": "Eve Adams",
  "purchaserEmail": "eve@example.com",
  "quantity": 1
}
```

**Expected Response:** `404 Not Found`
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Event with identifier '00000000-0000-0000-0000-000000000099' was not found.",
  "instance": "/api/events/00000000-0000-0000-0000-000000000099/tickets"
}
```

---

### 4.6 Invalid — Pricing Tier Does Not Belong to Event

**Test ID:** `PT-06`  
**Description:** Supply a valid `pricingTierId` that belongs to a *different* event.

**Pre-condition:** Create two events (CE-01 and CE-02). Use `{EVENT_1_ID}` with `{TIER_FROM_EVENT_2_ID}`.

**Request:**
```http
POST /api/events/{EVENT_1_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_FROM_EVENT_2_ID}",
  "purchaserName": "Frank Green",
  "purchaserEmail": "frank@example.com",
  "quantity": 1
}
```

**Expected Response:** `404 Not Found`
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "PricingTier with identifier '{TIER_FROM_EVENT_2_ID}' was not found.",
  "instance": "/api/events/{EVENT_1_ID}/tickets"
}
```

---

### 4.7 Invalid — Quantity Exceeds Available Inventory (Oversell)

**Test ID:** `PT-07`  
**Description:** Attempt to purchase more tickets than are available in the tier.

**Pre-condition:** Create an event with only 5 tickets in a tier. Purchase all 5 (PT-01 × 5 or PT-02 with quantity 5). Then attempt to purchase 1 more.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Grace Lee",
  "purchaserEmail": "grace@example.com",
  "quantity": 1
}
```

**Expected Response:** `409 Conflict`
```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot purchase 1 ticket(s) for tier 'General Admission'. Only 0 ticket(s) available.",
  "instance": "/api/events/{VALID_EVENT_ID}/tickets"
}
```

---

### 4.8 Invalid — Quantity = 0

**Test ID:** `PT-08`  
**Description:** `quantity` of 0 fails validation (must be at least 1).

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Henry Ford",
  "purchaserEmail": "henry@example.com",
  "quantity": 0
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "Quantity": ["Quantity must be at least 1."]
  }
}
```

---

### 4.9 Invalid — Quantity Exceeds Maximum (> 10)

**Test ID:** `PT-09`  
**Description:** `quantity` of 11 exceeds the per-transaction maximum of 10.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Iris Chan",
  "purchaserEmail": "iris@example.com",
  "quantity": 11
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "Quantity": ["A maximum of 10 tickets can be purchased per transaction."]
  }
}
```

---

### 4.10 Invalid — Missing `purchaserName`

**Test ID:** `PT-10`  
**Description:** `purchaserName` is empty string.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "",
  "purchaserEmail": "test@example.com",
  "quantity": 1
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PurchaserName": ["Purchaser name is required."]
  }
}
```

---

### 4.11 Invalid — Invalid Email Format

**Test ID:** `PT-11`  
**Description:** `purchaserEmail` is not a valid email address.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Jack Black",
  "purchaserEmail": "not-an-email",
  "quantity": 1
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PurchaserEmail": ["A valid email address is required."]
  }
}
```

---

### 4.12 Invalid — Missing `purchaserEmail`

**Test ID:** `PT-12`  
**Description:** `purchaserEmail` is empty string.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Karen White",
  "purchaserEmail": "",
  "quantity": 1
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PurchaserEmail": ["Purchaser email is required."]
  }
}
```

---

### 4.13 Invalid — Missing `pricingTierId` (Empty GUID)

**Test ID:** `PT-13`  
**Description:** `pricingTierId` is the empty GUID (all zeros), which fails the `NotEmpty` rule.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "00000000-0000-0000-0000-000000000000",
  "purchaserName": "Leo King",
  "purchaserEmail": "leo@example.com",
  "quantity": 1
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PricingTierId": ["Pricing tier ID is required."]
  }
}
```

---

### 4.14 Invalid — Multiple Validation Errors Simultaneously

**Test ID:** `PT-14`  
**Description:** All fields fail validation at once.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "00000000-0000-0000-0000-000000000000",
  "purchaserName": "",
  "purchaserEmail": "bad-email",
  "quantity": 0
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "title": "Validation Failed",
  "status": 422,
  "errors": {
    "PricingTierId": ["Pricing tier ID is required."],
    "PurchaserName": ["Purchaser name is required."],
    "PurchaserEmail": ["A valid email address is required."],
    "Quantity": ["Quantity must be at least 1."]
  }
}
```

---

### 4.15 Invalid — `purchaserName` Exceeds 200 Characters

**Test ID:** `PT-15`  
**Description:** `purchaserName` exceeds the 200-character maximum.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "purchaserEmail": "test@example.com",
  "quantity": 1
}
```
*(Name is 202 characters)*

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "PurchaserName": ["Purchaser name must not exceed 200 characters."]
  }
}
```

---

### 4.16 Invalid — Negative Quantity

**Test ID:** `PT-16`  
**Description:** `quantity` is a negative number.

**Request:**
```http
POST /api/events/{VALID_EVENT_ID}/tickets
Content-Type: application/json

{
  "pricingTierId": "{TIER_ID}",
  "purchaserName": "Mary Jane",
  "purchaserEmail": "mary@example.com",
  "quantity": -3
}
```

**Expected Response:** `422 Unprocessable Entity`
```json
{
  "errors": {
    "Quantity": ["Quantity must be at least 1."]
  }
}
```

---

## 5. Supporting GET Endpoints (Quick Reference)

These endpoints are primarily used to set up state for POST/PUT tests and to verify results.

| Test ID | Method | URL | Purpose | Expected Status |
|---|---|---|---|---|
| `GE-01` | GET | `/api/events?page=1&pageSize=20` | List all events (paginated) | `200 OK` |
| `GE-02` | GET | `/api/events/{VALID_EVENT_ID}` | Get event by valid ID | `200 OK` |
| `GE-03` | GET | `/api/events/00000000-0000-0000-0000-000000000001` | Get event by non-existent ID | `404 Not Found` |
| `GE-04` | GET | `/api/events/{VALID_EVENT_ID}/tickets/availability` | Get ticket availability | `200 OK` |
| `GE-05` | GET | `/api/events/00000000-0000-0000-0000-000000000001/tickets/availability` | Availability for non-existent event | `404 Not Found` |
| `GE-06` | GET | `/api/reports/events/{VALID_EVENT_ID}/sales` | Sales report for event | `200 OK` |
| `GE-07` | GET | `/api/reports/events/sales?page=1&pageSize=20` | All sales reports (paginated) | `200 OK` |

### GE-04 Availability Response Shape

> ⚠️ **The availability endpoint returns a buyer-focused subset of data.**
> `totalQuantity`, `soldQuantity`, and `totalCapacity` are **intentionally omitted** — those are internal
> inventory metrics available only via the sales report (`GE-06`).

**Expected `200 OK` body for `GE-04`:**
```json
{
  "eventId": "<<event-guid>>",
  "eventName": "Summer Music Festival",
  "totalAvailable": 450,
  "tiers": [
    {
      "id": "<<tier-guid>>",
      "name": "General Admission",
      "price": 49.99,
      "availableQuantity": 450
    }
  ]
}
```

**Fields returned:**

| Field | Description |
|---|---|
| `eventId` | GUID of the event |
| `eventName` | Display name of the event |
| `totalAvailable` | Total tickets available across all tiers |
| `tiers[].id` | Use this as `pricingTierId` in the purchase request |
| `tiers[].name` | Display name of the tier |
| `tiers[].price` | Price per ticket for this tier |
| `tiers[].availableQuantity` | Remaining tickets in this tier |

**Fields intentionally absent (use sales report for these):**

| Field | Why omitted |
|---|---|
| `totalCapacity` | Internal capacity management — not buyer-relevant |
| `tiers[].totalQuantity` | Internal allocation — not buyer-relevant |
| `tiers[].soldQuantity` | Internal sales metric — exposed via `/api/reports/events/{id}/sales` |

---

## 6. End-to-End Scenario Flows

### Scenario A — Full Happy Path

| Step | Action | Test ID | Expected |
|---|---|---|---|
| 1 | Create event with 3 tiers (200 + 600 + 200 = 1000 capacity) | CE-02 | `201 Created` |
| 2 | Verify event exists | GE-02 | `200 OK` |
| 3 | Check availability (all 1000 available) | GE-04 | `200 OK`, `availableQuantity` = capacity |
| 4 | Purchase 10 Early Bird tickets | PT-02 | `200 OK`, `totalPrice` = `99.00 × 10` |
| 5 | Check availability (190 Early Bird remaining) | GE-04 | `200 OK`, Early Bird `availableQuantity` = 190 |
| 6 | Update event name and VIP price | UE-03 | `200 OK` |
| 7 | Check sales report | GE-06 | `200 OK` (eventually consistent) |

---

### Scenario B — Oversell Prevention

| Step | Action | Test ID | Expected |
|---|---|---|---|
| 1 | Create event with 2-ticket tier | CE-01 (capacity=2) | `201 Created` |
| 2 | Purchase 2 tickets | PT-01 (qty=2) | `200 OK` |
| 3 | Attempt to purchase 1 more | PT-07 | `409 Conflict` — oversell message |

---

### Scenario C — Idempotency Key Replay

| Step | Action | Test ID | Expected |
|---|---|---|---|
| 1 | Create event | CE-01 | `201 Created` |
| 2 | Purchase with `Idempotency-Key: test-key-001` | PT-03 (1st call) | `200 OK`, ticket created |
| 3 | Repeat identical request with same key | PT-03 (2nd call) | `200 OK`, **same `ticketId`** |
| 4 | Check availability — only 1 ticket consumed | GE-04 | `availableQuantity` reduced by 1, not 2 |

---

### Scenario D — Delete Event with Active Tickets (409)

| Step | Action | Expected |
|---|---|---|
| 1 | Create event (CE-01) | `201 Created` |
| 2 | Purchase 1 ticket (PT-01) | `200 OK` |
| 3 | `DELETE /api/events/{VALID_EVENT_ID}` | `409 Conflict` — "Event cannot be deleted because it has active ticket holders." |
| 4 | (Cancel all tickets first — if endpoint exists) | — |
| 5 | `DELETE /api/events/{VALID_EVENT_ID}` | `204 No Content` |

---

*Document generated: 2026-03-01 | Ticket Service API v1*