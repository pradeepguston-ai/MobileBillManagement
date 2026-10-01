# Phase 1 Database Domain Model Implementation Plan

> **For agentic workers:** Execute this plan task-by-task with test-first verification.

**Goal:** Create the EF Core 10 domain model, SQL Server mapping, and InitialCreate migration for the approved mobile bill system without production data or processing features.

**Architecture:** Domain entities model master data, mobile allocations, bills, exceptions, approvals, and auditing. Infrastructure uses EF Core Fluent API for decimal precision, relationships, indexes, check constraints, and SQL Server triggers that reject effective-period overlaps.

**Tech Stack:** .NET 10, EF Core 10, SQL Server, xUnit.

**Spec:** docs/SYSTEM_DESIGN.md

## Constraints

- Money uses C# decimal and SQL Server decimal(18,2).
- No seed data, parser, endpoint, calculation, or workflow service is added.
- Preserve every PDF source-line amount, raw source text, page number, and extraction status.
- Mobile numbers can be reassigned but cannot have overlapping active allocation periods.

## Task 1: Domain entities and allocation-period guard

**Files:** Create Domain Common, Entities, and Enums folders; add EffectivePeriodTests.cs.

1. Write failing tests for inclusive date-period overlap, open-ended periods, and adjacent non-overlapping periods.
2. Run the filtered EffectivePeriodTests command and confirm it fails because the value object is missing.
3. Add auditable Guid-keyed entities, required enums, and EffectivePeriod.Overlaps.
4. Re-run the filtered test command and confirm it passes.

## Task 2: DbContext, mappings, and migration

**Files:** Modify MobileBillDbContext.cs; create Persistence Configurations and migrations.

1. Add DbSets and one Fluent configuration per aggregate/reference entity.
2. Configure required fields, relationships, status/mobile/EPF/period indexes, unique reference codes, decimal(18,2), and effective-date checks.
3. Generate InitialCreate with dotnet ef and add SQL Server triggers that reject overlapping active MobileAccount allocations and overlapping MobileEntitlement periods.

## Task 3: Verification

1. Run dotnet restore.
2. Run dotnet build --no-restore.
3. Run dotnet test --no-restore.
4. Inspect the InitialCreate migration and dotnet ef migrations list output for constraints, indexes, and triggers.
