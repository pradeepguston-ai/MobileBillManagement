# Phase 3 PDF Bill Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deterministically extract every telecom account row and the exact Total Due grand total from `sample-bill.pdf`, with the converted XLSX used solely as an independent regression reference.

**Architecture:** Application defines the parser interface and immutable result/line models. Infrastructure implements `PdfBillParser` with UglyToad.PdfPig words and positional row/column reconstruction; it never invokes OCR, AI, SQL, HTTP, or Excel parsing. Tests use ClosedXML to read the converted sample workbook independently and compare every account and field against the PDF result.

**Tech Stack:** .NET 10, C#, UglyToad.PdfPig, ClosedXML (test-only reference reader), xUnit.

**Spec:** docs/SYSTEM_DESIGN.md and the approved Phase 3 requirements in this thread.

## Global Constraints

- PDF is the only production input. XLSX is test-only reference data, not a parser fallback or import feature.
- The parser uses positioned PdfPig words/column boundaries; flattened text is retained only as source evidence.
- Every candidate account row is returned as either successful or failed. Zero total rows remain successful records.
- Each successful line has one string account number and exactly sixteen `decimal` monetary values.
- Grand total extraction is separate from account-line extraction and must equal `390096.74m` for the sample.
- No database persistence, API, UI, OCR, or AI is added.

### Task 1: Contracts and numeric/row validation

**Files:**
- Create: `src/MobileBill.Application/Pdf/ExtractionStatus.cs`, `ParsedBillLine.cs`, `PdfBillParseResult.cs`, `IPdfBillParser.cs`
- Test: `tests/MobileBill.UnitTests/Pdf/PdfBillParserContractTests.cs`

- [ ] Write failing tests for string account identifiers, comma decimal parsing, zero values, invalid account candidates, and exactly sixteen monetary fields.
- [ ] Run the unit-test filter and confirm parser contract types are missing.
- [ ] Add parser models, summary counts, warnings, and deterministic decimal/account helpers.
- [ ] Re-run the unit test and confirm contract validation passes.

### Task 2: Positioned PdfPig implementation

**Files:**
- Create: `src/MobileBill.Infrastructure/Pdf/PdfBillParser.cs`, `PdfBillParserOptions.cs`
- Modify: `src/MobileBill.Infrastructure/MobileBill.Infrastructure.csproj`, `DependencyInjection.cs`
- Test: `tests/MobileBill.IntegrationTests/Pdf/SampleBillPdfParserTests.cs`

- [ ] Add a failing sample-PDF regression test that expects 259 parsed account candidates and grand total `390096.74m`.
- [ ] Add PdfPig and implement pages-to-words extraction, Y-coordinate row grouping, positional columns, candidate identification, and separate footer grand-total detection.
- [ ] Return malformed account-looking rows as failed `ParsedBillLine` values with page/raw text/error.
- [ ] Re-run the sample test and verify no header/footer becomes a line and zero-value lines are retained.

### Task 3: XLSX reference comparison regression tests

**Files:**
- Create: `tests/MobileBill.IntegrationTests/Pdf/SampleBillXlsxReferenceReader.cs`
- Modify: `tests/MobileBill.IntegrationTests/MobileBill.IntegrationTests.csproj`
- Test: `tests/MobileBill.IntegrationTests/Pdf/SampleBillPdfParserTests.cs`

- [ ] Add a failing test which reads expected XLSX rows using ClosedXML and reports missing, unexpected, and per-field mismatches by mobile account.
- [ ] Implement a test-only workbook reader; it maps identifiers as strings and all 16 values as decimals.
- [ ] Add assertions for first, middle, final, zero, IDD, roaming, VAS, AddToBill, comma-formatted rows, total counts, success/failure counts and both grand totals.
- [ ] Re-run regression tests and confirm PDF/XLSX mismatch count is zero with useful diagnostics on failure.

### Task 4: Verification

**Files:** No production changes beyond issues found during verification.

- [ ] Run `dotnet build MobileBill.sln --no-restore`.
- [ ] Run `dotnet test MobileBill.sln --no-restore`.
- [ ] Run `npm run build` in `src/mobilebill-web` to preserve the existing frontend baseline.
- [ ] Report parser architecture, PDF/XLSX counts/totals/mismatches, executed tests, and positional-parsing risks.
