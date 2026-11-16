# Servidor

API REST en ASP.NET Core 10 con Entity Framework Core y MySQL. Gestiona usuarios, partidas y el ranking por nivel.

## Estructura

| Carpeta | Contenido |
|---|---|
| `src/Late.Api/Controllers` | Endpoints: `AuthController`, `RunsController`, `LevelsController` |
| `src/Late.Api/Data` | `LateDbContext`, entidades y migraciones de EF Core |
| `src/Late.Api/Auth` | Creación de tokens JWT |
| `tests/Late.Api.Tests` | Pruebas de integración con xUnit contra un MySQL real (Testcontainers) |

Los contratos (DTO) están en [`../shared`](../shared) y los usa también el juego.

## Endpoints

| Método | Ruta | Cuenta | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | No | Crea la cuenta y devuelve el token |
| POST | `/api/auth/login` | No | Entra con nombre o email y devuelve el token |
| POST | `/api/runs` | Sí | Envía 1 a 100 partidas (las pendientes de SQLite) |
| GET | `/api/levels` | No | Niveles del juego |
| GET | `/api/levels/{orden}/ranking?top=10` | No | Mejor tiempo de cada jugador ganando a la música |
| GET | `/health` | No | La API y la base de datos responden |

Detalles:

- **Contraseñas**: hash PBKDF2 con sal (`PasswordHasher` de ASP.NET Core Identity). Login y registro limitados a 10 intentos por minuto e IP.
- **Token**: JWT firmado con HMAC-SHA256, válido 30 días. Se envía como `Authorization: Bearer <token>`.
- **Reenvíos**: una partida se identifica por usuario, nivel y fecha (con milisegundos). Si el juego la reenvía, cuenta como `alreadyStored` y no se duplica.
- **Partidas inválidas** (nivel inexistente, tiempo fuera de 0-3600 s, fecha futura): cuentan como `rejected` y no se guardan.

## Ejecutar

Con Docker, la API y MySQL juntos: ver [`../docker`](../docker).

Sin Docker hace falta el .NET 10 SDK y un MySQL accesible:

```
cd server/src/Late.Api
dotnet user-secrets set "Jwt:Key" "<al menos 32 caracteres>"
dotnet user-secrets set "ConnectionStrings:Default" "server=localhost;port=3306;database=late;user=late;password=..."
dotnet ef database update
dotnet run
```

## Pruebas

Necesitan Docker en marcha (cada ejecución levanta un MySQL temporal):

```
cd server
dotnet test
```

Sin el SDK instalado, dentro de un contenedor:

```
docker run --rm -v "$PWD:/src" -v /var/run/docker.sock:/var/run/docker.sock \
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal -w /src/server \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test
```

## Migraciones

Al cambiar las entidades:

```
dotnet ef migrations add <Nombre> --project src/Late.Api --output-dir Data/Migrations
```

En Docker se aplican solas al arrancar (`Database__MigrateOnStartup=true`).
