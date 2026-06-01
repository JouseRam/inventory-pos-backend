App.init('sales');

let currentSaleId = null;

// Default dates: current month
const now = new Date();
const firstOfMonth = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0];
const today = now.toISOString().split('T')[0];
document.getElementById('f-from').value = firstOfMonth;
document.getElementById('f-to').value   = today;

async function loadSales() {
  const from    = document.getElementById('f-from').value;
  const to      = document.getElementById('f-to').value;
  const method  = document.getElementById('f-method').value;

  let url = '/sales?pageSize=200';
  if (from)   url += '&from=' + from;
  if (to)     url += '&to=' + to;
  if (method) url += '&paymentMethod=' + encodeURIComponent(method);

  const tbody = document.getElementById('table-body');
  tbody.innerHTML = '<tr><td colspan="10" style="text-align:center;padding:20px"><div class="spinner"></div></td></tr>';

  try {
    const res = await api.get(url);
    const list = res.data || [];

    // KPIs
    const total = list.reduce((s, x) => s + x.total, 0);
    document.getElementById('k-count').textContent = list.length;
    document.getElementById('k-total').textContent = '$' + fmt(total);
    document.getElementById('k-avg').textContent   = '$' + fmt(list.length ? total / list.length : 0);

    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="10"><div class="empty-state"><i class="bi bi-receipt"></i><p>Sin ventas en este período</p></div></td></tr>';
      return;
    }

    tbody.innerHTML = list.map(s => `
      <tr>
        <td style="font-weight:600">${s.folio}</td>
        <td>${s.customerName}</td>
        <td><span class="badge badge-blue">${s.paymentMethod}</span></td>
        <td>$${fmt(s.subtotal)}</td>
        <td>${s.discount > 0 ? '-$' + fmt(s.discount) : '—'}</td>
        <td>${s.tax > 0 ? '$' + fmt(s.tax) : '—'}</td>
        <td style="font-weight:700;color:var(--primary)">$${fmt(s.total)}</td>
        <td style="font-size:.8rem;color:var(--text-muted)">${fmtDateTime(s.createdAt)}</td>
        <td><span class="badge ${s.status === 'Cancelada' ? 'badge-red' : 'badge-green'}">${s.status}</span></td>
        <td><button class="btn btn-secondary btn-sm btn-icon" onclick="viewDetail(${s.id})" title="Ver detalle"><i class="bi bi-eye"></i></button></td>
      </tr>`).join('');
  } catch (e) { toast(e.message, 'error'); }
}

async function viewDetail(id) {
  currentSaleId = id;
  document.getElementById('d-body').innerHTML = '<div style="text-align:center;padding:30px"><div class="spinner"></div></div>';
  document.getElementById('detail-modal').classList.add('open');

  const s = await api.get('/sales/' + id);
  document.getElementById('d-title').textContent = `Venta ${s.folio}`;
  document.getElementById('d-cancel-btn').style.display = s.status !== 'Cancelada' ? 'block' : 'none';

  document.getElementById('d-body').innerHTML = `
    <div class="form-row" style="margin-bottom:16px">
      <div><label style="font-size:.75rem;color:var(--text-muted)">Folio</label><div style="font-weight:700;font-size:1.1rem">${s.folio}</div></div>
      <div><label style="font-size:.75rem;color:var(--text-muted)">Fecha</label><div>${fmtDateTime(s.createdAt)}</div></div>
      <div><label style="font-size:.75rem;color:var(--text-muted)">Cliente</label><div>${s.customerName}</div></div>
      <div><label style="font-size:.75rem;color:var(--text-muted)">Método</label><div><span class="badge badge-blue">${s.paymentMethod}</span></div></div>
    </div>
    <div class="table-responsive" style="margin-bottom:16px">
      <table>
        <thead><tr><th>Producto</th><th>Cant.</th><th>Precio unit.</th><th>Descuento</th><th>Subtotal</th></tr></thead>
        <tbody>
          ${s.items.map(i => `
            <tr>
              <td style="font-weight:500">${i.productName}</td>
              <td>${fmt(i.quantity, 2)}</td>
              <td>$${fmt(i.unitPrice)}</td>
              <td>${i.discount > 0 ? '-$' + fmt(i.discount) : '—'}</td>
              <td style="font-weight:600">$${fmt(i.subtotal)}</td>
            </tr>`).join('')}
        </tbody>
      </table>
    </div>
    <div style="display:flex;flex-direction:column;gap:4px;align-items:flex-end;background:#f8fafc;border-radius:8px;padding:14px">
      <div style="display:flex;gap:40px"><span style="color:var(--text-muted)">Subtotal</span><strong>$${fmt(s.subtotal)}</strong></div>
      ${s.discount > 0 ? `<div style="display:flex;gap:40px"><span style="color:var(--text-muted)">Descuento</span><strong>-$${fmt(s.discount)}</strong></div>` : ''}
      ${s.tax > 0 ? `<div style="display:flex;gap:40px"><span style="color:var(--text-muted)">IVA</span><strong>$${fmt(s.tax)}</strong></div>` : ''}
      <div style="display:flex;gap:40px;font-size:1.1rem"><span>TOTAL</span><strong style="color:var(--primary)">$${fmt(s.total)}</strong></div>
      <div style="display:flex;gap:40px;font-size:.85rem"><span style="color:var(--text-muted)">Recibido</span><span>$${fmt(s.amountPaid)}</span></div>
      <div style="display:flex;gap:40px;font-size:.85rem"><span style="color:var(--text-muted)">Cambio</span><span style="color:var(--success)">$${fmt(s.change)}</span></div>
    </div>
    ${s.notes ? `<p style="margin-top:12px;color:var(--text-muted);font-size:.85rem">Notas: ${s.notes}</p>` : ''}`;
}

function closeDetail() {
  document.getElementById('detail-modal').classList.remove('open');
  currentSaleId = null;
}

async function cancelSale() {
  if (!currentSaleId) return;
  if (!window.confirm('¿Cancelar esta venta? Se restaurará el stock.')) return;
  try {
    await api.del('/sales/' + currentSaleId);
    toast('Venta cancelada', 'success');
    closeDetail();
    loadSales();
  } catch (e) { toast(e.message, 'error'); }
}

loadSales();
