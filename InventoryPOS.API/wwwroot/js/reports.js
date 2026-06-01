App.init('reports');

let daysChart, methodsChart, catsChart;

async function loadReports() {
  const days = document.getElementById('period').value;
  const from = new Date(Date.now() - days * 86400000).toISOString().split('T')[0];
  const to   = new Date().toISOString().split('T')[0];

  const [summary, byDay, topProds, byMethod, byCat] = await Promise.all([
    api.get(`/reports/summary?from=${from}&to=${to}`),
    api.get(`/reports/sales-by-day?days=${days}`),
    api.get(`/reports/top-products?days=${days}&limit=10`),
    api.get(`/reports/payment-methods?days=${days}`),
    api.get(`/reports/by-category?days=${days}`)
  ]);

  // KPIs
  document.getElementById('r-count').textContent   = summary.totalSales;
  document.getElementById('r-revenue').textContent = '$' + fmt(summary.totalRevenue);
  document.getElementById('r-avg').textContent     = '$' + fmt(summary.averageTicket);
  document.getElementById('r-disc').textContent    = '$' + fmt(summary.totalDiscount);

  // Bar chart: days
  if (daysChart) daysChart.destroy();
  daysChart = new Chart(document.getElementById('chart-days'), {
    type: 'bar',
    data: {
      labels: byDay.map(d => fmtDate(d.date)),
      datasets: [{
        label: 'Ventas',
        data: byDay.map(d => d.total),
        backgroundColor: 'rgba(99,102,241,.75)',
        borderRadius: 6,
        borderSkipped: false
      }]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { display: false }, ticks: { maxTicksLimit: 10, font: { size: 10 } } },
        y: { grid: { color: '#f1f5f9' }, ticks: { callback: v => '$' + fmt(v, 0), font: { size: 10 } } }
      }
    }
  });

  // Doughnut: payment methods
  if (methodsChart) methodsChart.destroy();
  if (byMethod.length) {
    methodsChart = new Chart(document.getElementById('chart-methods'), {
      type: 'doughnut',
      data: {
        labels: byMethod.map(m => m.method + ` (${m.count})`),
        datasets: [{
          data: byMethod.map(m => m.total),
          backgroundColor: ['#6366f1','#10b981','#f59e0b','#ef4444'],
          borderWidth: 0
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { position: 'bottom', labels: { font: { size: 11 }, padding: 12 } } }
      }
    });
  }

  // Top products table
  const tbody = document.getElementById('top-products-body');
  tbody.innerHTML = topProds.length
    ? topProds.map((p, i) => `
        <tr>
          <td style="color:var(--text-muted);font-size:.8rem">${i + 1}</td>
          <td style="font-weight:500">${p.productName}</td>
          <td>${fmt(p.totalQty, 0)}</td>
          <td style="font-weight:600;color:var(--primary)">$${fmt(p.totalRevenue)}</td>
        </tr>`).join('')
    : '<tr><td colspan="4" class="empty-state"><i class="bi bi-trophy"></i><p>Sin datos</p></td></tr>';

  // Bar chart: categories
  if (catsChart) catsChart.destroy();
  if (byCat.length) {
    catsChart = new Chart(document.getElementById('chart-cats'), {
      type: 'bar',
      data: {
        labels: byCat.map(c => c.category),
        datasets: [{
          label: 'Ingresos',
          data: byCat.map(c => c.totalRevenue),
          backgroundColor: ['#6366f1','#10b981','#f59e0b','#ef4444','#06b6d4','#8b5cf6','#f97316'],
          borderRadius: 6,
          borderSkipped: false
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        indexAxis: 'y',
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { color: '#f1f5f9' }, ticks: { callback: v => '$' + fmt(v, 0), font: { size: 10 } } },
          y: { grid: { display: false }, ticks: { font: { size: 10 } } }
        }
      }
    });
  }
}

// Responsive bottom grid
const mq = window.matchMedia('(max-width:700px)');
function adjustGrid() {
  const g = document.getElementById('bottom-grid');
  if (g) g.style.gridTemplateColumns = mq.matches ? '1fr' : '1fr 1fr';
}
mq.addEventListener('change', adjustGrid);
adjustGrid();

loadReports().catch(e => toast(e.message, 'error'));
