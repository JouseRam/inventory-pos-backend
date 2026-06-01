App.init('categories');

let categories = [];

async function load() {
  categories = await api.get('/categories');
  render();
}

function render() {
  const q = document.getElementById('search').value.toLowerCase();
  const list = q ? categories.filter(c => c.name.toLowerCase().includes(q)) : categories;
  const tbody = document.getElementById('table-body');
  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><i class="bi bi-tags"></i><p>Sin categorías</p></div></td></tr>';
    return;
  }
  tbody.innerHTML = list.map(c => `
    <tr>
      <td style="font-weight:600">${c.name}</td>
      <td style="color:var(--text-muted)">${c.description || '—'}</td>
      <td><span class="badge badge-blue">${c.productCount}</span></td>
      <td><span class="badge ${c.isActive ? 'badge-green' : 'badge-gray'}">${c.isActive ? 'Activa' : 'Inactiva'}</span></td>
      <td>
        <button class="btn btn-secondary btn-sm btn-icon" onclick="openModal(${c.id})"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-danger btn-sm btn-icon" onclick="del(${c.id})" style="margin-left:4px"><i class="bi bi-trash"></i></button>
      </td>
    </tr>`).join('');
}

document.getElementById('search').addEventListener('input', render);

function openModal(id) {
  document.getElementById('f-id').value = id || '';
  if (id) {
    const c = categories.find(x => x.id === id);
    document.getElementById('modal-title').textContent = 'Editar categoría';
    document.getElementById('f-name').value = c.name;
    document.getElementById('f-desc').value = c.description || '';
  } else {
    document.getElementById('modal-title').textContent = 'Nueva categoría';
    document.getElementById('f-name').value = '';
    document.getElementById('f-desc').value = '';
  }
  document.getElementById('modal').classList.add('open');
}

function closeModal() { document.getElementById('modal').classList.remove('open'); }

async function save() {
  const name = document.getElementById('f-name').value.trim();
  if (!name) { toast('El nombre es requerido', 'warning'); return; }
  const dto = { name, description: document.getElementById('f-desc').value.trim() || null };
  try {
    const id = document.getElementById('f-id').value;
    if (id) await api.put(`/categories/${id}`, dto);
    else await api.post('/categories', dto);
    toast(id ? 'Categoría actualizada' : 'Categoría creada', 'success');
    closeModal();
    categories = await api.get('/categories');
    render();
  } catch (e) { toast(e.message, 'error'); }
}

async function del(id) {
  if (!window.confirm('¿Eliminar esta categoría?')) return;
  try {
    await api.del(`/categories/${id}`);
    toast('Categoría eliminada', 'success');
    categories = await api.get('/categories');
    render();
  } catch (e) { toast(e.message, 'error'); }
}

load().catch(e => toast(e.message, 'error'));
