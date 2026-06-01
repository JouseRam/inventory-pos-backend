App.init('pos');

let allProducts = [];
let cart = [];
let paymentMethod = 'Efectivo';

// ── Load data ──────────────────────────────────────────
async function loadData() {
  const [products, categories, customers] = await Promise.all([
    api.get('/products?active=true'),
    api.get('/categories'),
    api.get('/customers')
  ]);

  allProducts = products;

  const catSel = document.getElementById('cat-filter');
  categories.forEach(c => {
    const o = document.createElement('option');
    o.value = c.id; o.textContent = c.name;
    catSel.appendChild(o);
  });

  const custSel = document.getElementById('customer-sel');
  customers.forEach(c => {
    const o = document.createElement('option');
    o.value = c.id; o.textContent = `${c.name}${c.phone ? ' · ' + c.phone : ''}`;
    custSel.appendChild(o);
  });

  renderProducts(allProducts);
}

function renderProducts(list) {
  const grid = document.getElementById('products-grid');
  if (!list.length) {
    grid.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:40px;color:var(--text-muted)"><i class="bi bi-search" style="font-size:2rem;opacity:.3"></i><p style="margin-top:8px">Sin resultados</p></div>';
    return;
  }
  grid.innerHTML = list.map(p => {
    const low = p.stock <= p.minStock;
    const out = p.stock <= 0;
    return `<div class="product-card ${low ? 'low-stock' : ''} ${out ? 'out-stock' : ''}" onclick="${out ? '' : 'addToCart(' + p.id + ')'}">
      <div class="p-name">${p.name}</div>
      ${p.sku ? `<div style="font-size:.7rem;color:var(--text-muted)">${p.sku}</div>` : ''}
      <div class="p-price">$${fmt(p.price)}</div>
      <div class="p-stock ${low ? 'badge badge-amber' : ''}">
        ${out ? '<span class="badge badge-red">Sin stock</span>' : fmt(p.stock, 0) + ' ' + p.unit}
      </div>
    </div>`;
  }).join('');
}

// ── Search & filter ────────────────────────────────────
document.getElementById('prod-search').addEventListener('input', filterProducts);
document.getElementById('cat-filter').addEventListener('change', filterProducts);

function filterProducts() {
  const q = document.getElementById('prod-search').value.toLowerCase().trim();
  const catId = +document.getElementById('cat-filter').value || null;
  let list = allProducts;
  if (q) list = list.filter(p => p.name.toLowerCase().includes(q) || (p.sku && p.sku.toLowerCase().includes(q)) || (p.barcode && p.barcode.toLowerCase().includes(q)));
  if (catId) list = list.filter(p => p.categoryId === catId);
  renderProducts(list);
}

// ── Cart ───────────────────────────────────────────────
function addToCart(productId) {
  const p = allProducts.find(x => x.id === productId);
  if (!p) return;
  const existing = cart.find(c => c.productId === productId);
  if (existing) {
    if (existing.qty >= p.stock) { toast('Stock insuficiente', 'warning'); return; }
    existing.qty++;
    existing.subtotal = parseFloat((existing.qty * existing.unitPrice).toFixed(2));
  } else {
    cart.push({ productId: p.id, productName: p.name, qty: 1, unitPrice: p.price, subtotal: p.price });
  }
  renderCart();
  toast(`${p.name} agregado`, 'success', 1200);
}

function removeFromCart(productId) {
  cart = cart.filter(c => c.productId !== productId);
  renderCart();
}

function changeQty(productId, delta) {
  const item = cart.find(c => c.productId === productId);
  if (!item) return;
  const p = allProducts.find(x => x.id === productId);
  item.qty += delta;
  if (item.qty <= 0) { removeFromCart(productId); return; }
  if (p && item.qty > p.stock) { item.qty -= delta; toast('Stock insuficiente', 'warning'); return; }
  item.subtotal = parseFloat((item.qty * item.unitPrice).toFixed(2));
  renderCart();
}

function clearCart() {
  if (cart.length && !window.confirm('¿Limpiar el carrito?')) return;
  cart = [];
  renderCart();
}

function renderCart() {
  const el = document.getElementById('cart-items');
  if (!cart.length) {
    el.innerHTML = '<div class="empty-state"><i class="bi bi-cart"></i><p>Carrito vacío</p></div>';
  } else {
    el.innerHTML = cart.map(item => `
      <div class="cart-item">
        <div class="item-name">${item.productName}</div>
        <div class="qty-ctrl">
          <button class="qty-btn" onclick="changeQty(${item.productId}, -1)">−</button>
          <span class="qty-val">${item.qty}</span>
          <button class="qty-btn" onclick="changeQty(${item.productId}, 1)">+</button>
        </div>
        <div class="item-sub">$${fmt(item.subtotal)}</div>
        <button class="item-remove" onclick="removeFromCart(${item.productId})"><i class="bi bi-x"></i></button>
      </div>`).join('');
  }
  calcTotals();
}

// ── Totals ─────────────────────────────────────────────
function calcTotals() {
  const sub = cart.reduce((s, i) => s + i.subtotal, 0);
  const disc = parseFloat(document.getElementById('t-discount').value) || 0;
  const applyTax = document.getElementById('apply-tax').checked;
  const taxable = Math.max(0, sub - disc);
  const tax = applyTax ? parseFloat((taxable * 0.16).toFixed(2)) : 0;
  const total = parseFloat((taxable + tax).toFixed(2));

  document.getElementById('t-sub').textContent   = '$' + fmt(sub);
  document.getElementById('t-tax').textContent   = '$' + fmt(tax);
  document.getElementById('t-total').textContent = '$' + fmt(total);
  calcChange();
}

function calcChange() {
  const total = parseFloat(document.getElementById('t-total').textContent.replace('$','').replace(',','')) || 0;
  const paid  = parseFloat(document.getElementById('amount-paid').value) || 0;
  const change = parseFloat((paid - total).toFixed(2));
  const el = document.getElementById('change-display');
  el.textContent = '$' + fmt(Math.max(0, change));
  el.style.color = change >= 0 ? 'var(--success)' : 'var(--danger)';
}

// ── Payment ────────────────────────────────────────────
function selectMethod(el) {
  document.querySelectorAll('.pay-method').forEach(e => e.classList.remove('active'));
  el.classList.add('active');
  paymentMethod = el.dataset.method;
}

// ── Process sale ───────────────────────────────────────
async function processSale() {
  if (!cart.length) { toast('El carrito está vacío', 'warning'); return; }

  const total = parseFloat(document.getElementById('t-total').textContent.replace('$','').replace(',','')) || 0;
  const paid  = parseFloat(document.getElementById('amount-paid').value) || 0;
  const disc  = parseFloat(document.getElementById('t-discount').value) || 0;
  const applyTax = document.getElementById('apply-tax').checked;
  const sub   = cart.reduce((s, i) => s + i.subtotal, 0);
  const tax   = applyTax ? parseFloat(((Math.max(0, sub - disc)) * 0.16).toFixed(2)) : 0;
  const customerId = +document.getElementById('customer-sel').value || null;

  if (paymentMethod === 'Efectivo' && paid < total) {
    toast('El monto recibido es menor al total', 'warning'); return;
  }

  const dto = {
    customerId,
    paymentMethod,
    amountPaid: paid || total,
    discount: disc,
    tax,
    notes: null,
    items: cart.map(i => ({
      productId: i.productId,
      quantity: i.qty,
      unitPrice: i.unitPrice,
      discount: 0
    }))
  };

  try {
    const btn = document.querySelector('button[onclick="processSale()"]');
    btn.disabled = true; btn.innerHTML = '<div class="spinner"></div>';

    const result = await api.post('/sales', dto);
    showReceipt(result, total, paid);
    cart = [];
    renderCart();
    document.getElementById('t-discount').value = '0';
    document.getElementById('apply-tax').checked = false;
    document.getElementById('amount-paid').value = '';
    document.getElementById('customer-sel').value = '';
    calcTotals();

    // Refresh product stock
    loadData();

    btn.disabled = false; btn.innerHTML = '<i class="bi bi-check-circle"></i> Registrar venta';
  } catch (e) {
    toast(e.message, 'error', 5000);
    const btn = document.querySelector('button[onclick="processSale()"]');
    btn.disabled = false; btn.innerHTML = '<i class="bi bi-check-circle"></i> Registrar venta';
  }
}

function showReceipt(sale, total, paid) {
  const change = Math.max(0, paid - total);
  document.getElementById('receipt-body').innerHTML = `
    <div style="text-align:center;margin-bottom:16px">
      <div style="font-size:2.5rem;color:var(--success)"><i class="bi bi-check-circle-fill"></i></div>
      <div style="font-size:1.4rem;font-weight:700;margin-top:8px">$${fmt(total)}</div>
      <div style="color:var(--text-muted);font-size:.85rem">Folio: <strong>${sale.folio}</strong></div>
    </div>
    <div style="background:#f8fafc;border-radius:8px;padding:14px">
      <div style="display:flex;justify-content:space-between;margin-bottom:6px"><span>Recibido</span><strong>$${fmt(paid)}</strong></div>
      <div style="display:flex;justify-content:space-between;color:var(--success)"><span>Cambio</span><strong>$${fmt(change)}</strong></div>
    </div>
    <p style="text-align:center;color:var(--text-muted);font-size:.8rem;margin-top:12px">¡Gracias por su compra!</p>`;
  document.getElementById('receipt-modal').classList.add('open');
}

function closeReceipt() {
  document.getElementById('receipt-modal').classList.remove('open');
  document.getElementById('prod-search').focus();
}

loadData().catch(e => toast(e.message, 'error'));
