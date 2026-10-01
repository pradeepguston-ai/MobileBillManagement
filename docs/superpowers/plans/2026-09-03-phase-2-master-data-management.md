# Phase 2 Master Data Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver server-side paged master-data CRUD APIs and reusable React management screens for employees, organisation references, mobile allocations, and entitlements.

**Architecture:** API controllers only translate HTTP into application-service calls. Application services own validation, overlap checks, soft deactivation, pagination, and not-found/conflict handling; Infrastructure supplies EF Core implementations. The frontend has one generic CRUD page framework and field-specific form adapters, leaving server validation authoritative.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core 10, SQL Server Express, React 19, TypeScript, Vite, Material UI, xUnit, Vitest.

**Spec:** docs/SYSTEM_DESIGN.md and the approved Phase 2 requirements in this thread.

## Global Constraints

- All list APIs return `{ items, pageNumber, pageSize, totalCount, totalPages }`; filtering, searching and paging execute in SQL.
- Only soft/status deactivation is exposed. No master record is physically deleted or automatically cascades deactivation.
- `EffectiveFrom` is required and `EffectiveTo`, when supplied, must be on or after it.
- A mobile number cannot have overlapping active allocations; a mobile account cannot have overlapping entitlement periods. Application validation and the existing SQL Server triggers both enforce the rules.
- All money is C# `decimal` and SQL Server `decimal(18,2)`.
- Billing/history records remain unchanged; Phase 2 only edits current master records and effective-dated allocations/entitlements.

### Task 1: Application contracts and validation

**Files:**
- Create: `src/MobileBill.Application/Common/PagedResult.cs`, `PagedRequest.cs`, `ValidationException.cs`, `NotFoundException.cs`
- Create: `src/MobileBill.Application/MasterData/*Dto.cs`, `IMasterDataService.cs`, `MasterDataValidation.cs`
- Test: `tests/MobileBill.UnitTests/Application/MasterDataValidationTests.cs`

- [ ] Write failing tests for invalid effective dates and inclusive date overlaps.
- [ ] Run the unit-test filter and confirm the missing application types fail to compile.
- [ ] Add request/response DTOs for all eight resources, generic paged request/result contracts, and validation helpers.
- [ ] Re-run the filter and confirm date and overlap rules pass.

### Task 2: EF Core master-data service

**Files:**
- Create: `src/MobileBill.Infrastructure/MasterData/EfMasterDataService.cs`
- Modify: `src/MobileBill.Infrastructure/DependencyInjection.cs`
- Test: `tests/MobileBill.IntegrationTests/MasterDataApiTests.cs`

- [ ] Write failing API tests for create, search/pagination, active filtering, duplicate EPF, not-found updates, allocation overlap, entitlement overlap, and soft deactivation.
- [ ] Run the integration-test filter and confirm endpoint responses fail before implementation.
- [ ] Implement async EF queries and commands, projection to DTOs, duplicate-code/EPF checks, overlap queries, non-cascading deactivation checks, and clear conflict messages.
- [ ] Register `IMasterDataService` in DI and ensure every call accepts a cancellation token.
- [ ] Re-run integration tests and confirm all master-data API behaviours pass.

### Task 3: Thin API controllers and HTTP problem mapping

**Files:**
- Create: `src/MobileBill.Api/Controllers/EmployeesController.cs`, `MobileAccountsController.cs`, `MobileEntitlementsController.cs`, `FactoriesController.cs`, `DepartmentsController.cs`, `DesignationsController.cs`, `CategoriesController.cs`, `ProvidersController.cs`
- Create: `src/MobileBill.Api/ExceptionHandling/ApiExceptionHandler.cs`
- Modify: `src/MobileBill.Api/Program.cs`

- [ ] Add a failing API test asserting validation is HTTP 400, conflicts are HTTP 409, and missing IDs are HTTP 404.
- [ ] Implement shared controller base/helpers and resource-specific route contracts: GET list, GET item, POST create, PUT edit, and POST `/{id}/deactivate`.
- [ ] Add global exception handling, route Swagger metadata, and exception-handler registration.
- [ ] Re-run integration tests and verify the documented status codes and response shapes.

### Task 4: Reusable frontend master-data framework

**Files:**
- Create: `src/mobilebill-web/src/api/http.ts`, `masterDataApi.ts`, `types.ts`
- Create: `src/mobilebill-web/src/components/master-data/MasterDataTable.tsx`, `MasterDataToolbar.tsx`, `MasterDataDialog.tsx`, `DeactivateConfirmationDialog.tsx`, `LoadingState.tsx`, `ErrorState.tsx`
- Test: `src/mobilebill-web/src/components/master-data/*.test.tsx`

- [ ] Add failing Vitest tests for pagination actions, active filter/search propagation, form validation display, error state, and deactivation confirmation.
- [ ] Run the frontend test filter and confirm it fails before component creation.
- [ ] Implement the generic paged API hook and reusable MUI table, toolbar, dialogs, loading/error states, and validation messages.
- [ ] Re-run the frontend component tests and confirm they pass.

### Task 5: Resource-specific React pages and routing

**Files:**
- Create: `src/mobilebill-web/src/pages/master-data/EmployeesPage.tsx`, `MobileAllocationsPage.tsx`, `EntitlementsPage.tsx`, `FactoriesPage.tsx`, `DepartmentsPage.tsx`, `DesignationsPage.tsx`, `CategoriesPage.tsx`, `ProvidersPage.tsx`
- Modify: `src/mobilebill-web/src/App.tsx`, `src/mobilebill-web/src/layouts/AppLayout.tsx`
- Test: `src/mobilebill-web/src/pages/master-data/*.test.tsx`

- [ ] Add failing page tests for the allocation and entitlement columns plus create/edit action wiring.
- [ ] Implement page schemas and forms. Allocation rows show mobile number, EPF, employee, factory, department, dates and status. Entitlement rows show mobile number, employee, credit limit, rental, dates and status.
- [ ] Add all eight routes and navigation entries without duplicating list/dialog/deactivation mechanics.
- [ ] Re-run page tests and confirm required headings and interactions pass.

### Task 6: Verification and database confirmation

**Files:** No production feature files beyond fixes discovered by verification.

- [ ] Run `dotnet build MobileBill.sln --no-restore`.
- [ ] Run `dotnet test MobileBill.sln --no-restore`.
- [ ] Run `npm run lint`, `npm test`, and `npm run build` in `src/mobilebill-web`.
- [ ] Run `dotnet ef migrations has-pending-model-changes` and apply a migration only if model changes are required.
- [ ] Verify the SQL Server Express database remains reachable and report any intentionally deferred historical snapshot/authorisation risk.
