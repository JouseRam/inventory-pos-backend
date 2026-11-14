---
title: Productos
module: Products
icon: bi-palette
summary: Dar de alta productos con sus tonos y presentaciones, precios, costos, mínimos y control por lote y caducidad.
---
## Producto y presentaciones

Un **producto** es la línea (por ejemplo *Igora Royal*). Cada **presentación** es lo que se vende y cuenta en el inventario: un tono y un tamaño (por ejemplo *7.1 · 60 ml*). Precio, costo, existencia y código de barras son de cada presentación.

![Lista de productos](shot:products)

## Buscar

Filtra por **nombre, marca, tono o SKU**, por **Categoría** y **Marca**. Activa **Solo por resurtir** para ver lo que está bajo su mínimo e **Incluir inactivos** para ver lo desactivado.

## Nuevo producto

![Alta de producto](shot:product-edit)

1. Pulsa **Nuevo producto**.
2. Captura el **Nombre del producto**, su **Categoría**, **Marca**, **Proveedor** y una **Descripción** opcional.
3. Activa **Controlar por lote y caducidad** en tintes, oxidantes y químicos.
4. En **Presentaciones**, pulsa **Agregar tono o tamaño** por cada una y captura:

| Campo | Ejemplo | Nota |
|---|---|---|
| Tono | `7.1` | El número como viene en la caja |
| Nombre del tono | Rubio medio ceniza | |
| SKU | IGR-71 | Clave interna única |
| Código de barras | 4045787… | Para el lector |
| Contenido y Unidad | 60 · ml | pza, ml, g, l, kg |
| Precio | 189.90 | **Con IVA incluido** |
| Costo | 91.00 | Para utilidad en reportes |
| Mínimo | 3 | Debajo de esto aparece en *Por resurtir* |
| Existencia inicial | 6 | Solo al crear; con lote y caducidad si aplica |

5. Pulsa **Guardar**.

## Editar y desactivar

- Abre el producto para cambiar sus datos o los de una presentación.
- **Desactivar** una presentación la quita del punto de venta, pero **conserva su historial**. No se borran productos con ventas.

## Lotes y caducidad

Con el control por lote, cada entrada de mercancía registra su **lote** y **fecha de caducidad**. Al vender, el sistema descuenta primero lo que **caduca antes**. Los lotes próximos a caducar aparecen en **Existencias → Por caducar** y en el **Resumen del día**.

> Las existencias no se capturan aquí después del alta: entran por **Recepciones** y se corrigen en **Existencias → Ajustar**.
