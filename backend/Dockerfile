# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from just the project files, so this layer caches across code-only changes.
COPY GhumoOdisha.Api/GhumoOdisha.Api.csproj GhumoOdisha.Api/
COPY GhumoOdisha.Application/GhumoOdisha.Application.csproj GhumoOdisha.Application/
COPY GhumoOdisha.Infrastructure/GhumoOdisha.Infrastructure.csproj GhumoOdisha.Infrastructure/
COPY GhumoOdisha.Domain/GhumoOdisha.Domain.csproj GhumoOdisha.Domain/
RUN dotnet restore GhumoOdisha.Api/GhumoOdisha.Api.csproj

COPY GhumoOdisha.Api/ GhumoOdisha.Api/
COPY GhumoOdisha.Application/ GhumoOdisha.Application/
COPY GhumoOdisha.Infrastructure/ GhumoOdisha.Infrastructure/
COPY GhumoOdisha.Domain/ GhumoOdisha.Domain/

RUN dotnet publish GhumoOdisha.Api/GhumoOdisha.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GhumoOdisha.Api.dll"]
