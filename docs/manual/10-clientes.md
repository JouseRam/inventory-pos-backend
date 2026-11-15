---
title: Clientes
module: Customers
icon: bi-person-heart
summary: Guardar a las clientas frecuentes, mayoristas con su descuento, el permiso para promociones y los datos fiscales.
---
![Clientes](shot:customers)

## Lista

Busca por **nombre, teléfono** (con o sin espacios), **correo, RFC o razón social**. La lista muestra nombre, teléfono, **tipo** (minorista, o mayorista con su porcentaje), si **acepta promociones**, RFC, correo, notas y fecha de alta.

Los clientes también se registran solos al cobrar, si se captura su teléfono. Ver el manual **Punto de venta**.

## Agregar o editar

Pulsa **Agregar cliente** (o **Editar** en un renglón) y captura:

- **Nombre**, **Teléfono** (10 dígitos), **Correo** y **Dirección**.
- **Tipo de cliente:** **Minorista** o **Mayorista**. Al elegir Mayorista aparece **Descuento de mayorista (%)**: el porcentaje que se le descuenta en cada compra.
- **Acepta recibir promociones por WhatsApp:** márcalo solo si el cliente te dio permiso. Si después pide no recibir más (por ejemplo, responde *BAJA*), desmárcalo.
- **Tono habitual, alergias, notas:** por ejemplo *6.1 con oxidante 20 vol; sensible al amoniaco*.

![Cliente mayorista](shot:customer-wholesale)

## Mayoristas

El descuento de un mayorista se aplica en el **Punto de venta** al elegirlo, sobre toda su compra, y queda guardado en la venta. Si cambias su porcentaje, se usa el nuevo en las siguientes ventas; las anteriores no cambian.

## Datos fiscales

![Datos fiscales del cliente](shot:customer-fiscal)

Solo si la clienta pide factura. Deben coincidir con su **constancia de situación fiscal**:

- **RFC:** 13 caracteres persona física, 12 persona moral.
- **Código postal fiscal.**
- **Nombre o razón social:** sin el régimen societario (sin "SA de CV").
- **Régimen fiscal** y **Uso del CFDI:** solo aparecen las opciones válidas para el tipo de RFC.

Se llenan todos o ninguno. Al facturar una venta, estos datos se proponen solos y lo que se capture en la factura se guarda aquí.

## Desactivar

**Desactivar** oculta al cliente de las listas; su historial de compras se conserva. Activa **Mostrar inactivos** para verlo.
