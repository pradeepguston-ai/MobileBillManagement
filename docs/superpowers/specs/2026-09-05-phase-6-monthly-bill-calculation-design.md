# Phase 6: Monthly Bill Calculation and Assessment

## Scope

Phase 6 calculates each matched `MonthlyBill` from its persisted bill line and entitlement snapshot, then allows an authorised bill reviewer to assess responsibility and final payroll deduction. It does not add approvals, authentication, report export, or unrelated UI work.

## Calculation source of truth

`MonthlyBillCalculation` is a domain value object/service and is the only component that calculates monetary values. It accepts decimals only:

```text
ActualBill            = BillLine.TotalDueAmount
AvailableEntitlement  = MonthlyCreditLimit + MonthlyRental
Variance              = AvailableEntitlement - ActualBill
CalculatedExcess      = max(0, ActualBill - AvailableEntitlement)
```

`FinalDeduction` is not derived automatically by the calculation. Matching creates `MonthlyBill` with the calculation values, `Responsibility = null`, and `FinalDeduction = 0.00`.

## Assessment state and responsibility

`Responsibility` remains nullable until assessment, but its only business values are `ByUser` and `ByCompany`. Null means unassessed; it is not a `Pending` responsibility.

`AssessedAt` and `AssessedBy` are server-owned fields. The UI/reporting uses them to distinguish an unassessed zero deduction from an assessed zero deduction.

On assessment:

- `ByCompany` sets `FinalDeduction = 0.00`.
- `ByUser` defaults `FinalDeduction` to `CalculatedExcess` when no value is supplied.
- A ByUser value different from `CalculatedExcess` is an override, including `0.00`, and requires a reason.
- Deductions must be between `0.00` and `CalculatedExcess`, inclusive.
- A ByUser calculation with `CalculatedExcess = 0.00` needs no override reason when the final value is `0.00`.
- Any reassessment requires a reason, including a responsibility change.

## Audit and authorization

`IBillReviewAuthorizationService` is invoked by the assessment application service before data is loaded or changed. `ICurrentUserService` and `IClock` populate actor/timestamps. Client requests contain only `Responsibility`, optional `FinalDeduction`, and optional `Reason`; audit identity/timestamps are never client writable.

Each assessment records a durable transaction snapshot of the effective final deduction override (`DeductionOverrideAmount`, `DeductionOverrideReason`, `DeductionOverrideBy`, `DeductionOverrideAt`) when applicable, plus an `AuditLog` before/after JSON entry for every initial assessment and reassessment.

## API

`PUT /api/monthly-bills/{id}/assessment` accepts an assessment request. Controllers forward DTOs only; `IBillAssessmentService` owns validation, authorization, persistence, and audit creation. A small assessment UI is excluded from this phase because API behavior is fully exercised through backend tests.
