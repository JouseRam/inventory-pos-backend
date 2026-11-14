---
title: Productos
module: Products
icon: bi-palette
summary: Dar de alta productos con sus tonos y presentaciones, precios, costos, mínimos y control por lote y caducidad.
---
## Producto y presentaciones

Un **producto** es la línea (por ejemplo *Igora Royal*). Cada **presentación** es lo que se vende y cuenta en el inventario: un tono o color y un tamaño (por ejemplo *7.1 · 60 ml*). Precio, costo, existencia, códigos e **ID del sistema** son de cada presentación.

![Lista de productos](shot:products)

## Buscar

Filtra por **nombre, marca, tono, SKU o ID del sistema** (también el ID de un lote), por **Categoría** y **Marca**. Activa **Solo por resurtir** para ver lo que está bajo su mínimo e **Incluir inactivos** para ver lo desactivado.

## Nuevo producto

![Alta de producto](shot:product-edit)

1. Pulsa **Nuevo producto**.
2. Captura el **Nombre del producto**, su **Categoría** (con subcategoría, por ejemplo *Cabello › Tintes*), **Marca**, **Proveedor** y una **Descripción** opcional.
3. Activa **Controlar por lote y caducidad** en tintes, oxidantes y químicos.
4. En **Presentaciones**, pulsa **Agregar tono o tamaño** por cada una y captura:

| Campo | Ejemplo | Nota |
|---|---|---|
| Tono o color | `7.1` | **Opcional.** El número como viene en la caja |
| Nombre del tono o color | Rubio medio ceniza, Negro | Opcional |
| Contenido y Unidad | 60 · ml | ml, g, pza, l, kg |
| Costo (precio del proveedor) | 91.00 | Lo que te cuesta cada pieza |
| Precio de venta | 189.90 | **Con IVA incluido** |
| SKU | IGR-71 | **Opcional.** Si lo dejas vacío se usa el ID del sistema |
| Código de barras del fabricante | 4045787… | Opcional; el escáner también lo lee |
| Mínimo | 3 | Debajo de esto aparece en *Por resurtir* |
| Existencia inicial | 6 | Solo al crear; con lote y caducidad si aplica |

Mientras capturas costo y precio aparece la **ganancia por pieza** y el **margen**, calculados sobre el precio **sin IVA** (el IVA incluido en el precio es del SAT). Si sale en rojo, vendes por debajo del costo.

5. Pulsa **Guardar**. El sistema genera el **ID** de cada presentación y de cada lote, y abre la impresión de **etiquetas** con una por pieza de la existencia inicial. Ver el manual **Etiquetas y escáner**.

## Editar y desactivar

- Abre el producto para cambiar sus datos o los de una presentación. En la presentación se ve su **ID del sistema**.
- **Etiquetas** (en cada producto) imprime una etiqueta por pieza en existencia, por lote.
- **Desactivar** una presentación la quita del punto de venta, pero **conserva su historial**. No se borran productos con ventas.

## Lotes y caducidad

Con el control por lote, cada entrada de mercancía registra su **lote** y **fecha de caducidad**. Al vender, el sistema descuenta primero lo que **caduca antes**. Los lotes próximos a caducar aparecen en **Existencias → Por caducar** y en el **Resumen del día**.

> Las existencias no se capturan aquí después del alta: entran por **Recepciones** y se corrigen en **Existencias → Ajustar**.
