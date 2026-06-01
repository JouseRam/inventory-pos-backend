App.init('suppliers');

let suppliers = [];

async function load() {
  const q = document.getElementById('search').value.trim();
  suppliers = await api.get('/suppliers' + (q ? '?q=' + encodeURIComponent(q) : ''));
  render();
}

function render() {
  const tbody = document.getElementById('table-body');
  if (!suppliers.length) {
    tbody.innerHTML = '<tr><td colspan="6"><div class="empty-state"><i class="bi bi-truck"></i><p>Sin proveedores</p></div></td></tr>';
    return;
  }
  tbody.innerHTML = suppliers.map(s => `
    <tr>
      <td style="font-weight:600">${s.name}</td>
      <td>${s.contactName || '—'}</td>
      <td>${s.phone || '—'}</td>
      <td>${s.email || '—'}</td>
      <td><span class="badge badge-blue">${s.productCount}</span></td>
      <td>
        <button class="btn btn-secondary btn-sm btn-icon" onclick="openModal(${s.id})"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-danger btn-sm btn-icon" onclick="del(${s.id})" style="margin-left:4px"><i class="bi bi-trash"></i></button>
      </td>
    </tr>`).join('');
}

function openModal(id) {
  document.getElementById('f-id').value = id || '';
  if (id) {
    const s = suppliers.find(x => x.id === id);
    document.getElementById('modal-title').textContent = 'Editar proveedor';
    document.getElementById('f-name').value    = s.name;
    document.getElementById('f-contact').value = s.contactName || '';
    document.getElementById('f-phone').value   = s.phone || '';
    document.getElementById('f-email').value   = s.email || '';
    document.getElementById('f-address').value = s.address || '';
  } else {
    document.getElementById('modal-title').textContent = 'Nuevo proveedor';
    ['f-name','f-contact','f-phone','f-email','f-address'].forEach(id => document.getElementById(id).value = '');
  }
  document.getElementById('modal').classList.add('open');
}

function closeModal() { document.getElementById('modal').classList.remove('open'); }

async function save() {
  const name = document.getElementById('f-name').value.trim();
  if (!name) { toast('El nombre es requerido', 'warning'); return; }
  const dto = {
    name,
    contactName: document.getElementById('f-contact').value.trim() || null,
    phone: document.getElementById('f-phone').value.trim() || null,
    email: document.getElementById('f-email').value.trim() || null,
    address: document.getElementById('f-address').value.trim() || null
  };
  try {
    const id = document.getElementById('f-id').value;
    if (id) await api.put(`/suppliers/${id}`, dto);
    else await api.post('/suppliers', dto);
    toast(id ? 'Proveedor actualizado' : 'Proveedor creado', 'success');
    closeModal();
    load();
  } catch (e) { toast(e.message, 'error'); }
}

async function del(id) {
  if (!window.confirm('¿Desactivar este proveedor?')) return;
  try {
    await api.del(`/suppliers/${id}`);
    toast('Proveedor desactivado', 'success');
    load();
  } catch (e) { toast(e.message, 'error'); }
}

load().catch(e => toast(e.message, 'error'));
