# Phase 6: Monthly Bill Calculation and Assessment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide one decimal-only calculation source of truth and an authorised, audited final-deduction assessment workflow.

**Architecture:** A domain calculation value object produces the four calculated fields. An application contract and infrastructure service assess existing monthly bills using the review-authorization, current-user, and clock abstractions; the API controller remains a thin transport layer. EF persists assessment and override evidence, and `AuditLog` preserves every decision transition.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10/SQL Server, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-05-phase-6-monthly-bill-calculation-design.md`

## Global Constraints

- Use `decimal` and SQL Server `decimal(18,2)` for all financial values.
- Responsibility business values are only `ByUser` and `ByCompany`; null means unassessed.
- `FinalDeduction` is zero until assessment but that zero never signifies assessed status.
- Authorized assessment uses `IBillReviewAuthorizationService`; actor/time come only from `ICurrentUserService` and `IClock`.
- Do not implement approvals, authentication, Excel reporting, or a Phase 6 UI.

### Task 1: Calculation source of truth

**Files:**
- Create: `src/MobileBill.Domain/Calculations/MonthlyBillCalculation.cs`
- Test: `tests/MobileBill.UnitTests/Domain/MonthlyBillCalculationTests.cs`

**Produces:** `MonthlyBillCalculation.Create(decimal totalDueAmount, decimal creditLimit, decimal monthlyRental)` returning `ActualBill`, `AvailableEntitlement`, `Variance`, and `CalculatedExcess`.

- [ ] Write tests for under, exact, above, zero bill, zero credit/rental/entitlement, negative credit adjustment, and decimal precision.
- [ ] Run the calculation tests and confirm they fail because `MonthlyBillCalculation` does not exist.
- [ ] Implement the decimal-only formulas and rerun the tests.

### Task 2: Persisted assessment evidence

**Files:**
- Modify: `src/MobileBill.Domain/Entities/MonthlyBill.cs`
- Modify: `src/MobileBill.Infrastructure/Persistence/Configurations/EntityConfigurations.cs`
- Create migration: `src/MobileBill.Infrastructure/Persistence/Migrations/*_Phase6MonthlyBillAssessment.cs`

**Produces:** nullable server-owned `AssessedAt`, `AssessedBy`, `DeductionOverrideReason`, `DeductionOverrideBy`, and `DeductionOverrideAt` fields, with appropriate max lengths and date/decimal configuration.

- [ ] Add persistence-model tests that demonstrate an unassessed monthly bill has null assessment fields and zero final deduction.
- [ ] Generate the migration from the updated EF model without applying it to SQL Server.

### Task 3: Authorised assessment application service

**Files:**
- Create: `src/MobileBill.Application/Billing/BillAssessmentContracts.cs`
- Modify: `src/MobileBill.Application/Billing/BillingExceptions.cs`
- Create: `src/MobileBill.Infrastructure/Billing/EfBillAssessmentService.cs`
- Modify: `src/MobileBill.Infrastructure/DependencyInjection.cs`
- Test: `tests/MobileBill.UnitTests/Billing/BillAssessmentServiceTests.cs`

**Consumes:** `IBillReviewAuthorizationService`, `ICurrentUserService`, `IClock`, `MonthlyBillCalculation`.

**Produces:** `IBillAssessmentService.AssessAsync(Guid monthlyBillId, AssessMonthlyBillRequest request, CancellationToken)`.

- [ ] Write failing service tests for ByCompany, ByUser default, lower and zero overrides, missing reason, negative/above-excess rejections, reassessment audit, authorization denial, and server-generated identity/time.
- [ ] Implement authorization before mutation, calculate/recalculate from stored `BillLine` and entitlement snapshots, validate the request, persist audit fields, and write an `AuditLog` containing old/new responsibility and deduction.
- [ ] Run focused tests and ensure all assessment rules pass.

### Task 4: Thin API and verification

**Files:**
- Create: `src/MobileBill.Api/Controllers/MonthlyBillsController.cs`
- Modify: `src/MobileBill.Api/ExceptionHandling/ApiExceptionHandler.cs` only if a dedicated not-found exception is introduced.
- Test: `tests/MobileBill.IntegrationTests/Billing/MonthlyBillAssessmentApiTests.cs`

- [ ] Add a thin `PUT /api/monthly-bills/{id}/assessment` endpoint that accepts no audit identity fields.
- [ ] Verify a client cannot supply server audit fields through the DTO and that application authorization remains mandatory.
- [ ] Run `dotnet build MobileBill.sln`, `dotnet test MobileBill.sln`, `npm run build`, `npm run lint`, and configured frontend tests.
