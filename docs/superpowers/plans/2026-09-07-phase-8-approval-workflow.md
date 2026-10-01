# Phase 8: Approval Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add sequential IT, HR, and Finance approvals plus a separately authorized batch lock.

**Architecture:** `IBillApprovalWorkflowService` owns state changes and server-owned history. Capability-based authorization, current user, and clock abstractions are injected into it. The React review page calls only command APIs and renders status-sensitive actions.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core 10/SQL Server, React, TypeScript, Material UI, xUnit, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-07-phase-8-approval-workflow-design.md`

## Global Constraints

- Controllers and React must never choose workflow roles, actor identity, timestamps, or batch statuses.
- Production authorization remains deny-all until a claims-based implementation replaces it.
- `Locked` batch business commands must fail with a consistent conflict error.
- Money remains decimal and all command I/O remains async with cancellation tokens.

---

### Task 1: Workflow contracts, history schema, and authorization capabilities

**Files:**
- Modify: `src/MobileBill.Domain/Enums/ApprovalStage.cs`, `ApprovalDecision.cs`, `BillBatchStatus.cs`
- Create: `src/MobileBill.Domain/Enums/WorkflowRole.cs`, `BillWorkflowAction.cs`
- Modify: `src/MobileBill.Domain/Entities/ApprovalHistory.cs`
- Modify: `src/MobileBill.Infrastructure/Persistence/Configurations/EntityConfigurations.cs`
- Modify: `src/MobileBill.Application/Billing/IBillReviewAuthorizationService.cs`
- Modify: `src/MobileBill.Infrastructure/Billing/DevelopmentBillReviewAuthorizationService.cs`
- Test: `tests/MobileBill.UnitTests/Billing/BillApprovalWorkflowServiceTests.cs`

**Interfaces:**
- Produces `BillWorkflowAction`, `WorkflowRole`, and durable `ApprovalHistory` action/status/identity fields.
- Produces `Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken)`.

- [ ] Write failing tests that assert development `dev-user` authorizes workflow capabilities and any other user is denied.
- [ ] Run the focused test and confirm it fails because the capability interface does not exist.
- [ ] Add the enums, extended authorization contract, development/deny-all implementations, and history fields/configuration.
- [ ] Run the focused test and confirm it passes.

### Task 2: Approval command service and immutable lock controls

**Files:**
- Create: `src/MobileBill.Application/Billing/BillApprovalWorkflowContracts.cs`
- Create: `src/MobileBill.Application/Billing/IBillApprovalWorkflowService.cs`
- Create: `src/MobileBill.Infrastructure/Billing/EfBillApprovalWorkflowService.cs`
- Modify: `src/MobileBill.Infrastructure/DependencyInjection.cs`
- Modify: `src/MobileBill.Application/Billing/BillingExceptions.cs`
- Modify: `src/MobileBill.Infrastructure/Billing/EfBillAssessmentService.cs`, `EfBillMatchingService.cs`, `EfBillExceptionReviewService.cs`, `EfBillBatchService.cs`
- Test: `tests/MobileBill.UnitTests/Billing/BillApprovalWorkflowServiceTests.cs`

**Interfaces:**
- Consumes `ICurrentUserService`, `IClock`, `IBillReviewAuthorizationService`.
- Produces `SubmitAsync`, `DecideAsync`, and `LockAsync` command operations.

- [ ] Write failing tests for Validated submit, IT/HR/Finance sequence, Finance-to-Completed, skipped-stage rejection, denied mutation, lock prerequisites, and lock audit identity/time.
- [ ] Run the focused test and confirm it fails because the service is absent.
- [ ] Implement transactional command methods, server-derived roles, approval history, lock audit, and locked-batch guard helpers.
- [ ] Run the focused tests and confirm they pass.

### Task 3: Thin HTTP API and database migration

**Files:**
- Create: `src/MobileBill.Api/Controllers/BillBatchWorkflowController.cs`
- Modify: `src/MobileBill.Api/ExceptionHandling/ApiExceptionHandler.cs`
- Create: `src/MobileBill.Infrastructure/Persistence/Migrations/<timestamp>_Phase8ApprovalWorkflow.cs`
- Test: `tests/MobileBill.IntegrationTests/Billing/BillBatchWorkflowApiTests.cs`

**Interfaces:**
- Produces the three command endpoints described in the design.
- Uses only request action/comment fields; actors/roles/times/statuses are server owned.

- [ ] Write failing API tests verifying server-owned history metadata and 403/409 behavior.
- [ ] Run the focused API test and confirm it fails because endpoints are absent.
- [ ] Add thin controller actions, exception mappings, and generate the migration.
- [ ] Run migration model-pending check and focused API tests.

### Task 4: Review-page workflow controls

**Files:**
- Create: `src/mobilebill-web/src/api/billWorkflowApi.ts`
- Modify: `src/mobilebill-web/src/pages/MonthlyBillReviewPage.tsx`
- Test: `src/mobilebill-web/src/pages/MonthlyBillReviewPage.test.tsx`

**Interfaces:**
- Consumes batch status from review summary and workflow command endpoints.
- Produces status-sensitive submit/decision/lock controls and an explicit lock confirmation dialog.

- [ ] Write failing UI tests for Completed lock visibility, confirmation copy, and no workflow role selector.
- [ ] Run the focused Vitest file and confirm it fails before controls exist.
- [ ] Add API client and Material UI controls; reload the batch summary after a successful action.
- [ ] Run focused Vitest and confirm it passes.

### Task 5: Final verification and migration handoff

**Files:**
- Modify: `docs/superpowers/specs/2026-09-07-phase-8-approval-workflow-design.md` only if test evidence changes a documented rule.

- [ ] Run `dotnet build MobileBill.sln --no-restore`.
- [ ] Run `dotnet test MobileBill.sln --no-restore --logger "console;verbosity=minimal"`.
- [ ] Run `npm run build`, `npm run lint`, and `npm test` from `src/mobilebill-web`.
- [ ] Run `dotnet-ef migrations has-pending-model-changes` and report the local SQL Server database-update command.
