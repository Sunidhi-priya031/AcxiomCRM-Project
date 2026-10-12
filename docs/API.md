# AcxiomCRM REST API

Base URL (local): `http://localhost:5173`. All payloads are JSON; enum values are sent and returned as strings
(for example `"Status": "Active"`, `"Stage": "Proposal"`).

## Authentication

Every endpoint except `POST /api/auth/login` requires authentication, by either:

* **Bearer token** – obtained from `POST /api/auth/login`, sent as `Authorization: Bearer <accessToken>`; or
* **Browser session cookie** – a signed-in user of the web app can call the API from the same site.

Data returned is always limited to the caller's authorised scope:

| Role | Records returned |
|---|---|
| Admin | All records |
| Manager | Own records and records of the Sales Executives who report to them |
| Sales Executive | Records assigned to or created by them |

### `POST /api/auth/login` (public, rate limited per IP)

```json
{ "email": "rohan.das@acxiomcrm.local", "password": "…" }
```

`200 OK`
```json
{ "tokenType": "Bearer", "accessToken": "…", "expiresIn": 3600, "refreshToken": "…" }
```
`401` invalid credentials, inactive or locked account (same lockout policy as the web login) · `429` too many attempts.

### `POST /api/auth/logout` (authenticated)

Rotates the user's security stamp, so every token and session issued to that user stops working on its next request.
`200 { "message": "Signed out." }`

## Customers

| Method | Endpoint | Description | Success |
|---|---|---|---|
| GET | `/api/customers` | List/search. Query: `q` (name, email, phone, company, code), `status`, `assignedTo`, `sort`, `page`, `pageSize` (max 100) | 200 |
| GET | `/api/customers/{id}` | Get one customer | 200 |
| POST | `/api/customers` | Create | 201 + `Location` |
| PUT | `/api/customers/{id}` | Update | 200 |
| DELETE | `/api/customers/{id}` | Delete; deactivates instead when the customer has related records. **Admin/Manager only** | 200 |

Request body (`CustomerRequest`):

```json
{
  "customerName": "Ritu Agarwal",          // required, max 100
  "email": "ritu@example.com",             // required, valid email, unique
  "phone": "9831122334",                   // required, 10-digit mobile (6-9 start), unique
  "companyName": "Agarwal Electricals",    // max 150
  "address": "…", "city": "Balasore", "state": "Odisha",
  "status": "Active",                      // Active | Inactive
  "notes": "…",                            // max 1000
  "assignedTo": "<user id>"                // optional; defaults to the caller; must be a user the caller may assign to
}
```

Response (`CustomerDto`): `customerId, customerCode, customerName, email, phone, companyName, address, city, state, status,
notes, assignedTo {id, name}, createdBy {id, name}, createdDate, modifiedDate`.

`DELETE` response: `{ "id": 12, "result": "Deleted" | "Deactivated", "message": "…" }`.

## Leads

| Method | Endpoint | Description | Success |
|---|---|---|---|
| GET | `/api/leads` | List/search. Query: `q`, `status`, `source`, `priority`, `assignedTo`, `sort`, `page`, `pageSize` | 200 |
| GET | `/api/leads/{id}` | Get one lead | 200 |
| POST | `/api/leads` | Create | 201 |

Body (`LeadRequest`): `leadName` (required), `email`, `phone`, `companyName`, `source` (required: Website, Referral, SocialMedia,
EmailCampaign, ColdCall, TradeShow, Partner, Other), `status` (required: New, Contacted, Qualified, Unqualified – a new lead
cannot start as Converted or Lost), `priority` (Low, Medium, High), `expectedValue` (0 – 999,999,999,999), `notes`, `assignedTo`.

## Opportunities

| Method | Endpoint | Description | Success |
|---|---|---|---|
| GET | `/api/opportunities` | List/search. Query: `q`, `stage`, `status`, `customerId`, `assignedTo`, `sort`, `page`, `pageSize` | 200 |
| GET | `/api/opportunities/{id}` | Get one | 200 |
| POST | `/api/opportunities` | Create | 201 |

Body (`OpportunityRequest`): `opportunityName` (required), `customerId` (required, active customer in scope), `leadId`,
`amount` (required, **> 0**), `stage` (required; new opportunities: Qualification, Proposal or Negotiation),
`probability` (required, **0–100**), `expectedCloseDate` (required, **not in the past** for an open opportunity), `source`,
`notes`, `assignedTo`. The response includes `weightedAmount = amount × probability / 100`.

## Follow-ups

| Method | Endpoint | Description | Success |
|---|---|---|---|
| GET | `/api/followups` | List. Query: `view` (pending, today, upcoming, overdue), `status`, `type`, `assignedTo`, `from`, `to`, `related`, `page`, `pageSize` | 200 |
| GET | `/api/followups/{id}` | Get one | 200 |
| POST | `/api/followups` | Schedule | 201 |

Body (`FollowUpRequest`): `subject` (required), `followUpType` (Call, Email, Meeting, Visit), `followUpDate` (required,
**not earlier than today**), `remarks`, exactly one of `customerId` / `leadId` / `opportunityId`, `assignedTo`.

## Reports

`GET /api/reports/pipeline?from=&to=&assignedTo=` – stage-wise and owner-wise pipeline including weighted amounts.
Admin: all data, Manager: team, Sales Executive: own pipeline (`"scope": "All" | "Team" | "Own"`).

## Errors

All errors use RFC 7807 problem details; stack traces and database details are never returned.

| Status | When |
|---|---|
| 400 | Validation failed – `{ "title": "One or more validation errors occurred.", "status": 400, "errors": { "Amount": ["Opportunity Amount must be greater than 0."] } }` |
| 401 | Not authenticated, token expired/revoked, or account deactivated |
| 403 | Authenticated but not allowed (record outside scope, or role cannot perform the action) |
| 404 | Record does not exist |
| 409 | Duplicate customer email or phone – `errors` names the conflicting field |
| 429 | Login rate limit exceeded |
| 500 | Unexpected error – generic message only |

A ready-to-run request collection is in `AcxiomCRM.http` (Visual Studio / VS Code REST Client).
