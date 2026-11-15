---
title: Facturación
module: Invoicing
icon: bi-receipt
summary: Timbrar facturas CFDI 4.0 desde la caja, descargarlas, cancelarlas y configurar, pagar o cambiar el proveedor.
---
## Cómo funciona

Las facturas se **timbran con un proveedor autorizado por el SAT (PAC)**. Hoy el sistema trabaja con **Facturama** y **Facturapi**; eliges uno en Configuración y puedes cambiar cuando quieras.

- La factura se emite **desde la caja** donde se hizo la venta.
- **Necesita internet.** Sin conexión, la venta queda registrada y la facturas cuando regrese.
- El total de la factura es **exactamente** lo que se cobró en el ticket: el sistema separa base e IVA de cada producto.

## Antes de la primera factura

| Qué | Dónde |
|---|---|
| RFC, razón social, régimen fiscal y código postal de expedición del negocio | Configuración → **Negocio** |
| Cuenta con el proveedor (PAC) | Configuración → **Facturación** |
| Certificado de sello digital (**CSD**: archivos .cer y .key con su contraseña) | Configuración → **Facturación** (Facturama) o el panel del proveedor (Facturapi) |
| Permiso **Facturación** para quien va a timbrar | Configuración → **Usuarios y permisos** |

El CSD se tramita en el SAT; **no es la e.firma**.

## Elegir, pagar y configurar el proveedor

![Configuración de facturación](shot:settings-invoicing)

En **Configuración → Facturación** aparece una tarjeta por proveedor con su forma de cobro y sus precios de referencia:

| | Facturama | Facturapi |
|---|---|---|
| Cobro | Anual: módulo API + folios en prepago | Mensual: suscripción + cargo por factura |
| Credenciales | Usuario y contraseña | Llave secreta (`sk_test_…` o `sk_live_…`) |
| CSD | Se sube desde Anna’s Beauty Supply POS | Se sube en el panel de Facturapi |
| Pruebas | Ambiente de pruebas gratis | Llave `sk_test_` gratis |

Botones de cada tarjeta:

- **Crear cuenta** / **Contratar**: abre el sitio del proveedor para registrarte.
- **Pagar o comprar folios**: abre la página de pago del proveedor (anualidad, folios o suscripción).
- **Precios**: su lista de precios vigente.
- **Configurar** / **Ver cuenta**: carga el formulario de ese proveedor.

> El pago se hace en el **sitio seguro del proveedor**. Anna’s Beauty Supply POS no recibe ni guarda datos de tarjeta.

Para configurarlo:

1. Pulsa **Configurar** en la tarjeta del proveedor.
2. Captura sus credenciales. En Facturama elige el **Ambiente**: *Pruebas* (gratis, sin validez) o *Producción* (facturas reales). En Facturapi el ambiente lo indica la llave.
3. Opcional: **Clave SAT de producto por defecto** (8 dígitos; `01010101` si no aplica una específica) y la **Serie** de las facturas.
4. Pulsa **Guardar y usar este proveedor** y luego **Probar conexión**.
5. Con Facturama, sube el **CSD**: el .cer, el .key y la contraseña de la llave. El sistema revisa que la contraseña sea correcta, que el .key corresponda al .cer, que el RFC sea el del negocio y que no esté vencido.

Cuando todo está listo, la tarjeta de la cuenta dice **Lista para facturar**. La contraseña o llave se guarda **cifrada** y nunca se vuelve a mostrar.

## Cambiar de proveedor

1. En la tarjeta del otro proveedor pulsa **Configurar**, captura su cuenta y pulsa **Guardar y usar este proveedor**.
2. Confirma el cambio.

Las nuevas facturas se timbran con el nuevo proveedor. **Las facturas ya emitidas se siguen descargando y cancelando con el proveedor que las timbró**, porque su cuenta se conserva.

## Facturar una venta

Desde el **Punto de venta** (botón **Facturar** después de cobrar) o desde **Ventas → detalle → Facturar**.

![Datos fiscales del cliente para timbrar](shot:invoice-form)

1. Captura el **RFC** del cliente. Si ya existe un cliente con ese RFC, el resto se llena solo.
2. Revisa el **código postal fiscal**, el **nombre o razón social** (tal como en su constancia, sin "SA de CV"), el **régimen fiscal** y el **uso del CFDI**. Solo aparecen las opciones que corresponden al tipo de RFC (12 caracteres persona moral, 13 persona física).
3. La **forma de pago** se propone según cómo pagó (la que cubrió la mayor parte); cámbiala si hace falta (por ejemplo, tarjeta de débito).
4. Opcional: el **correo** del cliente, que se guarda en su ficha.
5. Pulsa **Timbrar factura**.

Si no hay internet aparece el aviso **Sin conexión a internet** y el botón se desactiva hasta que regrese.

Los datos fiscales quedan guardados en el cliente para la próxima vez.

## Descargar y reimprimir

Con la factura timbrada se muestran el **folio fiscal (UUID)**, el cliente y el total, con los botones **PDF** y **XML**. Los archivos se guardan en la computadora, así que se pueden volver a descargar **sin internet**. En Ventas, la venta muestra **Ver factura**.

Las facturas hechas en **modo de pruebas** llevan la etiqueta **Prueba** y no tienen validez ante el SAT.

## Cancelar una factura

1. Abre la factura (Ventas → detalle → **Ver factura**).
2. Despliega **Cancelar esta factura ante el SAT**.
3. Elige el **motivo**:
   - **01** Con errores, con relación: se sustituye por otra factura (pide el UUID de la nueva).
   - **02** Con errores, sin relación.
   - **03** No se llevó a cabo la operación.
   - **04** Operación nominativa relacionada en una factura global.
4. Confirma.

Algunas cancelaciones requieren que el cliente las **acepte**; mientras tanto la factura sigue vigente y el sistema lo indica. Una venta facturada no se puede cancelar hasta que su factura esté cancelada.

## Permisos

El módulo **Facturación** tiene dos niveles: **Ver** permite descargar facturas; **Editar** permite timbrar y cancelar. La configuración del proveedor requiere permiso de **Configuración**.

## Problemas comunes

| Mensaje | Qué hacer |
|---|---|
| *Falta configurar la facturación* | Completa lo que indica la lista (datos del negocio, cuenta o CSD). |
| *No hay conexión a internet* | Revisa la red; la venta ya quedó registrada. |
| *…rechazó el usuario o la contraseña / la llave* | Revisa las credenciales y que tu suscripción o folios estén vigentes (botón **Pagar o comprar folios**). |
| *No se pudo timbrar: …* | Es la respuesta del SAT o del proveedor; normalmente un dato fiscal del cliente que no coincide con su constancia. |
| *Las ventas al público en general van en la factura global* | El RFC genérico XAXX010101000 no se usa en facturas individuales. |
