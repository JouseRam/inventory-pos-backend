App.init('dashboard');

let salesChart, topChart;

async function loadDashboard() {
  const [dash, byDay, topProds] = await Promise.all([
    api.get('/reports/dashboard'),
    api.get('/reports/sales-by-day?days=30'),
    api.get('/reports/top-products?days=30&limit=6')
  ]);

  // KPIs
  document.getElementById('k-today').textContent    = '$' + fmt(dash.salesToday);
  document.getElementById('k-week').textContent     = '$' + fmt(dash.salesWeek);
  document.getElementById('k-month').textContent    = '$' + fmt(dash.salesMonth);
  document.getElementById('k-products').textContent = dash.productCount;
  document.getElementById('k-lowstock').textContent = dash.lowStockCount;
  document.getElementById('k-customers').textContent= dash.customerCount;

  // Recent sales table
  const tbody = document.getElementById('recent-sales');
  if (!dash.recentSales.length) {
    tbody.innerHTML = '<tr><td colspan="4" class="empty-state"><i class="bi bi-receipt"></i><p>Sin ventas aún</p></td></tr>';
  } else {
    tbody.innerHTML = dash.recentSales.map(s => `
      <tr>
        <td><a href="sales.html" style="color:var(--primary);font-weight:600">${s.folio}</a></td>
        <td>${s.customerName}</td>
        <td style="font-weight:600">$${fmt(s.total)}</td>
        <td style="color:var(--text-muted);font-size:.8rem">${fmtDateTime(s.createdAt)}</td>
      </tr>`).join('');
  }

  // Low stock table
  const ls = document.getElementById('low-stock-table');
  if (!dash.lowStockProducts.length) {
    ls.innerHTML = '<tr><td colspan="3" style="text-align:center;padding:20px;color:var(--text-muted)">Sin alertas de stock</td></tr>';
  } else {
    ls.innerHTML = dash.lowStockProducts.map(p => `
      <tr>
        <td style="font-weight:500">${p.name}</td>
        <td><span class="badge badge-red">${fmt(p.stock, 0)} ${p.unit}</span></td>
        <td style="color:var(--text-muted)">${fmt(p.minStock, 0)}</td>
      </tr>`).join('');
  }

  // Bar chart: sales by day
  const labels = byDay.map(d => fmtDate(d.date));
  const values = byDay.map(d => d.total);
  if (salesChart) salesChart.destroy();
  salesChart = new Chart(document.getElementById('chart-sales'), {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Ventas ($)',
        data: values,
        backgroundColor: 'rgba(99,102,241,.7)',
        borderRadius: 6,
        borderSkipped: false
      }]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { display: false }, ticks: { maxTicksLimit: 8, font: { size: 11 } } },
        y: { grid: { color: '#f1f5f9' }, ticks: { callback: v => '$' + fmt(v, 0), font: { size: 11 } } }
      }
    }
  });

  // Pie chart: top products
  if (topProds.length) {
    if (topChart) topChart.destroy();
    topChart = new Chart(document.getElementById('chart-top'), {
      type: 'doughnut',
      data: {
        labels: topProds.map(p => p.productName),
        datasets: [{
          data: topProds.map(p => p.totalRevenue),
          backgroundColor: ['#6366f1','#10b981','#f59e0b','#ef4444','#06b6d4','#8b5cf6'],
          borderWidth: 0
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { position: 'bottom', labels: { font: { size: 11 }, padding: 10 } }
        }
      }
    });
  }
}

loadDashboard().catch(e => toast(e.message, 'error'));

// Responsive tables row
const mq = window.matchMedia('(max-width:700px)');
function adjustLayout() {
  const row = document.getElementById('tables-row');
  if (row) row.style.gridTemplateColumns = mq.matches ? '1fr' : '1fr 1fr';
}
mq.addEventListener('change', adjustLayout);
adjustLayout();
