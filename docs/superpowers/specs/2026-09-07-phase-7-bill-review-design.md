# Phase 7: Monthly Bill Review

## Scope

Provide a batch-specific, read-only review screen at `/billing/{batchId}/review`. The billing period and batch metadata are read-only. The screen supports server-side rows, filtering, sorting, pagination, KPIs, and a historical-detail drawer. Approval workflow, authentication, Excel export, and cross-batch navigation are excluded.

## Read model

`IBillBatchReviewQueryService` is a read-only application boundary, independent from assessment commands. It projects EF Core queries with `AsNoTracking`; no per-row loading or client-side calculations are permitted.

- `GET /api/bill-batches/{batchId}/review`: batch metadata and full-batch KPI totals.
- `GET /api/bill-batches/{batchId}/review/rows`: server-side page/sort/filter results.
- `GET /api/bill-batches/{batchId}/review/rows/{monthlyBillId}`: drawer detail, verifying row ownership by batch.

Top KPIs are always full-batch values, independent of row filters. Company Responsibility Amount is the sum of `CalculatedExcess` for `ByCompany` rows; this presentation definition is an explicit Phase 7 assumption because the samples do not define it.

## Historical facts

`MonthlyBill` persists EPF, employee name, calling name, category, designation, factory, and department snapshots when matched. Review rows and drawer use transaction snapshots. Stored bill-line monetary components are the sole PDF charge breakdown. Live data is not mixed into historical content.

## Filters and sort

Rows support factory, department, category, responsibility (`Unassessed`, `ByUser`, `ByCompany`), exception (`All`, `HasException`, `NoException`), row status, and trimmed mobile/EPF/name/calling-name search. Sorting is whitelisted, uses known query expressions only, and falls back to MobileNumber for unsupported names. Pagination defaults to 20, caps at 100, and returns total count/pages.

## UI

The React page uses KPI cards, disabled billing-period metadata, Material UI filter controls, a sticky table header inside a horizontal scroll container, responsive density, currency formatting, exception highlighting, loading/empty/error states, and a responsive detail drawer. The drawer states “No approval history yet” only when stored approval history is empty and displays only existing audit events.

