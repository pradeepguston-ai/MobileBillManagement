# Phase 7: Monthly Bill Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a historical, batch-specific monthly-bill review API and responsive React review page.

**Architecture:** An EF Core read-only query service returns full-batch summaries, server-filtered rows, and row details. Matching snapshots organisation attributes into `MonthlyBill`; React consumes DTOs only and never performs business calculations.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/SQL Server, React, TypeScript, Material UI, xUnit, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-07-phase-7-bill-review-design.md`

## Tasks

1. Add organisation snapshots to `MonthlyBill`, populate them in automatic/manual matching, configure EF, and generate a migration.
2. Add review DTOs, read-only query interface/service, whitelist sorting, full-batch KPIs, historical drawer projections, DI, and three API endpoints.
3. Add backend tests for batch ownership, KPI semantics, paging/filter/search/sort, unassessed rows, no duplicate rows with multiple exceptions, and snapshots.
4. Add React API types, route/page, KPI cards, server filters/sort/pagination, sticky responsive grid, and historical drawer.
5. Add focused frontend tests and run full backend/frontend verification.
