FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY OficinaMecanica.sln ./
COPY src/OficinaMecanica.Contracts/OficinaMecanica.Contracts.csproj src/OficinaMecanica.Contracts/
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
# A porta 8080 vem do ASPNETCORE_HTTP_PORTS da imagem base (aspnet:8.0); redefinir ASPNETCORE_URLS aqui geraria o aviso
# "Overriding HTTP_PORTS" a cada boot.
# Usuario nao-root: a imagem base ja traz o usuario app (UID em $APP_UID).
USER $APP_UID
ENTRYPOINT ["dotnet", "OficinaMecanica.API.dll"]
