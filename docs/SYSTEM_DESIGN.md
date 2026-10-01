# Mobile Bill Management System - Proposed Design

> **Implementation update (2026-09-22):** Mobile allocation and monthly entitlement are one master-data record. A mobile allocation stores the mobile number, selected employee, monthly credit limit, and monthly rental in `MobileAccounts`. The separate `MobileEntitlements` table and screen were removed. New allocations have no effective-date fields; assessed `MonthlyBills` retain their monetary snapshots. The historical proposal below predates this change.

## 1. Scope and evidence

This is a proposed production design for processing monthly corporate telecom bills from PDF upload through review, approvals, Excel export, and period lock. It is based only on:

- AGENTS.md, which supplies the required workflow and calculation rules.
- docs/samples/sample-bill.pdf, a ten-page telecom invoice and summary for corporate code PR48799679.
- docs/samples/sample-report.xlsx, the current manual report.

Facts that cannot be established from those sources are explicitly marked **Confirmation required**. They must not be encoded as assumed business policy.

### Observed reconciliation

| Measure | PDF | Manual report | Result |
| --- | ---: | ---: | --- |
| Account rows | 259 | 258 | One PDF row is not in the report |
| Total due / Actual Bill | LKR 390,096.74 | LKR 390,096.74 | Reconciles exactly |
| Unmatched PDF account | 768791861 | Not present | Its Total Due Amount is LKR 0.00 |

The PDF's zero-value unmatched row proves that a balanced monetary total alone is not evidence of complete processing. The system must store every parsed PDF line and raise an exception for unmatched accounts, even when their total due is zero.

The invoice first page establishes the period and invoice evidence: Billing Period
15 July 2026 to 14 August 2026, Date of Supply 14 August 2026, and Date of
Invoice 14 August 2026. The parser must extract these values from that first
page. Corporate code remains stored source evidence but is not displayed in
the final report.

## 2. Business process

The authoritative workflow is:

~~~text
PDF Upload
  -> deterministic parse
  -> validation and reconciliation
  -> mobile-number-to-employee matching
  -> entitlement snapshot and calculation
  -> exception review and resolution
  -> IT check
  -> HR approval
  -> Finance approval
  -> final Excel generation
  -> billing-period lock
~~~

Only a batch with a balanced source total, no blocking unresolved exceptions, and the required approvals may be exported or locked. A locked batch is immutable; correction requires a formally controlled revision or reopening, retaining the original audit history.

## 3. PDF fields

The source is a text-based tabular summary. The parser should use deterministic text/table extraction first, never OCR where a usable text layer exists. Alongside parsed values, the application must retain original raw text, page number, parse status, and parser error details.

| PDF field | Type | Notes |
| --- | --- | --- |
| Corporate Code | text | Header; sample value PR48799679 |
| Mobile / Account Number | identifier string | Never treat as a monetary number |
| Previous Due Amount | decimal | Source component |
| Payments | decimal | Source component |
| Total Usage Charges | decimal | Source component |
| IDD | decimal | Source component |
| Roaming | decimal | Source component |
| Value Added Services (VAS) | decimal | Source component |
| Discounts | decimal | Source component |
| Bill Adjustments / Balance Transfers | decimal | Source component |
| Commitment Charges | decimal | Source component |
| Late Payment Charges | decimal | Source component |
| Add to Bill | decimal | Source component |
| Instalment Plans | decimal | Source component |
| Government Taxes & Levies | decimal | Source component |
| VAT | decimal | Source component |
| Charges for Bill Period | decimal | Different from Total Due in the sample |
| Total Due Amount | decimal | Required source of ActualBill |

The sample grand Total Due Amount is LKR 390,096.74. Its grand Charges for Bill Period is LKR 390,394.74. These are distinct source fields and must never be substituted.

## 4. Excel fields

The populated worksheet is Master- August1; Sheet1 is empty. Its data occupies rows 5-262 (258 records), totals are in row 264, and printed approval labels are in row 266.

| Column | Report field | Observed source / behaviour |
| --- | --- | --- |
| A | Serial | Report sequence |
| B | Mobile Phone | PDF Mobile / Account Number |
| C | EPF | Employee master data |
| D | Name | Employee master data |
| E | Category | Employee or entitlement master data |
| F | Designation | Employee master data |
| G | Factory | Organisation master data / employee assignment |
| H | Department | Organisation master data / employee assignment |
| I | Calling Name | Employee master data |
| J | Monthly Credit Limit (LKR) | Effective monthly entitlement |
| K | Monthly Rental (LKR) | Effective monthly entitlement or account plan |
| L | Actual Bill (LKR) | PDF Total Due Amount |
| M | Variance (LKR) | Formula J + K - L |
| N | Deduction | Final payroll deduction; formula -M for negative-variance rows, otherwise a dash |
| O | Deduction Status | By User or By Company |
| P | Unlabelled calculated column | Formula MAX(0, N) on every row |
| Q | Unlabelled notes | Only three notes: Roaming / misspelled Roming |

There are 51 negative-variance rows and all 51 receive an N-column formula. Of
these, 32 are marked By User and 19 By Company. The business has confirmed
that column N is the final payroll deduction. Keep the By User / By Company
status exactly as used in the current report; no additional responsibility
rules are required for the initial system. Columns P and Q are not required in
the generated report.

## 5. PDF-to-report mapping

| Report target | Mapping | Confidence |
| --- | --- | --- |
| Mobile Phone | PDF Mobile / Account Number | Proven |
| Actual Bill | PDF Total Due Amount | Proven: all 258 report values match and totals reconcile |
| Monthly Credit Limit | Effective entitlement master data | Required by workflow; absent from PDF |
| Monthly Rental | Effective entitlement/account-plan master data | Required by workflow; absent from PDF |
| Variance | MonthlyCreditLimit + MonthlyRental - ActualBill | Confirmed in AGENTS.md and Excel formulas |
| Calculated Excess | max(0, ActualBill - MonthlyCreditLimit - MonthlyRental) | Confirmed in AGENTS.md |
| Final payroll deduction | Negative variance expressed as a positive amount in the current report | Confirmed by business as report column N |
| Employee/organisation fields | Employee-mobile assignment and master data | Not present in PDF |
| Responsibility | By User / By Company, retained exactly as the current report uses it | Confirmed by business |
| Exceptional charge note | Manual reviewer note, when needed | Confirmed to be manual |

The report generated by the new system includes the existing named Deduction
column as final payroll deduction. It excludes the manual workbook's
unlabelled P and Q columns.

## 6. Master data required

All master data is effective-dated. A later employee transfer or limit change must not modify a completed assessment.

### Employee and organisation

- Employee: unique EPF number, legal/display name, calling name, active status.
- Organisation assignment: category, designation, factory, department, effective dates.
- Telecom account: provider, mobile/account number, corporate code as source evidence, active status.
- Employee-mobile assignment: employee, telecom account, effective-from and effective-to dates; no overlapping active account assignments.

### Entitlement and policy

- Employee-mobile entitlement: monthly credit limit, monthly rental, currency, effective date range. This is the approved allowance for a mobile number that populates report columns J and K. Its date range preserves historical report calculations when the allowance changes later.
- Responsibility decision values: Pending, By User, By Company.
- Provider, factory, department, category, and report-branding reference data.

For each bill assessment, snapshot the matched employee, organisation fields, account assignment, and entitlement values. Master-data changes after the billing period must not rewrite historical results.

## 7. Calculation and validation controls

Use C# decimal and SQL Server decimal(18,2) for all money. Never use float or double.

~~~text
ActualBill       = ParsedBillLine.TotalDueAmount
Variance         = MonthlyCreditLimit + MonthlyRental - ActualBill
CalculatedExcess = max(0, ActualBill - MonthlyCreditLimit - MonthlyRental)
~~~

Before a batch can be submitted:

1. Store the original PDF, raw page text, each parsed line, page number, parser version, parse status, and parse diagnostics.
2. Reconcile the sum of parsed TotalDueAmount with the PDF grand Total Due Amount exactly; a mismatch blocks submission.
3. Require each parsed line to be matched to exactly one effective mobile assignment, or to an explicitly resolved exception. Never silently omit a line.
4. Require exactly one effective entitlement per assessable line. Missing or overlapping values block submission.
5. Flag unexpected duplicate account lines, malformed source values, and source-line anomalies.
6. Recalculate assessments server-side whenever source data, matching, entitlement, or responsibility changes.
7. Block export and lock until reconciliation, required exception resolutions, and all approvals pass.

## 8. Exception handling

Exceptions are first-class, auditable records, never free-text report comments. Each has a type, severity, state, assignee, source link, resolution, evidence, and history.

| Exception | Trigger | Handling |
| --- | --- | --- |
| Parse failure | Page/table/amount cannot be read deterministically | Block; retain source and diagnostics |
| PDF total mismatch | Parsed Total Due sum differs from source grand total | Block submission |
| Unmatched account | No effective mobile assignment | Block until resolved; a verified disconnected account is retained in the batch and excluded from the final report |
| Ambiguous account match | More than one effective assignment | Block until data/resolution identifies one |
| Missing/overlapping entitlement | No unique effective limit/rental | Block calculation/submission |
| Duplicate source account | Unexpected repetition within a batch | Review before submission |
| Source anomaly | Missing identifier, invalid total, unexpected source values | Flag; severity drives blocking |
| Exceptional charge | Roaming, IDD, VAS, instalment, tax, late-fee, prior-due, adjustment, or other charge requires treatment | Apply a manual reviewer decision and note when required |
| Approval rejection | IT, HR, or Finance rejects | Return to editable review state with reason |

The unmatched zero-due account 768791861 is a required parser/reconciliation regression case.

## 9. Proposed database entities

All business transactions have CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, and an optimistic concurrency token. Source/approval history is append-only through audit records.

| Entity | Purpose |
| --- | --- |
| Employee | EPF, employee identity, status |
| OrganisationUnit | Factory/department hierarchy and codes |
| EmployeeOrganisationAssignment | Effective-dated category, designation, organisation |
| TelecomProvider | Provider configuration |
| TelecomAccount | Indexed mobile/account number, provider, source corporate code, status |
| EmployeeMobileAssignment | Effective-dated employee-to-account link |
| MobileEntitlement | Effective credit limit, rental, currency, scope |
| BillingPeriod | Year/month, state, lock user/time |
| BillingBatch | Period, provider, revision, workflow state, source totals |
| StoredFile | PDF filename, storage key, MIME type, SHA-256, size |
| BillUpload | Batch, file, uploaded-by/time, parser version/status |
| ParsedBillLine | Every source row, page/raw text, all PDF amounts, parse status |
| BatchReconciliation | Expected/parsed totals and row counts, outcome |
| BillAssessment | Line, matched snapshots, entitlement snapshots, actual bill, variance, excess, responsibility, final deduction |
| BillingException | Type, severity, status, linked source/assessment, resolution |
| ExceptionComment | Comments and supporting evidence |
| Approval | Batch, IT/HR/Finance stage, decision, actor/time/reason |
| GeneratedReport | Batch, report version, Excel file, checksum, generator/time |
| AuditEvent | Actor/action, entity, before/after JSON, timestamp, correlation ID |

Index mobile/account number, employee EPF, billing period, batch state, parsed-line batch/account, exception status, and approval batch/stage. Enforce uniqueness for EPF, billing-period/revision, and active/effective account assignments.

## 10. Proposed API structure

Implement an ASP.NET Core .NET 10 Web API. Controllers are thin; application services own parsing, matching, calculations, workflow state transitions, ClosedXML report generation, and audit logging. Use DTOs, async I/O, cancellation tokens, DI, and Swagger/OpenAPI.

| Area | Representative endpoints |
| --- | --- |
| Identity | GET /api/me; role/permission claims |
| Employees | GET/POST /api/employees; GET/PUT /api/employees/{id}; organisation assignment endpoints |
| Mobile accounts | GET/POST /api/mobile-accounts; assignment history endpoints |
| Entitlements | GET/POST /api/entitlements; effective-dated update/retire operations |
| Billing periods | GET/POST /api/billing-periods; POST /{periodId}/lock; controlled reopen |
| Upload/parse | POST /api/billing-batches; POST /{id}/uploads; POST /{id}/parse; GET /{id}/reconciliation |
| Source evidence | GET /api/billing-batches/{id}/parsed-lines; raw source evidence endpoint |
| Assessments | GET /api/billing-batches/{id}/assessments; match/entitlement/responsibility commands |
| Exceptions | GET /api/exceptions; POST /api/exceptions/{id}/resolve; comment/evidence endpoints |
| Workflow | POST /api/billing-batches/{id}/submit; POST /{id}/approvals/it, /hr, /finance |
| Reports | POST /api/billing-batches/{id}/reports; GET /api/reports/{id}/download; preview |
| Audit | GET /api/billing-batches/{id}/audit and entity-level history |

Commands validate state transitions and optimistic-concurrency tokens. Authorisation separates preparer, IT, HR, Finance, administrator, and auditor capabilities.

## 11. Proposed React screens

The React + TypeScript + Vite frontend uses Material UI and consumes API DTOs. It does not perform business calculations.

| Screen | Purpose |
| --- | --- |
| Dashboard | Batch status, reconciliation totals, open exceptions, approvals |
| Billing Periods | Create/select/lock periods and view revisions |
| Upload and Parse | Upload PDF, parsing progress, extracted billing period, source evidence |
| Reconciliation Review | PDF and parsed counts/totals, unmatched records, blockers |
| Bill Assessments | Searchable source components, employee match, entitlement, variance, excess, responsibility, final deduction |
| Exception Work Queue | Filter, assign, resolve, comment, attach evidence |
| IT Check | IT decision and return-for-correction action |
| HR Approval | HR decision and approval history |
| Finance Approval | Finance decision and approval history |
| Report Preview and Export | Export rows/totals, generated versions, download |
| Employee Master | Employees, organisation details, effective assignments |
| Mobile and Entitlement Master | Accounts, employee links, credit limits/rentals, effective dates |
| Audit Trail | Read-only batch, exception, decision, and report history |

## 12. Solution architecture

~~~text
src/
  MobileBill.Api/             HTTP, identity, Swagger, composition root
  MobileBill.Application/     use cases, DTOs, validators, interfaces
  MobileBill.Domain/          entities, value objects, calculations, workflow rules
  MobileBill.Infrastructure/  EF Core, SQL Server, storage, PDF parser, ClosedXML export
  mobilebill-web/             React, TypeScript, Material UI
tests/
  MobileBill.UnitTests/       calculation, workflow, validation tests
  MobileBill.IntegrationTests/ API, EF Core, storage/parser/report integration tests
docs/samples/                 golden parser and report fixtures
~~~

The parser is a replaceable infrastructure adapter. It begins with deterministic extraction against the text/table layer. OCR is used only when no usable text layer exists and that exceptional mode is recorded. ClosedXML creates the final workbook.

## 13. Development phases

1. **Rule confirmation and fixtures** - Confirm unresolved policy; formalise the sample PDF/report as golden regression fixtures and acceptance criteria.
2. **Foundation** - Create solution projects, clean boundaries, SQL Server configuration, EF Core migrations, identity/authorisation, audit foundation, and CI.
3. **Master data** - Implement employees, organisation, accounts, effective assignments, and entitlements.
4. **Bill ingestion and reconciliation** - Implement upload/storage, deterministic parsing, raw evidence retention, validation, total reconciliation, and exception queue.
5. **Assessment and resolution** - Implement matching, entitlement snapshots, server-side calculations, responsibility decisions, exception workflow, and unit tests for every calculation.
6. **Approvals and reporting** - Implement IT/HR/Finance workflow, explicit report headings/totals, ClosedXML export, and locking.
7. **Hardening and rollout** - Security, concurrency, performance, backup/monitoring, parser regression with sample-bill.pdf, UAT, and master-data migration.

## 14. Decisions that cannot be proven from the samples

The following implementation constraints are now confirmed:

1. The first invoice page supplies the billing period, date of supply, and invoice date.
2. The current report's Deduction column is the final payroll deduction.
3. Keep By User and By Company exactly as they are currently used; do not add responsibility logic in the initial release.
4. Exceptional charge treatment is a manual reviewer decision.
5. An entitlement is the approved monthly allowance (credit limit plus rental) for a mobile number. It must be effective-dated so historical periods retain the allowance used at the time. The authority to set it, category defaults, proration, and transfer rules are deferred business features.
6. Account 768791861 is disconnected. Retain its PDF line and resolved-disconnected audit record, but exclude it from the final report.
7. IT, HR, and Finance review the final monthly report in order and add comments.
8. Do not include the manual P and Q columns in the new report; the detailed visual/report contract is not required now.
9. Do not display the corporate code in the final report.
10. Locked periods need prior-report and history viewing only; no reopening mechanism is required.
