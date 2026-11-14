# syntax=docker/dockerfile:1
# Multi-stage build: the SDK image builds; the small ASP.NET runtime image runs as a non-root user.
# Build from the repository root:  docker build -f backend/Dockerfile -t speakmate-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/SpeakMate.slnx backend/
COPY backend/src/SpeakMate.Domain/SpeakMate.Domain.csproj backend/src/SpeakMate.Domain/
COPY backend/src/SpeakMate.Application/SpeakMate.Application.csproj backend/src/SpeakMate.Application/
COPY backend/src/SpeakMate.Infrastructure/SpeakMate.Infrastructure.csproj backend/src/SpeakMate.Infrastructure/
COPY backend/src/SpeakMate.Api/SpeakMate.Api.csproj backend/src/SpeakMate.Api/
RUN dotnet restore backend/src/SpeakMate.Api/SpeakMate.Api.csproj
COPY .editorconfig ./
COPY backend/src backend/src
RUN dotnet publish backend/src/SpeakMate.Api/SpeakMate.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
# The image's built-in non-root user; the app cannot modify its own files.
USER $APP_UID
EXPOSE 8080
# Probe over HTTP from the orchestrator: liveness GET /health/live, readiness GET /health/ready.
ENTRYPOINT ["dotnet", "SpeakMate.Api.dll"]
