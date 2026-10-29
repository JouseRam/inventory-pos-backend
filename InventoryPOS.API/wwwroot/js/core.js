/* Shared runtime for every page: API client, session, app shell, formatting and the shade decoder. */
(function () {
  const TOKEN_KEY = 'pos.token';
  const PROFILE_KEY = 'pos.profile';
  const THEME_KEY = 'pos.theme';

  const store = {
    get(key, area = localStorage) { try { return area.getItem(key); } catch { return null; } },
    set(key, value, area = localStorage) { try { value == null ? area.removeItem(key) : area.setItem(key, value); } catch { /* storage blocked */ } },
  };

  // ── Formatting ──
  const money = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
  const qtyFmt = new Intl.NumberFormat('es-MX', { maximumFractionDigits: 3 });
  const fmt = {
    money: n => money.format(Number(n || 0)),
    qty: n => qtyFmt.format(Number(n || 0)),
    date: d => d ? new Date(d).toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' }) : '',
    /** Calendar dates (lot expiration) come without time zone; show them as-is. */
    day: d => d ? new Date(String(d).slice(0, 10) + 'T12:00:00').toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' }) : '',
    dateTime: d => d ? new Date(d).toLocaleString('es-MX', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '',
    time: d => d ? new Date(d).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' }) : '',
    isoDay: (d = new Date()) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10),
  };

  /** Escapes text for safe interpolation into HTML templates. */
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // ── Hair-color shade decoder: "7.1" = level 7 (medium blonde) + reflect 1 (ash) ──
  const LEVELS = { 1: 'Negro', 2: 'Castaño muy oscuro', 3: 'Castaño oscuro', 4: 'Castaño medio', 5: 'Castaño claro', 6: 'Rubio oscuro',
    7: 'Rubio medio', 8: 'Rubio claro', 9: 'Rubio muy claro', 10: 'Rubio platino', 11: 'Superaclarante', 12: 'Superaclarante especial' };
  const REFLECTS = {
    0: { name: 'Natural', h: 28, s: 30 }, 1: { name: 'Ceniza', h: 215, s: 14 }, 2: { name: 'Irisado', h: 285, s: 26 },
    3: { name: 'Dorado', h: 42, s: 62 }, 4: { name: 'Cobrizo', h: 20, s: 70 }, 5: { name: 'Caoba', h: 345, s: 48 },
    6: { name: 'Rojo', h: 356, s: 68 }, 7: { name: 'Mate', h: 95, s: 20 }, 8: { name: 'Perla', h: 230, s: 16 }, 9: { name: 'Ceniza suave', h: 205, s: 10 },
  };
  const shade = {
    parse(code) {
      const m = String(code || '').trim().match(/^(\d{1,2})(?:\s*[.,/-]\s*(\d{1,2}))?$/);
      if (!m) return null;
      const level = Number(m[1]);
      if (level < 1 || level > 12) return null;
      const digits = (m[2] || '0').split('').map(Number);
      return { level, reflects: digits.filter(d => REFLECTS[d]) };
    },
    describe(code) {
      const p = shade.parse(code);
      if (!p) return '';
      const reflectNames = p.reflects.filter((d, i) => !(d === 0 && i > 0)).map(d => REFLECTS[d].name);
      const unique = [...new Set(reflectNames)];
      return `${LEVELS[p.level]} · ${unique.join(' ') || 'Natural'}`;
    },
    /** Approximates the dyed color: level drives lightness, first reflect dominates hue, second one tints it. */
    color(code) {
      const p = shade.parse(code);
      if (!p) return null;
      const light = Math.min(88, 9 + (p.level - 1) * 8.2);
      const base = { h: 28, s: 30 };
      const [r1, r2] = p.reflects;
      const a = r1 != null ? REFLECTS[r1] : base;
      const b = r2 != null ? REFLECTS[r2] : a;
      const mix = (x, y, w) => x + (y - x) * w;
      // Hue is an angle: blend along the shorter arc so copper (20) + red (356) stays warm.
      const mixHue = (x, y, w) => { const d = ((y - x + 540) % 360) - 180; return (x + d * w + 360) % 360; };
      const hue = r1 == null || r1 === 0 ? base.h : mixHue(a.h, b.h, .25);
      const sat = mix(base.s, mix(a.s, b.s, .25), r1 == null || r1 === 0 ? 0 : .8) * (p.level <= 2 ? .4 : 1);
      return `hsl(${Math.round(hue)} ${Math.round(sat)}% ${Math.round(light)}%)`;
    },
    swatch(code, size = '') {
      const c = shade.color(code);
      return c
        ? `<span class="swatch ${size}" style="--c:${c}" title="${esc(shade.describe(code))}" aria-hidden="true"></span>`
        : `<span class="swatch none ${size}" aria-hidden="true"><i class="bi bi-droplet"></i></span>`;
    },
  };

  // ── Session ──
  const session = {
    get token() { return store.get(TOKEN_KEY); },
    get profile() { try { return JSON.parse(store.get(PROFILE_KEY, sessionStorage) || 'null'); } catch { return null; } },
    save(auth) {
      store.set(TOKEN_KEY, auth.token);
      store.set(PROFILE_KEY, JSON.stringify(auth.user), sessionStorage);
    },
    clear() { store.set(TOKEN_KEY, null); store.set(PROFILE_KEY, null, sessionStorage); },
    can(module, edit = false) {
      const p = session.profile;
      if (!p) return false;
      if (p.isSuperAdmin) return true;
      const perm = p.permissions.find(x => x.module === module);
      return !!perm && (edit ? perm.canEdit : perm.canView);
    },
    logout() { session.clear(); location.href = 'login.html'; },
  };

  // ── API client: JSON in/out, bearer token, ProblemDetails messages ──
  class ApiError extends Error { constructor(message, status) { super(message); this.status = status; } }
  async function request(method, path, body) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (session.token) headers.Authorization = 'Bearer ' + session.token;
    let res;
    try {
      res = await fetch('/api' + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
    } catch {
      throw new ApiError('No hay conexión con el servidor. Revisa la red e intenta de nuevo.', 0);
    }
    if (res.status === 401 && !path.startsWith('/auth/login')) {
      session.clear();
      location.href = 'login.html?next=' + encodeURIComponent(location.pathname.slice(1) + location.search);
      throw new ApiError('Tu sesión expiró.', 401);
    }
    if (res.status === 204) return null;
    const text = await res.text();
    const data = text ? JSON.parse(text) : null;
    if (!res.ok) {
      const message = data?.detail || data?.title || (res.status === 403 ? 'No tienes permiso para esta acción.' : `Error ${res.status}`);
      throw new ApiError(message, res.status);
    }
    return data;
  }
  const api = {
    get: p => request('GET', p),
    post: (p, b = {}) => request('POST', p, b),
    put: (p, b = {}) => request('PUT', p, b),
    del: p => request('DELETE', p),
  };

  // ── UI helpers ──
  function toast(message, type = '') {
    let box = document.getElementById('toasts');
    if (!box) { box = document.createElement('div'); box.id = 'toasts'; box.setAttribute('role', 'status'); document.body.appendChild(box); }
    const el = document.createElement('div');
    el.className = 'toast ' + type;
    el.textContent = message;
    box.appendChild(el);
    setTimeout(() => el.remove(), type === 'error' ? 6000 : 3200);
  }
  const fail = err => toast(err.message || String(err), 'error');

  const $ = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

  /** Reads a form into an object; data-type="number|bool|int-or-null" coerces values. */
  function formData(form) {
    const out = {};
    for (const el of form.elements) {
      if (!el.name) continue;
      const t = el.dataset.type;
      if (el.type === 'checkbox') out[el.name] = el.checked;
      else if (t === 'number') out[el.name] = el.value === '' ? 0 : Number(el.value);
      else if (t === 'nullable-number') out[el.name] = el.value === '' ? null : Number(el.value);
      else out[el.name] = el.value.trim() === '' ? null : el.value.trim();
    }
    return out;
  }
  function fillForm(form, data) {
    for (const el of form.elements) {
      if (!el.name || !(el.name in data)) continue;
      const v = data[el.name];
      if (el.type === 'checkbox') el.checked = !!v;
      else if (el.type === 'date') el.value = v ? String(v).slice(0, 10) : '';
      else el.value = v ?? '';
    }
  }

  /** Wires a <dialog> form: shows inline errors and keeps the dialog open until the save succeeds. */
  function dialogForm(dialog, onSubmit) {
    const form = dialog.querySelector('form');
    const errorBox = dialog.querySelector('.form-error');
    form.addEventListener('submit', async e => {
      e.preventDefault();
      if (e.submitter?.value === 'cancel') { dialog.close(); return; }
      const btn = e.submitter;
      if (btn) btn.disabled = true;
      if (errorBox) errorBox.hidden = true;
      try {
        await onSubmit(formData(form), form);
        dialog.close();
      } catch (err) {
        if (errorBox) { errorBox.textContent = err.message; errorBox.hidden = false; } else fail(err);
      } finally {
        if (btn) btn.disabled = false;
      }
    });
    dialog.querySelectorAll('[data-close]').forEach(b => b.addEventListener('click', () => dialog.close()));
    return {
      open(data = {}) {
        form.reset();
        if (errorBox) errorBox.hidden = true;
        fillForm(form, data);
        dialog.showModal();
        form.querySelector('input:not([type=hidden]),select,textarea')?.focus();
      },
    };
  }

  /** In-page confirmation (native confirm() is avoided so the flow stays styled and testable). */
  function confirmBox(message, { okText = 'Confirmar', danger = false, input = null } = {}) {
    return new Promise(resolve => {
      const d = document.createElement('dialog');
      d.innerHTML = `<form method="dialog"><div class="body"><p style="margin:0">${esc(message)}</p>
        ${input ? `<label class="field"><span>${esc(input)}</span><input id="confirm-input" required maxlength="300"></label>` : ''}</div>
        <footer><button class="btn ghost" value="cancel" formnovalidate>Cancelar</button>
        <button class="btn ${danger ? 'danger' : 'primary'}" value="ok">${esc(okText)}</button></footer></form>`;
      document.body.appendChild(d);
      d.querySelector('form').addEventListener('submit', e => {
        const ok = e.submitter?.value === 'ok';
        const text = d.querySelector('#confirm-input')?.value.trim();
        if (ok && input && !text) { e.preventDefault(); return; }
        resolve(ok ? (input ? text : true) : false);
      });
      d.addEventListener('close', () => { d.remove(); resolve(false); });
      d.showModal();
    });
  }

  // ── Theme ──
  function applyTheme(theme) {
    if (theme) document.documentElement.dataset.theme = theme; else delete document.documentElement.dataset.theme;
  }
  applyTheme(store.get(THEME_KEY));
  function toggleTheme() {
    const current = document.documentElement.dataset.theme
      || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    const next = current === 'dark' ? 'light' : 'dark';
    store.set(THEME_KEY, next);
    applyTheme(next);
  }

  // ── App shell ──
  const NAV = [
    { group: 'Venta', items: [
      { href: 'pos.html', label: 'Punto de venta', icon: 'bi-upc-scan', module: 'Sales' },
      { href: 'cash.html', label: 'Caja', icon: 'bi-cash-stack', module: 'CashRegister' },
      { href: 'sales.html', label: 'Ventas', icon: 'bi-receipt', module: 'Sales' },
    ] },
    { group: 'Inventario', items: [
      { href: 'products.html', label: 'Productos', icon: 'bi-palette2', module: 'Products' },
      { href: 'inventory.html', label: 'Existencias', icon: 'bi-box-seam', module: 'Inventory' },
      { href: 'purchases.html', label: 'Recepciones', icon: 'bi-truck', module: 'Purchases' },
      { href: 'catalogs.html', label: 'Categorías y marcas', icon: 'bi-tags', module: 'Products' },
    ] },
    { group: 'Personas', items: [
      { href: 'customers.html', label: 'Clientes', icon: 'bi-person-heart', module: 'Customers' },
      { href: 'suppliers.html', label: 'Proveedores', icon: 'bi-building', module: 'Suppliers' },
    ] },
    { group: 'Análisis', items: [
      { href: 'dashboard.html', label: 'Resumen del día', icon: 'bi-speedometer2', module: 'Dashboard' },
      { href: 'reports.html', label: 'Reportes', icon: 'bi-graph-up', module: 'Reports' },
    ] },
    { group: 'Sistema', items: [
      { href: 'settings.html', label: 'Configuración', icon: 'bi-sliders', module: 'Settings' },
    ] },
  ];

  function firstAllowedPage() {
    for (const g of NAV) for (const i of g.items) if (session.can(i.module)) return i.href;
    return 'login.html';
  }

  async function loadProfile() {
    const profile = await api.get('/auth/me');
    store.set(PROFILE_KEY, JSON.stringify(profile), sessionStorage);
    return profile;
  }

  function buildShell(title) {
    const page = document.getElementById('page');
    const current = location.pathname.split('/').pop() || 'dashboard.html';
    const profile = session.profile;
    const nav = NAV.map(g => {
      const items = g.items.filter(i => session.can(i.module));
      if (!items.length) return '';
      return `<div class="nav-group eyebrow">${esc(g.group)}</div><nav class="nav">${items.map(i =>
        `<a href="${i.href}" ${i.href === current ? 'aria-current="page"' : ''}><i class="bi ${i.icon}"></i>${esc(i.label)}</a>`).join('')}</nav>`;
    }).join('');

    const shell = document.createElement('div');
    shell.className = 'shell';
    shell.innerHTML = `
      <aside class="rail" id="rail">
        <a class="brand" href="${firstAllowedPage()}"><b>Matiz</b><span>POS</span></a>
        ${nav}
        <div class="rail-foot">
          <div class="who"><b>${esc(profile?.fullName || profile?.userName || '')}</b><span class="muted">${profile?.isSuperAdmin ? 'Administrador' : esc(profile?.userName || '')}</span></div>
          <div style="display:flex;gap:6px">
            <button class="btn ghost sm" id="theme-btn" type="button"><i class="bi bi-circle-half"></i>Tema</button>
            <button class="btn ghost sm" id="logout-btn" type="button"><i class="bi bi-box-arrow-right"></i>Salir</button>
          </div>
        </div>
      </aside>
      <div class="main">
        <header class="topbar">
          <button class="btn ghost icon-btn menu-btn" id="menu-btn" type="button" aria-label="Abrir menú"><i class="bi bi-list"></i></button>
          <h1>${esc(title)}</h1>
          <div class="spacer"></div>
          <div id="topbar-actions" style="display:flex;gap:8px;flex-wrap:wrap"></div>
          <a class="cash-chip" id="cash-chip" href="cash.html" hidden><span class="dot"></span><span id="cash-chip-text">Caja</span></a>
        </header>
      </div>`;
    page.replaceWith(shell);
    shell.querySelector('.main').appendChild(page);
    const actions = document.getElementById('page-actions');
    if (actions) shell.querySelector('#topbar-actions').append(...actions.content.cloneNode(true).childNodes);

    shell.querySelector('#theme-btn').onclick = toggleTheme;
    shell.querySelector('#logout-btn').onclick = () => session.logout();
    shell.querySelector('#menu-btn').onclick = () => shell.classList.toggle('nav-open');
    page.addEventListener('click', () => shell.classList.remove('nav-open'));
  }

  /** Shows the cashier's register state in the top bar on every page. */
  async function refreshCashChip() {
    const chip = document.getElementById('cash-chip');
    if (!chip || !session.can('Sales')) return null;
    try {
      const s = await api.get('/cash-sessions/current');
      chip.hidden = false;
      chip.classList.toggle('open', !!s);
      document.getElementById('cash-chip-text').textContent = s ? `Caja abierta · ${s.folio}` : 'Caja cerrada';
      return s;
    } catch { return null; }
  }

  /**
   * Boots a page: requires a session, checks the module, builds the shell and runs the page.
   * A user without access is sent to the first page they can open.
   */
  async function boot({ module, title }, run) {
    if (!session.token) { location.href = 'login.html?next=' + encodeURIComponent(location.pathname.slice(1)); return; }
    try {
      if (!session.profile) await loadProfile();
    } catch (err) { if (err.status !== 401) fail(err); return; }
    if (module && !session.can(module)) { location.href = firstAllowedPage(); return; }
    buildShell(title);
    refreshCashChip();
    try { await run(); } catch (err) { fail(err); }
  }

  window.POS = { api, session, fmt, esc, shade, toast, fail, $, $$, formData, fillForm, dialogForm, confirmBox, boot, refreshCashChip, loadProfile, firstAllowedPage };
})();
