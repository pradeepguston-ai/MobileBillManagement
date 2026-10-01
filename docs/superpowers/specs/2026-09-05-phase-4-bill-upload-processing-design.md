# Phase 4: Bill Upload and Processing Design

## Scope

Phase 4 adds durable PDF upload, deterministic parsing, persisted parsed rows, and batch validation. It excludes authentication, OCR, report generation, employee matching, and frontend billing processing.

## Architecture

Controllers remain thin. `IBillBatchService` in the Application layer owns state transitions and business validation. The Infrastructure layer implements that service with EF Core, `IPdfBillParser`, and an `IBillFileStorage` abstraction.

`ICurrentUserService` belongs in Application and exposes `UserId` and `DisplayName`. Infrastructure provides `DevelopmentCurrentUserService`, which returns `dev-user` and `Development User`. `IClock` belongs in Application and exposes `DateTimeOffset UtcNow`; Infrastructure provides a system-clock implementation. No client request can set upload or audit identities or timestamps.

## File Storage

`IBillFileStorage` uses staged/compensating writes below a configured directory outside `wwwroot`. The physical filename is server-generated from the batch identifier and hash; `OriginalFileName` is metadata only.

Upload flow:

1. Require a non-empty multipart file within configured maximum size.
2. Check `.pdf`, declared `application/pdf` MIME type, and `%PDF-` file signature.
3. Stream the upload to a staging file while calculating SHA-256.
4. Check the hash and persist it under a filtered unique database constraint; the constraint is the final concurrent-request guard.
5. Finalize/move the staged file to its server-controlled final path.
6. Compensate by deleting staged/final files when database persistence or finalization fails. A successful request leaves the batch `Uploaded`; a failed request leaves it `Draft`.

The duplicate policy is global: identical PDF byte content may never be uploaded twice. A duplicate returns HTTP 409 without exposing physical paths.

## Schema

`BillBatch` uses nullable upload metadata until a successful upload:

- `OriginalFileName`, `StoredFilePath`, `FileHash`, `UploadedBy`, `UploadedAt`
- `StatedGrandTotal decimal?`
- `CalculatedGrandTotal decimal?`
- `Difference decimal?`
- `GrandTotalSource` (`PdfFooter`, `DerivedFromLines`, `None`)
- `ValidationLevel` (`None`, `StructuralOnly`, `IndependentTotal`)
- `ValidationMessage` / warning text
- validation timestamp and user identity, populated through `IClock` and `ICurrentUserService`

`BillLine` adds nullable `ExtractionError`. It already persists extraction status, page number, raw text, and all sixteen monetary fields.

The migration adds `Validated` to the string-backed batch status model, makes draft-time upload fields nullable, adds the new total/validation fields, and creates a filtered unique index on non-null `FileHash`.

## State Machine

Allowed transitions are exclusively service-controlled:

```text
Draft --upload--> Uploaded --parse--> Parsed --validate--> Validated
                                           \--validate--> ValidationFailed --validate--> Validated | ValidationFailed
```

- Upload is accepted only from `Draft`.
- Parse is accepted only from `Uploaded`.
- Validate is accepted only from `Parsed` or `ValidationFailed`.
- Invalid transitions return a conflict-style application error; controllers do not assign status.
- Parse failures roll back the transaction, leave status `Uploaded`, and leave no partial `BillLine` set.

## Parsing and Totals

Parsing maps every `ParsedBillLine` to a `BillLine` atomically, including failed candidates. Failed candidate rows are retained and never contribute to `CalculatedGrandTotal`.

`StatedGrandTotal` means a genuinely independent PDF footer/source total. `CalculatedGrandTotal` is the exact decimal sum of successful `BillLine.TotalDueAmount` values. `GrandTotalSource` describes how, if at all, a source total was obtained.

For the current sample PDF, the visual footer is not independently text-extractable by PdfPig. Therefore:

- `StatedGrandTotal = null`
- `CalculatedGrandTotal = 390096.74m`
- `Difference = null`
- `GrandTotalSource = DerivedFromLines`

The sample regression value is test data only; it is never used in production validation.

## Validation

Validation has two independent dimensions: workflow status and validation strength.

- Any failed parser candidate produces `ValidationFailed`.
- With a stated total, validation requires non-null calculated total and exact `Difference == 0`; on success it sets `Validated` with `ValidationLevel.IndependentTotal`.
- Without a stated total, successful structural parsing may set `Validated` with `ValidationLevel.StructuralOnly`, `Difference = null`, and a warning explaining that no independent PDF total was available.

API batch detail returns validation level and warning so later UI phases can surface it.

## API

- `POST /api/bill-batches` creates a Draft from provider, corporate code, billing year, and billing month.
- `POST /api/bill-batches/{id}/upload` accepts `multipart/form-data` with a single `file` field.
- `POST /api/bill-batches/{id}/parse` parses the durable file.
- `GET /api/bill-batches/{id}` returns batch metadata, totals, status, validation level, and warning.
- `GET /api/bill-batches/{id}/lines` returns a paged list of persisted rows.
- `POST /api/bill-batches/{id}/validate` performs structural and, when available, independent-total checks.

Errors use the existing exception handler and ProblemDetails convention: 400 for request validation, 404 for missing batches, and 409 for duplicate content or invalid transitions. Filesystem paths and stack traces are never returned.

## Testing

Integration coverage uses a test file-storage implementation and an EF-compatible test setup. It verifies upload security, duplicate conflict behavior, database unique-constraint race protection, server-generated metadata, state transitions, transactional parse rollback, candidate preservation, total behavior, validation levels, API paging, and public response shape.
