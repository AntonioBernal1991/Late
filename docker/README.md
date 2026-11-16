# Docker

`docker-compose.yml` levanta la API y MySQL 8.4.

```
cd docker
cp .env.example .env      # y rellena las contraseñas y JWT_KEY
docker compose up -d --build
```

- API: http://localhost:8080 (comprobar con http://localhost:8080/health)
- Swagger, para probar los endpoints desde el navegador: http://localhost:8080/swagger (si `SWAGGER_ENABLED=true`)
- Las migraciones se aplican solas al arrancar la API.
- Los datos de MySQL se guardan en el volumen `late_mysql-data`. MySQL no se expone fuera de Docker.

Comandos útiles:

```
docker compose logs -f api     # ver el log de la API
docker compose down            # parar (conserva los datos)
docker compose down -v         # parar y borrar los datos
```

El archivo `.env` tiene secretos: está en `.gitignore` y nunca se sube.
