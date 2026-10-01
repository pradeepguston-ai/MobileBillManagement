# Phase 5: Mobile-to-Employee Matching

## Scope

Phase 5 matches parsed `BillLine` records to effective-dated master data, creates historical `MonthlyBill` transactions, records review exceptions, and provides an exception-review API and React page. It excludes deductions, report generation, authentication, and master-data correction.

## Billing-period matching

For batch year/month, `PeriodStart` is the first calendar day and `PeriodEnd` is the last. Allocation and entitlement overlap is inclusive:

```text
EffectiveFrom <= PeriodEnd AND (EffectiveTo IS NULL OR EffectiveTo >= PeriodStart)
```

The matcher never selects earliest, latest, or month-end records where more than one overlaps.

## Automatic outcome

For each persisted line, query same-number allocations. Zero creates `MOBILE_NOT_FOUND`; more than one creates `MULTIPLE_ACTIVE_ALLOCATIONS`; exactly one proceeds. An inactive employee creates `EMPLOYEE_NOT_ACTIVE`. Entitlements are then evaluated separately using the same overlap rule: zero creates `ENTITLEMENT_NOT_FOUND`; multiple creates `MULTIPLE_ENTITLEMENTS`; exactly one creates a `MonthlyBill`.

`ZERO_BILL` is an information exception but does not discard the line. `PARSER_WARNING` is created for failed parser candidates or parser/batch warnings. No proration is performed.

## Historical transactions and resolution

`MonthlyBill` snapshots the employee identity, allocation and entitlement effective periods, credit limit, rental, and match methods. FKs remain for traceability but reports use transaction snapshots.

`MOBILE_NOT_FOUND` can be resolved by selecting only an existing `MobileAccount` with the same mobile number, including historical non-overlapping records. A meaningful comment is required. The override does not alter master data, dates, or entitlements. Entitlement matching then runs normally; a non-overlapping entitlement is never adopted automatically.

`BillExceptionResolution` stores the immutable override snapshot, original exception/billing period, selection data, comment, actor, and timestamp. `AuditLog` also records before/after resolution data. The monthly bill uses `AllocationMatchMethod.ManualHistoricalOverride` and remains marked manual after later master-data edits.

## Authorization

`ICurrentUserService` identifies the actor. `IBillReviewAuthorizationService` authorizes manual resolution in the application service. Development authorizes `dev-user` only; a production deny-all implementation fails closed until claims authorization exists. Unauthorized attempts return 403 and make no database changes.

## APIs and UI

- `POST /api/bill-batches/{id}/match`
- `GET /api/bill-batches/{id}/exceptions`
- `GET /api/bill-exceptions/{id}/mobile-account-candidates`
- `POST /api/bill-exceptions/{id}/resolve-mobile-account`

The React Exception Review page lists/filter exceptions, shows candidate details, requires a resolution comment, exposes matching methods/history, and treats authorization UI as convenience only.
