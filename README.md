# AcxiomCRM

Role-based CRM built with **ASP.NET Core 8 MVC**, **Entity Framework Core**, **ASP.NET Core Identity**, **Bootstrap 5** and **Chart.js**.
Covers the full lead → follow-up → opportunity → customer lifecycle with validation, security, audit logging, REST APIs, dashboard and reports.

## Run it

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore
dotnet run
```

Open the HTTPS URL printed in the console. The SQLite database (`acxiomcrm.db`) and seed data are created automatically on first run.
To use SQL Server instead, set `"DatabaseProvider": "SqlServer"` and a SQL Server `DefaultConnection` in `appsettings.json`.

### Demo accounts (seeded)

| Role | Email | Password |
|---|---|---|
| Admin | admin@acxiomcrm.com | Admin@12345 |
| Manager | manager@acxiomcrm.com | Manager@12345 |
| SalesExecutive | sales@acxiomcrm.com | Sales@12345 |

Self-registration (`/Account/Register`) always creates a **SalesExecutive**. Change/remove the demo accounts before any real deployment.

## Project structure

```
Controllers/        MVC controllers (+ Controllers/Api = REST controllers)
Models/             Entities, enums, NotPastDate validator, Identity user
ViewModels/         Dashboard, report, account and user view models
Dtos/               API DTOs (entities are never exposed)
Data/               ApplicationDbContext, DbSeeder
Services/           AuditService, CustomerService, LeadService (workflow), Scope (role data-scoping)
Views/              Razor views per module
wwwroot/            site.css, site.js (custom client-side date rule)
```

## Requirement coverage

| Requirement | Implementation |
|---|---|
| Authentication (Login/Register/Logout) | `AccountController`, ASP.NET Core Identity cookies (HttpOnly, SameSite, Secure outside dev, 30 min sliding) |
| Password hashing / policy / lockout | Identity hasher; min 8 + upper/lower/digit/symbol; 5 failed attempts → 15 min lockout; admin unlock |
| Roles & server-side authorization | `[Authorize(Roles=…)]`, global `AuthorizeFilter`, per-record `Scope.CanAccess` / `ForUser` (Sales Executives only see assigned records; URL tampering → 403) |
| Anti-forgery | `AutoValidateAntiforgeryTokenAttribute` globally + `ValidateAntiForgeryToken` on auth forms |
| SQL injection | EF Core LINQ only (parameterised) |
| Client-side validation | jQuery validation/unobtrusive: Required, Email, Phone (`^[6-9]\d{9}$`), Length, Range, Date (custom `notpast` rule) |
| Server-side validation | DataAnnotations + `[NotPastDate]` + service checks (duplicates, FK existence/scope, assignee, lead workflow); API re-validates everything |
| Business rules | Amount > 0, Probability 0–100, close date not past (active only), follow-up date not before today (planned only), lead status workflow, unique email/phone, duplicate customer (name + company) |
| Customers / Leads / Opportunities / Follow-Ups / Activities | Full CRUD (customers soft-deleted), search + filter + pagination, lead → customer/opportunity conversion (transactional), customer history |
| Dashboard | KPI cards, date filters (All/Today/Week/Month/Custom), Chart.js: lead status, opportunity pipeline, monthly sales; admin security stats |
| User & Role management | Admin-only: create/edit/activate/deactivate, role change, password reset, unlock, roles + permission matrix pages |
| Audit log | Login/failed login/lockout/logout/register, create/update/delete, status & role changes, password reset, lead conversion, follow-up completion/reschedule; old/new values, IP; append-only (DbContext rejects edits/deletes; no UI to alter); Admin filter by user/module/action/date |
| Reports | Customer, Lead, Follow-up, Opportunity, Pipeline, Conversion, User Activity, Audit; filters, sort, pagination, CSV export (formula-injection safe); role-scoped |
| REST API | See below |

## REST API

Cookie-authenticated (sign in via `POST /api/auth/login`, rate-limited to 10/min/IP). Unauthenticated → `401`, forbidden → `403` (no redirects). DTOs only; no password hashes or tokens are ever returned. Errors are `ProblemDetails` JSON without stack traces.

| Method | Endpoint | Notes |
|---|---|---|
| POST | /api/auth/login, /api/auth/logout | |
| GET/POST | /api/customers | list (`q`, `page`, `pageSize`) / create (`201`, `409` on duplicate) |
| GET/PUT/DELETE | /api/customers/{id} | DELETE = deactivate (Admin/Manager) |
| GET/POST | /api/leads, GET /api/leads/{id} | |
| GET/POST | /api/opportunities, GET /api/opportunities/{id} | |
| GET/POST | /api/followups, GET /api/followups/{id} | |
| GET | /api/reports/pipeline | Admin/Manager |

Example:
```bash
curl -k -c jar -H "Content-Type: application/json" -d '{"email":"admin@acxiomcrm.com","password":"Admin@12345"}' https://localhost:5001/api/auth/login
curl -k -b jar https://localhost:5001/api/customers
```
(API endpoints skip the MVC antiforgery token; the cookie is `SameSite=Lax`, which blocks cross-site state-changing requests.)

## Final acceptance scenario – how to check

1. Open any CRM URL logged out → redirected to login.
2. Register / log in → Dashboard.
3. New Customer with bad email/phone → inline client-side errors, submit blocked.
4. Disable JS (or craft a POST) → server still rejects.
5. Opportunity amount ≤ 0, probability 101, past close date (active stage) → rejected.
6. Follow-up dated yesterday → rejected.
7. Log in as each role and compare menus / data; paste another user's record URL as Sales Executive → 403.
8. Admin → Audit Log shows entries for every action above.
9. `GET /api/customers` after API login → JSON.
10. Dashboard shows KPI cards and the three charts.

## Notes
- The UI loads Bootstrap, jQuery validation and Chart.js from CDNs (internet needed in the browser).
- The schema is created with `EnsureCreated` for easy setup; switch to EF migrations (`dotnet ef migrations add Initial`) for production.
- Use HTTPS and a secrets store for connection strings in production.
