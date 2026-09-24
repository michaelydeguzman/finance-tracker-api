# Build from the repository root, which is where the shared Directory.*.props files live:
#   docker build -f FinanceTracker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first, so the restore layer is reused until a dependency changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY FinanceTracker/FinanceTracker.API.csproj FinanceTracker/
COPY FinanceTracker.Application/FinanceTracker.Application.csproj FinanceTracker.Application/
COPY FinanceTracker.Domain/FinanceTracker.Domain.csproj FinanceTracker.Domain/
COPY FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj FinanceTracker.Infrastructure/
RUN dotnet restore FinanceTracker/FinanceTracker.API.csproj

COPY . .
RUN dotnet publish FinanceTracker/FinanceTracker.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# The commit this image was built from; the deploy pipeline passes it in.
ARG SOURCE_SHA=
ENV SOURCE_SHA=$SOURCE_SHA

# The base image's non-root user. It listens on 8080 (ASPNETCORE_HTTP_PORTS); TLS ends at
# the platform's ingress, which forwards plain HTTP.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinanceTracker.API.dll"]
