# Build from the repository root, which is where the shared Directory.*.props files live:
#   docker build -f FinanceTracker.Worker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first, so the restore layer is reused until a dependency changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY FinanceTracker.Worker/FinanceTracker.Worker.csproj FinanceTracker.Worker/
COPY FinanceTracker.Domain/FinanceTracker.Domain.csproj FinanceTracker.Domain/
COPY FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj FinanceTracker.Infrastructure/
RUN dotnet restore FinanceTracker.Worker/FinanceTracker.Worker.csproj

COPY . .
RUN dotnet publish FinanceTracker.Worker/FinanceTracker.Worker.csproj -c Release -o /app --no-restore

# Runtime only: the worker is a run-and-exit console app and serves no HTTP.
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "FinanceTracker.Worker.dll"]
