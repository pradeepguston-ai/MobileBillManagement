# Mobile Bill Management System

## Technology

Frontend:
- React
- TypeScript
- Vite
- Material UI

Backend:
- ASP.NET Core Web API
- .NET 10
- Entity Framework Core 10

Database:
- Microsoft SQL Server

Testing:
- xUnit for backend
- Vitest for frontend

PDF:
- Use deterministic text extraction first.
- Do not use AI/OCR unless the PDF has no usable text layer.

Excel:
- Use ClosedXML for .xlsx generation.

## Architecture

Use clean separation:

src/
  MobileBill.Api
  MobileBill.Application
  MobileBill.Domain
  MobileBill.Infrastructure
  mobilebill-web

tests/
  MobileBill.UnitTests
  MobileBill.IntegrationTests

Business logic must not be placed in React components or controllers.

## Business requirements

The system processes monthly telecom corporate bills.

Workflow:

PDF Upload
-> Parse Bill
-> Validate Extracted Data
-> Match Mobile Number to Employee
-> Retrieve Monthly Entitlement
-> Calculate Variance
-> Calculate Potential Deduction
-> Review Exceptions
-> IT Check
-> HR Approval
-> Finance Approval
-> Generate Final Excel Report
-> Lock Billing Period

## Important calculation rules

ActualBill = PDF Total Due Amount

Variance =
    MonthlyCreditLimit
    + MonthlyRental
    - ActualBill

CalculatedExcess =
    max(
        0,
        ActualBill
        - MonthlyCreditLimit
        - MonthlyRental
    )

CalculatedExcess is NOT automatically an employee deduction.

Final deduction depends on responsibility:
- By User
- By Company

## Data integrity

Never silently discard PDF records.

Store:
- original PDF
- every extracted bill line
- extraction status
- original raw text
- page number

Validate extracted TotalDueAmount sum against PDF grand total.

Do not submit a billing batch when totals do not balance.

## Coding rules

- Use async/await for database and file operations.
- Use decimal for monetary values, never double or float.
- Use cancellation tokens in backend services.
- Use DTOs between API and frontend.
- Use EF Core migrations.
- Add indexes for mobile number, employee EPF, billing period and status.
- Use nullable reference types.
- Enable Swagger/OpenAPI.
- Do not hard-code database passwords.
- Use dependency injection.
- Add audit fields to business transactions.

## Testing

Every business calculation must have unit tests.

Every PDF-parser change must be tested against docs/samples/sample-bill.pdf.

Before reporting a task complete:
- run dotnet build
- run dotnet test
- run frontend build
- report failures instead of hiding them

## Critical PDF Mapping

ActualBill MUST map to PDF TotalDueAmount.

Do NOT use ChargesForBillPeriod as ActualBill.

Both fields must still be stored.