# Memoria del TFG

Fuente de `docs/Memoria_TFG_Late.pdf` (esqueleto de thePower / Prometeo).

- `content.html`: texto de la memoria.
- `diagrams.py`: diagramas (arquitectura, casos de uso, clases, E/R, base de datos, navegación, red, Gantt).
- `capturas/`: pantallas reales del juego a 1080 × 2160.
- `hours.json` (opcional): horas reales de las fases 6 a 10, p. ej. `{"6": 14, "7": 18, "8": 35, "9": 28, "10": 25}`.

Regenerar (necesita Python con `pypdf`, Node y Microsoft Edge):

```
pip install pypdf
python build.py ../Memoria_TFG_Late.pdf
```
