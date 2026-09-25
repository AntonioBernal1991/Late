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

Después de clonar el repositorio, ejecuta:

```
git lfs install
git lfs pull
```

## Estructura

| Carpeta | Contenido |
|---|---|
| `Assets/Late/Scripts` | Código del juego, organizado por sistemas |
| `Assets/Late/Editor` | Herramientas del editor, incluido el generador de niveles |
| `Assets/Late/Levels` | Niveles generados, horneados y optimizados para móvil |
| `Assets/Late/Prefabs` | Prefabs reutilizables |
| `Assets/Late/Materials` | Materiales |
| `Assets/Late/Audio` | Música y efectos |
| `Assets/Late/Scenes` | Escenas del juego |

## Convención de commits

Cada commit empieza por el código de la tarea del RFTP a la que corresponde:

```
R01F01T01: generador de módulos y caminos
```

Los cambios de configuración o documentación que no corresponden a una tarea usan el prefijo `chore:` o `docs:`.

## Recursos de terceros

Los recursos de terceros que use el juego se listarán aquí con su licencia.
