# Phase 5: Mobile-to-Employee Matching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task.

**Goal:** Match parsed bill lines to effective-dated allocations and entitlements, preserving exceptions, transaction snapshots, manual overrides, and audit history.

**Architecture:** `IBillMatchingService` owns inclusive calendar-month matching and `IBillExceptionReviewService` owns authorized manual resolution. Infrastructure uses EF Core transactions, current user/clock abstractions, and a deny-by-default review authorization service; API and React are thin clients.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/SQL Server, React/TypeScript/Vite/Material UI, xUnit/Vitest.

**Spec:** `docs/superpowers/specs/2026-09-05-phase-5-mobile-employee-matching-design.md`

## Global Constraints

- Inclusive period overlap only; no latest/earliest/month-end guessing and no proration.
- Never modify master data while resolving a billing transaction.
- Preserve zero bills and failed parser candidates.
- Manual resolution requires same-number allocation and meaningful comment.
- Enforce authorization in application services; production fails closed.

### Task 1: Domain snapshots, exceptions, and authorization contracts

**Files:** Modify `BillException.cs`, `MonthlyBill.cs`, `AuditLog.cs`, configurations; create match-method/exception-type enums, `BillExceptionResolution.cs`, `IBillReviewAuthorizationService.cs`, matching/review DTOs and interfaces; add migration.

- [ ] Write failing tests for inclusive overlap, match methods, snapshot persistence, and a manual-resolution record.
- [ ] Add nullable/immutable transaction snapshots for EPF/name/mobile, allocation/entitlement dates, and match methods; add resolution entity with original type/month/comment/actor/time.
- [ ] Add string-backed exception-type enum values: MOBILE_NOT_FOUND, EMPLOYEE_NOT_ACTIVE, ENTITLEMENT_NOT_FOUND, MULTIPLE_ACTIVE_ALLOCATIONS, MULTIPLE_ENTITLEMENTS, ZERO_BILL, PARSER_WARNING.
- [ ] Generate `Phase5BillMatching` migration and run focused tests.

### Task 2: Automatic matcher

**Files:** Create Application matching contracts/service; create Infrastructure `EfBillMatchingService.cs`; register DI; test `BillMatchingServiceTests.cs`.

- [ ] Write failing tests for each zero/one/multiple allocation and entitlement outcome, inactive employee, zero bill, and parser warning.
- [ ] Calculate period boundaries with `DateOnly(year, month, 1)` and `DateOnly.DaysInMonth`; query overlap with the approved predicate.
- [ ] Create monthly transactions only for one allocation, active employee, one entitlement; snapshot every selected value and mark Automatic.
- [ ] Persist all exceptions and transaction changes atomically; reject repeat matching after results exist.
- [ ] Run matching tests.

### Task 3: Authorized transaction-only exception resolution

**Files:** Create Development/DenyAll authorization services; create `EfBillExceptionReviewService.cs`; update DI/error handler; test authorization and resolution behavior.

- [ ] Write failing tests: dev-user allowed, non-dev denied, denial changes neither MonthlyBill nor exception/audit, comment required, candidate same-number restriction.
- [ ] Implement development-only registration and deny-all production registration.
- [ ] Resolve `MOBILE_NOT_FOUND` only after `CanResolveExceptionsAsync`; select a same-number historical allocation, create resolution/audit snapshots, mark exception Resolved, then perform normal entitlement matching.
- [ ] Run focused tests.

### Task 4: APIs and exception review UI

**Files:** Create `BillMatchingController.cs`; add exception DTOs; create `ExceptionReviewPage.tsx` and API hooks/types; update `App.tsx` and `AppLayout.tsx`; add backend integration and Vitest coverage.

- [ ] Write failing API tests for match, exception paging, candidate restriction, 403 resolution, and successful resolution.
- [ ] Implement four endpoints with ProblemDetails and no writable audit fields.
- [ ] Build Material UI table/filter/detail dialog; require comment and show manual match method/snapshot. Disable resolution based on supplied authorization capability only.
- [ ] Run API and frontend tests.

### Task 5: Verification

- [ ] Confirm no JWT, Entra, master-data mutation, entitlement override, proration, reporting, or deduction scope was added.
- [ ] Run `dotnet build MobileBill.sln`, `dotnet test MobileBill.sln`, `npm run build`, `npm run lint`, and configured frontend tests.
