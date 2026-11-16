# Shared

Contratos de la API (DTO) comunes al juego y al servidor, para que los dos usen exactamente los mismos objetos.

El código está en `Runtime/ApiContracts.cs` y se compila de dos formas:

- **Servidor**: `Late.Shared.csproj` (.NET Standard 2.1), referenciado desde `server/`.
- **Unity**: la carpeta es un paquete local (`package.json` + `Runtime/Late.Shared.asmdef`). Se añade al juego con
  `"com.late.shared": "file:../../shared"` en `LateGame/Packages/manifest.json`.

Por eso solo usa C# compatible con Unity 2022.3: clases con propiedades, sin `record` ni tipos exclusivos de .NET moderno.
Las carpetas de compilación son `bin~/` y `obj~/`, que Unity ignora.
