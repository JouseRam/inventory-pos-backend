---
title: Primeros pasos
module: -
icon: bi-flag
summary: Qué es Matiz POS, cómo entrar, cómo se organiza el menú y qué hacer el primer día.
---
## Qué es Matiz POS

Matiz POS es el sistema de punto de venta e inventario de la tienda. Se usa de dos formas:

| | Caja (programa de escritorio) | Web central |
|---|---|---|
| Para qué | Vender, cobrar, abrir y cerrar caja, recibir mercancía, facturar | Administrar catálogo, precios y usuarios, y consultar lo que envían las cajas |
| Internet | Funciona **sin internet**; sincroniza cuando hay conexión | Siempre en línea |
| Dónde | La computadora del mostrador (`MatizPOS.exe`) | En el navegador, con la dirección de la tienda |

Si la tienda tiene una sola computadora y no usa la web, la caja hace todo.

## Entrar al sistema

![Pantalla de inicio de sesión](shot:login)

1. Abre **Matiz POS** desde el escritorio (o la dirección de la web).
2. Escribe tu **usuario** y tu **contraseña** y pulsa **Entrar**.
3. Después de 5 intentos fallidos la cuenta se bloquea 10 minutos.

**La primera vez** que se abre una caja nueva aparece **Configura tu caja**: escribe el nombre de la tienda, tu nombre, un usuario y una contraseña (8 caracteres o más, con al menos un número). Esa persona queda como **administrador**.

## El menú

![Menú lateral con los módulos](shot:menu)

El menú de la izquierda muestra solo los módulos a los que tienes permiso. Están agrupados así:

- **Venta:** Punto de venta, Caja y Ventas.
- **Inventario:** Productos, Existencias, Recepciones, Categorías y marcas.
- **Personas:** Clientes, Proveedores y Promociones.
- **Análisis:** Resumen del día y Reportes.
- **Sistema:** Configuración y Documentación.

En la esquina superior derecha se ve si la caja está **abierta** y, en las cajas vinculadas, el estado de la **sincronización**. Abajo a la izquierda están tu nombre, el botón **Tema** (claro u oscuro) y **Salir**.

## Atajos de teclado

| Tecla | Dónde | Qué hace |
|---|---|---|
| <kbd>F2</kbd> | Punto de venta | Ir al buscador de productos |
| <kbd>F9</kbd> | Punto de venta | Cobrar |
| <kbd>Enter</kbd> | Buscador del punto de venta | Con un código de barras o SKU, agrega el producto directo al ticket |
| <kbd>F11</kbd> | Caja de escritorio | Pantalla completa |

## Lista para el primer día

1. **Configuración → Negocio:** nombre, RFC, dirección y teléfono (salen en el ticket) y el porcentaje de IVA.
2. **Configuración → Impresora y respaldos:** conecta la impresora de tickets e imprime una prueba.
3. **Categorías y marcas** y **Proveedores:** da de alta los que usas.
4. **Productos:** registra tus productos con sus tonos y presentaciones.
5. **Recepciones:** captura la mercancía que tienes (con lote y caducidad en tintes y oxidantes).
6. **Configuración → Usuarios y permisos:** crea un usuario para cada persona y dale solo los módulos que necesita.
7. **Caja → Abrir caja** y ya puedes vender.

> **Sin internet:** la caja sigue vendiendo con normalidad. Lo único que necesita conexión es **facturar** y **sincronizar con la web**.

## Dónde se guardan los datos

En la caja de escritorio todo queda en la carpeta `%LOCALAPPDATA%\MatizPOS` de la computadora: la base de datos, los **respaldos automáticos** (uno al día, se guardan los últimos 30) y los registros de errores. Para llevar la información a otra computadora, copia un respaldo de esa carpeta.
