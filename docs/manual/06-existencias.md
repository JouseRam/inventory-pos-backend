---
title: Existencias
module: Inventory
icon: bi-box-seam
summary: Ver lo que hay que resurtir, los lotes por caducar y el kardex de movimientos, y ajustar existencias.
---
## Pestañas

![Existencias](shot:inventory)

- **Por resurtir:** presentaciones con existencia igual o menor a su mínimo.
- **Por caducar:** lotes que caducan dentro del plazo que elijas en la misma pestaña (30, 60 o 90 días, o 6 meses). El aviso del Resumen del día usa los días de Configuración → Negocio → Inventario (60 por omisión).
- **Kardex:** todos los movimientos de inventario con fecha, presentación, tipo de movimiento, lote, referencia (folio de venta o recepción), cantidad y existencia resultante. Filtra por presentación y por fechas.

## Ajustar existencia

Para corregir diferencias de un conteo físico, productos dañados o muestras para cabina:

1. Pulsa **Ajustar existencia**.
2. Busca la **Presentación**.
3. Elige el **Tipo**: entrada (suma) o salida (resta).
4. Captura la **Cantidad**. En productos con lote, indica el **Lote** (y la **Caducidad** si es una entrada nueva).
5. Escribe el **Motivo** y pulsa **Registrar ajuste**.

El ajuste queda en el kardex con tu usuario y su motivo.

> Para mercancía que llega del proveedor usa **Recepciones**, no un ajuste: así queda el costo y el proveedor.
