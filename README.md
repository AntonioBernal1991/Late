# Late

Late es un videojuego 3D para Android y PC ambientado en un mundo abstracto de laberintos. El objetivo es siempre el mismo: llegar al final del laberinto antes de que termine la música. La canción es el cronómetro y no hay reloj en pantalla.

Proyecto intermodular del CFGS Desarrollo de Aplicaciones Multiplataforma.

- **Alumno:** Antonio Bernal
- **Tutor:** Jordi Cidoncha

## Estado

En desarrollo. El proyecto se construye por partes, siguiendo el RFTP (requisitos, funciones, tareas y pruebas) del anteproyecto.

## Requisitos

- Unity 2022.3.38f1 LTS
- Git LFS, para audio, texturas, modelos y niveles
- .NET 8 SDK y Docker, para el servidor (a partir de la fase 5)

Después de clonar el repositorio, ejecuta:

```
git lfs install
git lfs pull
```

El proyecto de Unity está en la carpeta `client/`: ábrelo desde Unity Hub con *Add project from disk*.

## Estructura

| Carpeta | Contenido |
|---|---|
| `client/` | Juego en Unity para Android y Windows |
| `server/` | API REST en ASP.NET Core: usuarios, partidas y ranking |
| `shared/` | Modelos de datos comunes al cliente y al servidor |
| `docker/` | Contenedores de la API y la base de datos MySQL |
| `docs/` | Anteproyecto y documentación técnica |

Dentro del cliente, el código y los recursos propios están en `client/Assets/Late/`:

| Carpeta | Contenido |
|---|---|
| `Scripts` | Código del juego, organizado por sistemas |
| `Editor` | Herramientas del editor, incluido el generador de niveles |
| `Levels` | Niveles generados, horneados y optimizados para móvil |
| `Prefabs` | Prefabs reutilizables |
| `Materials` | Materiales |
| `Audio` | Música y efectos |
| `Scenes` | Escenas del juego |

Los recursos de terceros van en `client/Assets/ThirdParty/`.

## Fases

| Fase | Contenido |
|---|---|
| 1 | Generador procedural de niveles |
| 2 | Gameplay, bucle de juego y escenas |
| 3 | Optimización para móvil y contenido final |
| 4 | Datos locales con SQLite |
| 5 | Servidor: API, base de datos y Docker |
| 6 | Integración online: login, resultados y ranking |
| 7 | Pruebas, integración continua y builds |

## Convención de commits

Cada commit empieza por el código de la tarea del RFTP a la que corresponde:

```
R01F01T01: generador de módulos y caminos
```

Los cambios de configuración o documentación que no corresponden a una tarea usan el prefijo `chore:` o `docs:`.

## Recursos de terceros

Los recursos de terceros que use el juego se listarán aquí con su licencia.
