App.init('products');

let products = [], categories = [];

async function load() {
  [products, categories] = await Promise.all([api.get('/products'), api.get('/categories')]);
  const catSel = document.getElementById('cat-filter');
  const catModal = document.getElementById('f-cat');
  categories.forEach(c => {
    catSel.innerHTML += `<option value="${c.id}">${c.name}</option>`;
    catModal.innerHTML += `<option value="${c.id}">${c.name}</option>`;
  });
  renderTable();
}

function renderTable() {
  const q = document.getElementById('search').value.toLowerCase();
  const catId = +document.getElementById('cat-filter').value || null;
  const stockF = document.getElementById('stock-filter').value;

  let list = products;
  if (q) list = list.filter(p => p.name.toLowerCase().includes(q) || (p.sku && p.sku.toLowerCase().includes(q)) || (p.barcode && p.barcode?.toLowerCase().includes(q)));
  if (catId) list = list.filter(p => p.categoryId === catId);
  if (stockF === 'low') list = list.filter(p => p.lowStock);

  const tbody = document.getElementById('table-body');
  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="8"><div class="empty-state"><i class="bi bi-box-seam"></i><p>Sin productos</p></div></td></tr>';
    return;
  }

  tbody.innerHTML = list.map(p => `
    <tr>
      <td><div style="font-weight:600">${p.name}</div>${p.description ? `<div style="font-size:.75rem;color:var(--text-muted)">${p.description.substring(0,60)}</div>` : ''}</td>
      <td style="color:var(--text-muted);font-size:.82rem">${p.sku || '—'}</td>
      <td>${p.categoryName ? `<span class="badge badge-blue">${p.categoryName}</span>` : '—'}</td>
      <td style="font-weight:600;color:var(--primary)">$${fmt(p.price)}</td>
      <td style="color:var(--text-muted)">$${fmt(p.cost)}</td>
      <td><span class="badge ${p.lowStock ? 'badge-red' : 'badge-green'}">${fmt(p.stock, 0)} ${p.unit}</span></td>
      <td><span class="badge ${p.isActive ? 'badge-green' : 'badge-gray'}">${p.isActive ? 'Activo' : 'Inactivo'}</span></td>
      <td>
        <button class="btn btn-secondary btn-sm btn-icon" title="Editar" onclick="openModal(${p.id})"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-danger btn-sm btn-icon" title="Desactivar" onclick="deleteProduct(${p.id})" style="margin-left:4px"><i class="bi bi-trash"></i></button>
      </td>
    </tr>`).join('');
}

['search', 'cat-filter', 'stock-filter'].forEach(id => {
  document.getElementById(id)?.addEventListener('input', renderTable);
  document.getElementById(id)?.addEventListener('change', renderTable);
});

function openModal(id) {
  clearForm();
  if (id) {
    const p = products.find(x => x.id === id);
    if (!p) return;
    document.getElementById('modal-title').textContent = 'Editar producto';
    document.getElementById('f-id').value      = p.id;
    document.getElementById('f-name').value    = p.name;
    document.getElementById('f-sku').value     = p.sku || '';
    document.getElementById('f-barcode').value = p.barcode || '';
    document.getElementById('f-price').value   = p.price;
    document.getElementById('f-cost').value    = p.cost;
    document.getElementById('f-stock').value   = p.stock;
    document.getElementById('f-minstock').value= p.minStock;
    document.getElementById('f-unit').value    = p.unit;
    document.getElementById('f-cat').value     = p.categoryId || '';
    document.getElementById('f-desc').value    = p.description || '';
    document.getElementById('f-active').checked= p.isActive;
  } else {
    document.getElementById('modal-title').textContent = 'Nuevo producto';
  }
  document.getElementById('modal').classList.add('open');
}

function closeModal() { document.getElementById('modal').classList.remove('open'); }
function clearForm() {
  ['f-id','f-name','f-sku','f-barcode','f-price','f-cost','f-stock','f-desc'].forEach(id => document.getElementById(id).value = '');
  document.getElementById('f-minstock').value = '5';
  document.getElementById('f-unit').value = 'pza';
  document.getElementById('f-cat').value = '';
  document.getElementById('f-active').checked = true;
}

async function saveProduct() {
  const name = document.getElementById('f-name').value.trim();
  if (!name) { toast('El nombre es requerido', 'warning'); return; }
  const price = parseFloat(document.getElementById('f-price').value);
  if (!price) { toast('Ingresa un precio válido', 'warning'); return; }

  const dto = {
    name,
    description: document.getElementById('f-desc').value.trim() || null,
    sku: document.getElementById('f-sku').value.trim() || null,
    barcode: document.getElementById('f-barcode').value.trim() || null,
    price,
    cost: parseFloat(document.getElementById('f-cost').value) || 0,
    stock: parseFloat(document.getElementById('f-stock').value) || 0,
    minStock: parseFloat(document.getElementById('f-minstock').value) || 5,
    unit: document.getElementById('f-unit').value,
    categoryId: +document.getElementById('f-cat').value || null,
    isActive: document.getElementById('f-active').checked
  };

  try {
    const id = document.getElementById('f-id').value;
    if (id) await api.put(`/products/${id}`, dto);
    else await api.post('/products', dto);
    toast(id ? 'Producto actualizado' : 'Producto creado', 'success');
    closeModal();
    products = await api.get('/products');
    renderTable();
  } catch (e) { toast(e.message, 'error'); }
}

async function deleteProduct(id) {
  if (!window.confirm('¿Desactivar este producto?')) return;
  try {
    await api.del(`/products/${id}`);
    toast('Producto desactivado', 'success');
    products = await api.get('/products');
    renderTable();
  } catch (e) { toast(e.message, 'error'); }
}

load().catch(e => toast(e.message, 'error'));
