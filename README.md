# inventory-pos-backend
Backend .NET para sistema POS offline-first

## Requisitos

- **.NET 8 SDK** (8.0.100 o superior, ver `global.json`).
- **Visual Studio 2022 17.8+** con las cargas de trabajo:
  - *ASP.NET and web development* (para `InventoryPOS.API`).
  - *.NET desktop development* (para `InventoryPOS.Desktop`, solo Windows; necesita el runtime de WebView2, que ya trae Windows 10/11 actualizado).
- SQL Server **no** es necesario para desarrollar: en `Development` la API usa SQLite (`InventoryPOS.API/App_Data/inventorypos-dev.db`, se crea solo).

## Ejecutar y depurar en Visual Studio

1. Abre `InventoryPOS.sln`.
2. Clic derecho en **InventoryPOS.API** → *Set as Startup Project*.
3. Elige el perfil **http** junto al botón ▶ y pulsa F5. Se abre `http://localhost:5080`.
   La primera vez la app pide crear el usuario administrador.
   Swagger queda en `http://localhost:5080/swagger`.

Para la caja de escritorio, usa **InventoryPOS.Desktop** como proyecto de inicio (solo Windows).

### Usar SQL Server LocalDB en lugar de SQLite (opcional)

Quita la sección `Database` / `ConnectionStrings` de `InventoryPOS.API/appsettings.Development.json`
para volver a `Server=(localdb)\MSSQLLocalDB` (requiere el componente *SQL Server Express LocalDB* del instalador de Visual Studio).

## Desde la terminal

```
dotnet run --project InventoryPOS.API --launch-profile http
dotnet test
```
