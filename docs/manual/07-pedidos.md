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

Cuando llegue el paquete, pulsa **Recibir mercancía** en el pedido. Se abre **Recepciones** con el proveedor, los productos, las cantidades y los costos ya llenos. Corrige lo que haya llegado distinto, captura **lote** y **caducidad** si aplica y pulsa **Registrar recepción**. El pedido queda **Recibido** y ligado a esa recepción.

## Lista de pedidos

En la pestaña **Pedidos y rastreo** están todos los pedidos con su estado, total estimado y guía. Filtra por **Estado** y haz clic en uno para abrirlo.

## Permisos

Pedidos usa el permiso del módulo **Recepciones**: **Ver** muestra las sugerencias y los pedidos; **Editar** permite crearlos, enviarlos, capturar la guía y recibirlos.
