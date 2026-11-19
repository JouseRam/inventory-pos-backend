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

## Facturación electrónica (CFDI 4.0)

Las facturas se emiten desde la caja con un PAC. Están integrados **Facturama** (API Multiemisor) y **Facturapi**;
el usuario elige y cambia de proveedor en Configuración → Facturación (cada uno guarda su cuenta, y las facturas
emitidas se siguen descargando y cancelando con el proveedor que las timbró). Cada tarjeta enlaza a la página de pago
del proveedor; el pago se hace en su sitio. Otro proveedor se agrega implementando `IInvoiceProvider` (con su `ProviderInfo`)
y registrándolo en `InventoryPOS.Infrastructure/DependencyInjection.cs`.

1. **Configuración → Negocio:** RFC, razón social, régimen fiscal y código postal de expedición.
2. **Configuración → Facturación:** proveedor, ambiente (pruebas o producción), credenciales de la cuenta
   y el CSD (.cer, .key y su contraseña; en Facturapi se sube en su panel). La contraseña se guarda cifrada con la llave `Jwt:Key` de la instalación;
   el CSD se envía al proveedor y no se guarda en la base de datos.
3. **Permisos:** el módulo *Facturación* (ver = descargar, editar = timbrar y cancelar).

En el punto de venta, al cobrar aparece **Facturar**; también desde **Ventas → detalle**. Sin internet se avisa y no se timbra.
En `Development` la API arranca como caja (`Pos:Role=Register`) para poder vender y facturar con F5.

## Manuales de usuario (módulo Documentación)

Los manuales en PDF están en `InventoryPOS.API/wwwroot/docs/manuals/` y se consultan en **Sistema → Documentación**
(también sin internet; la caja de escritorio los incluye). El texto vive en `docs/manual/*.md`, uno por módulo.

Para regenerarlos después de cambiar el texto o las pantallas (toma capturas reales de la app):

```
dotnet run --project InventoryPOS.API --launch-profile http -- --ConnectionStrings:Sqlite="Data Source=App_Data/manuals.db" --Database:BackupDirectory=App_Data/backups
cd build/manuals && npm install && npx playwright install chromium
npm run manuals
```

Usa una base de datos vacía: el script crea un administrador de demostración y datos de ejemplo.

## Licencias: una activación por computadora

La caja de escritorio solo abre en la computadora para la que se activó. La primera vez muestra un **código de equipo**
(sacado del identificador de Windows, el UUID y el número de serie de la tarjeta madre); el distribuidor genera con ese
código una **clave de activación** firmada con su llave privada. Si se copia el programa o su carpeta de datos a otra
computadora, el código cambia y vuelve a pedir activación. Reinstalar Windows o cambiar una pieza no la invalida
(basta con que coincidan 2 de los 3 datos); cambiar de computadora sí.

**Una sola vez**, en tu computadora (no en la del cliente):

```
dotnet run --project InventoryPOS.LicenseTool -- llaves
```

Crea `llave-privada-licencias.txt` (guárdala con respaldo y **nunca** la subas al repositorio; `.gitignore` ya la
excluye) e imprime la llave pública: pégala en `InventoryPOS.Desktop/LicensePublicKey.cs`. `build/publish-desktop.ps1`
no publica sin ella. Si pierdes la llave privada tendrás que crear otra, publicar de nuevo y reactivar todas las cajas.

**Por cada computadora** que te envíen su código:

```
dotnet run --project InventoryPOS.LicenseTool -- activar --codigo XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX --cliente "Anna’s Beauty Supply" --salida clave.txt
```

Opcional: `--vence AAAA-MM-DD` para una licencia con fecha de vencimiento. Manda la clave (texto o archivo) al cliente;
la pega o carga en la pantalla de activación. La clave queda en `%LOCALAPPDATA%\MatizPOS\licencia.key`.

La llave pública ya está en `LicensePublicKey.cs`; la privada la guarda el distribuidor fuera del repositorio.

Para el instalador de un solo archivo (accesos directos con el icono, desinstalador, sin permisos de administrador):
`bash build/installer/build.sh` genera `artifacts/Instalar-AnnasBeautySupply.exe` (requiere NSIS: `apt install nsis`).

Para compilar la caja sin Windows (Linux o macOS): `bash build/desktop-linux/publish.sh` genera la carpeta lista para
copiar a la computadora (mismo código, con Windows Forms tomado de sus paquetes de NuGet). En Windows usa
`build/publish-desktop.ps1`.

Al depurar desde Visual Studio (Debug) no se pide activación; para probarla define la variable `MATIZPOS_LICENSE=1`.
