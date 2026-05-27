# syntax=docker/dockerfile:1.7

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + shared props first for restore caching
COPY *.sln Directory.Build.props ./

# Copy csproj files first so dotnet restore can be cached independently of source.
COPY src/Shared/Memoria.Shared.Kernel/*.csproj            ./src/Shared/Memoria.Shared.Kernel/
COPY src/Shared/Memoria.Shared.Infrastructure/*.csproj    ./src/Shared/Memoria.Shared.Infrastructure/
COPY src/Modules/Users/Memoria.Users.Contracts/*.csproj   ./src/Modules/Users/Memoria.Users.Contracts/
COPY src/Modules/Users/Memoria.Users/*.csproj             ./src/Modules/Users/Memoria.Users/
COPY src/Modules/Cards/Memoria.Cards.Contracts/*.csproj   ./src/Modules/Cards/Memoria.Cards.Contracts/
COPY src/Modules/Cards/Memoria.Cards/*.csproj             ./src/Modules/Cards/Memoria.Cards/
COPY src/Modules/Reminders/Memoria.Reminders.Contracts/*.csproj ./src/Modules/Reminders/Memoria.Reminders.Contracts/
COPY src/Modules/Reminders/Memoria.Reminders/*.csproj     ./src/Modules/Reminders/Memoria.Reminders/
COPY src/Modules/Reviews/Memoria.Reviews.Contracts/*.csproj ./src/Modules/Reviews/Memoria.Reviews.Contracts/
COPY src/Modules/Reviews/Memoria.Reviews/*.csproj         ./src/Modules/Reviews/Memoria.Reviews/
COPY src/Memoria.Bot/*.csproj                             ./src/Memoria.Bot/
COPY src/Memoria.Api/*.csproj                             ./src/Memoria.Api/
COPY src/Memoria.Host/*.csproj                            ./src/Memoria.Host/

RUN dotnet restore src/Memoria.Host/Memoria.Host.csproj

# Now bring in the rest of the source and publish.
COPY src/ ./src/

WORKDIR /src/src/Memoria.Host
RUN dotnet publish Memoria.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl нужен docker-compose healthcheck-у (см. docker-compose.yml).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Memoria.Host.dll"]
