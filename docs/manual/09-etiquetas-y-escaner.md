---
title: Etiquetas y escáner
module: Products
icon: bi-qr-code
summary: Imprimir etiquetas con QR o código de barras en hojas carta y cobrar escaneándolas.
---
## Cómo funciona

Cada **presentación** y cada **lote** tienen un **ID del sistema** que se genera solo al darlos de alta: 9 letras y números, por ejemplo `P7K2QX9MZ` (presentación) o `L80Q2BWER` (lote). Ese ID va en el **QR o código de barras** de la etiqueta y también impreso debajo, para buscarlo a mano.

Al escanear la etiqueta en el **Punto de venta**, el sistema encuentra el producto, su **lote** y su **precio actual**. Si cambias el precio, no hace falta reimprimir para cobrar bien: el código guarda el ID, no el precio. Solo vuelve a imprimir si quieres que el precio impreso coincida.

El ID se puede buscar en **Productos**, en el buscador del **Punto de venta** y en el selector de presentaciones de **Existencias** y **Recepciones**. Da igual escribirlo en mayúsculas o minúsculas.

## Tamaños

| Etiqueta | Medida | Lleva |
|---|---|---|
| **Pequeña** | 2 × 1 cm | QR, nombre (una línea), precio, lote e ID. Para delineadores, labiales y frascos chicos. |
| **Mediana** | 3 × 2 cm | QR o código de barras, nombre, **categoría - subcategoría** (por ejemplo *Cabello - Tintes*), tono o color y contenido, **lote**, caducidad, **precio** e ID. |

La pequeña siempre lleva **QR**: un código de barras de 2 cm queda demasiado apretado para leerse bien.

## Imprimir etiquetas

Las etiquetas se ofrecen solas:

- al **guardar un producto nuevo** (una por cada pieza de la existencia inicial), y
- al **registrar una recepción** (una por cada pieza recibida, con su lote).

También puedes imprimirlas cuando quieras:

- en **Productos**, botón **Etiquetas** del producto (una por cada pieza en existencia, por lote), y
- en **Recepciones**, abriendo una recepción → **Imprimir etiquetas**.

![Impresión de etiquetas](shot:labels-dialog)

1. Elige el **Tamaño** y el **Código** (QR recomendado).
2. Ajusta las **Copias** de cada renglón.
3. Si vas a usar una hoja que ya tiene etiquetas usadas, indica en **Empezar en la posición** dónde está la primera libre. Se cuenta de izquierda a derecha y de arriba abajo.
4. Pulsa **Imprimir** y, en la ventana de impresión, deja la **escala al 100 %** (tamaño real). No uses "ajustar a la página".

## Papel para impresora láser

Sí se pueden imprimir en la **impresora láser** con **hojas de etiquetas tamaño carta** para láser (adhesivo que resiste el calor del fusor). Hay dos opciones:

- **Hoja adhesiva completa (sin precortar):** la más fácil de conseguir. Activa **Líneas guía para recortar** y córtalas con guillotina o cúter.
- **Hojas precortadas:** abre **Acomodo en la hoja carta** y copia las medidas del empaque: margen superior, margen izquierdo y separación entre etiquetas. El sistema muestra cuántas caben por hoja. Las medidas se guardan en la computadora.

Consejos:

- Usa etiquetas **blancas mate**: el brillo dificulta la lectura del escáner.
- Imprime primero **una hoja de prueba en papel normal** y ponla a contraluz sobre la hoja de etiquetas para revisar que coincidan.
- Si cada pocos días imprimes muchas etiquetas, considera una **impresora térmica de etiquetas** (rollo de 3 × 2 cm). Para empezar, la láser con hojas carta funciona bien.

## Escáner

Cualquier lector que funcione **como teclado** sirve, sin instalar nada: "escribe" el ID y un Enter, y el Punto de venta lo agrega al ticket. El sistema reconoce el escaneo **aunque el cursor no esté en el buscador**.

Qué buscar al comprar uno:

- **2D (imager):** lee **QR** y códigos de barras. Uno 1D (láser rojo) solo lee códigos de barras y no servirá con las etiquetas pequeñas.
- **Bluetooth con base o receptor USB:** la base suele dar mejor alcance que el Bluetooth de la computadora y no se desconecta.
- Modo **HID / teclado**, con **Enter** al final de cada lectura (casi todos vienen así de fábrica).

Ejemplos de gama media y profesional: **Zebra DS2278**, **Honeywell Voyager 1472g**. También hay modelos económicos 2D Bluetooth de otras marcas que funcionan igual si cumplen lo anterior.

> Los ID solo usan letras y números a propósito: los símbolos como el guion salen cambiados cuando el escáner está configurado en inglés y Windows en español.

### Si una lectura no aparece

| Situación | Qué hacer |
|---|---|
| No pasa nada al escanear | Prueba en el Bloc de notas: si no escribe nada, vuelve a emparejar el escáner con su base. |
| Escribe el código pero no lo agrega | Revisa que el escáner mande **Enter** al final (viene en su manual como *suffix CR/Enter*). |
| *No hay producto con el código…* | La etiqueta es de otra tienda o la presentación está desactivada. Búscala por nombre. |
| Lee mal la etiqueta pequeña | Imprime con mejor calidad (sin modo ahorro de tóner) o usa la mediana. |
