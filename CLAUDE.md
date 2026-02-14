# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **supplier management system** with a .NET 9 backend and Vue 3 frontend:

- **Backend** (`SupplierSystem/`): .NET 9 Web API with SQL Server
- **Frontend** (`app/apps/web/`): Vue 3 + TypeScript + Element Plus

## Commands

### .NET API
```bash
cd SupplierSystem
dotnet run                # Start API (port 5001)
dotnet build              # Build solution
dotnet test               # Run tests
```

### Vue Frontend
```bash
cd app
npm run dev:web           # Start dev server (port 5173)
npm run build             # Build for production
npm run test:e2e          # Playwright E2E tests
npm run format            # Prettier format
npm run format:check      # Check formatting
```

## Architecture

### .NET API Structure (`SupplierSystem/src/`)
```
SupplierSystem/src/
├── SupplierSystem.Api/           # Web API controllers (~60 controllers)
├── SupplierSystem.Application/   # DTOs, interfaces, auth models
├── SupplierSystem.Domain/        # Entity definitions (~90 entities)
├── SupplierSystem.Infrastructure/# EF Core services, DbContext
└── SupplierSystem.Tests/         # xUnit tests
```

Key patterns:
- Controller -> Service -> Repository pattern
- Entity Framework Core with Code-First approach
- JWT authentication with refresh tokens

### Vue Frontend Structure (`app/apps/web/src/`)
```
apps/web/src/
├── api/                  # Axios API clients for each domain
├── components/           # Reusable Vue components
├── views/                # Page-level components
├── stores/               # Pinia stores (auth, permissions, supplier)
├── composables/          # Vue composables (usePermission, useFileUpload)
├── router/               # Vue Router configuration
├── i18n.ts               # Internationalization setup
├── services/             # HTTP client, notification service
└── utils/                # Helpers, role mapping, formatting
```

## Database

**Backend**: SQL Server
- Connection string in `SupplierSystem/appsettings.json`
- Target: `172.21.90.165:1433`, Database: `SMP`
- Entity Framework Core migrations

## Environment Configuration

### .NET API
Configuration via `appsettings.json`:
- `ConnectionStrings:SupplierSystem` - SQL Server connection
- `Jwt` - JWT authentication settings
- `Cors` - Allowed origins

### Vue Frontend
Environment variables with `VITE_*` prefix:
- `VITE_API_BASE_URL` - Backend API URL (default: `http://localhost:5001`)
- `VITE_UPLOAD_URL` - Upload endpoint URL

## API Communication

Frontend requests are proxied via Vite dev server:

```
Frontend -> /api/* -> http://localhost:5001 (ASP.NET Core)
```

Configure via `vite.config.ts`:
```typescript
proxy: {
  '/api': {
    target: env.VITE_API_BASE_URL || 'http://localhost:5001',
    changeOrigin: true,
  }
}
```

## Security Features

- **JWT authentication**: Bearer tokens with refresh token support
- **Rate limiting**: Configured per endpoint
- **CORS**: Configurable origin whitelist
- **Password hashing**: Argon2

## Common Development Tasks

### Adding a New API Endpoint
1. Create entity in `SupplierSystem.Domain/Entities/`
2. Add DbSet in `SupplierSystem.Infrastructure/Data/AppDbContext.cs`
3. Create DTO in `SupplierSystem.Application/DTOs/`
4. Create repository in `SupplierSystem.Infrastructure/Repositories/`
5. Create service in `SupplierSystem.Application/Services/`
6. Create controller in `SupplierSystem.Api/Controllers/`
7. Add API client in `app/apps/web/src/api/{domain}.ts`

### Adding a New Vue Page
1. Create view in `app/apps/web/src/views/{Name}View.vue`
2. Add route in `app/apps/web/src/router/index.ts`
3. Add menu permission in `src/constants/permissions.ts`
4. Create API client in `src/api/{name}.ts`

## Testing Frameworks

- **Backend**: xUnit (.NET built-in test framework)
- **Frontend**: Vitest (unit), Playwright (E2E)

## Notes

- The .NET API is the only backend - all business logic lives there
- Frontend proxies API requests via Vite during development
- SQL Server database must be accessible at `172.21.90.165`
