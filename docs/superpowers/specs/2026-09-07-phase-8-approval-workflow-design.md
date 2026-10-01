# Phase 8: Approval Workflow Design

## Scope

Implement a server-enforced approval and period-lock workflow for a validated bill batch. It covers submission, sequential IT/HR/Finance decisions, period locking, immutable history, development-only workflow authorization, and a small set of actions on the existing batch review page.

Reopening is intentionally out of scope. A future reopen action must require elevated authorization, a reason, an audit event, and a transition from `Locked` to `Completed`.

## Status state machine

```text
Validated --Submit--> ITReview --IT Approve--> HRApproval --HR Approve--> FinanceApproval
FinanceApproval --Finance Approve--> Completed --Lock--> Locked

ITReview | HRApproval | FinanceApproval --Reject or ReturnForCorrection--> Validated
```

`Completed` means the three approval stages passed and the batch is awaiting formal closure. It remains readable and may be used by later report-generation work. It is not locked.

`Locked` means immutable for business commands. Read, review, report, and export operations remain allowed. Lock requires a `Completed` batch, passed validation, no unresolved blocking exceptions, every `MonthlyBill` assessed, and a recorded Finance approval.

`Submit` is valid only from `Validated`. `Approve`, `Reject`, and `ReturnForCorrection` are valid only for the current stage implied by the batch status. Rejection and return both return the batch to `Validated`; their separately recorded actions preserve their distinct audit meaning. The supplied requirements do not establish distinct long-lived rejection/correction statuses.

## Domain model and audit history

Extend `ApprovalHistory` with server-owned workflow evidence:

- `WorkflowRole`: `ITReviewer`, `HRApprover`, `FinanceApprover`, or `PeriodLocker`.
- `Action`: `Submit`, `Approve`, `Reject`, or `ReturnForCorrection`.
- `UserId` and `DisplayName` from `ICurrentUserService`.
- `PreviousStatus` and `NewStatus`.
- existing stage, timestamp, and optional comment.

`ApprovalHistory.Id` is its immutable approval-history identifier. The client submits only an action/comment command; it cannot supply a role, user, display name, timestamps, or statuses.

Lock is a lifecycle action, not a Finance approval. It writes one `AuditLog` record using `IClock` and `ICurrentUserService`; no synthetic approval row is created. This avoids duplicating the lock in two inconsistent audit models.

## Application services and authorization

Create a dedicated `IBillApprovalWorkflowService` command service. Controllers call it, but do not select roles or change `BillBatch.Status` themselves. The service:

1. Loads the batch and verifies the state-machine transition.
2. Verifies workflow authorization by capability.
3. Checks lock prerequisites when locking.
4. Writes `ApprovalHistory` or `AuditLog`, sets state/audit metadata through `IClock` and `ICurrentUserService`, and saves atomically.

Extend `IBillReviewAuthorizationService` with a generic capability check over `BillWorkflowAction` (`Submit`, `ApproveIt`, `ApproveHr`, `ApproveFinance`, `Reject`, `ReturnForCorrection`, `Lock`). The workflow service determines the required capability from the current batch state; no role is client controlled.

In Development, only `dev-user` may perform every Phase 8 action. Development history records `Development User` as identity and the server-derived workflow role appropriate to the action. Production uses the existing deny-all implementation until a claims-based implementation is added. It fails closed.

**Temporary segregation-of-duties limitation:** development authorization allows one development identity to execute all workflow stages for end-to-end testing. Production must replace it with claims/role-based authorization and segregation-of-duties policy before use.

## Edit guards

Business command services reject updates whenever the relevant batch is `Locked`:

- bill assessment;
- matching;
- exception resolution, including historical allocation overrides;
- upload, parse, and validation.

Existing master-data effective dating and `MonthlyBill` snapshots preserve historical values independently of batch status. No controller or React visibility check is treated as the lock enforcement boundary.

## API

```text
POST /api/bill-batches/{batchId}/workflow/submit
POST /api/bill-batches/{batchId}/workflow/decision
POST /api/bill-batches/{batchId}/lock
```

The decision request contains only `action` (`Approve`, `Reject`, `ReturnForCorrection`) and optional `comment`. The active stage is inferred from status. Submit accepts an optional comment. Lock has no writable actor/role/time fields.

Errors use the project ProblemDetails convention:

- 400: invalid command values;
- 403: identified but unauthorized user;
- 404: batch not found;
- 409: invalid state transition, unmet lock prerequisite, or locked batch edit.

## React

The batch-specific review header displays the status and, according to the server-reported status, exposes only the relevant action controls:

- `Validated`: Submit for IT Review;
- `ITReview`, `HRApproval`, `FinanceApproval`: Approve, Reject, Return for Correction;
- `Completed`: Lock Billing Period;
- `Locked`: read-only status.

The user cannot choose a role. Lock uses a confirmation dialog with the approved immutability warning. API authorization and state validation remain mandatory.

## Tests

Backend unit/integration tests cover sequential transitions, correct recorded role, user/time ownership, denied action non-mutation, skipped-stage rejection, Completed-not-Locked Finance outcome, lock prerequisites, lock audit, and locked-command guards. Frontend tests cover status-sensitive controls and lock confirmation; they do not assert authorization as a security boundary.

## Rules not proven by the supplied samples

- The samples do not establish mandatory comments for rejection or return actions; Phase 8 captures optional comments.
- The samples do not define a separate long-lived `Rejected` or `CorrectionRequired` status; both actions return to `Validated` and retain their action history.
- Production workflow-role mapping and segregation-of-duties policy require a future claims/identity decision.
