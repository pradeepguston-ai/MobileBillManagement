# Phase 4: Bill Upload and Processing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver secure, transactional PDF bill upload, persisted parsing, and explicit structural/independent-total validation through the six Phase 4 APIs.

**Architecture:** Controllers delegate to an application-facing `IBillBatchService`; infrastructure supplies EF Core persistence, PdfPig parsing, staged filesystem storage, a development current-user implementation, and a clock. `StatedGrandTotal` is independent only when read from a source footer, while `CalculatedGrandTotal` is a decimal sum of successful persisted lines; workflow status and validation strength are independent.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core 10 / SQL Server, PdfPig, xUnit, SQLite in-memory integration test host.

**Spec:** `docs/superpowers/specs/2026-09-05-phase-4-bill-upload-processing-design.md`

## Global Constraints

- Do not implement authentication, OCR, report generation, employee matching, or frontend billing processing.
- `UploadedBy`, audit identities, and timestamps are server-controlled through `ICurrentUserService` and `IClock`; neither is writable in API DTOs.
- Use `decimal` for every monetary amount and exact zero comparisons.
- Store uploaded files below a server-controlled directory outside `wwwroot`; never use client filenames as physical paths.
- Preserve every parsed candidate, including failures and zero-total lines, in one transaction.
- The hard-coded `390096.74m` sample value is regression-test data only and must not enter production validation.
- Return consistent ProblemDetails without file-system paths or stack traces.

---

## File Structure

- Create `src/MobileBill.Application/Common/ICurrentUserService.cs` — current user abstraction.
- Create `src/MobileBill.Application/Common/IClock.cs` — server clock abstraction.
- Create `src/MobileBill.Application/Billing/BillBatchContracts.cs` — requests, DTOs, totals, validation result, line DTO.
- Create `src/MobileBill.Application/Billing/IBillBatchService.cs` — service boundary.
- Create `src/MobileBill.Application/Billing/BillBatchExceptions.cs` — not-found, validation, conflict, parsing exceptions.
- Create `src/MobileBill.Infrastructure/Billing/DevelopmentCurrentUserService.cs` — fixed development identity.
- Create `src/MobileBill.Infrastructure/Billing/SystemClock.cs` — `DateTimeOffset.UtcNow` implementation.
- Create `src/MobileBill.Infrastructure/Billing/BillStorageOptions.cs` — configured root directory and maximum file length.
- Create `src/MobileBill.Infrastructure/Billing/IBillFileStorage.cs` — staged file storage abstraction.
- Create `src/MobileBill.Infrastructure/Billing/FileSystemBillFileStorage.cs` — hardened staged/final file implementation.
- Create `src/MobileBill.Infrastructure/Billing/EfBillBatchService.cs` — state transitions, EF transactions, parsing, validation.
- Create `src/MobileBill.Api/Controllers/BillBatchesController.cs` — six thin endpoints.
- Modify `src/MobileBill.Domain/Entities/BillBatch.cs` and `src/MobileBill.Domain/Entities/BillLine.cs` — Phase 4 persisted fields.
- Modify `src/MobileBill.Domain/Enums/BillBatchStatus.cs`; create `GrandTotalSource.cs` and `ValidationLevel.cs` — state and validation enums.
- Modify `src/MobileBill.Infrastructure/Persistence/Configurations/EntityConfigurations.cs` — nullability, lengths, precision, and filtered unique hash index.
- Modify `src/MobileBill.Infrastructure/DependencyInjection.cs` — options and service registrations.
- Modify `src/MobileBill.Api/ExceptionHandling/ApiExceptionHandler.cs` — billing exceptions to 400/404/409 ProblemDetails.
- Modify `src/MobileBill.Api/appsettings.Development.json` — development-only bill storage configuration.
- Create EF migration `Phase4BillBatchProcessing` in `src/MobileBill.Infrastructure/Persistence/Migrations`.
- Create `tests/MobileBill.UnitTests/Billing/EfBillBatchServiceTests.cs` — service behavior with deterministic doubles.
- Create `tests/MobileBill.IntegrationTests/Billing/BillBatchApiTests.cs` and `BillingTestWebApplicationFactory.cs` — HTTP and relational-constraint coverage.
- Modify `tests/MobileBill.IntegrationTests/MobileBill.IntegrationTests.csproj` — add SQLite provider if absent.

## Task 1: Model the workflow and application contracts

**Files:**
- Create: `src/MobileBill.Application/Common/ICurrentUserService.cs`
- Create: `src/MobileBill.Application/Common/IClock.cs`
- Create: `src/MobileBill.Application/Billing/BillBatchContracts.cs`
- Create: `src/MobileBill.Application/Billing/IBillBatchService.cs`
- Create: `src/MobileBill.Application/Billing/BillBatchExceptions.cs`
- Modify: `src/MobileBill.Domain/Entities/BillBatch.cs`
- Modify: `src/MobileBill.Domain/Entities/BillLine.cs`
- Modify: `src/MobileBill.Domain/Enums/BillBatchStatus.cs`
- Create: `src/MobileBill.Domain/Enums/GrandTotalSource.cs`
- Create: `src/MobileBill.Domain/Enums/ValidationLevel.cs`
- Test: `tests/MobileBill.UnitTests/Billing/BillBatchContractTests.cs`

**Interfaces:**
- Produces `ICurrentUserService.UserId`, `ICurrentUserService.DisplayName`, `IClock.UtcNow`, and `IBillBatchService` service operations for tasks 2–7.
- Produces `CreateBillBatchRequest(Guid ProviderId, string CorporateCode, int BillingYear, int BillingMonth)` with no audit or upload fields.

- [ ] **Step 1: Write failing contract tests**

```csharp
[Fact]
public void Create_request_does_not_expose_uploaded_by_or_uploaded_at()
{
    Assert.DoesNotContain(typeof(CreateBillBatchRequest).GetProperties(), p =>
        p.Name is "UploadedBy" or "UploadedAt");
}

[Fact]
public void Draft_batch_has_no_upload_metadata_or_independent_total()
{
    var batch = new BillBatch();
    Assert.Null(batch.FileHash);
    Assert.Null(batch.StatedGrandTotal);
    Assert.Equal(GrandTotalSource.None, batch.GrandTotalSource);
}
```

- [ ] **Step 2: Run the contract test and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~BillBatchContractTests`

Expected: compile/test failure because the Phase 4 types and nullable model do not exist.

- [ ] **Step 3: Add the minimal domain and contract types**

```csharp
public interface ICurrentUserService { string UserId { get; } string DisplayName { get; } }
public interface IClock { DateTimeOffset UtcNow { get; } }
public enum GrandTotalSource { None, PdfFooter, DerivedFromLines }
public enum ValidationLevel { None, StructuralOnly, IndependentTotal }
```

Make Draft-time upload values nullable. Add nullable `StatedGrandTotal`, `CalculatedGrandTotal`, `Difference`, validation-level/warning metadata, validation audit metadata, and `BillLine.ExtractionError`.

- [ ] **Step 4: Run contract tests and verify GREEN**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~BillBatchContractTests`

Expected: PASS.

## Task 2: Configure persistence and generate the migration

**Files:**
- Modify: `src/MobileBill.Infrastructure/Persistence/Configurations/EntityConfigurations.cs`
- Create: `src/MobileBill.Infrastructure/Persistence/Migrations/<timestamp>_Phase4BillBatchProcessing.cs`
- Modify: `src/MobileBill.Infrastructure/Persistence/Migrations/MobileBillDbContextModelSnapshot.cs`
- Test: `tests/MobileBill.UnitTests/Billing/BillBatchPersistenceTests.cs`

**Interfaces:**
- Consumes the nullable batch/line model from Task 1.
- Produces SQL Server schema rules used by upload and parsing: money precision, nullable draft metadata, and unique uploaded content hash.

- [ ] **Step 1: Write the relational-model test**

```csharp
[Fact]
public async Task Non_null_file_hash_is_unique()
{
    await using var db = CreateSqliteContext();
    db.BillBatches.AddRange(CreateUploadedBatch("a"), CreateUploadedBatch("a"));
    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
}
```

- [ ] **Step 2: Run it and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~BillBatchPersistenceTests`

Expected: duplicate hash is accepted before the filtered unique index exists.

- [ ] **Step 3: Configure columns and constraints**

Configure every total as `decimal(18,2)`, `ExtractionError` as nullable bounded text, `GrandTotalSource` and `ValidationLevel` as strings, and a filtered unique index:

```csharp
builder.HasIndex(batch => batch.FileHash)
    .IsUnique()
    .HasFilter("[FileHash] IS NOT NULL");
```

Generate migration:

```powershell
dotnet ef migrations add Phase4BillBatchProcessing --project src/MobileBill.Infrastructure --startup-project src/MobileBill.Api
```

- [ ] **Step 4: Run persistence test and inspect migration**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~BillBatchPersistenceTests`

Expected: duplicate non-null hash is rejected; multiple Draft batches with null hashes are accepted.

## Task 3: Add current-user, clock, and safe staged file storage

**Files:**
- Create: `src/MobileBill.Infrastructure/Billing/DevelopmentCurrentUserService.cs`
- Create: `src/MobileBill.Infrastructure/Billing/SystemClock.cs`
- Create: `src/MobileBill.Infrastructure/Billing/BillStorageOptions.cs`
- Create: `src/MobileBill.Infrastructure/Billing/IBillFileStorage.cs`
- Create: `src/MobileBill.Infrastructure/Billing/FileSystemBillFileStorage.cs`
- Modify: `src/MobileBill.Infrastructure/DependencyInjection.cs`
- Modify: `src/MobileBill.Api/appsettings.Development.json`
- Test: `tests/MobileBill.UnitTests/Billing/FileSystemBillFileStorageTests.cs`

**Interfaces:**
- Produces `StageAsync(Stream, FileStorageUpload, CancellationToken)`, `FinalizeAsync(StagedBillFile, Guid, CancellationToken)`, `DeleteAsync(string, CancellationToken)`, and `OpenReadAsync(string, CancellationToken)`.
- Consumes the abstraction types from Task 1 and is consumed by upload/parse in Task 4 and Task 5.

- [ ] **Step 1: Write failing storage tests**

```csharp
[Theory]
[InlineData("invoice.txt", "application/pdf")]
[InlineData("invoice.pdf", "text/plain")]
public async Task Stage_rejects_invalid_pdf_metadata(string name, string contentType) =>
    await Assert.ThrowsAsync<BillBatchValidationException>(() => storage.StageAsync(...));

[Fact]
public async Task Stage_rejects_non_pdf_signature_and_never_uses_client_name_as_path() { ... }
```

- [ ] **Step 2: Run storage tests and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~FileSystemBillFileStorageTests`

Expected: missing file-storage types.

- [ ] **Step 3: Implement staging and compensating behavior**

`StageAsync` enforces configured byte length, `.pdf`, `application/pdf`, non-empty content, and the `%PDF-` header while streaming to a randomized staging filename and computing SHA-256. `FinalizeAsync` moves it to `<root>/<batch-id>/<hash>.pdf`; `DeleteAsync` removes only paths verified inside the configured root. Register `DevelopmentCurrentUserService` and `SystemClock` through DI. Configure a relative `App_Data/BillUploads` root in development only.

- [ ] **Step 4: Run storage tests and verify GREEN**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~FileSystemBillFileStorageTests`

Expected: PASS; test root is empty after every rejected or compensated operation.

## Task 4: Implement Draft creation and staged upload

**Files:**
- Create: `src/MobileBill.Infrastructure/Billing/EfBillBatchService.cs`
- Test: `tests/MobileBill.UnitTests/Billing/EfBillBatchServiceTests.cs`

**Interfaces:**
- Consumes `MobileBillDbContext`, `IBillFileStorage`, `ICurrentUserService`, and `IClock`.
- Produces `CreateAsync`, `UploadAsync`, and batch DTO retrieval for Tasks 5–7.

- [ ] **Step 1: Write failing service tests**

```csharp
[Fact]
public async Task Upload_sets_server_identity_time_hash_and_uploaded_status()
{
    var result = await service.UploadAsync(batch.Id, ValidPdf("client-name.pdf"), token);
    Assert.Equal("Development User", result.UploadedBy);
    Assert.Equal(clock.UtcNow, result.UploadedAt);
    Assert.Equal(BillBatchStatus.Uploaded, result.Status);
}

[Fact]
public async Task Duplicate_content_returns_conflict_and_cleans_staged_file() { ... }

[Fact]
public async Task Upload_is_rejected_outside_draft_state() { ... }
```

- [ ] **Step 2: Run service tests and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: interface and upload operation do not exist.

- [ ] **Step 3: Implement create/upload state transitions**

`CreateAsync` creates a Draft after validating active provider and billing period. `UploadAsync` stages the file, checks existing hash, assigns server metadata from `ICurrentUserService` and `IClock`, saves database state, finalizes the staged file, and cleans storage plus restores Draft metadata if finalization or persistence fails. Convert unique-index `DbUpdateException` to `BillBatchConflictException`.

- [ ] **Step 4: Run service tests and verify GREEN**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: all state, identity, time, duplicate, and cleanup tests pass.

## Task 5: Implement transactional parsing and line persistence

**Files:**
- Modify: `src/MobileBill.Infrastructure/Billing/EfBillBatchService.cs`
- Test: `tests/MobileBill.UnitTests/Billing/EfBillBatchServiceTests.cs`

**Interfaces:**
- Consumes `IPdfBillParser.ParseAsync(Stream, CancellationToken)` and durable storage from Task 3.
- Produces persisted `BillLine` rows, `CalculatedGrandTotal`, and `Parsed` status for Task 6.

- [ ] **Step 1: Write failing parse tests with a deterministic parser double**

```csharp
[Fact]
public async Task Parse_persists_successful_and_failed_candidates_atomically()
{
    parser.Result = new PdfBillParseResult([successful, failed], 0m, ["footer unavailable"]);
    await service.ParseAsync(batch.Id, token);
    Assert.Equal(2, await db.BillLines.CountAsync());
    Assert.Equal("bad row", (await db.BillLines.SingleAsync(x => x.ExtractionStatus == ValidationFailed)).ExtractionError);
    Assert.Equal(successful.TotalDueAmount, batch.CalculatedGrandTotal);
}

[Fact]
public async Task Parser_exception_leaves_uploaded_batch_with_zero_persisted_lines() { ... }
```

- [ ] **Step 2: Run parse tests and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: parse operation is absent.

- [ ] **Step 3: Implement parse in an explicit EF transaction**

Allow parse only from Uploaded. Open stored file, parse, map every candidate to `BillLine`, set `ExtractionStatus` and `ExtractionError`, calculate `CalculatedGrandTotal` from `Extracted` lines only, set `StatedGrandTotal = null`, `Difference = null`, and `GrandTotalSource = DerivedFromLines` for the current parser result. Persist rows and Parsed status in one transaction. On parser/storage/transaction error, roll back and leave Uploaded with no lines.

- [ ] **Step 4: Run parse tests and verify GREEN**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: successful and failed candidates persist together; failure leaves no partial set.

## Task 6: Implement validation strength and paging queries

**Files:**
- Modify: `src/MobileBill.Infrastructure/Billing/EfBillBatchService.cs`
- Test: `tests/MobileBill.UnitTests/Billing/EfBillBatchServiceTests.cs`

**Interfaces:**
- Consumes parsed batch/lines from Task 5 and `IClock`/`ICurrentUserService` from Task 1.
- Produces `ValidateAsync`, `GetAsync`, and `GetLinesAsync` used by the API task.

- [ ] **Step 1: Write failing validation tests**

```csharp
[Fact]
public async Task No_stated_footer_total_validates_as_structural_only_with_warning()
{
    var result = await service.ValidateAsync(parsedBatch.Id, token);
    Assert.Equal(BillBatchStatus.Validated, result.Status);
    Assert.Equal(ValidationLevel.StructuralOnly, result.ValidationLevel);
    Assert.Null(result.Difference);
    Assert.Contains("could not be independently extracted", result.ValidationWarning);
}

[Fact]
public async Task Independent_matching_total_validates_at_independent_total_level() { ... }
[Fact]
public async Task Failed_candidate_or_nonzero_difference_sets_validation_failed() { ... }
```

- [ ] **Step 2: Run validation tests and verify RED**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: validation-level behavior is not implemented.

- [ ] **Step 3: Implement validation rules and query projection**

Recalculate `CalculatedGrandTotal` as decimal sum of successful rows. If a stated total exists, set `Difference = StatedGrandTotal - CalculatedGrandTotal`; otherwise set null. Failed parser candidates always give ValidationFailed. With no stated total and no failed rows, set Validated/StructuralOnly with prescribed warning. With exact independent equality, set Validated/IndependentTotal. Populate validation user/time through abstractions. Implement paged lines using `PagedRequest` and return failed status/error without raw storage location.

- [ ] **Step 4: Run validation tests and verify GREEN**

Run: `dotnet test tests/MobileBill.UnitTests --filter FullyQualifiedName~EfBillBatchServiceTests`

Expected: all validation paths and pagination tests pass.

## Task 7: Expose thin HTTP endpoints and integration tests

**Files:**
- Create: `src/MobileBill.Api/Controllers/BillBatchesController.cs`
- Modify: `src/MobileBill.Api/ExceptionHandling/ApiExceptionHandler.cs`
- Modify: `src/MobileBill.Infrastructure/DependencyInjection.cs`
- Create: `tests/MobileBill.IntegrationTests/Billing/BillingTestWebApplicationFactory.cs`
- Create: `tests/MobileBill.IntegrationTests/Billing/BillBatchApiTests.cs`
- Modify: `tests/MobileBill.IntegrationTests/MobileBill.IntegrationTests.csproj`

**Interfaces:**
- Consumes `IBillBatchService` from Tasks 4–6.
- Produces the six documented endpoints and ProblemDetails responses.

- [ ] **Step 1: Write failing HTTP tests**

```csharp
[Fact]
public async Task Create_upload_parse_validate_and_read_lines_follow_the_state_machine() { ... }

[Fact]
public async Task Upload_duplicate_returns_409_problem_details_without_storage_path() { ... }

[Fact]
public async Task Invalid_transition_returns_409_and_non_pdf_returns_400() { ... }
```

Use a custom `WebApplicationFactory` with a shared SQLite in-memory connection, a temporary storage root, deterministic clock/current user, and a parser double. SQLite's relational unique index verifies that concurrent duplicate attempts have a database-level final guard.

- [ ] **Step 2: Run integration tests and verify RED**

Run: `dotnet test tests/MobileBill.IntegrationTests --filter FullyQualifiedName~BillBatchApiTests`

Expected: 404 because no batch controller or service registration exists.

- [ ] **Step 3: Implement controller and exception mapping**

Expose exactly:

```text
POST /api/bill-batches
POST /api/bill-batches/{id}/upload
POST /api/bill-batches/{id}/parse
GET  /api/bill-batches/{id}
GET  /api/bill-batches/{id}/lines
POST /api/bill-batches/{id}/validate
```

Use `[FromForm] IFormFile file` only for upload. Map billing validation to 400, not found to 404, and duplicate/invalid state to 409. Do not return `StoredFilePath` or accept user/time fields in requests.

- [ ] **Step 4: Run integration tests and verify GREEN**

Run: `dotnet test tests/MobileBill.IntegrationTests --filter FullyQualifiedName~BillBatchApiTests`

Expected: all endpoint, duplicate race, rollback, validation-level, and security tests pass.

## Task 8: Run complete verification

**Files:**
- Verify: `docs/superpowers/specs/2026-09-05-phase-4-bill-upload-processing-design.md`
- Verify: `docs/superpowers/plans/2026-09-05-phase-4-bill-upload-processing.md`

- [ ] **Step 1: Review scope against the specification**

Confirm no authentication, OCR, reporting, employee matching, or frontend billing processing was added. Confirm `390096.74m` appears only in regression tests.

- [ ] **Step 2: Run backend verification**

Run:

```powershell
dotnet build MobileBill.sln
dotnet test MobileBill.sln
```

Expected: build succeeds with zero errors; all existing and Phase 4 tests pass.

- [ ] **Step 3: Run frontend build required by repository policy**

Run:

```powershell
npm run build
```

from `src/mobilebill-web`.

Expected: Vite production build succeeds without modifying Phase 4 frontend scope.

## Plan Self-Review

- Spec coverage: Tasks 1–7 cover all API endpoints, metadata, state transitions, storage staging/cleanup, duplicate database guard, parsing rollback, candidate retention, separate totals, validation strength, user/time abstractions, and HTTP error handling.
- Placeholder scan: no deferred implementation markers are present.
- Type consistency: service, storage, user, clock, DTO, enum, and exception names introduced in the plan are used consistently by later tasks.
