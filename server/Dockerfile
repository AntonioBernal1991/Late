# Imagen de la API. Se construye desde la raíz del repositorio porque necesita shared/:
#   docker build -f server/Dockerfile .
# (docker/docker-compose.yml ya lo hace así.)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los proyectos: la restauración de paquetes queda en caché mientras no cambien.
COPY shared/Late.Shared.csproj shared/Directory.Build.props shared/
COPY server/src/Late.Api/Late.Api.csproj server/src/Late.Api/
RUN dotnet restore server/src/Late.Api/Late.Api.csproj

COPY shared/ shared/
COPY server/src/ server/src/
RUN dotnet publish server/src/Late.Api/Late.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Usuario sin privilegios que ya trae la imagen oficial.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Late.Api.dll"]
