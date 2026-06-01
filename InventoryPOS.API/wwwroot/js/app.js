const NAV_ITEMS = [
  { section: 'Principal' },
  { id: 'dashboard', label: 'Dashboard',       icon: 'speedometer2',    href: 'dashboard.html' },
  { id: 'pos',       label: 'Punto de Venta',  icon: 'cash-register',   href: 'pos.html' },
  { section: 'Inventario' },
  { id: 'products',  label: 'Productos',        icon: 'box-seam',        href: 'products.html' },
  { id: 'categories',label: 'Categorías',       icon: 'tags',            href: 'categories.html' },
  { id: 'suppliers', label: 'Proveedores',      icon: 'truck',           href: 'suppliers.html' },
  { section: 'Ventas' },
  { id: 'sales',     label: 'Historial Ventas', icon: 'receipt',         href: 'sales.html' },
  { id: 'customers', label: 'Clientes',         icon: 'people',          href: 'customers.html' },
  { id: 'reports',   label: 'Reportes',         icon: 'bar-chart-line',  href: 'reports.html' },
];

const App = {
  init(pageId) {
    this.renderSidebar(pageId);
    this.renderTopbar(pageId);
    this.initToggle();
  },

  renderSidebar(activeId) {
    const el = document.getElementById('sidebar');
    if (!el) return;

    let html = `
      <div class="brand">
        <h1><i class="bi bi-box-fill" style="color:#6366f1"></i> InventoryPOS</h1>
        <span>Sistema de punto de venta</span>
      </div>
      <nav>`;

    NAV_ITEMS.forEach(item => {
      if (item.section) {
        html += `<div class="nav-section">${item.section}</div>`;
      } else {
        const active = item.id === activeId ? 'active' : '';
        html += `<a href="${item.href}" class="${active}"><i class="bi bi-${item.icon}"></i> ${item.label}</a>`;
      }
    });

    html += `</nav>
      <div class="sidebar-footer">v1.0.0 &bull; InventoryPOS</div>`;
    el.innerHTML = html;
  },

  renderTopbar(pageId) {
    const el = document.getElementById('topbar');
    if (!el) return;
    const item = NAV_ITEMS.find(i => i.id === pageId);
    const label = item?.label || 'InventoryPOS';
    el.innerHTML = `
      <div style="display:flex;align-items:center;gap:12px">
        <button class="menu-toggle" onclick="App.toggleSidebar()"><i class="bi bi-list"></i></button>
        <span class="page-title">${label}</span>
      </div>
      <div class="topbar-actions">
        <a href="pos.html" class="btn btn-primary btn-sm"><i class="bi bi-plus-lg"></i> Nueva venta</a>
      </div>`;
  },

  toggleSidebar() {
    document.getElementById('sidebar')?.classList.toggle('open');
  },

  initToggle() {
    document.addEventListener('click', e => {
      const sidebar = document.getElementById('sidebar');
      if (!sidebar) return;
      if (!sidebar.contains(e.target) && !e.target.closest('.menu-toggle'))
        sidebar.classList.remove('open');
    });
  }
};
