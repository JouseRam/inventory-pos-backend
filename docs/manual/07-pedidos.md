---
title: Pedidos
module: Purchases
icon: bi-cart-check
summary: Pedir a cada proveedor lo que hace falta con la lista sugerida por el sistema, enviarla por WhatsApp y rastrear el envío.
---
## Cómo funciona

**Pedidos** te ayuda a no quedarte sin producto. El sistema revisa tus existencias y lo que más se vende, arma una **lista sugerida para cada proveedor**, tú la ajustas y se la mandas por **WhatsApp**. Cuando el proveedor te da el **número de guía**, lo capturas para seguir el envío, y al llegar la mercancía la recibes con un clic.

El pedido pasa por estos estados:

| Estado | Qué significa |
|---|---|
| **Borrador** | Lo estás armando; todavía no se lo mandas al proveedor. |
| **Enviado** | Ya se lo mandaste (por WhatsApp o como prefieras). |
| **En camino** | El proveedor lo envió y capturaste la guía. |
| **Recibido** | La mercancía llegó y entró al inventario con una recepción. |
| **Cancelado** | Ya no se va a surtir. |

## Pedidos sugeridos

![Pedidos sugeridos](shot:orders-suggested)

En la pestaña **Sugeridos** aparecen, agrupados por el **proveedor que surte** cada producto, los que conviene volver a pedir:

- **En su mínimo o debajo**: la existencia llegó al mínimo que definiste en **Productos**.
- **Al ritmo de venta se agota en N días**: todavía hay, pero por lo que se vende al día se acabaría antes del periodo que quieres cubrir. Así los **más vendidos** se piden a tiempo.

La cantidad sugerida alcanza para el periodo y deja el mínimo de reserva. Arriba eliges con cuántos días de ventas se calcula (**Ventas de los últimos**) y para cuántos días quieres pedir (**Pedir para cubrir**).

> Lo que ya va en camino en otro pedido **no se vuelve a sugerir**. Los productos sin proveedor aparecen en el grupo **Sin proveedor asignado**: asígnalo en **Productos** para que entren en la lista de quien los surte.

**La lista es editable**: cambia la cantidad en **Pedir** o quita la marca de lo que no quieras. El total estimado se actualiza solo. Cuando esté lista, pulsa **Crear pedido con esta lista**.

## Armar un pedido a mano

Pulsa **Armar pedido a mano**, elige el **Proveedor** y agrega productos con el buscador (por nombre, tono, SKU o ID). Puedes combinar: crear el pedido desde la sugerencia y después agregar o quitar productos.

## Enviar el pedido al proveedor

![Pedido con rastreo](shot:orders-detail)

En el pedido puedes cambiar cantidades y costos, agregar **Notas para el proveedor** (por ejemplo *entregar antes del viernes*) y:

- **Enviar por WhatsApp**: abre el chat del proveedor con el pedido ya escrito (folio, cantidades, productos y notas) y lo marca como **Enviado**. Solo aparece si el proveedor tiene su teléfono de **10 dígitos** registrado en **Proveedores**.
- **Copiar pedido**: copia el mismo texto para pegarlo en un correo o en otro medio.
- **Marcar como enviado**: si lo pediste por teléfono o en persona.

Si el proveedor no tiene algo, ajusta la lista y **Guardar cambios**; puedes **Reenviar por WhatsApp** la versión corregida.

## Rastrear el pedido

Cuando el proveedor te pase el **número de guía**:

1. En **Rastreo**, elige la **Paquetería** (Estafeta, DHL, FedEx, UPS, Paquetexpress, Redpack, J&T, 99 Minutos, Correos de México u Otra).
2. Escribe el **Número de guía** y pulsa **Guardar guía**. El pedido pasa a **En camino**.
3. Pulsa **Ver movimientos** para abrir la página de rastreo de esa paquetería con tu guía (para las que no tienen liga directa se usa el rastreador universal 17TRACK).

En **Movimientos del pedido** queda la historia: cuándo se creó, se envió, se capturó la guía y se recibió. También puedes **agregar movimientos** a mano, por ejemplo lo que viste en la página de la paquetería (*Llegó al centro de distribución de Pachuca*).

> Consultar el rastreo necesita internet. Las paqueterías no ofrecen gratis la consulta automática, por eso el sistema abre su página en lugar de traer los movimientos solo.

## Recibir la mercancía

Cuando llegue el paquete, pulsa **Recibir mercancía** en el pedido. Se abre **Recepciones** con el proveedor, los productos y los costos ya llenos.

![Recepción de un pedido que llegó incompleto](shot:order-receive)

1. **Cuenta lo que viene en la caja** y captúralo en **Llegó**, producto por producto. Si todo vino bien, pulsa **Llegó todo completo**. Lo que no llegó déjalo en **0**.
2. Junto a cada producto ves si está **completo** (✓), cuántas piezas **faltan** o si llegaron **de más**. Arriba, el resumen dice si el pedido **llegó completo** o qué falta.
3. Captura **lote** y **caducidad** si aplica y pulsa **Registrar recepción**. La existencia sube solo con lo que de verdad llegó.
4. Se abre la **impresión de etiquetas** con una etiqueta por pieza recibida (código **QR** o **de barras**, con su lote). Imprímelas y pégalas en la mercancía antes de acomodarla.

Qué pasa con el pedido:

| Si… | El pedido queda |
|---|---|
| Llegó todo | **Recibido**. |
| Faltó algo | **Recibido parcial**: lo que falta queda pendiente. Cuando llegue, abre el pedido y pulsa **Recibir lo que falta**; se abre solo con esas piezas. |
| Faltó algo y ya no lo vas a esperar | Marca **Cerrar el pedido con lo que llegó** al recibir, o después pulsa **Cerrar con faltante** en el pedido. Queda **Recibido** con el faltante anotado. |

En el pedido, la columna **Llegó** muestra lo recibido contra lo pedido (**Completo** o **Faltan N**), y en **Recepciones de este pedido** están todas sus recepciones con **Imprimir etiquetas** para volver a sacarlas. Cada recepción queda en **Movimientos del pedido** con lo que llegó y lo que faltó, para reclamarlo al proveedor.

> Un pedido que ya llegó en parte no se cancela: se cierra con faltante, así queda el registro de lo que sí entró.

## Lista de pedidos

En la pestaña **Pedidos y rastreo** están todos los pedidos con su estado, total estimado y guía. Filtra por **Estado** y haz clic en uno para abrirlo.

## Compras por proveedor (para negociar)

![Compras por proveedor](shot:orders-stats)

La pestaña **Compras por proveedor** muestra, para el **Periodo** que elijas (3 meses, 6 meses, 1 año o 2 años), lo que entró por **Recepciones** de cada proveedor:

| Columna | Qué te dice |
|---|---|
| **Comprado** y **% de tus compras** | Cuánto dinero le has comprado y qué parte de todo lo que compras representa. |
| **Compras** y **Frecuencia** | Cuántas veces le compraste y cada cuántos días en promedio. |
| **Promedio al mes** | Lo que le compras al mes desde tu primera compra del periodo. |
| **Piezas** y **Última compra** | El volumen total y cuándo fue la más reciente. |
| **Entrega** | Días promedio entre que le mandaste el pedido y lo recibiste. |

Haz clic en un proveedor para ver sus **compras por mes**, los **productos que le compras** (piezas, dinero, veces y cómo cambió su costo en el periodo) y una **propuesta para negociar** ya escrita con tus números: cuánto le has comprado, cada cuánto y tus productos principales, pidiéndole **descuento por volumen, mejores precios o producto de regalo**. Edítala y pulsa **Enviar por WhatsApp** o **Copiar**.

> Si un producto subió de costo (marcado en amarillo), es buen argumento para pedir que te respeten el precio anterior. También puedes ir a la pestaña desde **Proveedores › Ver cuánto le compras a cada proveedor**.

## Permisos

Pedidos usa el permiso del módulo **Recepciones**: **Ver** muestra las sugerencias y los pedidos; **Editar** permite crearlos, enviarlos, capturar la guía y recibirlos.
