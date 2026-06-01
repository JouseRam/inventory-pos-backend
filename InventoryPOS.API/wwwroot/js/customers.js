App.init('customers');

let customers = [];

async function load() {
  const q = document.getElementById('search').value.trim();
  customers = await api.get('/customers' + (q ? '?q=' + encodeURIComponent(q) : ''));
  render();
}

function render() {
  const tbody = document.getElementById('table-body');
  if (!customers.length) {
    tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><i class="bi bi-people"></i><p>Sin clientes registrados</p></div></td></tr>';
    return;
  }
  tbody.innerHTML = customers.map(c => `
    <tr>
      <td style="font-weight:600">${c.name}</td>
      <td>${c.phone || '—'}</td>
      <td>${c.email || '—'}</td>
      <td style="color:var(--text-muted);font-size:.82rem">${c.address || '—'}</td>
      <td><span class="badge badge-blue">${c.salesCount}</span></td>
      <td style="color:var(--text-muted);font-size:.8rem">${fmtDate(c.createdAt)}</td>
      <td>
        <button class="btn btn-secondary btn-sm btn-icon" onclick="openModal(${c.id})"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-danger btn-sm btn-icon" onclick="del(${c.id})" style="margin-left:4px"><i class="bi bi-trash"></i></button>
      </td>
    </tr>`).join('');
}

function openModal(id) {
  document.getElementById('f-id').value = id || '';
  if (id) {
    const c = customers.find(x => x.id === id);
    document.getElementById('modal-title').textContent = 'Editar cliente';
    document.getElementById('f-name').value    = c.name;
    document.getElementById('f-phone').value   = c.phone || '';
    document.getElementById('f-email').value   = c.email || '';
    document.getElementById('f-address').value = c.address || '';
  } else {
    document.getElementById('modal-title').textContent = 'Nuevo cliente';
    ['f-name','f-phone','f-email','f-address'].forEach(id => document.getElementById(id).value = '');
  }
  document.getElementById('modal').classList.add('open');
}

function closeModal() { document.getElementById('modal').classList.remove('open'); }

async function save() {
  const name = document.getElementById('f-name').value.trim();
  if (!name) { toast('El nombre es requerido', 'warning'); return; }
  const dto = {
    name,
    phone: document.getElementById('f-phone').value.trim() || null,
    email: document.getElementById('f-email').value.trim() || null,
    address: document.getElementById('f-address').value.trim() || null
  };
  try {
    const id = document.getElementById('f-id').value;
    if (id) await api.put(`/customers/${id}`, dto);
    else await api.post('/customers', dto);
    toast(id ? 'Cliente actualizado' : 'Cliente registrado', 'success');
    closeModal();
    load();
  } catch (e) { toast(e.message, 'error'); }
}

async function del(id) {
  if (!window.confirm('¿Desactivar este cliente?')) return;
  try {
    await api.del(`/customers/${id}`);
    toast('Cliente desactivado', 'success');
    load();
  } catch (e) { toast(e.message, 'error'); }
}

load().catch(e => toast(e.message, 'error'));
