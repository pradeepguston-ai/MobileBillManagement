# Initial Solution Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a buildable .NET 10 and React foundation for Mobile Bill Management without implementing billing business features.

**Architecture:** A clean .NET solution separates Domain, Application, Infrastructure, and API projects. The API owns host configuration only, uses Infrastructure through DI, exposes Swagger, CORS, and a health endpoint, and has a placeholder EF Core SQL Server DbContext. The React Vite application contains a Material UI application shell and React Router route placeholders only.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core 10 SQL Server, xUnit, React, TypeScript, Vite, Material UI, React Router, Vitest.

**Spec:** docs/SYSTEM_DESIGN.md

## Global Constraints

- Target .NET 10 and use nullable reference types.
- Keep business logic out of controllers and React components.
- Do not add bill-processing, master-data, approval, or report features in this phase.
- Use SQL Server through EF Core 10, with no database password in configuration.
- Configure CORS only for local Vite development at http://localhost:5173.
- Provide Swagger, dependency injection, a health endpoint, React Router, and a basic frontend layout.
- Keep source PDFs and final report policy work out of this foundation task.

---

### Task 1: Update the approved design evidence

**Files:**
- Modify: docs/SYSTEM_DESIGN.md

**Interfaces:**
- Consumes: user-confirmed PDF, deduction, disconnected-account, approval, reporting, and history decisions.
- Produces: an approved foundation scope used by Tasks 2-5.

- [ ] **Step 1: Record invoice-page evidence**

Document that the first PDF page supplies Billing Period 07/15/2026-08/14/2026, Date of Supply 08/14/2026, and Date of Invoice 08/14/2026.

- [ ] **Step 2: Record confirmed report and workflow policy**

Document column N as final payroll deduction, P/Q as excluded, the manual exception decision, ordered IT/HR/Finance review comments, and historical report viewing after lock.

- [ ] **Step 3: Explain the deferred entitlement policy**

Document entitlement as the effective-dated monthly credit-limit/rental allowance for a mobile number, while deferring authority, defaulting, proration, and transfer rules.

### Task 2: Scaffold the .NET solution and project dependency graph

**Files:**
- Create: global.json
- Create: MobileBill.sln
- Create: src/MobileBill.Domain/MobileBill.Domain.csproj
- Create: src/MobileBill.Application/MobileBill.Application.csproj
- Create: src/MobileBill.Infrastructure/MobileBill.Infrastructure.csproj
- Create: src/MobileBill.Api/MobileBill.Api.csproj
- Create: tests/MobileBill.UnitTests/MobileBill.UnitTests.csproj
- Create: tests/MobileBill.IntegrationTests/MobileBill.IntegrationTests.csproj

**Interfaces:**
- Consumes: Task 1 foundation scope.
- Produces: a solution with Domain <- Application <- Infrastructure <- API references, plus test-project references.

- [ ] **Step 1: Generate projects and solution**

Run dotnet new globaljson --sdk-version 10.0.201 --roll-forward latestFeature, dotnet new sln --name MobileBill, class-library templates for Domain/Application/Infrastructure, a webapi template for API, and xunit templates for both test projects.

- [ ] **Step 2: Set project references**

Reference Domain from Application; Domain and Application from Infrastructure; Application and Infrastructure from API; Application and Domain from unit tests; and API from integration tests.

- [ ] **Step 3: Add infrastructure and integration dependencies**

Add Microsoft.EntityFrameworkCore.SqlServer and Microsoft.EntityFrameworkCore.Design to Infrastructure, Swashbuckle.AspNetCore to API, and Microsoft.AspNetCore.Mvc.Testing to integration tests. Restore packages before compilation.

- [ ] **Step 4: Remove generated sample application code**

Delete the WeatherForecast endpoint/controller and the generated placeholder test files so no accidental business/sample API is shipped.

### Task 3: Add the tested API host foundation

**Files:**
- Create: src/MobileBill.Infrastructure/Persistence/MobileBillDbContext.cs
- Create: src/MobileBill.Infrastructure/DependencyInjection.cs
- Create: src/MobileBill.Api/Configuration/CorsPolicies.cs
- Create: src/MobileBill.Api/appsettings.Development.json
- Modify: src/MobileBill.Api/Program.cs
- Modify: src/MobileBill.Api/MobileBill.Api.csproj
- Test: tests/MobileBill.IntegrationTests/HealthEndpointTests.cs

**Interfaces:**
- Consumes: API and Infrastructure projects from Task 2.
- Produces: IServiceCollection.AddInfrastructure(IConfiguration), the local-development CORS policy, GET /health, Swagger UI in Development, and a public partial Program test host.

- [ ] **Step 1: Write the failing health contract test**

Create HealthEndpointTests using WebApplicationFactory<Program>; GET /health must return HTTP 200.

- [ ] **Step 2: Run the health test and verify failure**

Run dotnet test tests/MobileBill.IntegrationTests/MobileBill.IntegrationTests.csproj --filter FullyQualifiedName~HealthEndpointTests. The unconfigured generated API must return 404 for /health.

- [ ] **Step 3: Implement the minimum host configuration**

Register AddHealthChecks, AddInfrastructure(configuration), AddCors with origin http://localhost:5173, AddEndpointsApiExplorer, AddSwaggerGen, and SQL Server DbContext options from ConnectionStrings:MobileBillDatabase. Map /health, Swagger, Swagger UI, and the CORS policy. Add public partial class Program for the test host.

- [ ] **Step 4: Run the health test and verify success**

Run the same filtered dotnet test command. It must return HTTP 200.

### Task 4: Add the tested React application shell

**Files:**
- Create: src/mobilebill-web/package.json
- Create: src/mobilebill-web/src/App.tsx
- Create: src/mobilebill-web/src/App.test.tsx
- Create: src/mobilebill-web/src/layouts/AppLayout.tsx
- Create: src/mobilebill-web/src/pages/DashboardPage.tsx
- Create: src/mobilebill-web/src/pages/NotFoundPage.tsx
- Create: src/mobilebill-web/src/test/setup.ts
- Modify: src/mobilebill-web/src/main.tsx
- Modify: src/mobilebill-web/src/index.css
- Modify: src/mobilebill-web/vite.config.ts

**Interfaces:**
- Consumes: React, Material UI, React Router, and Vitest dependencies.
- Produces: an App component mounted at / with a Dashboard route and fallback route.

- [ ] **Step 1: Scaffold Vite and install UI/test dependencies**

Run npm create vite@latest src/mobilebill-web -- --template react-ts, then install @mui/material, @emotion/react, @emotion/styled, react-router-dom, vitest, jsdom, @testing-library/react, and @testing-library/jest-dom.

- [ ] **Step 2: Write the failing shell test**

Create App.test.tsx that renders App and expects a heading named Mobile Bill Management and a navigation link named Dashboard. Configure Vitest to use jsdom and the jest-dom setup file.

- [ ] **Step 3: Run the test and verify failure**

Run npm run test -- --run from src/mobilebill-web. The generated Vite app must fail the required heading/navigation assertions.

- [ ] **Step 4: Implement the minimal routed Material UI shell**

Create BrowserRouter routes for / and a wildcard fallback. AppLayout provides an AppBar with Mobile Bill Management, a permanent Dashboard navigation item, and a main content area. DashboardPage renders a Foundation Ready message and states that business features are not yet implemented.

- [ ] **Step 5: Run the frontend test and verify success**

Run npm run test -- --run from src/mobilebill-web. The App test must pass.

### Task 5: Verify the complete foundation

**Files:**
- Modify: docs/SYSTEM_DESIGN.md only if verification identifies a documented mismatch.

**Interfaces:**
- Consumes: completed Tasks 1-4.
- Produces: evidence that the requested commands pass.

- [ ] **Step 1: Restore dependencies**

Run dotnet restore from the repository root and npm install from src/mobilebill-web.

- [ ] **Step 2: Build and test .NET**

Run dotnet build and dotnet test from the repository root. Both commands must succeed.

- [ ] **Step 3: Build the frontend**

Run npm run build from src/mobilebill-web. It must succeed.

- [ ] **Step 4: Inspect the final changes**

Run git diff --check and git status --short. Confirm that no generated sample endpoint, generated placeholder test, or business feature remains.
