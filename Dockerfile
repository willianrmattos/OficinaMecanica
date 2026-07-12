FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY OficinaMecanica.sln ./
COPY src/OficinaMecanica.Domain/OficinaMecanica.Domain.csproj src/OficinaMecanica.Domain/
COPY src/OficinaMecanica.Application/OficinaMecanica.Application.csproj src/OficinaMecanica.Application/
COPY src/OficinaMecanica.Infrastructure/OficinaMecanica.Infrastructure.csproj src/OficinaMecanica.Infrastructure/
COPY src/OficinaMecanica.API/OficinaMecanica.API.csproj src/OficinaMecanica.API/

RUN dotnet restore src/OficinaMecanica.API/OficinaMecanica.API.csproj

COPY src/ src/
RUN dotnet publish src/OficinaMecanica.API/OficinaMecanica.API.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
# Utilizando usuario nao-root (ja vem criado na imagem base mcr.microsoft.com/dotnet/aspnet:8.0)
USER $APP_UID
ENTRYPOINT ["dotnet", "OficinaMecanica.API.dll"]
