/* Small SVG charts drawn from the theme tokens: area-over-time with hover, ranked bars and a payment mix strip. */
(function () {
  const { esc, fmt } = POS;

  /** Rounds the max up to a 1/2/2.5/5 step so the y ticks name values the axis really reaches. */
  function niceTicks(max, count = 4) {
    if (max <= 0) return [0, 1];
    const raw = max / count;
    const pow = 10 ** Math.floor(Math.log10(raw));
    const step = [1, 2, 2.5, 5, 10].map(m => m * pow).find(s => s >= raw);
    const ticks = [];
    for (let v = 0; v <= max + step * .001; v += step) ticks.push(v);
    if (ticks[ticks.length - 1] < max) ticks.push(ticks[ticks.length - 1] + step);
    return ticks;
  }
  const short = v => v >= 1000 ? `$${(v / 1000).toLocaleString('es-MX', { maximumFractionDigits: 1 })}k` : `$${Math.round(v)}`;

  /**
   * Daily sales area chart. `days` = [{ date, total, count }] already filled for every day.
   * One series, so no legend: the panel title names it.
   */
  function area(box, days) {
    const W = 640, H = 220, L = 46, R = 12, T = 12, B = 26;
    const max = Math.max(...days.map(d => d.total), 0);
    const ticks = niceTicks(max);
    const top = ticks[ticks.length - 1];
    const x = i => L + (days.length === 1 ? (W - L - R) / 2 : i * (W - L - R) / (days.length - 1));
    const y = v => T + (H - T - B) * (1 - v / top);
    const pts = days.map((d, i) => [x(i), y(d.total)]);
    const line = pts.map((p, i) => `${i ? 'L' : 'M'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join('');
    const areaPath = `${line}L${x(days.length - 1)},${y(0)}L${x(0)},${y(0)}Z`;
    const every = Math.ceil(days.length / 7);
    const label = d => new Date(d + 'T12:00:00').toLocaleDateString('es-MX', { day: 'numeric', month: 'short' });
    const last = pts[pts.length - 1];

    box.innerHTML = `
      <svg class="chart" viewBox="0 0 ${W} ${H}" role="img" aria-label="Ventas por día">
        <defs><linearGradient id="area-fill" x1="0" x2="0" y1="0" y2="1">
          <stop offset="0" stop-color="var(--series-1)" stop-opacity=".22"/><stop offset="1" stop-color="var(--series-1)" stop-opacity="0"/></linearGradient></defs>
        ${ticks.map(t => `<line class="gridline" x1="${L}" x2="${W - R}" y1="${y(t)}" y2="${y(t)}"/><text x="${L - 8}" y="${y(t) + 4}" text-anchor="end">${short(t)}</text>`).join('')}
        ${days.map((d, i) => i % every === 0 || i === days.length - 1 ? `<text x="${x(i)}" y="${H - 6}" text-anchor="middle">${label(d.date)}</text>` : '').join('')}
        <path d="${areaPath}" fill="url(#area-fill)"/>
        <path d="${line}" fill="none" stroke="var(--series-1)" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>
        <circle cx="${last[0]}" cy="${last[1]}" r="4.5" fill="var(--series-1)" stroke="var(--surface)" stroke-width="2"/>
        <line id="cross" y1="${T}" y2="${y(0)}" stroke="var(--ink-2)" stroke-dasharray="3 3" visibility="hidden"/>
        <circle id="cross-dot" r="5" fill="var(--series-1)" stroke="var(--surface)" stroke-width="2" visibility="hidden"/>
        <rect x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}" fill="transparent" id="hit"/>
      </svg>
      <div class="chart-tip" hidden></div>`;

    const svg = box.querySelector('svg'), tip = box.querySelector('.chart-tip');
    const cross = svg.querySelector('#cross'), dot = svg.querySelector('#cross-dot');
    svg.querySelector('#hit').addEventListener('pointermove', e => {
      const r = svg.getBoundingClientRect();
      const px = (e.clientX - r.left) * W / r.width;
      const i = Math.max(0, Math.min(days.length - 1, Math.round((px - L) / ((W - L - R) / Math.max(days.length - 1, 1)))));
      const [cx, cy] = pts[i];
      cross.setAttribute('x1', cx); cross.setAttribute('x2', cx); cross.setAttribute('visibility', 'visible');
      dot.setAttribute('cx', cx); dot.setAttribute('cy', cy); dot.setAttribute('visibility', 'visible');
      tip.hidden = false;
      tip.style.left = `${cx * r.width / W}px`;
      tip.style.top = `${cy * r.height / H}px`;
      tip.innerHTML = `<b>${esc(label(days[i].date))}</b> · ${fmt.money(days[i].total)} · ${days[i].count} ventas`;
    });
    svg.querySelector('#hit').addEventListener('pointerleave', () => {
      tip.hidden = true; cross.setAttribute('visibility', 'hidden'); dot.setAttribute('visibility', 'hidden');
    });
  }

  /** Ranked horizontal bars, one hue (magnitude, not identity), value printed at the end of each row. */
  function bars(box, rows, { value = r => r.value, label = r => r.label, format = fmt.money, empty = 'Sin datos en el periodo.' } = {}) {
    if (!rows.length) { box.innerHTML = `<div class="empty">${esc(empty)}</div>`; return; }
    const max = Math.max(...rows.map(value), 0) || 1;
    box.innerHTML = `<div class="bar-list">${rows.map(r => `
      <div class="bar-row" title="${esc(label(r))}: ${esc(format(value(r)))}">
        <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(label(r))}</span>
        <span class="track"><span class="fill" style="width:${(value(r) / max * 100).toFixed(1)}%"></span></span>
        <span class="num">${esc(format(value(r)))}</span>
      </div>`).join('')}</div>`;
  }

  /** Share-of-total strip for payment methods; fixed slot per method so colors follow the method, not its rank. */
  const METHOD_SLOT = { Efectivo: 1, Tarjeta: 2, Transferencia: 3 };
  function mix(box, rows) {
    const total = rows.reduce((s, r) => s + r.total, 0);
    if (!total) { box.innerHTML = '<div class="empty">Sin cobros en el periodo.</div>'; return; }
    const ordered = Object.keys(METHOD_SLOT).map(m => rows.find(r => r.method === m)).filter(Boolean);
    box.innerHTML = `
      <div class="mix" role="img" aria-label="Mezcla de formas de pago">${ordered.map(r =>
        `<span style="flex:${r.total};background:var(--series-${METHOD_SLOT[r.method]})" title="${esc(r.method)} ${fmt.money(r.total)}"></span>`).join('')}</div>
      <div class="legend" style="margin-top:10px">${ordered.map(r =>
        `<span><i style="background:var(--series-${METHOD_SLOT[r.method]})"></i>${esc(r.method)} <b class="num">${fmt.money(r.total)}</b> <span class="muted">${Math.round(r.total / total * 100)}%</span></span>`).join('')}</div>`;
  }

  /** Fills missing days with zero so the time axis stays continuous. */
  function fillDays(rows, days) {
    const map = new Map(rows.map(r => [String(r.date).slice(0, 10), r]));
    const out = [];
    const today = new Date();
    for (let i = days - 1; i >= 0; i--) {
      const d = new Date(today); d.setDate(today.getDate() - i);
      const key = fmt.isoDay(d);
      out.push({ date: key, total: map.get(key)?.total || 0, count: map.get(key)?.count || 0 });
    }
    return out;
  }

  window.Charts = { area, bars, mix, fillDays };
})();
