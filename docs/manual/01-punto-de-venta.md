---
title: Punto de venta
module: Sales
icon: bi-upc-scan
summary: Buscar tonos, armar el ticket, cobrar con efectivo, tarjeta o transferencia, imprimir y facturar.
---
## Antes de vender

La caja debe estar **abierta**. Si no lo está, el punto de venta te pide el **fondo inicial** y el botón **Abrir caja**. Cada cajero vende en su propia caja abierta.

## Pantalla

![Punto de venta con un tono en el ticket](shot:pos)

- **Izquierda:** el buscador y los resultados. Cada tarjeta muestra el tono, su nombre, el producto, la marca, el contenido, la existencia y el precio.
- **Derecha:** el **ticket** con los productos, el **cliente**, el **descuento** y el **total**.

## Agregar productos

1. Pulsa <kbd>F2</kbd> (o haz clic en el buscador).
2. Escribe el **número del tono** (por ejemplo `7.1`), el nombre, la marca o el SKU. Aparecen todas las marcas que tienen ese tono.
3. Haz clic en la tarjeta para agregarla al ticket. Si la vuelves a elegir, suma una pieza.
4. Con el **escáner**, lee la **etiqueta** del producto (QR o código de barras), el código del fabricante o escribe su SKU o ID y pulsa Enter: se agrega directo. El escaneo funciona aunque el cursor no esté en el buscador.

En el ticket puedes cambiar la cantidad o quitar un renglón. El sistema no deja vender más de lo que hay en existencia y avisa si un producto está agotado.

> El ticket se conserva si recargas la página, mientras no cierres la pestaña.

## Cliente y descuento

En el ticket elige el tipo de cliente:

- **Minorista:** escoge una clienta de la lista para que la venta quede en su historial (y para facturarle más rápido). Si no eliges, la venta queda como **Mostrador**.
- **Mayorista:** aparece **Buscar cliente mayorista**. Escribe el nombre o el teléfono y elige al cliente: se muestra su **porcentaje** y el ticket agrega la línea **Descuento mayorista (%)** sobre toda la compra. La **×** lo quita.

![Venta a mayorista](shot:pos-wholesale)

- **Descuento:** un monto en pesos adicional, que decide el cajero.

El descuento de mayorista lo vuelve a calcular el sistema al registrar la venta, con el porcentaje guardado en **Clientes**.

## Cobrar

![Ventana de cobro](shot:pos-pay)

1. Pulsa **Cobrar** o <kbd>F9</kbd>.
2. Captura cómo paga la clienta. Se pueden combinar formas de pago:
   - **Efectivo:** lo que entrega; el sistema calcula el **cambio**.
   - **Tarjeta:** el monto y el número de **autorización** del voucher.
   - **Transferencia:** el monto y la **referencia**.
3. Opcional, en **Cliente**: si no elegiste uno, captura su **Teléfono** (10 dígitos) y su **Nombre**. Si el teléfono ya está registrado, el sistema muestra quién es; si es nuevo, lo guarda en **Clientes** con la venta.
4. Pregúntale si quiere recibir promociones por WhatsApp y, si acepta, marca **Acepta recibir promociones por WhatsApp**.
5. Opcional: **Notas** de la venta.
6. Pulsa **Registrar venta**.

Tarjeta y transferencia no pueden pasar del total; el efectivo sí, y el excedente es el cambio.

## Después de cobrar

Aparece el folio de la venta y el **cambio a entregar**. Desde ahí puedes:

- **Imprimir ticket** (si la impresora está configurada; puede imprimirse solo al cobrar).
- **Facturar**: abre la factura de esa venta. Ver el manual de **Facturación**.
- **Nueva venta**: limpia el ticket para la siguiente clienta.

Si el cobro incluye efectivo y está activado, el **cajón** se abre solo.

## Productos con lote y caducidad

En tintes, oxidantes y químicos el sistema descuenta primero las piezas del **lote que caduca antes**. Si escaneas la **etiqueta de un lote**, el renglón muestra **Lote …** y la pieza se descuenta de ese lote, para que el inventario por lote coincida con el anaquel.

## Problemas comunes

| Mensaje | Qué hacer |
|---|---|
| *Abre la caja antes de registrar ventas* | Abre tu caja desde el mismo punto de venta o en **Caja**. |
| *Solo hay N piezas* | La existencia no alcanza; revisa **Existencias** o captura una **Recepción**. |
| *Pago insuficiente* | La suma de los pagos es menor al total. |
| *No se imprimió* | Revisa que la impresora esté encendida y su configuración en **Configuración → Impresora**. La venta sí quedó registrada. |
