/* Products and their sellable variants (shade × size). Stock is never edited here, only through inventory. */
POS.boot({ module: 'Products', title: 'Productos' }, async () => {
  const { api, session, fmt, esc, shade, toast, fail, confirmBox, formData, fillForm, $, $$ } = POS;
  const canEdit = session.can('Products', true);
  const [categories, brands, suppliers, catalogs] = await Promise.all([
    api.get('/categories'), api.get('/brands'),
    session.can('Suppliers') ? api.get('/suppliers') : Promise.resolve([]),
    api.get('/catalogs'),
  ]);
  const units = catalogs.units;
  let products = [];
  let editing = null;

  const options = (list, label = x => x.name) => list.map(x => `<option value="${x.id}">${esc(label(x))}</option>`).join('');
  const catLabel = c => c.parentName ? `${c.parentName} › ${c.name}` : c.name;
  $('#f-category').insertAdjacentHTML('beforeend', options(categories, catLabel));
  $('#f-brand').insertAdjacentHTML('beforeend', options(brands));
  $('#p-category').insertAdjacentHTML('beforeend', options(categories, catLabel));
  $('#p-brand').insertAdjacentHTML('beforeend', options(brands));
  $('#p-supplier').insertAdjacentHTML('beforeend', options(suppliers));
  if (!canEdit) $('#new-product').hidden = true;

  // ── List ──
  async function load() {
    const qs = new URLSearchParams();
    if ($('#q').value.trim()) qs.set('q', $('#q').value.trim());
    if ($('#f-category').value) qs.set('categoryId', $('#f-category').value);
    if ($('#f-brand').value) qs.set('brandId', $('#f-brand').value);
    if ($('#f-low').checked) qs.set('lowStock', 'true');
    if ($('#f-inactive').checked) qs.set('includeInactive', 'true');
    products = await api.get('/products?' + qs);
    render();
  }

  function render() {
    if (!products.length) {
      $('#list').innerHTML = `<div class="panel empty">${$('#q').value ? 'Ningún producto coincide con la búsqueda.' : 'Aún no hay productos. Da de alta el primero con «Nuevo producto».'}</div>`;
      return;
    }
    $('#list').innerHTML = products.map(p => `
      <article class="panel prod" data-id="${p.id}">
        <div class="prod-head">
          <h3>${esc(p.name)}</h3>
          <span class="meta">${[p.brandName, p.categoryName].filter(Boolean).map(esc).join(' · ') || 'Sin marca ni categoría'}</span>
          ${p.tracksBatches ? '<span class="pill accent"><i class="bi bi-upc"></i>Por lote</span>' : ''}
          ${p.hasLowStock ? '<span class="pill warn"><i class="bi bi-exclamation-triangle"></i>Por resurtir</span>' : ''}
          ${p.isActive ? '' : '<span class="pill">Inactivo</span>'}
          <span class="grow"></span>
          <span class="muted num">${fmt.qty(p.totalStock)} en existencia</span>
          ${canEdit ? `<button class="btn ghost sm" type="button" data-edit="${p.id}"><i class="bi bi-pencil"></i>Editar</button>` : ''}
        </div>
        <div class="variants">${p.variants.map(v => `
          <button class="vchip ${v.lowStock && v.isActive ? 'low' : ''} ${v.isActive ? '' : 'off'}" type="button" data-variant="${v.id}" data-product="${p.id}"
            title="${esc(v.sku)}${v.barcode ? ' · ' + esc(v.barcode) : ''}">
            ${shade.swatch(v.shadeCode, 'sm')}
            ${v.shadeCode ? `<span class="shade-code">${esc(v.shadeCode)}</span>` : `<span class="mono">${esc(v.sku)}</span>`}
            ${v.size ? `<span class="muted">${fmt.qty(v.size)} ${esc(v.unit)}</span>` : ''}
            <b class="num">${fmt.money(v.price)}</b>
            <span class="${v.lowStock ? '' : 'muted'}">${fmt.qty(v.stock)} pzas</span>
          </button>`).join('')}
          ${canEdit ? `<button class="vchip" type="button" data-add-variant="${p.id}"><i class="bi bi-plus" style="padding:0 6px"></i>Presentación</button>` : ''}
        </div>
      </article>`).join('');
  }

  let timer;
  $('#q').addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(load, 250); });
  ['#f-category', '#f-brand', '#f-low', '#f-inactive'].forEach(id => $(id).addEventListener('change', load));

  // ── Variant fields (shared by the product form rows and the variant dialog) ──
  function variantFields(v = {}, { withStock = false } = {}) {
    return `
      <label class="field"><span>Tono</span><input name="shadeCode" value="${esc(v.shadeCode)}" maxlength="20" class="mono" placeholder="7.1"></label>
      <label class="field"><span>Nombre del tono</span><input name="shadeName" value="${esc(v.shadeName)}" maxlength="100" placeholder="Rubio medio ceniza"></label>
      <label class="field"><span>SKU</span><input name="sku" value="${esc(v.sku)}" required maxlength="50" class="mono"></label>
      <label class="field"><span>Código de barras</span><input name="barcode" value="${esc(v.barcode)}" maxlength="50" class="mono"></label>
      <label class="field"><span>Contenido</span><input name="size" type="number" min="0" step="0.01" value="${v.size ?? ''}" data-type="nullable-number"></label>
      <label class="field"><span>Unidad</span><select name="unit">${units.map(u => `<option ${u === (v.unit || 'pza') ? 'selected' : ''}>${u}</option>`).join('')}</select></label>
      <label class="field"><span>Precio</span><input name="price" type="number" min="0" step="0.01" value="${v.price ?? ''}" required data-type="number" class="mono"></label>
      <label class="field"><span>Costo</span><input name="cost" type="number" min="0" step="0.01" value="${v.cost ?? ''}" data-type="number" class="mono"></label>
      <label class="field"><span>Mínimo</span><input name="minStock" type="number" min="0" step="1" value="${v.minStock ?? 3}" data-type="number"></label>
      ${withStock ? `
        <label class="field"><span>Existencia inicial</span><input name="initialStock" type="number" min="0" step="1" value="0" data-type="number"></label>
        <label class="field lot-field"><span>Lote</span><input name="lotNumber" maxlength="50" class="mono"></label>
        <label class="field lot-field"><span>Caducidad</span><input name="expirationDate" type="date"></label>` : ''}
      <div class="preview"></div>`;
  }
  function wirePreview(row) {
    const update = () => {
      const code = row.querySelector('[name=shadeCode]').value;
      const desc = shade.describe(code);
      row.querySelector('.preview').innerHTML = desc ? `${shade.swatch(code, 'sm')}<span>${esc(desc)}</span>` : (code ? '<span>Código libre (no sigue la numeración de tintes)</span>' : '');
    };
    row.querySelector('[name=shadeCode]').addEventListener('input', update);
    update();
  }
  const readRow = row => {
    const v = {};
    row.querySelectorAll('[name]').forEach(el => {
      const t = el.dataset.type;
      v[el.name] = t === 'number' ? Number(el.value || 0) : t === 'nullable-number' ? (el.value === '' ? null : Number(el.value)) : (el.value.trim() || null);
    });
    return v;
  };

  // ── Product dialog ──
  const pd = $('#product-dialog');
  function addVariantRow(v) {
    const row = document.createElement('div');
    row.className = 'vrow';
    row.innerHTML = variantFields(v, { withStock: true }) + '<button class="btn ghost sm remove" type="button" aria-label="Quitar presentación"><i class="bi bi-x"></i></button>';
    row.querySelector('.remove').onclick = () => { if ($$('.vrow', pd).length > 1) row.remove(); };
    $('#variant-rows').appendChild(row);
    wirePreview(row);
    toggleLotFields();
  }
  function toggleLotFields() { $$('.lot-field', pd).forEach(el => { el.hidden = !$('#p-batches').checked; }); }
  $('#p-batches').addEventListener('change', toggleLotFields);
  $('#add-variant').addEventListener('click', () => addVariantRow());

  function openProduct(p) {
    editing = p || null;
    const form = pd.querySelector('form');
    form.reset();
    pd.querySelector('.form-error').hidden = true;
    $('#product-title').textContent = p ? `Editar ${p.name}` : 'Nuevo producto';
    $('#variant-section').hidden = !!p;
    $('#p-active-wrap').hidden = !p;
    $('#variant-rows').innerHTML = '';
    if (p) fillForm(form, p); else addVariantRow();
    toggleLotFields();
    pd.showModal();
  }
  $('#new-product').addEventListener('click', () => openProduct());

  pd.querySelector('form').addEventListener('submit', async e => {
    e.preventDefault();
    if (e.submitter?.value === 'cancel') { pd.close(); return; }
    const err = pd.querySelector('.form-error');
    err.hidden = true;
    const data = {};
    for (const el of e.target.elements) {
      if (!el.name || el.closest('.vrow')) continue;
      Object.assign(data, formData({ elements: [el] }));
    }
    try {
      if (editing) {
        await api.put(`/products/${editing.id}`, data);
        toast('Producto actualizado.');
      } else {
        data.variants = $$('.vrow', pd).map(readRow);
        await api.post('/products', data);
        toast('Producto creado.');
      }
      pd.close();
      await load();
    } catch (ex) { err.textContent = ex.message; err.hidden = false; }
  });

  // ── Variant dialog ──
  const vd = $('#variant-dialog');
  let variantCtx = null;
  function openVariant(product, variant) {
    variantCtx = { product, variant };
    $('#variant-title').textContent = variant ? `${product.name} · ${variant.shadeCode || variant.sku}` : `Nueva presentación de ${product.name}`;
    $('#variant-edit').innerHTML = `<div class="form-grid">${variantFields(variant || {}, { withStock: !variant })}</div>
      ${variant ? `<p class="muted" style="margin:0;font-size:.85rem">Existencia actual: <b>${fmt.qty(variant.stock)}</b>. Para cambiarla usa una recepción o un ajuste en Existencias.</p>
      <label class="check"><input type="checkbox" name="isActive" ${variant.isActive ? 'checked' : ''}> Activa para venta</label>` : ''}`;
    $('#variant-edit').querySelectorAll('.lot-field').forEach(el => { el.hidden = !product.tracksBatches; });
    wirePreview($('#variant-edit'));
    $('#variant-delete').hidden = !variant || !variant.isActive;
    vd.querySelector('.form-error').hidden = true;
    vd.showModal();
  }
  vd.querySelector('form').addEventListener('submit', async e => {
    e.preventDefault();
    const action = e.submitter?.value;
    if (action === 'cancel') { vd.close(); return; }
    const err = vd.querySelector('.form-error');
    const { product, variant } = variantCtx;
    try {
      if (action === 'delete') {
        if (!await confirmBox(`Desactivar ${variant.displayName}. Ya no aparecerá en el punto de venta; su historial se conserva.`, { okText: 'Desactivar', danger: true })) return;
        await api.del(`/variants/${variant.id}`);
        toast('Presentación desactivada.');
      } else {
        const data = readRow($('#variant-edit'));
        const active = $('#variant-edit').querySelector('[name=isActive]');
        data.isActive = active ? active.checked : true;
        if (variant) await api.put(`/variants/${variant.id}`, data);
        else await api.post(`/products/${product.id}/variants`, data);
        toast('Presentación guardada.');
      }
      vd.close();
      await load();
    } catch (ex) { err.textContent = ex.message; err.hidden = false; }
  });

  $('#list').addEventListener('click', e => {
    const edit = e.target.closest('[data-edit]');
    const chip = e.target.closest('[data-variant]');
    const add = e.target.closest('[data-add-variant]');
    const find = id => products.find(p => p.id === Number(id));
    if (edit) openProduct(find(edit.dataset.edit));
    else if (chip && canEdit) { const p = find(chip.dataset.product); openVariant(p, p.variants.find(v => v.id === Number(chip.dataset.variant))); }
    else if (add) openVariant(find(add.dataset.addVariant), null);
  });

  await load();
});
