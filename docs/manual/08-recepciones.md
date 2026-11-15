---
title: Recepciones
module: Purchases
icon: bi-truck
summary: Capturar la mercancía que llega del proveedor con su costo, lote y caducidad.
---
## Historial

![Recepciones](shot:purchases)

La lista muestra cada recepción con su folio, fecha, proveedor, factura o remisión, número de renglones y total. Filtra por fechas y haz clic en una para ver su detalle.

## Recibir mercancía

1. Pulsa **Recibir mercancía**.
2. Elige el **Proveedor** y captura la **Factura o remisión** y **Notas** si hace falta.
3. Por cada producto, pulsa **Agregar renglón** y captura:
   - **Presentación** (busca por nombre, tono o SKU),
   - **Cantidad** y **Costo unitario**,
   - **Lote** y **Caducidad** en productos con control por lote.
4. Pulsa **Registrar recepción**.

Las existencias suben al momento y el costo de la presentación se actualiza. Cada lote nuevo recibe su **ID** y se abre la impresión de **etiquetas**, una por pieza recibida. Para reimprimirlas, abre la recepción y pulsa **Imprimir etiquetas**.

> Si la mercancía viene de un **pedido**, ábrelo en **Pedidos** y pulsa **Recibir mercancía**: la recepción se abre con lo pedido para que captures en **Llegó** lo que de verdad vino. El sistema compara contra el pedido y lo deja **Recibido** si llegó completo o **Recibido parcial** si faltó algo (ver el manual de **Pedidos**).

> Si un mismo tono llega en dos lotes con distinta caducidad, captura **un renglón por lote**.

Las recepciones se capturan en la **caja**; la web solo las consulta.
