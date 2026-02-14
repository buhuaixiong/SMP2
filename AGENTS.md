# Repository Guidelines

## Project Structure & Module Organization
- `app/` houses the frontend workspace and shared tooling.
  - `app/apps/web/` is the Vue 3 + TypeScript app (`src/` for code, `tests/` for unit tests).
  - `app/packages/common/` contains shared utilities.
  - `app/tools/`, `app/scripts/`, and `app/docs/` hold helper scripts and documentation.
- `SupplierSystem/` is the .NET 9 API solution.
  - `SupplierSystem/src/` contains `SupplierSystem.Api`, `SupplierSystem.Application`, `SupplierSystem.Domain`, and `SupplierSystem.Infrastructure`.
  - `SupplierSystem/tests/` holds xUnit tests.
  - `SupplierSystem/sql/` contains SQL scripts.
- `backups/` and `app/archive/` store database backups; do not edit these by hand.

## Build, Test, and Development Commands
- Frontend (from `app/`):
  - `npm run dev:web` starts the Vite dev server.
  - `npm run build` type-checks and builds the web app.
  - `npm run test:web` runs Vitest unit tests.
- Frontend E2E (from `app/apps/web/`):
  - `npm run test:e2e` runs Playwright.
- Full dev stack (from `app/`):
  - `npm run dev:all` starts the web app plus the .NET API.
- .NET API (from `SupplierSystem/`):
  - `dotnet run` starts the API.
  - `dotnet build` builds the solution.
  - `dotnet test` runs xUnit tests.

## Coding Style & Naming Conventions
- Frontend: Prettier enforces 2-space indentation, semicolons, double quotes, and `printWidth: 100`. Format with `npm run format`.
- C#: `SupplierSystem/.editorconfig` sets 4-space indentation, `I`-prefixed interfaces, `_camelCase` private fields, and `Async` suffixes for async methods.

## Testing Guidelines
- Web tests live in `app/apps/web/tests` and use `.spec.ts` or `.test.ts` filenames.
- .NET tests live in `SupplierSystem/tests` and use `*Tests.cs` filenames.
- Add or update tests for new features and bug fixes; no coverage target is enforced in this repo.

## Commit & Pull Request Guidelines
- This checkout has no Git history; use Conventional Commits (e.g., `feat(web): add supplier filters`).
- PRs should include a clear description, test commands run, linked issues, and screenshots for UI changes.
- Call out SQL or config changes (`SupplierSystem/sql`, `app/.env*`, `SupplierSystem/src/SupplierSystem.Api/appsettings*.json`).
