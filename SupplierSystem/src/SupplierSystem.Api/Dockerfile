# =============================================
# Dockerfile for Supplier .NET API (Alpine)
# =============================================

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY src/SupplierSystem.Domain/SupplierSystem.Domain.csproj ./src/SupplierSystem.Domain/
COPY src/SupplierSystem.Application/SupplierSystem.Application.csproj ./src/SupplierSystem.Application/
COPY src/SupplierSystem.Infrastructure/SupplierSystem.Infrastructure.csproj ./src/SupplierSystem.Infrastructure/
COPY src/SupplierSystem.Api/SupplierSystem.Api.csproj ./src/SupplierSystem.Api/
COPY tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj ./tests/SupplierSystem.Tests/

# Copy solution file
COPY *.sln ./

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build the solution
RUN dotnet publish -c Release -o /app/publish --no-restore

# =============================================
# Production Stage (Alpine - ~100MB)
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install dependencies for System.Drawing (Excel processing) and curl
RUN apk add --no-cache libgdiplus curl libc6-compat

# Create non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Copy published output from build stage
COPY --from=build --chown=appuser:appgroup /app/publish .

# Create necessary directories
RUN mkdir -p uploads logs && chown -R appuser:appgroup uploads logs

# Switch to non-root user
USER appuser

# Expose ports (HTTP and HTTPS)
EXPOSE 5000 5001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget -q --spider http://localhost:5001/health/healthz || exit 1

# Start the application
ENTRYPOINT ["dotnet", "SupplierSystem.Api.dll"]
