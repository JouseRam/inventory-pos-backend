/* Point of sale: search or scan variants, build the ticket, collect mixed payments. */
POS.boot({ module: 'Sales', title: 'Punto de venta' }, async () => {
  const { api, session, fmt, esc, shade, toast, fail, $ } = POS;
  const CART_KEY = 'pos.cart';

  let cart = loadCart();          // [{ variant, qty }]
  let cashSession = null;
  let printer = { enabled: false, autoPrint: false };
  let lastSaleId = null;
  let searchTimer = null;

  // ── Cart persistence (per browser tab, survives a reload) ──
  function loadCart() { try { return JSON.parse(sessionStorage.getItem(CART_KEY) || '[]'); } catch { return []; } }
  function saveCart() { try { sessionStorage.setItem(CART_KEY, JSON.stringify(cart)); } catch { /* storage blocked */ } }

  // ── Search ──
  const hintCodes = ['5.0', '6.1', '7.1', '8.3', '9.12', '6.46'];
  $('#hint-codes').innerHTML = hintCodes.map(c =>
    `<button class="btn sm" type="button" data-code="${c}">${shade.swatch(c, 'sm')}<span class="shade-code">${c}</span><span class="muted">${esc(shade.describe(c))}</span></button>`).join('');
  $('#hint-codes').addEventListener('click', e => {
    const b = e.target.closest('[data-code]');
    if (b) { $('#search').value = b.dataset.code; search(); }
  });

  function stockPill(v) {
    if (v.stock <= 0) return '<span class="pill crit"><i class="bi bi-x-circle"></i>Agotado</span>';
    if (v.lowStock) return `<span class="pill warn"><i class="bi bi-exclamation-triangle"></i>Quedan ${fmt.qty(v.stock)} pzas</span>`;
    return `<span class="pill">${fmt.qty(v.stock)} pzas</span>`;
  }

  function renderResults(list, query) {
    $('#hint').hidden = !!query;
    if (!query) { $('#results').innerHTML = ''; return; }
    if (!list.length) {
      $('#results').innerHTML = `<div class="panel empty" style="grid-column:1/-1">Nada coincide con «${esc(query)}». Prueba con el nombre, la marca o el número de tono.</div>`;
      return;
    }
    $('#results').innerHTML = list.map(v => `
      <button class="shade-card" type="button" data-id="${v.id}" ${v.stock <= 0 ? 'disabled' : ''}>
        ${shade.swatch(v.shadeCode)}
        <span class="code">${esc(v.shadeCode || v.sku)}</span>
        <span class="desc">${esc(v.shadeName || shade.describe(v.shadeCode) || v.sku)}</span>
        <span class="name">${esc(v.productName)}${v.brandName ? ` <span class="muted">· ${esc(v.brandName)}</span>` : ''}</span>
        <span class="meta"><span>${v.size ? `${fmt.qty(v.size)} ${esc(v.unit)}` : esc(v.sku)}</span>${stockPill(v)}<span class="price">${fmt.money(v.price)}</span></span>
      </button>`).join('');
    $('#results').__list = list;
  }

  async function search() {
    const q = $('#search').value.trim();
    if (!q) { renderResults([], ''); return; }
    try { renderResults(await api.get('/variants/search?q=' + encodeURIComponent(q)), q); } catch (err) { fail(err); }
  }

  $('#search').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(search, 220); });
  // Enter: a barcode or SKU adds straight to the ticket; otherwise it just searches.
  $('#search').addEventListener('keydown', async e => {
    if (e.key === 'Escape') { e.target.value = ''; renderResults([], ''); }
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const q = e.target.value.trim();
    if (!q) return;
    try {
      const v = await api.get('/variants/code/' + encodeURIComponent(q));
      add(v);
      e.target.value = '';
      renderResults([], '');
    } catch (err) {
      if (err.status === 404) search(); else fail(err);
    }
  });
  $('#results').addEventListener('click', e => {
    const card = e.target.closest('.shade-card');
    if (!card) return;
    add($('#results').__list.find(v => v.id === Number(card.dataset.id)));
    $('#search').focus();
  });

  // ── Ticket ──
  function add(v) {
    if (v.stock <= 0) { toast(`${v.displayName} está agotado.`, 'error'); return; }
    const line = cart.find(l => l.variant.id === v.id);
    if (line) {
      if (line.qty + 1 > v.stock) { toast(`Solo hay ${fmt.qty(v.stock)} de ${v.displayName}.`, 'error'); return; }
      line.qty += 1;
      line.variant = v;
    } else {
      cart.push({ variant: v, qty: 1 });
    }
    renderTicket();
  }

  function totals() {
    const subtotal = cart.reduce((s, l) => s + Math.round(l.qty * l.variant.price * 100) / 100, 0);
    const discount = Math.min(Math.max(Number($('#discount').value) || 0, 0), subtotal);
    return { subtotal, discount, total: Math.round((subtotal - discount) * 100) / 100 };
  }

  function renderTicket() {
    saveCart();
    $('#lines').innerHTML = cart.length ? cart.map((l, i) => `
      <div class="line">
        <div class="what">${shade.swatch(l.variant.shadeCode, 'sm')}<span>${esc(l.variant.displayName)}</span></div>
        <div class="amount">${fmt.money(l.qty * l.variant.price)}</div>
        <div class="controls">
          <span class="stepper">
            <button type="button" data-dec="${i}" aria-label="Quitar uno">−</button>
            <input type="number" min="0.001" step="1" value="${l.qty}" data-qty="${i}" aria-label="Cantidad">
            <button type="button" data-inc="${i}" aria-label="Agregar uno">+</button>
          </span>
          × ${fmt.money(l.variant.price)}
          <span style="flex:1"></span>
          <button class="btn ghost sm danger" type="button" data-del="${i}" aria-label="Eliminar renglón"><i class="bi bi-trash3"></i></button>
        </div>
      </div>`).join('') : '<div class="empty">Escanea un producto o elige un tono para empezar.</div>';

    const t = totals();
    $('#t-sub').textContent = fmt.money(t.subtotal);
    $('#t-total').textContent = fmt.money(t.total);
    $('#t-tax').textContent = `IVA incluido · ${cart.reduce((n, l) => n + l.qty, 0)} pzas`;
    $('#pay-btn').disabled = !cart.length || !cashSession || t.total <= 0;
  }

  $('#lines').addEventListener('click', e => {
    const t = e.target.closest('button');
    if (!t) return;
    const idx = Number(t.dataset.inc ?? t.dataset.dec ?? t.dataset.del);
    const line = cart[idx];
    if (!line) return;
    if ('inc' in t.dataset) {
      if (line.qty + 1 > line.variant.stock) { toast(`Solo hay ${fmt.qty(line.variant.stock)} disponibles.`, 'error'); return; }
      line.qty += 1;
    } else if ('dec' in t.dataset) {
      line.qty -= 1;
      if (line.qty <= 0) cart.splice(idx, 1);
    } else {
      cart.splice(idx, 1);
    }
    renderTicket();
  });
  $('#lines').addEventListener('change', e => {
    const idx = e.target.dataset.qty;
    if (idx === undefined) return;
    const line = cart[Number(idx)];
    const qty = Number(e.target.value);
    if (!(qty > 0)) cart.splice(Number(idx), 1);
    else if (qty > line.variant.stock) toast(`Solo hay ${fmt.qty(line.variant.stock)} disponibles.`, 'error');
    else line.qty = qty;
    renderTicket();
  });
  $('#discount').addEventListener('input', renderTicket);
  $('#clear-btn').addEventListener('click', () => { cart = []; $('#discount').value = 0; renderTicket(); });

  // ── Cash session gate ──
  async function refreshCash() {
    cashSession = await POS.refreshCashChip();
    $('#closed-note').hidden = !!cashSession;
    $('#open-form').hidden = !session.can('CashRegister', true);
    $('#ticket-meta').textContent = cashSession ? `Caja ${cashSession.folio} · ${session.profile.userName}` : 'Caja cerrada';
    renderTicket();
  }
  $('#open-form').addEventListener('submit', async e => {
    e.preventDefault();
    try {
      await api.post('/cash-sessions/open', { openingAmount: Number($('#opening').value) || 0 });
      toast('Caja abierta.');
      await refreshCash();
    } catch (err) { fail(err); }
  });

  // ── Payment ──
  const dlg = $('#pay-dialog');
  const num = id => Math.max(Number($(id).value) || 0, 0);

  function payState() {
    const { total } = totals();
    const cash = num('#pay-cash'), card = num('#pay-card'), transfer = num('#pay-transfer');
    const nonCash = card + transfer;
    const paid = cash + nonCash;
    if (nonCash > total) return { ok: false, text: ['Tarjeta + transferencia exceden el total', ''], cls: 'short' };
    if (paid < total) return { ok: false, text: ['Falta', fmt.money(total - paid)], cls: 'short' };
    return { ok: true, text: ['Cambio', fmt.money(paid - total)], cls: 'ok' };
  }
  /** Suggested bills cover what is left after card and transfer: the exact amount plus the next round notes. */
  function renderQuick() {
    const due = Math.max(Math.round((totals().total - num('#pay-card') - num('#pay-transfer')) * 100) / 100, 0);
    const bills = due > 0 ? [...new Set([due, ...[50, 100, 200, 500, 1000].map(b => Math.ceil(due / b) * b)])].filter(v => v >= due).slice(0, 4) : [];
    $('#quick-cash').innerHTML = bills.map((b, i) => `<button class="btn sm" type="button" data-bill="${b}">${i === 0 ? 'Exacto' : fmt.money(b)}</button>`).join('');
  }
  function renderPay() {
    const s = payState();
    $('#pay-status').className = 'pay-status ' + s.cls;
    $('#pay-status').innerHTML = `<span>${esc(s.text[0])}</span><span>${esc(s.text[1])}</span>`;
    $('#pay-confirm').disabled = !s.ok;
  }
  ['#pay-cash', '#pay-card', '#pay-transfer'].forEach(id => $(id).addEventListener('input', renderPay));
  ['#pay-card', '#pay-transfer'].forEach(id => $(id).addEventListener('input', renderQuick));

  function openPay() {
    if ($('#pay-btn').disabled) return;
    const { total } = totals();
    $('#pay-total').textContent = fmt.money(total);
    ['#pay-cash', '#pay-card', '#pay-transfer', '#pay-card-ref', '#pay-transfer-ref', '#pay-notes'].forEach(id => { $(id).value = ''; });
    $('#pay-body').hidden = false; $('#pay-done').hidden = true;
    $('#pay-confirm').hidden = false; $('#pay-cancel').hidden = false; $('#pay-new').hidden = true; $('#pay-print').hidden = true;
    $('#pay-error').hidden = true;
    renderQuick();
    renderPay();
    dlg.showModal();
    $('#pay-cash').focus();
  }
  $('#quick-cash').addEventListener('click', e => {
    const b = e.target.closest('[data-bill]');
    if (!b) return;
    $('#pay-cash').value = b.dataset.bill;
    renderPay();
  });
  $('#pay-btn').addEventListener('click', openPay);

  $('#pay-form').addEventListener('submit', async e => {
    e.preventDefault();
    const action = e.submitter?.value;
    if (action === 'cancel') { dlg.close(); return; }
    if (action === 'new') { dlg.close(); $('#search').focus(); return; }
    if (!payState().ok) return;

    const payments = [
      { method: 'Efectivo', amount: num('#pay-cash'), reference: null },
      { method: 'Tarjeta', amount: num('#pay-card'), reference: $('#pay-card-ref').value.trim() || null },
      { method: 'Transferencia', amount: num('#pay-transfer'), reference: $('#pay-transfer-ref').value.trim() || null },
    ].filter(p => p.amount > 0);
    const body = {
      customerId: $('#customer').value ? Number($('#customer').value) : null,
      discount: totals().discount,
      notes: $('#pay-notes').value.trim() || null,
      items: cart.map(l => ({ variantId: l.variant.id, quantity: l.qty, unitPrice: null, discount: 0 })),
      payments,
    };
    $('#pay-confirm').disabled = true;
    try {
      const sale = await api.post('/sales', body);
      $('#done-folio').textContent = sale.folio;
      $('#done-change').textContent = fmt.money(sale.change);
      lastSaleId = sale.id;
      $('#pay-print').hidden = !printer.enabled;
      $('#done-print').textContent = '';
      if (printer.autoPrint) printTicket(false);
      $('#pay-body').hidden = true; $('#pay-done').hidden = false;
      $('#pay-confirm').hidden = true; $('#pay-cancel').hidden = true; $('#pay-new').hidden = false;
      $('#pay-new').focus();
      cart = []; $('#discount').value = 0; $('#customer').value = '';
      renderTicket();
      search();
      refreshCash();
    } catch (err) {
      $('#pay-error').textContent = err.message;
      $('#pay-error').hidden = false;
      $('#pay-confirm').disabled = false;
    }
  });

  // ── Ticket printing: a failed print never undoes the sale ──
  async function printTicket(reprint) {
    if (!lastSaleId) return;
    $('#done-print').textContent = 'Imprimiendo ticket…';
    try {
      await api.post(`/printing/sales/${lastSaleId}?reprint=${reprint}`);
      $('#done-print').textContent = reprint ? 'Ticket reimpreso.' : 'Ticket impreso.';
    } catch (err) {
      $('#done-print').textContent = `No se imprimió: ${err.message}`;
    }
  }
  $('#pay-print').addEventListener('click', () => printTicket(true));

  // ── Keyboard: F2 search, F9 pay ──
  document.addEventListener('keydown', e => {
    if (e.key === 'F2') { e.preventDefault(); $('#search').focus(); $('#search').select(); }
    if (e.key === 'F9') { e.preventDefault(); openPay(); }
  });

  // ── Initial data ──
  const [settings, customers, printerStatus] = await Promise.all([
    api.get('/settings').catch(() => []),
    api.get('/customers').catch(() => []),
    api.get('/printing/status').catch(() => null),
  ]);
  if (printerStatus) printer = printerStatus;
  const name = settings.find(s => s.key === 'BusinessName')?.value;
  if (name) $('#store-name').textContent = name;
  $('#customer').insertAdjacentHTML('beforeend', customers.map(c =>
    `<option value="${c.id}">${esc(c.name)}${c.phone ? ' · ' + esc(c.phone) : ''}</option>`).join(''));
  await refreshCash();
  $('#search').focus();
});
