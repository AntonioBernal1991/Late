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

El proyecto de Unity está en la carpeta `LateGame/`: ábrelo desde Unity Hub con *Add project from disk*.

## Estructura

| Carpeta | Contenido |
|---|---|
| `LateGame/` | Juego en Unity para Android y Windows |
| `server/` | API REST en ASP.NET Core: usuarios, partidas y ranking |
| `shared/` | Modelos de datos comunes al cliente y al servidor |
| `docker/` | Contenedores de la API y la base de datos MySQL |
| `docs/` | Anteproyecto y documentación técnica |

Dentro del cliente, el código y los recursos propios están en `LateGame/Assets/Late/`:

| Carpeta | Contenido |
|---|---|
| `Scripts` | Código del juego, organizado por sistemas |
| `Editor` | Herramientas del editor, incluido el generador de niveles |
| `Levels` | Niveles generados, horneados y optimizados para móvil |
| `Prefabs` | Prefabs reutilizables |
| `Materials` | Materiales |
| `Audio` | Música y efectos |
| `Scenes` | Escenas del juego |

Los recursos de terceros van en `LateGame/Assets/ThirdParty/`.

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

Solo se incluyen los archivos que usa el juego, en `LateGame/Assets/ThirdParty/` (salvo TextMesh Pro, que va en su carpeta estándar).

| Recurso | Uso | Licencia |
|---|---|---|
| EYE ADVANCED (Tanuki Digital) | Ojo de la secuencia final | Asset Store, de pago: no redistribuir |
| Rust Key | Llave | Asset Store |
| SpaceSkies Free | Skyboxes de los niveles | Asset Store, gratuito |
| Progressive Trance Vol. 1 | Música de los niveles | Asset Store |
| JetBrains Mono | Fuente de la UI | SIL Open Font License |
| TextMesh Pro | Textos de la UI | Paquete de Unity |

Por las licencias de la Asset Store, el repositorio debe ser privado.
