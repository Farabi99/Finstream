# ============================================================================
# FINSTREAM DOCKERFILE — Multi-Stage Build
# ============================================================================
#
# This Dockerfile builds BOTH the API and Processor from a single file.
# docker-compose uses the "target" property to pick which stage to run.
#
# HOW IT WORKS:
#   Stage 1 ("build"):     Compiles the entire .NET solution
#   Stage 2a ("api"):      Lightweight runtime image for the REST API + WebSocket
#   Stage 2b ("processor"): Lightweight runtime image for the background worker
#
# WHY MULTI-STAGE?
#   - The .NET SDK image is ~1.8GB (needed to compile)
#   - The runtime images are ~220MB (only needed to run)
#   - Multi-stage builds keep only the compiled output → small, fast images
#
# USAGE (via docker-compose):
#   docker-compose up --build
#
# USAGE (manual, if you want to build individually):
#   docker build --target api -t finstream-api .
#   docker build --target processor -t finstream-processor .
# ============================================================================

# ──────────────────────────────────────────────────────────────────────────────
# STAGE 1: BUILD — Compile the entire .NET solution
# ──────────────────────────────────────────────────────────────────────────────
# Uses the full .NET SDK image which includes the compiler, NuGet, etc.
# This stage is thrown away after compilation — it never runs in production.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# OPTIMIZATION: Copy only .csproj and .sln files first, then restore.
# Docker caches layers — if the .csproj files haven't changed, the NuGet
# restore step is SKIPPED on subsequent builds (huge time saver!)
COPY FinStream.sln ./
COPY FinStream.Domain/FinStream.Domain.csproj FinStream.Domain/
COPY FinStream.Application/FinStream.Application.csproj FinStream.Application/
COPY FinStream.Infrastructure/FinStream.Infrastructure.csproj FinStream.Infrastructure/
COPY FinStream.API/FinStream.API.csproj FinStream.API/
COPY FinStream.Processor/FinStream.Processor.csproj FinStream.Processor/
COPY FinStream.Tests/FinStream.Tests.csproj FinStream.Tests/
COPY FinStream.IntegrationTests/FinStream.IntegrationTests.csproj FinStream.IntegrationTests/

# Restore NuGet packages for the entire solution
# This downloads all dependencies (StackExchange.Redis, EF Core, etc.)
RUN dotnet restore FinStream.sln

# Now copy ALL source code and build
# This layer is only rebuilt when actual source code changes
COPY . .

# Publish the API project in Release mode (optimized, no debug symbols)
# -o /app/api puts the output in a clean directory
RUN dotnet publish FinStream.API/FinStream.API.csproj -c Release -o /app/api --no-restore

# Publish the Processor project in Release mode
RUN dotnet publish FinStream.Processor/FinStream.Processor.csproj -c Release -o /app/processor --no-restore

# ──────────────────────────────────────────────────────────────────────────────
# STAGE 2a: API RUNTIME — The "Read Side" (REST API + WebSocket + Swagger)
# ──────────────────────────────────────────────────────────────────────────────
# Uses the ASP.NET runtime image (includes Kestrel web server, but NOT the compiler)
# This is what actually runs in production.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app

# Copy only the compiled output from Stage 1
COPY --from=build /app/api .

# Tell ASP.NET Core to listen on port 5106 on ALL interfaces (0.0.0.0)
# This is critical for Docker — without it, the container only listens on localhost
# which means your browser can't reach it from outside the container.
EXPOSE 5106
ENV ASPNETCORE_URLS=http://+:5106

# Set to Development so Swagger UI is enabled.
# In production, you'd set this to "Production" and Swagger would be hidden.
ENV ASPNETCORE_ENVIRONMENT=Development

# Start the API
ENTRYPOINT ["dotnet", "FinStream.API.dll"]

# ──────────────────────────────────────────────────────────────────────────────
# STAGE 2b: PROCESSOR RUNTIME — The "Write Side" (Background Worker)
# ──────────────────────────────────────────────────────────────────────────────
# Uses the base .NET runtime image (no web server needed — Processor has no HTTP)
# This is lighter than aspnet:8.0 because it doesn't include Kestrel.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS processor
WORKDIR /app

# Copy only the compiled output from Stage 1
COPY --from=build /app/processor .

# Start the Processor background worker
ENTRYPOINT ["dotnet", "FinStream.Processor.dll"]
