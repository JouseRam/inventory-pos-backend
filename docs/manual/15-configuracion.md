---
title: Configuración
module: Settings
icon: bi-sliders
summary: Datos del negocio, facturación, impresora, respaldos, usuarios y permisos, y la vinculación con la web.
---
La configuración está dividida en pestañas. Lo que se ve depende de tus permisos y de si estás en una caja o en la web.

## Negocio

![Datos del negocio](shot:settings-business)

- **Negocio:** nombre, RFC, dirección y teléfono. Aparecen en el encabezado del ticket.
- **Facturación:** razón social, régimen fiscal y código postal de expedición, tal como en tu constancia del SAT.
- **Ventas:** el **IVA incluido en los precios** (16 % por omisión) y el **pie del ticket**.
- **Inventario:** con cuántos días de anticipación avisar de una caducidad.

Pulsa **Guardar configuración**. En una caja vinculada a la web estos datos se administran desde la web y aquí solo se consultan.

## Facturación

El proveedor de facturación (PAC), su cuenta, su pago y el CSD. Ver el manual de **Facturación**.

## Impresora y respaldos

![Impresora y respaldos](shot:settings-printer)

**Impresora de tickets** (se configura en cada computadora):

1. **Conexión:** *Sin impresora*, *USB* (una impresora instalada en Windows) o *Red* (IP y puerto, normalmente 9100).
2. **Ancho del papel:** 80 mm o 58 mm.
3. **Acentos y ñ:** PC858 (recomendado), PC850 o sin acentos si la impresora no los muestra bien.
4. Activa **Imprimir el ticket al cobrar** y **Abrir el cajón cuando el cobro incluye efectivo** si los quieres automáticos.
5. Pulsa **Guardar** y luego **Imprimir prueba**. **Abrir cajón** prueba el cajón de dinero.

**Respaldos de la base de datos:** la caja hace un respaldo automático al día y guarda los últimos 30. **Respaldar ahora** crea uno al momento (recomendado antes de actualizar el programa).

## Usuarios y permisos

![Usuarios y permisos](shot:settings-users)

Solo para administradores.

1. Pulsa **Agregar usuario** y captura nombre completo, usuario, correo y contraseña (8 caracteres o más, con un número).
2. Marca **Administrador** para dar acceso total, incluida la gestión de usuarios.
3. Para los demás, marca por módulo **Ver** (consultar) o **Editar** (registrar y cambiar).
4. Desmarca **Activo** para que alguien ya no pueda entrar sin perder su historial.

Sugerencia para un cajero: *Ver/Editar* en Punto de venta, Caja, Ventas y Facturación; *Ver* en Existencias y Clientes.

## Sincronización con la web

![Sincronización](shot:settings-sync)

Sirve para que una o varias cajas envíen sus ventas, caja e inventario a la **web central**, y reciban de ella el catálogo, los precios y los usuarios.

**En la web:** en **Cajas vinculadas** pulsa **Agregar caja**; se genera un **código de vinculación**.

**En la caja:**

1. Captura la **Dirección de la web** (por ejemplo `https://mitienda.somee.com`) y el **Código de vinculación**.
2. Pulsa **Vincular**.

Desde ese momento la caja sincroniza sola cuando hay internet; **Sincronizar ahora** lo fuerza. Si no hay conexión, la caja sigue vendiendo y envía todo después.

Una caja vinculada ya no administra el catálogo, los usuarios ni los datos del negocio: eso se hace en la web. En la web, **Revocar** desvincula una caja (por ejemplo, si se cambió de computadora).
